using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace ClubPoker.Core
{
    /// <summary>
    /// Translated text by key. One JSON file per language in
    /// Assets/Resources/Localization/{code}.json, flat key → text:
    ///
    ///   { "confirm": "Confirm", "msg.player_reconnected": "{name} reconnected" }
    ///
    /// Lookup order: current language → English → the key itself, so a missing
    /// translation shows English and a missing key is visible (not blank).
    ///
    /// Usage:
    ///   Loc.Get("confirm")
    ///   Loc.Get("msg.player_reconnected", ("name", player.Username))
    /// UI texts use the LocalizedText component instead.
    /// </summary>
    public static class Loc
    {
        public const string DefaultLanguage = "en";

        /// <summary>Supported languages, in picker order. Names are always shown in their own language.</summary>
        public static readonly IReadOnlyList<(string Code, string NativeName)> Languages = new[]
        {
            ("en", "English"),
            ("es", "Español"),
            ("pt", "Português"),
            ("ru", "Русский"),
            ("it", "Italiano"),
            ("de", "Deutsch"),
        };

        private const string PREF_KEY = "Settings.Language";
        private const string RESOURCE_DIR = "Localization/";

        /// <summary>Raised after the language changes; LocalizedText refreshes on it.</summary>
        public static event Action OnLanguageChanged;

        public static string Language { get; private set; } = DefaultLanguage;

        /// <summary>
        /// The player picked a language this run (vs. loaded from the device or the
        /// server). Login uses it so a choice made on the Login screen isn't
        /// overwritten by the server's older value — it's pushed up instead.
        /// </summary>
        public static bool ChosenByPlayerThisRun { get; private set; }

        private static Dictionary<string, string> _english = new Dictionary<string, string>();
        private static Dictionary<string, string> _current = new Dictionary<string, string>();

        private static bool _initialized;

        // Before the first scene so every text is right on its first frame. Also run
        // on first use, for code that asks before then (EditMode tests, editor tools).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _initialized = true;
            _english = LoadTable(DefaultLanguage);

            string saved = PlayerPrefs.GetString(PREF_KEY, null);
            string start = IsSupported(saved) ? saved : FromDevice();
            Apply(start);
        }

        public static bool IsSupported(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;
            foreach (var l in Languages)
                if (l.Code == code)
                    return true;
            return false;
        }

        /// <summary>
        /// Switch language, save it on the device and refresh every LocalizedText.
        /// <paramref name="byPlayer"/> false when applying a value from the server.
        /// </summary>
        public static void SetLanguage(string code, bool byPlayer = true)
        {
            if (!IsSupported(code))
            {
                Debug.LogWarning($"[Loc] Unsupported language '{code}' — ignored.");
                return;
            }

            if (byPlayer)
                ChosenByPlayerThisRun = true;

            if (code == Language)
                return;

            PlayerPrefs.SetString(PREF_KEY, code);
            PlayerPrefs.Save();
            Apply(code);
            Debug.Log($"[Loc] Language → {code}");
            OnLanguageChanged?.Invoke();
        }

        public static string Get(string key)
        {
            if (!_initialized)
                Initialize();
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (_current.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text))
                return text;
            if (_english.TryGetValue(key, out string en) && !string.IsNullOrEmpty(en))
                return en;
            return key;
        }

        /// <summary>Fills {name} placeholders: Get("msg.won", ("amount", 500)).</summary>
        public static string Get(string key, params (string Name, object Value)[] args)
        {
            string text = Get(key);
            foreach (var (name, value) in args)
                text = text.Replace("{" + name + "}", value?.ToString() ?? string.Empty);
            return text;
        }

        public static bool Has(string key)
        {
            if (!_initialized)
                Initialize();
            return _current.ContainsKey(key) || _english.ContainsKey(key);
        }

        /// <summary>
        /// A server error in the player's language: "error.&lt;code&gt;" (e.g. error.E001)
        /// when we have it, otherwise the server's own message unchanged.
        /// </summary>
        public static string Error(string code, string serverMessage)
        {
            string key = "error." + code;
            return !string.IsNullOrEmpty(code) && Has(key) ? Get(key) : serverMessage;
        }

        private static void Apply(string code)
        {
            Language = code;
            _current = code == DefaultLanguage ? _english : LoadTable(code);
        }

        private static Dictionary<string, string> LoadTable(string code)
        {
            var asset = Resources.Load<TextAsset>(RESOURCE_DIR + code);
            if (asset == null)
            {
                Debug.LogWarning($"[Loc] No Resources/{RESOURCE_DIR}{code}.json — falling back to English.");
                return new Dictionary<string, string>();
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text)
                       ?? new Dictionary<string, string>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Loc] {code}.json is not valid JSON — falling back to English: {e.Message}");
                return new Dictionary<string, string>();
            }
        }

        // First launch: the phone's language if we have it, else English.
        private static string FromDevice()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Spanish:    return "es";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Russian:    return "ru";
                case SystemLanguage.Italian:    return "it";
                case SystemLanguage.German:     return "de";
                default:                        return DefaultLanguage;
            }
        }
    }
}
