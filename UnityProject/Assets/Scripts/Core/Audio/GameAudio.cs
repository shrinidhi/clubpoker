using System;
using UnityEngine;

namespace ClubPoker.Core
{
    /// <summary>
    /// Sound trigger bus. Gameplay and UI code raise a SoundId here; the Audio
    /// module listens and plays it. Callers never reference the Audio assembly,
    /// and an audio failure can never break the handler that raised it.
    /// </summary>
    public static class GameAudio
    {
        public static event Action<SoundId> OnPlay;
        public static event Action<SoundId> OnStop;

        public static void Play(SoundId id) => Raise(OnPlay, id);

        /// <summary>Stops a looping sound (or cuts any still-playing instance).</summary>
        public static void Stop(SoundId id) => Raise(OnStop, id);

        // Music: one track at a time, crossfaded, survives scene loads. Scenes
        // listed in SoundLibrary's scene music switch automatically — call these
        // only to override (e.g. a victory track).
        public static event Action<SoundId> OnPlayMusic;
        public static event Action OnStopMusic;

        /// <summary>Crossfades to this track. No-op if it's already playing.</summary>
        public static void PlayMusic(SoundId id) => Raise(OnPlayMusic, id);

        /// <summary>Fades the current track out.</summary>
        public static void StopMusic()
        {
            try
            {
                OnStopMusic?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void Raise(Action<SoundId> evt, SoundId id)
        {
            if (evt == null || id == SoundId.None)
                return;

            try
            {
                evt(id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
