using System;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ClubPoker.Core
{

    [Serializable]
    public class ServerConfig
    {
        public string status;
        public ServerConfigData data;

        public bool IsSuccess => status == "ok" || status == "success";
    }

    [Serializable]
    public class ServerConfigData
    {
        public string appVersion;
        public string minClientVersion;
        public bool maintenanceMode;
        public string maintenanceMessage;
        public int signupBonusChips;
        public int dailyBonusChips;
        public int guestChips;
        public int guestSessionMinutes;
        public int reconnectGracePeriodSecs;
        public int chatRateLimitPerWindow;
        public int chatWindowSeconds;
        public int maxTablesPerPlayer;
    }
    
    public class ConfigValidator : MonoBehaviour
    {
        public static ConfigValidator Instance { get; private set; }

        [SerializeField] private GameObject forceUpgradeScreenPrefab;

        public event Action OnValidationSuccess;
        public event Action<string> OnValidationFailed;
        public event Action OnVersionOutdated;

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

        public async UniTask ValidateAsync()
        {
            Debug.Log("[ConfigValidator] Validating config...");

            AppConfig config = ConfigManager.Instance.Config;

            // Step 1 - Validate fields are not empty
            if (!ValidateFields(config)) return;

            // Step 2 - Validate URL formats
            if (!ValidateUrls(config)) return;

            // Step 3 - Fetch server config
            ServerConfig serverConfig = await FetchServerConfigAsync(config.apiBaseUrl);

            // Step 4 - Check app version against server
            if (serverConfig.IsSuccess && serverConfig.data != null)
            {
                if (serverConfig.data.maintenanceMode)
                {
                    Debug.LogWarning("[ConfigValidator] Server is in maintenance mode!");
                    // TODO: Show maintenance screen
                    return;
                }

                CheckVersion(Application.version, serverConfig.data.minClientVersion);
            }
            else
            {
                // If fetch fails use local minimum version
                CheckVersion(Application.version, config.minimumAppVersion);
            }
        }

        private bool ValidateFields(AppConfig config)
        {
            if (string.IsNullOrEmpty(config.apiBaseUrl))
            {
                string error = "API Base URL is empty!";
                Debug.LogError($"[ConfigValidator] {error}");
                OnValidationFailed?.Invoke(error);
                return false;
            }

            if (string.IsNullOrEmpty(config.webSocketUrl))
            {
                string error = "WebSocket URL is empty!";
                Debug.LogError($"[ConfigValidator] {error}");
                OnValidationFailed?.Invoke(error);
                return false;
            }

            if (string.IsNullOrEmpty(config.minimumAppVersion))
            {
                string error = "Minimum App Version is empty!";
                Debug.LogError($"[ConfigValidator] {error}");
                OnValidationFailed?.Invoke(error);
                return false;
            }

            Debug.Log("[ConfigValidator] Fields validated successfully!");
            return true;
        }

        private bool ValidateUrls(AppConfig config)
        {
            if (!config.apiBaseUrl.StartsWith("http://") &&
                !config.apiBaseUrl.StartsWith("https://"))
            {
                string error = $"Invalid API URL format: {config.apiBaseUrl}";
                Debug.LogError($"[ConfigValidator] {error}");
                OnValidationFailed?.Invoke(error);
                return false;
            }

            if (!config.webSocketUrl.StartsWith("ws://") &&
                !config.webSocketUrl.StartsWith("wss://"))
            {
                string error = $"Invalid WebSocket URL format: {config.webSocketUrl}";
                Debug.LogError($"[ConfigValidator] {error}");
                OnValidationFailed?.Invoke(error);
                return false;
            }

            Debug.Log("[ConfigValidator] URLs validated successfully!");
            return true;
        }

        private async UniTask<ServerConfig> FetchServerConfigAsync(string apiBaseUrl)
        {
            string url = $"{apiBaseUrl}/api/utility/config";
            Debug.Log($"[ConfigValidator] Fetching server config: {url}");

            try
            {
                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.timeout = 10;

                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    ServerConfig serverConfig = JsonConvert.DeserializeObject<ServerConfig>(json);
                    
                    Debug.Log($"[ConfigValidator] Server config response: {json}");
                    if (serverConfig?.IsSuccess == true && serverConfig.data != null)
                    {
                        Debug.Log($"[ConfigValidator] Server config fetched successfully!");
                        Debug.Log($"[ConfigValidator] Maintenance mode: {serverConfig.data.maintenanceMode}");
                        return serverConfig;
                    }
                    else
                    {
                        Debug.LogWarning("[ConfigValidator] Invalid server config response!");
                        return null;
                    }
                }
                else
                {
                    Debug.LogWarning($"[ConfigValidator] Failed to fetch server config: {request.error}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConfigValidator] Server unreachable, using local config: {e.Message}");
                return null;
            }
        }
        private void CheckVersion(string current, string minimum)
        {
            Debug.Log($"[ConfigValidator] Current version: {current}");
            Debug.Log($"[ConfigValidator] Minimum version: {minimum}");

            if (IsVersionOutdated(current, minimum))
            {
                Debug.LogWarning("[ConfigValidator] App version outdated!");
                ShowForceUpgradeScreen(current, minimum);
                OnVersionOutdated?.Invoke();
                return;
            }

            Debug.Log("[ConfigValidator] Version check passed!");
            OnValidationSuccess?.Invoke();
        }

        private bool IsVersionOutdated(string current, string minimum)
        {
            Version currentVersion = new Version(current);
            Version minimumVersion = new Version(minimum);
            return currentVersion < minimumVersion;
        }

        private void ShowForceUpgradeScreen(string current, string minimum)
        {
            if (forceUpgradeScreenPrefab != null)
            {
                GameObject screen = Instantiate(forceUpgradeScreenPrefab);
                DontDestroyOnLoad(screen);
                ClubPoker.UI.ForceUpgradeScreen upgradeScreen = 
                    screen.GetComponent<ClubPoker.UI.ForceUpgradeScreen>();
                upgradeScreen.Show(current, minimum);
            }
        }
    }
}