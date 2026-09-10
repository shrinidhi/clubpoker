using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace ClubPoker.Core
{
    public class FeatureFlagManager : MonoBehaviour
    {
        #region Singleton

        public static FeatureFlagManager Instance { get; private set; }

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

        #endregion

        #region Properties

        public bool IsFlagsFetched { get; private set; }
        public List<PokerVariant> AvailableVariants { get; private set; } = new List<PokerVariant>();

        private Dictionary<string, bool> _flags = new Dictionary<string, bool>();

        #endregion

        #region Constants

        private const string VARIANTS_ENDPOINT = "/api/utility/variants";

        #endregion

        #region Public Methods

        public async UniTask InitializeAsync()
        {
            Debug.Log("[FeatureFlagManager] Initializing feature flags...");

            // Load default values from AppConfig
            LoadDefaultFlags();

            // Fetch variants from server
            await FetchVariantsAsync();
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

        /// <summary>
        /// Flag value, or <paramref name="fallback"/> when the config doesn't carry
        /// it. No warning: a flag that is meant to be absent from some environments
        /// is a normal state, not a mistake.
        /// </summary>
        public bool IsEnabled(string flagName, bool fallback)
        {
            return _flags.TryGetValue(flagName, out bool value) ? value : fallback;
        }

        /// <summary>
        /// Flag value from anywhere, including scenes entered without Bootstrap (the
        /// editor's play-from-this-scene) where the manager doesn't exist yet — those
        /// get <paramref name="fallback"/> rather than a null check at every call.
        /// </summary>
        public static bool IsOn(string flagName, bool fallback = false)
        {
            return Instance != null ? Instance.IsEnabled(flagName, fallback) : fallback;
        }

        #endregion

        #region Flag names

        /// Daily bonus button + the popup that opens itself on the main menu.
        public const string FlagDailyBonus = "daily_bonus";

        /// Tournament (MTT) entry points. No tournament screen is built yet.
        public const string FlagTournaments = "tournaments";

        #endregion

        #region Private Methods

        private void LoadDefaultFlags()
        {
            AppConfig config = ConfigManager.Instance.Config;

            foreach (FeatureFlag flag in config.featureFlags)
            {
                _flags[flag.flagName] = flag.defaultValue;
                Debug.Log($"[FeatureFlagManager] Default flag: {flag.flagName} = {flag.defaultValue}");
            }
        }

        private async UniTask FetchVariantsAsync()
        {
            string url = $"{ConfigManager.Instance.Config.apiBaseUrl}{VARIANTS_ENDPOINT}";
            Debug.Log($"[FeatureFlagManager] Fetching variants from: {url}");

            try
            {
                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.timeout = 10;

                try
                {
                    await request.SendWebRequest();
                }
                catch (UnityWebRequestException)
                {
                    // Handled below
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    VariantsApiResponse response = JsonConvert.DeserializeObject<VariantsApiResponse>(json);

                    if (response?.IsSuccess == true && response.data?.Variants != null)
                    {
                        AvailableVariants = response.data.Variants;

                        foreach (PokerVariant variant in AvailableVariants)
                        {
                            _flags[$"variant_{variant.Id}"] = variant.Enabled;
                            Debug.Log($"[FeatureFlagManager] Variant: {variant.DisplayName} = {variant.Enabled}");
                        }

                        IsFlagsFetched = true;
                        Debug.Log("[FeatureFlagManager] Variants fetched successfully!");
                    }
                }
                else
                {
                    Debug.LogWarning($"[FeatureFlagManager] Failed to fetch variants, using defaults");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FeatureFlagManager] Server unreachable, using defaults: {e.Message}");
            }
        }

        #endregion
    }

        // Simple response wrapper for FeatureFlagManager
    public class VariantsApiResponse
    {
        public string status { get; set; }
        public VariantsResponse data { get; set; }
        public bool IsSuccess => status == "ok" || status == "success";
    }
}