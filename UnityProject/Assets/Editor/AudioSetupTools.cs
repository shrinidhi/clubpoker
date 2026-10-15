using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClubPoker.Audio;
using ClubPoker.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Sound library setup.
/// Tools ▸ ClubPoker ▸ Audio
///
/// Real clips: drop .wav/.ogg into Assets/Audio/Sfx, then drag each onto its
/// row in SoundLibrary.asset — the slot is made addressable automatically when
/// you run Setup Sound Library again.
/// </summary>
public static class AudioSetupTools
{
    private const string AUDIO_DIR       = "Assets/Audio";
    private const string LIBRARY_PATH    = AUDIO_DIR + "/SoundLibrary.asset";
    private const string PLACEHOLDER_DIR = AUDIO_DIR + "/Placeholder";
    private const string GROUP_NAME      = "Audio";

    [MenuItem("Tools/ClubPoker/Audio/Setup Sound Library")]
    public static void SetupLibrary()
    {
        Directory.CreateDirectory(AUDIO_DIR + "/Sfx");
        Directory.CreateDirectory(AUDIO_DIR + "/Music");
        AssetDatabase.Refresh();

        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LIBRARY_PATH);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, LIBRARY_PATH);
            Debug.Log($"[Audio] Created {LIBRARY_PATH}");
        }

        // One row per SoundId; never touch rows that already exist.
        var existing = new HashSet<SoundId>(library.entries.Where(e => e != null).Select(e => e.id));
        int added = 0;
        foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
        {
            if (id == SoundId.None || existing.Contains(id))
                continue;
            library.entries.Add(DefaultEntry(id));
            added++;
        }
        library.entries.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));

        // Scene → track defaults; only filled when the list is empty, so edits stick.
        if (library.sceneMusic.Count == 0)
        {
            foreach (string scene in new[] { "MainMenu", "LobbyScene", "ClubScene", "ProfileScene", "SettingsScene" })
                library.sceneMusic.Add(new SceneMusic { sceneName = scene, music = SoundId.MusicMenu });
            library.sceneMusic.Add(new SceneMusic { sceneName = "GameTableScene", music = SoundId.MusicTable });
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        AddressableAssetGroup group = GetOrCreateGroup(settings);

        MakeAddressable(settings, group, LIBRARY_PATH, SoundLibrary.ADDRESS);

        // Clips dragged onto rows by hand aren't addressable yet — fix that here.
        foreach (SoundEntry e in library.entries)
        {
            string guid = e.clip?.AssetGUID;
            if (string.IsNullOrEmpty(guid))
                continue;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                MakeAddressable(settings, group, path, "sfx/" + e.id);
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Selection.activeObject = library;

        var missing = library.entries.Where(e => string.IsNullOrEmpty(e.clip?.AssetGUID)).Select(e => e.id.ToString()).ToList();
        Debug.Log($"[Audio] Library ready — {added} rows added, {library.entries.Count} total." +
                  (missing.Count > 0 ? $" No clip yet: {string.Join(", ", missing)}" : " All clips assigned."));
    }

    /// <summary>
    /// Writes a short synth tone per SoundId and assigns it to every row that has
    /// no clip yet. Dev aid only — real clips replace them row by row.
    /// </summary>
    [MenuItem("Tools/ClubPoker/Audio/Generate Placeholder Tones")]
    public static void GeneratePlaceholders()
    {
        SetupLibrary();
        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LIBRARY_PATH);

        Directory.CreateDirectory(PLACEHOLDER_DIR);

        var written = new List<(SoundEntry entry, string path)>();
        foreach (SoundEntry e in library.entries)
        {
            // A looping beep as music is worse than silence.
            if (!string.IsNullOrEmpty(e.clip?.AssetGUID) || e.channel == AudioChannel.Music)
                continue;
            string path = $"{PLACEHOLDER_DIR}/{e.id}.wav";
            WriteToneWav(path, e.id);
            written.Add((e, path));
        }

        AssetDatabase.Refresh();

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        AddressableAssetGroup group = GetOrCreateGroup(settings);

        foreach (var (entry, path) in written)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            entry.clip = new AssetReferenceT<AudioClip>(guid);
            MakeAddressable(settings, group, path, "sfx/" + entry.id);
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Audio] {written.Count} placeholder tones assigned.");
    }

    private static SoundEntry DefaultEntry(SoundId id)
    {
        var e = new SoundEntry { id = id };

        switch (id)
        {
            case SoundId.ButtonClick:
                e.volume = 0.6f; e.maxConcurrent = 2;
                break;
            case SoundId.Notification:
                e.volume = 0.7f; e.minInterval = 0.3f; e.maxConcurrent = 1;
                break;
            case SoundId.Coins:
                e.minInterval = 0.5f; e.maxConcurrent = 1; e.preload = false;
                break;

            case SoundId.CardDeal:
            case SoundId.CardFlip:
                e.pitchRange = new Vector2(0.95f, 1.05f); e.maxConcurrent = 3;
                break;

            case SoundId.Check:
            case SoundId.Fold:
            case SoundId.Call:
            case SoundId.Raise:
            case SoundId.AllIn:
                e.pitchRange = new Vector2(0.97f, 1.03f); e.minInterval = 0.1f; e.maxConcurrent = 2;
                break;

            case SoundId.ChipsToPot:
                e.pitchRange = new Vector2(0.95f, 1.05f); e.minInterval = 0.15f; e.maxConcurrent = 1;
                break;
            case SoundId.PotWin:
                e.minInterval = 0.3f; e.maxConcurrent = 1;
                break;

            case SoundId.YourTurn:
            case SoundId.TimerWarning:
            case SoundId.DealerButton:
            case SoundId.ChatMessage:
                e.minInterval = 0.5f; e.maxConcurrent = 1;
                break;

            // Rare — load on first play, released on scene change.
            case SoundId.TimeBank:
            case SoundId.PlayerJoin:
            case SoundId.PlayerLeave:
            case SoundId.HandWon:
            case SoundId.Busted:
            case SoundId.Jackpot:
                e.minInterval = 0.5f; e.maxConcurrent = 1; e.preload = false;
                break;

            // Music — played by MusicPlayer (own sources, crossfade, streamed).
            case SoundId.MusicMenu:
            case SoundId.MusicTable:
                e.channel = AudioChannel.Music; e.volume = 0.5f; e.loop = true;
                e.preload = false; e.maxConcurrent = 1; e.minInterval = 0f;
                break;
        }

        return e;
    }

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings)
    {
        AddressableAssetGroup group = settings.FindGroup(GROUP_NAME);
        if (group != null)
            return group;

        group = settings.CreateGroup(GROUP_NAME, false, false, true, null,
            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

        var bundled = group.GetSchema<BundledAssetGroupSchema>();
        bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;

        Debug.Log($"[Audio] Created Addressables group '{GROUP_NAME}'");
        return group;
    }

    private static void MakeAddressable(AddressableAssetSettings settings, AddressableAssetGroup group,
                                        string assetPath, string address)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    // ── Placeholder tone synthesis ──────────────────────────────────────────

    private static void WriteToneWav(string path, SoundId id)
    {
        const int rate = 44100;

        // Short blips for frequent sounds, longer two-note stings for results.
        bool sting = id == SoundId.HandWon || id == SoundId.Jackpot || id == SoundId.PotWin ||
                     id == SoundId.Busted || id == SoundId.Coins;
        float seconds = sting ? 0.45f : 0.09f;
        float freq = 320f + ((int)id % 40) * 22f;

        int samples = (int)(rate * seconds);
        var pcm = new short[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float f = sting && t > seconds / 2f
                ? (id == SoundId.Busted ? freq * 0.75f : freq * 1.5f)
                : freq;
            float env = Mathf.Min(1f, t / 0.005f) * (1f - (float)i / samples);
            pcm[i] = (short)(Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f * short.MaxValue);
        }

        using (var w = new BinaryWriter(File.Create(path)))
        {
            int dataBytes = samples * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // mono
            w.Write(rate);
            w.Write(rate * 2);      // byte rate
            w.Write((short)2);      // block align
            w.Write((short)16);     // bits
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (short s in pcm)
                w.Write(s);
        }
    }
}

/// <summary>
/// First-import defaults for audio under Assets/Audio: short SFX decompress on
/// load (no decode hitch on play), music streams. Re-importing keeps any manual
/// changes made in the inspector.
/// </summary>
public class AudioImportDefaults : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Audio/") || !assetImporter.importSettingsMissing)
            return;

        var importer = (AudioImporter)assetImporter;
        bool music = assetPath.StartsWith("Assets/Audio/Music/");

        importer.forceToMono = !music;
        importer.loadInBackground = music;

        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.7f;
        s.preloadAudioData = !music;
        importer.defaultSampleSettings = s;
    }
}
