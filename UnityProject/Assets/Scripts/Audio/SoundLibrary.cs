using System;
using System.Collections.Generic;
using ClubPoker.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ClubPoker.Audio
{
    // Serialized as ints in SoundLibrary.asset — keep the values fixed
    // (1 was a removed Ui channel; don't reuse it).
    public enum AudioChannel
    {
        Sfx   = 0,
        Music = 2,
    }

    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        public AssetReferenceT<AudioClip> clip;
        public AudioChannel channel = AudioChannel.Sfx;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Random pitch per play; (1,1) = no variation.")]
        public Vector2 pitchRange = Vector2.one;

        [Tooltip("Ignore repeat plays of this id closer together than this (seconds).")]
        public float minInterval = 0.05f;

        [Tooltip("Max simultaneous instances of this id.")]
        public int maxConcurrent = 3;

        [Tooltip("Load at startup. Off = load on first play (rare sounds).")]
        public bool preload = true;

        public bool loop;

        public bool HasClip => clip != null && clip.RuntimeKeyIsValid();
    }

    [Serializable]
    public class SceneMusic
    {
        [Tooltip("Scene file name, e.g. GameTableScene (not the Addressables address).")]
        public string sceneName;

        [Tooltip("Track for this scene. None = fade music out.")]
        public SoundId music;
    }

    /// <summary>
    /// SoundId → clip + playback settings. Addressable (address "SoundLibrary").
    /// Create / refresh via Tools ▸ ClubPoker ▸ Audio ▸ Setup Sound Library.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "ClubPoker/Audio/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        public const string ADDRESS = "SoundLibrary";

        [Tooltip("AudioSources created up front.")]
        public int initialVoices = 12;

        [Tooltip("Hard cap; past this the oldest non-looping voice is stolen.")]
        public int maxVoices = 24;

        [Tooltip("A lazy-loaded clip that arrives later than this is dropped, not played late.")]
        public float lateLoadDropSeconds = 0.3f;

        public List<SoundEntry> entries = new List<SoundEntry>();

        [Header("Music")]
        [Tooltip("Crossfade length when the track changes (seconds).")]
        public float musicFadeSeconds = 1f;

        [Tooltip("Track to switch to when each scene loads. Scenes not listed keep whatever is playing.")]
        public List<SceneMusic> sceneMusic = new List<SceneMusic>();

        public bool TryGetSceneMusic(string sceneName, out SoundId music)
        {
            foreach (SceneMusic m in sceneMusic)
            {
                if (m != null && m.sceneName == sceneName)
                {
                    music = m.music;
                    return true;
                }
            }

            music = SoundId.None;
            return false;
        }

        public Dictionary<SoundId, SoundEntry> BuildLookup()
        {
            var lookup = new Dictionary<SoundId, SoundEntry>(entries.Count);

            foreach (SoundEntry e in entries)
            {
                if (e == null || e.id == SoundId.None)
                    continue;

                if (lookup.ContainsKey(e.id))
                {
                    Debug.LogWarning($"[Audio] Duplicate SoundLibrary entry for {e.id} — first one wins.");
                    continue;
                }

                lookup.Add(e.id, e);
            }

            return lookup;
        }
    }
}
