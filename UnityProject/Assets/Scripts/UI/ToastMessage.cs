using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    /// <summary>
    /// The one toast. Placed once in Bootstrap under its own overlay canvas and kept
    /// alive across scenes; any extra copy (an old per-scene one) removes itself.
    /// Show it through <see cref="ToastEvents.Show"/>, never directly.
    ///
    /// Display-only: it never takes a tap, so buttons underneath (the action bar on
    /// the game table) keep working while it is up.
    /// </summary>
    public class ToastMessage : MonoBehaviour
    {
        public static ToastMessage Instance { get; private set; }

        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private RectTransform toastRect;

        private const float ANIMATION_DURATION = 0.3f;
        private const float HIDDEN_Y = -120f;

        [Tooltip("Height above the bottom of the screen while visible, for scenes " +
                 "without a ToastPlacement.")]
        [SerializeField] private float defaultVisibleY = 120f;

        [Tooltip("Seconds the toast stays fully visible, between the slide in and " +
                 "the slide out.")]
        [SerializeField] private float displayDuration = 2f;

        private const string BOOTSTRAP_SCENE = "Bootstrap";

        private float _visibleY;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Destroy only the toast, not its root — a leftover scene copy sits
                // under that scene's main canvas.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _visibleY = defaultVisibleY;

            // Only the Bootstrap copy persists. Its root is the dedicated toast
            // canvas; a scene copy's root is that scene's UI and must not leave it.
            if (gameObject.scene.name == BOOTSTRAP_SCENE)
                DontDestroyOnLoad(transform.root.gameObject);

            MakeDisplayOnly();

            ToastEvents.HasView = true;
            ToastEvents.OnShowToast += Show;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;
            ToastEvents.HasView = false;
            ToastEvents.OnShowToast -= Show;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void Start()
        {
            ApplyPlacement();
            toastRect.anchoredPosition = new Vector2(0f, HIDDEN_Y);
            toastRect.gameObject.SetActive(false);
        }

        private void MakeDisplayOnly()
        {
            var group = toastRect.GetComponent<CanvasGroup>();
            if (group == null)
                group = toastRect.gameObject.AddComponent<CanvasGroup>();

            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyPlacement();

        private void ApplyPlacement()
        {
            var placement = FindObjectOfType<ToastPlacement>();
            _visibleY = placement != null ? placement.visibleY : defaultVisibleY;

            // Already showing across a scene change → move it to the new height.
            if (toastRect.gameObject.activeSelf)
            {
                toastRect.DOKill();
                toastRect.anchoredPosition = new Vector2(0f, _visibleY);
            }
        }

        public void Show(string message)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            ShowAsync(message, _cts.Token).Forget();
        }

        private async UniTaskVoid ShowAsync(string message, CancellationToken ct)
        {
            // slide down current if visible
            if (toastRect.gameObject.activeSelf)
            {
                toastRect.DOKill();
                await toastRect.DOAnchorPosY(HIDDEN_Y, ANIMATION_DURATION)
                    .SetEase(Ease.InBack)
                    .AsyncWaitForCompletion()
                    .AsUniTask().AttachExternalCancellation(ct);
            }

            if (ct.IsCancellationRequested) return;

            if (messageText != null)
                messageText.text = message;

            toastRect.gameObject.SetActive(true);
            toastRect.DOKill();
            toastRect.DOAnchorPosY(_visibleY, ANIMATION_DURATION).SetEase(Ease.OutBack);

            await UniTask.Delay((int)(displayDuration * 1000), cancellationToken: ct);

            toastRect.DOAnchorPosY(HIDDEN_Y, ANIMATION_DURATION)
                .SetEase(Ease.InBack)
                .OnComplete(() => toastRect.gameObject.SetActive(false));
        }
    }
}
