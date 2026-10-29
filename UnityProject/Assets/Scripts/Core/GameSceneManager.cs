using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace ClubPoker.Core
{
    public class GameSceneManager : MonoBehaviour
    {
        // Singleton instance
        public static GameSceneManager Instance { get; private set; }

        [SerializeField] private GameObject loadingScreenPrefab;

        // Events
        public event Action<float> OnLoadingProgress;
        public event Action OnLoadingComplete;

        private SceneInstance _currentSceneInstance;
        private bool _isLoading = false;
        private GameObject _loadingScreenInstance;

        private void Awake()
        {
            // Prevent duplicate instances
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string sceneAddress)
        {
            if (_isLoading)
            {
                Debug.LogWarning("[GameSceneManager] Scene load already in progress!");
                return;
            }

            StartCoroutine(LoadSceneAsync(sceneAddress));
        }

        /// <summary>
        /// Light hop between small scenes (Login ↔ Register): crossfade, no loading
        /// screen. The current screen is captured and held on top while the next
        /// scene loads underneath, then dissolves away — no black frame between.
        /// </summary>
        public void LoadSceneWithFade(string sceneAddress, float fadeDuration = 0.3f)
        {
            if (_isLoading)
            {
                Debug.LogWarning("[GameSceneManager] Scene load already in progress!");
                return;
            }

            StartCoroutine(LoadSceneWithFadeAsync(sceneAddress, fadeDuration));
        }

        private IEnumerator LoadSceneWithFadeAsync(string sceneAddress, float fadeDuration)
        {
            _isLoading = true;

            // Grab the frame as it is now — must be after rendering finishes.
            yield return new WaitForEndOfFrame();
            Texture2D snapshot = CaptureScreenSRGB();

            CanvasGroup fade = GetFadeOverlay();
            _fadeImage.texture = snapshot;
            fade.alpha = 1f;
            fade.blocksRaycasts = true;   // no taps on either scene mid-swap
            fade.gameObject.SetActive(true);

            AsyncOperationHandle<SceneInstance> loadHandle =
                Addressables.LoadSceneAsync(sceneAddress, LoadSceneMode.Single);

            yield return loadHandle;

            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                _currentSceneInstance = loadHandle.Result;
                Debug.Log($"[GameSceneManager] Successfully loaded: {sceneAddress}");
            }
            else
            {
                Debug.LogError($"[GameSceneManager] Failed to load scene: {sceneAddress}");
            }

            // One frame for the new scene's Awake/Start/layout before revealing it.
            yield return null;

            yield return FadeTo(fade, 0f, fadeDuration);

            fade.blocksRaycasts = false;
            fade.gameObject.SetActive(false);

            _fadeImage.texture = null;
            Destroy(snapshot);

            _isLoading = false;
        }

        // The project renders in Linear space; the captured pixels are gamma-encoded,
        // so they go into a texture explicitly flagged sRGB. Otherwise the held frame
        // can come out lighter/darker than the live screen and the swap shows.
        private static Texture2D CaptureScreenSRGB()
        {
            Texture2D raw = ScreenCapture.CaptureScreenshotAsTexture();

            var snapshot = new Texture2D(raw.width, raw.height, TextureFormat.RGBA32,
                                         mipChain: false, linear: false);
            snapshot.SetPixels32(raw.GetPixels32());
            snapshot.Apply(false, true);   // GPU only from here

            Destroy(raw);
            return snapshot;
        }

        private static IEnumerator FadeTo(CanvasGroup group, float target, float duration)
        {
            float start = group.alpha;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }

            group.alpha = target;
        }

        private CanvasGroup _fadeOverlay;
        private UnityEngine.UI.RawImage _fadeImage;

        // Full-screen overlay holding the outgoing screen's snapshot, built once
        // and kept across scene loads.
        private CanvasGroup GetFadeOverlay()
        {
            if (_fadeOverlay != null)
                return _fadeOverlay;

            var root = new GameObject("SceneFadeOverlay");
            DontDestroyOnLoad(root);

            // Above scene canvases, just under the top inset bar.
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            _fadeOverlay = root.AddComponent<CanvasGroup>();
            _fadeOverlay.alpha = 0f;

            var image = new GameObject("Snapshot", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            image.transform.SetParent(root.transform, false);
            _fadeImage = image.GetComponent<UnityEngine.UI.RawImage>();

            var rt = (RectTransform)image.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            root.SetActive(false);
            return _fadeOverlay;
        }

        private IEnumerator LoadSceneAsync(string sceneAddress)
        {
            _isLoading = true;

            // Show loading screen
            ShowLoadingScreen();

            // Report 0% progress
            OnLoadingProgress?.Invoke(0f);

            // Load new scene
            AsyncOperationHandle<SceneInstance> loadHandle =
                Addressables.LoadSceneAsync(sceneAddress, LoadSceneMode.Single);

            // Report progress while loading
            while (!loadHandle.IsDone)
            {
                OnLoadingProgress?.Invoke(loadHandle.PercentComplete);
                yield return null;
            }

            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                _currentSceneInstance = loadHandle.Result;

                // Report 100% progress
                OnLoadingProgress?.Invoke(1f);
                OnLoadingComplete?.Invoke();

                Debug.Log($"[GameSceneManager] Successfully loaded: {sceneAddress}");
            }
            else
            {
                Debug.LogError($"[GameSceneManager] Failed to load scene: {sceneAddress}");
            }

            _isLoading = false;
        }

        private void ShowLoadingScreen()
        {
            if (loadingScreenPrefab != null && _loadingScreenInstance == null)
            {
                _loadingScreenInstance = Instantiate(loadingScreenPrefab);
                DontDestroyOnLoad(_loadingScreenInstance);
            }
            else if (_loadingScreenInstance != null)
            {
                _loadingScreenInstance.SetActive(true);
            }
        }
    }
}