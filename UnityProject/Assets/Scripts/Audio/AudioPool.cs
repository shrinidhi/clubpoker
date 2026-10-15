using System.Collections.Generic;
using ClubPoker.Core;
using UnityEngine;

namespace ClubPoker.Audio
{
    /// <summary>
    /// Fixed set of AudioSources reused for every sound. Busy state is tracked by
    /// our own end time rather than AudioSource.isPlaying, so it's deterministic
    /// and testable. Grows up to maxVoices, then steals the oldest one-shot.
    /// </summary>
    public class AudioPool
    {
        public class Voice
        {
            public AudioSource Source;
            public SoundId Id;
            public float StartTime;
            public float BusyUntil;   // float.MaxValue while looping

            public bool IsBusy(float now) => now < BusyUntil;
            public bool IsLooping => BusyUntil == float.MaxValue;
        }

        private readonly Transform _parent;
        private readonly int _maxVoices;
        private readonly List<Voice> _voices = new List<Voice>();

        public int Count => _voices.Count;

        public AudioPool(Transform parent, int initialVoices, int maxVoices)
        {
            _parent = parent;
            _maxVoices = Mathf.Max(1, maxVoices);

            int initial = Mathf.Clamp(initialVoices, 1, _maxVoices);
            for (int i = 0; i < initial; i++)
                _voices.Add(CreateVoice(i));
        }

        /// <summary>
        /// Free voice, or a new one under the cap, or the oldest one-shot stolen.
        /// Null only when every voice is a loop.
        /// </summary>
        public Voice Rent(float now)
        {
            foreach (Voice v in _voices)
                if (!v.IsBusy(now))
                    return v;

            if (_voices.Count < _maxVoices)
            {
                Voice created = CreateVoice(_voices.Count);
                _voices.Add(created);
                return created;
            }

            Voice oldest = null;
            foreach (Voice v in _voices)
            {
                if (v.IsLooping)
                    continue;
                if (oldest == null || v.StartTime < oldest.StartTime)
                    oldest = v;
            }

            if (oldest != null)
                oldest.Source.Stop();

            return oldest;
        }

        public void MarkPlaying(Voice v, SoundId id, float now, float duration, bool loop)
        {
            v.Id = id;
            v.StartTime = now;
            v.BusyUntil = loop ? float.MaxValue : now + Mathf.Max(0f, duration);
        }

        public int CountActive(SoundId id, float now)
        {
            int n = 0;
            foreach (Voice v in _voices)
                if (v.Id == id && v.IsBusy(now))
                    n++;
            return n;
        }

        public void Stop(SoundId id)
        {
            foreach (Voice v in _voices)
            {
                if (v.Id != id)
                    continue;
                v.Source.Stop();
                v.Source.clip = null;
                v.BusyUntil = 0f;
            }
        }

        public void StopAll()
        {
            foreach (Voice v in _voices)
            {
                v.Source.Stop();
                v.Source.clip = null;
                v.BusyUntil = 0f;
            }
        }

        private Voice CreateVoice(int index)
        {
            var go = new GameObject($"Voice_{index:00}");
            go.transform.SetParent(_parent, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // 2D

            return new Voice { Source = src, Id = SoundId.None };
        }
    }
}
