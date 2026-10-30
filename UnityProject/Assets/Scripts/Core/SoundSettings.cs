using System;
using UnityEngine;

namespace ClubPoker.Core
{
    /// <summary>
    /// Sound effects on/off, saved per device. Music has its own switch
    /// (MusicSettings) — AudioManager skips and stops SFX while this is off and
    /// leaves music alone.
    /// </summary>
    public static class SoundSettings
    {
        private const string KEY = "Settings.SoundOn";

        public static event Action<bool> OnChanged;

        public static bool IsOn => PlayerPrefs.GetInt(KEY, 1) == 1;

        public static void SetOn(bool on)
        {
            // Logged with its stack trace, so a surprise mute shows who did it
            // (drawer tap vs server settings applied at login).
            Debug.Log($"[SoundSettings] Sound {(on ? "ON" : "OFF")} (was {(IsOn ? "ON" : "OFF")})");

            PlayerPrefs.SetInt(KEY, on ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke(on);
        }

        public static void Toggle() => SetOn(!IsOn);

        // Builds before this change muted through AudioListener.volume; make sure
        // nothing leaves the listener at 0, or music would stay silent too.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetListenerAtStartup()
        {
            AudioListener.volume = 1f;

            if (!IsOn)
                Debug.Log("[SoundSettings] Starting with sound effects OFF (music unaffected).");
        }
    }
}
