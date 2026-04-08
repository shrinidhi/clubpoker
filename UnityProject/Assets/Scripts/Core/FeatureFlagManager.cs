using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace ClubPoker.Core
{
    [Serializable]
    public class ServerFeatureFlags
    {
        public Dictionary<string, bool> flags;
    }

    public class FeatureFlagManager : MonoBehaviour
    {
        public static FeatureFlagManager Instance { get; private set; }

        private Dictionary<string, bool> _flags = new Dictionary<string, bool>();
        public bool IsFlagsFetched { get; private set; }

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

        public async UniTask InitializeAsync()
        {
            Debug.Log("[FeatureFlagManager] Initializing feature flags...");

            // - Load default values from AppConfig
            LoadDefaultFlags();

            // - Fetch from server (overrides defaults)
            await FetchFlagsFromServerAsync();
        }

        private void LoadDefaultFlags()
        {
            AppConfig config = ConfigManager.Instance.Config;

            foreach (FeatureFlag flag in config.featureFlags)
            {
                _flags[flag.flagName] = flag.defaultValue;
                Debug.Log($"[FeatureFlagManager] Default flag: {flag.flagName} = {flag.defaultValue}");
            }
        }

        private async UniTask FetchFlagsFromServerAsync()
        {
            string url = $"{ConfigManager.Instance.Config.apiBaseUrl}/api/utility/config";
            Debug.Log($"[FeatureFlagManager] Fetching flags from: {url}");

            try
            {
                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.timeout = 10;

                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    ServerFeatureFlags serverFlags = 
                        JsonConvert.DeserializeObject<ServerFeatureFlags>(json);

                    if (serverFlags?.flags != null)
                    {
                        foreach (var flag in serverFlags.flags)
                        {
                            _flags[flag.Key] = flag.Value;
                            Debug.Log($"[FeatureFlagManager] Server flag: {flag.Key} = {flag.Value}");
                        }
                    }

                    IsFlagsFetched = true;
                    Debug.Log("[FeatureFlagManager] Flags fetched from server!");
                }
                else
                {
                    Debug.LogWarning($"[FeatureFlagManager] Failed to fetch flags, using defaults: {request.error}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FeatureFlagManager] Server unreachable, using defaults: {e.Message}");
            }
        }

        public bool IsEnabled(string flagName)
        {
            if (_flags.TryGetValue(flagName, out bool value))
            {
                return value;
            }

            Debug.LogWarning($"[FeatureFlagManager] Flag not found: {flagName} - returning false");
            return false;
        }
    }
}