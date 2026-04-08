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