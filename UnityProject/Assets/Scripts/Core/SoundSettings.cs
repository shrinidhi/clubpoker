using System;
using UnityEngine;

namespace ClubPoker.Core
{
    /// <summary>
    /// Global sound on/off, saved per device. Muting goes through
    /// AudioListener.volume, so every AudioSource in every scene follows it
    /// without each one having to check a flag.
    /// </summary>
    public static class SoundSettings
    {
        private const string KEY = "Settings.SoundOn";

        public static event Action<bool> OnChanged;

        public static bool IsOn => PlayerPrefs.GetInt(KEY, 1) == 1;

        public static void SetOn(bool on)
        {
            PlayerPrefs.SetInt(KEY, on ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            OnChanged?.Invoke(on);
        }

        public static void Toggle() => SetOn(!IsOn);

        // Apply the saved choice before the first scene loads, so a muted player
        // never hears the splash.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            AudioListener.volume = IsOn ? 1f : 0f;
        }
    }
}
