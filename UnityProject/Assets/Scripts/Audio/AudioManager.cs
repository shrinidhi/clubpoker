using System;
using System.Collections.Generic;
using ClubPoker.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace ClubPoker.Audio
{
    /// <summary>
    /// Plays every SoundId raised on GameAudio. Creates itself before the first
    /// scene, survives scene loads, loads SoundLibrary + clips via Addressables
    /// and plays them through a pooled set of AudioSources.
    ///
    /// Two independent switches: SoundSettings gates SFX (skipped while off, and
    /// playing ones stopped on the switch), MusicSettings gates music.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public bool IsReady { get; private set; }

        private SoundLibrary _library;
        private AsyncOperationHandle<SoundLibrary> _libraryHandle;
        private Dictionary<SoundId, SoundEntry> _entries;

        private readonly Dictionary<SoundId, AudioClip> _clips = new Dictionary<SoundId, AudioClip>();
        private readonly Dictionary<SoundId, AsyncOperationHandle<AudioClip>> _clipHandles =
            new Dictionary<SoundId, AsyncOperationHandle<AudioClip>>();
        private readonly HashSet<SoundId> _loading = new HashSet<SoundId>();
        private readonly HashSet<SoundId> _warnedMissing = new HashSet<SoundId>();
        private readonly SoundThrottle _throttle = new SoundThrottle();
        private float _sfxVolume = 1f;
        private float _musicVolume = 1f;

        private AudioPool _pool;
        private MusicPlayer _music;
        private SoundId _pendingMusic = SoundId.None;
        private bool _pendingMusicStop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            new GameObject("[AudioManager]").AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            gameObject.AddComponent<UIClickSounds>();

            // The one AudioListener for the whole app. Scenes carry their own on
            // their cameras, some disabled (MainMenu, Lobby, Club → silent) and
            // some enabled (→ "2 listeners" warning); DisableSceneListeners
            // switches those off as each scene loads. All sound is 2D, so the
            // listener's position doesn't matter.
            gameObject.AddComponent<AudioListener>();

            GameAudio.OnPlay += Play;
            GameAudio.OnStop += Stop;
            GameAudio.OnPlayMusic += PlayMusic;
            GameAudio.OnStopMusic += StopMusic;
            MusicSettings.OnChanged += OnMusicToggled;
            SoundSettings.OnChanged += OnSoundToggled;
            ToastEvents.OnShowToast += OnToast;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += DisableSceneListeners;
        }

        private void DisableSceneListeners(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
        }

        private void Start()
        {
            InitializeAsync().Forget();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            GameAudio.OnPlay -= Play;
            GameAudio.OnStop -= Stop;
            GameAudio.OnPlayMusic -= PlayMusic;
            GameAudio.OnStopMusic -= StopMusic;
            MusicSettings.OnChanged -= OnMusicToggled;
            SoundSettings.OnChanged -= OnSoundToggled;
            ToastEvents.OnShowToast -= OnToast;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= DisableSceneListeners;

            _music?.Dispose();

            foreach (var handle in _clipHandles.Values)
                if (handle.IsValid())
                    Addressables.Release(handle);
            _clipHandles.Clear();
            _clips.Clear();

            if (_libraryHandle.IsValid())
                Addressables.Release(_libraryHandle);

            Instance = null;
        }

        #region Init

        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                // Check first so a missing library is one clear warning, not an
                // Addressables InvalidKeyException.
                var locationsHandle = Addressables.LoadResourceLocationsAsync(SoundLibrary.ADDRESS, typeof(SoundLibrary));
                var locations = await locationsHandle;
                int found = locations?.Count ?? 0;
                Addressables.Release(locationsHandle);

                if (found == 0)
                {
                    Debug.LogWarning("[Audio] No addressable SoundLibrary — sound disabled. " +
                                     "Run Tools ▸ ClubPoker ▸ Audio ▸ Setup Sound Library.");
                    return;
                }

                _libraryHandle = Addressables.LoadAssetAsync<SoundLibrary>(SoundLibrary.ADDRESS);
                _library = await _libraryHandle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Audio] SoundLibrary load failed — sound disabled: {e.Message}");
                return;
            }

            if (this == null || _library == null)
                return;

            _entries = _library.BuildLookup();
            _pool = new AudioPool(transform, _library.initialVoices, _library.maxVoices);

            var preloads = new List<UniTask<AudioClip>>();
            foreach (SoundEntry e in _entries.Values)
                if (e.preload && e.HasClip)
                    preloads.Add(LoadClipAsync(e));

            await UniTask.WhenAll(preloads);

            if (this == null)
                return;

            _music = new MusicPlayer(transform,
                id => _entries.TryGetValue(id, out SoundEntry e) ? e : null,
                () => _musicVolume,
                _library.musicFadeSeconds);

            IsReady = true;
            Debug.Log($"[Audio] Ready — {_entries.Count} sounds, {_clips.Count} preloaded, {_pool.Count} voices.");

            // Music asked for before we were ready wins; otherwise the scene's own track.
            if (_pendingMusic != SoundId.None)
                _music.Play(_pendingMusic);
            else if (!_pendingMusicStop)
                ApplySceneMusic(SceneManager.GetActiveScene().name);
            _pendingMusic = SoundId.None;
            _pendingMusicStop = false;
        }

        private async UniTask<AudioClip> LoadClipAsync(SoundEntry entry)
        {
            if (_clips.TryGetValue(entry.id, out AudioClip cached))
                return cached;

            // Already in flight — the caller drops this play rather than queueing.
            if (!_loading.Add(entry.id))
                return null;

            var handle = Addressables.LoadAssetAsync<AudioClip>(entry.clip.RuntimeKey);
            try
            {
                AudioClip clip = await handle;
                _clipHandles[entry.id] = handle;
                _clips[entry.id] = clip;
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Audio] Clip load failed for {entry.id}: {e.Message}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                return null;
            }
            finally
            {
                _loading.Remove(entry.id);
            }
        }

        #endregion

        #region Playback

        private void Play(SoundId id)
        {
            if (!IsReady)
                return;

            // A music row played as a one-shot would loop forever in the SFX pool.
            if (_entries.TryGetValue(id, out SoundEntry music) && music.channel == AudioChannel.Music)
            {
                _music.Play(id);
                return;
            }

            // Sound off silences effects only; music follows its own switch.
            if (!SoundSettings.IsOn)
                return;

            if (!_entries.TryGetValue(id, out SoundEntry entry) || !entry.HasClip)
            {
                if (_warnedMissing.Add(id))
                    Debug.LogWarning($"[Audio] No clip assigned for {id} — skipping (warned once).");
                return;
            }

            float now = Time.unscaledTime;
            if (!_throttle.CanPlay(id, entry.minInterval, entry.maxConcurrent, _pool.CountActive(id, now), now))
                return;

            if (_clips.TryGetValue(id, out AudioClip clip))
            {
                PlayClip(entry, clip, now);
                return;
            }

            PlayLazyAsync(entry).Forget();
        }

        private async UniTaskVoid PlayLazyAsync(SoundEntry entry)
        {
            float requested = Time.unscaledTime;
            AudioClip clip = await LoadClipAsync(entry);

            if (clip == null || this == null)
                return;

            // A late sound is worse than none (e.g. a fold sound a second after the fold).
            float now = Time.unscaledTime;
            if (now - requested > _library.lateLoadDropSeconds)
                return;

            PlayClip(entry, clip, now);
        }

        private void PlayClip(SoundEntry entry, AudioClip clip, float now)
        {
            AudioPool.Voice voice = _pool.Rent(now);
            if (voice == null)
                return;

            float pitch = UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);
            if (pitch < 0.1f)
                pitch = 1f;

            AudioSource src = voice.Source;
            src.clip = clip;
            src.loop = entry.loop;
            src.pitch = pitch;
            src.volume = entry.volume * _sfxVolume;
            src.Play();

            _pool.MarkPlaying(voice, entry.id, now, clip.length / pitch, entry.loop);
            _throttle.Record(entry.id, now);
        }

        private void Stop(SoundId id)
        {
            if (_music != null && _music.Current == id)
            {
                _music.Stop();
                return;
            }

            _pool?.Stop(id);
        }

        private void PlayMusic(SoundId id)
        {
            if (!IsReady)
            {
                _pendingMusic = id;
                _pendingMusicStop = false;
                return;
            }

            _music.Play(id);
        }

        private void StopMusic()
        {
            if (!IsReady)
            {
                _pendingMusic = SoundId.None;
                _pendingMusicStop = true;
                return;
            }

            _music.Stop();
        }

        private void OnMusicToggled(bool on) => _music?.OnMusicToggled(on);

        // Cut effects already playing (loops included) the moment sound goes off.
        private void OnSoundToggled(bool on)
        {
            if (!on)
                _pool?.StopAll();
        }

        private void ApplySceneMusic(string sceneName)
        {
            if (!_library.TryGetSceneMusic(sceneName, out SoundId track))
                return;   // unlisted scene — keep whatever is playing

            if (track == SoundId.None)
                _music.Stop();
            else
                _music.Play(track);
        }

        /// <summary>Applies to sounds started after the call.</summary>
        public void SetChannelVolume(AudioChannel channel, float volume)
        {
            if (channel == AudioChannel.Music)
                _musicVolume = Mathf.Clamp01(volume);
            else
                _sfxVolume = Mathf.Clamp01(volume);
        }

        private void OnToast(string _) => Play(SoundId.Notification);

        #endregion

        #region Memory

        // Rare (non-preloaded) clips are loaded on demand; drop them on every scene
        // change so a jackpot sting doesn't sit in memory for the whole session.
        private void OnActiveSceneChanged(Scene from, Scene to)
        {
            if (!IsReady)
                return;   // InitializeAsync applies the then-active scene's music

            ApplySceneMusic(to.name);

            foreach (SoundEntry e in _entries.Values)
            {
                if (e.preload || !_clipHandles.TryGetValue(e.id, out var handle))
                    continue;

                _pool.Stop(e.id);
                _clips.Remove(e.id);
                _clipHandles.Remove(e.id);
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        #endregion

        #region Debug

        [ContextMenu("Play Every Sound")]
        private void DebugPlayAll() => DebugPlayAllAsync().Forget();

        private async UniTaskVoid DebugPlayAllAsync()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                if (id == SoundId.None || this == null)
                    continue;
                if (_entries != null && _entries.TryGetValue(id, out SoundEntry e) && e.channel == AudioChannel.Music)
                    continue;

                Debug.Log($"[Audio] ▶ {id}");
                Play(id);
                await UniTask.Delay(800, ignoreTimeScale: true);
                Stop(id);
            }
        }

        #endregion
    }
}
