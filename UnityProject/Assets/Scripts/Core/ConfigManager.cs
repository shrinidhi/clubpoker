using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

namespace ClubPoker.Core
{
    public class ConfigManager : MonoBehaviour
    {
        public static ConfigManager Instance { get; private set; }

        [SerializeField] private string configAddress = "AppConfig_Dev";

        public AppConfig Config { get; private set; }
        public bool IsConfigLoaded { get; private set; }

        public event Action OnConfigLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async UniTask LoadConfigAsync()
        {
            Debug.Log($"[ConfigManager] Loading config: {configAddress}");

            AsyncOperationHandle<AppConfig> handle =
                Addressables.LoadAssetAsync<AppConfig>(configAddress);

            await handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Config = handle.Result;
                IsConfigLoaded = true;
                OnConfigLoaded?.Invoke();
                Debug.Log($"[ConfigManager] Config loaded: {Config.environmentName}");
                Debug.Log($"[ConfigManager] API URL: {Config.apiBaseUrl}");
                Debug.Log($"[ConfigManager] WebSocket URL: {Config.webSocketUrl}");
            }
            else
            {
                Debug.LogError("[ConfigManager] Failed to load config!");
            }
        }
    }
}