using System;
using System.Threading;
using ClubPoker.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ClubPoker.Audio
{
    /// <summary>
    /// One music track at a time on two dedicated AudioSources (not the SFX pool),
    /// crossfading between them. Clips load through Addressables with no late-drop
    /// and are released once faded out. Owned by AudioManager, so it survives
    /// scene loads.
    /// </summary>
    public class MusicPlayer
    {
        private readonly AudioSource[] _sources = new AudioSource[2];
        private readonly AsyncOperationHandle<AudioClip>[] _handles = new AsyncOperationHandle<AudioClip>[2];
        private readonly Func<SoundId, SoundEntry> _lookup;
        private readonly Func<float> _channelVolume;
        private readonly float _fadeSeconds;

        private int _active;
        private int _version;                      // bumped per request; stale loads bail out
        private SoundId _current = SoundId.None;   // playing or loading
        private SoundId _wanted = SoundId.None;    // last request — kept while music is toggled off
        private CancellationTokenSource _fadeCts;
        private readonly System.Collections.Generic.HashSet<SoundId> _warnedMissing =
            new System.Collections.Generic.HashSet<SoundId>();

        public SoundId Current => _current;

        public MusicPlayer(Transform parent, Func<SoundId, SoundEntry> lookup, Func<float> channelVolume, float fadeSeconds)
        {
            _lookup = lookup;
            _channelVolume = channelVolume;
            _fadeSeconds = Mathf.Max(0f, fadeSeconds);

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"Music_{i}");
                go.transform.SetParent(parent, false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.priority = 0;   // never culled for SFX
                _sources[i] = src;
            }
        }

        public void Play(SoundId id)
        {
            _wanted = id;

            if (!MusicSettings.IsOn || id == _current)
                return;

            PlayAsync(id).Forget();
        }

        public void Stop()
        {
            _wanted = SoundId.None;
            FadeOutCurrent();
        }

        public void OnMusicToggled(bool on)
        {
            if (!on)
            {
                FadeOutCurrent();   // _wanted kept so turning it back on resumes
                return;
            }

            if (_wanted != SoundId.None && _wanted != _current)
                PlayAsync(_wanted).Forget();
        }

        public void Dispose()
        {
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
            _fadeCts = null;

            for (int i = 0; i < 2; i++)
                ReleaseSlot(i);
        }

        private async UniTaskVoid PlayAsync(SoundId id)
        {
            int version = ++_version;
            _current = id;

            SoundEntry entry = _lookup(id);
            if (entry == null || !entry.HasClip)
            {
                if (_warnedMissing.Add(id))
                    Debug.LogWarning($"[Audio] No music clip assigned for {id} — silent (warned once).");
                if (version == _version)
                    FadeOutCurrent();
                return;
            }

            var handle = Addressables.LoadAssetAsync<AudioClip>(entry.clip.RuntimeKey);
            AudioClip clip;
            try
            {
                clip = await handle;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Audio] Music load failed for {id}: {e.Message}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                if (version == _version)
                    _current = SoundId.None;
                return;
            }

            // A newer Play/Stop arrived while this was loading.
            if (version != _version || _sources[0] == null)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                return;
            }

            int oldSlot = _active;
            int newSlot = 1 - _active;

            // The incoming slot may still be fading out an earlier track — cut it.
            ReleaseSlot(newSlot);

            AudioSource to = _sources[newSlot];
            _handles[newSlot] = handle;
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            _active = newSlot;

            float target = entry.volume * _channelVolume();
            await CrossfadeAsync(_sources[oldSlot], oldSlot, to, target);
        }

        private void FadeOutCurrent()
        {
            _version++;
            _current = SoundId.None;

            // If a crossfade was mid-way, the outgoing track is still audible on
            // the other slot and its fade is about to be cancelled — cut it.
            ReleaseSlot(1 - _active);
            CrossfadeAsync(_sources[_active], _active, null, 0f).Forget();
        }

        private async UniTask CrossfadeAsync(AudioSource from, int fromSlot, AudioSource to, float toTarget)
        {
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
            _fadeCts = new CancellationTokenSource();
            CancellationToken ct = _fadeCts.Token;

            float fromStart = from != null ? from.volume : 0f;
            float toStart = to != null ? to.volume : 0f;

            for (float t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / _fadeSeconds;
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (to != null) to.volume = Mathf.Lerp(toStart, toTarget, k);

                // Cancelled = a newer crossfade took over; it finishes this job.
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    return;
            }

            if (to != null) to.volume = toTarget;
            if (fromSlot != _active || to == null)
                ReleaseSlot(fromSlot);
        }

        private void ReleaseSlot(int slot)
        {
            AudioSource src = _sources[slot];
            if (src != null)
            {
                src.Stop();
                src.clip = null;
                src.volume = 0f;
            }

            if (_handles[slot].IsValid())
                Addressables.Release(_handles[slot]);
            _handles[slot] = default;
        }
    }
}
