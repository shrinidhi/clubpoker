using System;
using UnityEngine;

namespace ClubPoker.Core
{
    /// <summary>
    /// Music on/off, saved per device. Separate from SoundSettings, which mutes
    /// everything (music included) through AudioListener.volume.
    /// </summary>
    public static class MusicSettings
    {
        private const string KEY = "Settings.MusicOn";

        public static event Action<bool> OnChanged;

        public static bool IsOn => PlayerPrefs.GetInt(KEY, 1) == 1;

        public static void SetOn(bool on)
        {
            if (on == IsOn)
                return;

            PlayerPrefs.SetInt(KEY, on ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke(on);
        }

        public static void Toggle() => SetOn(!IsOn);
    }
}
