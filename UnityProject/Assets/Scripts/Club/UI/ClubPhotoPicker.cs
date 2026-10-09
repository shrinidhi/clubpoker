using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Pick a club photo: device gallery (NativeGallery) → our crop screen
/// (ClubPhotoCropScreen: fixed square, the player moves / zooms the photo) → small
/// JPG data URI ready to show and upload as base64 JSON. Shared by Create Club and
/// Club Profile.
/// </summary>
public static class ClubPhotoPicker
{
    // Loaded for cropping. Decode time grows with this (Android decodes, re-encodes a
    // temp file, Unity decodes again), so keep it just above the 512 output: zoomed
    // up to ~1.5× the crop still has 512 real pixels.
    private const int  LoadMaxSize = 768;
    // Quick look: decoding a big camera JPEG straight to a small size is several
    // times faster (Android samples it down while decoding).
    private const int  PreviewMaxSize = 256;
    // Final icon size, and the upload cap.
    private const int  MaxSize  = 512;
    private const long MaxBytes = 200 * 1024;

    // Resources/ClubPhotoCropScreen.prefab — created on first use, kept hidden.
    private const string CropScreenPrefab = "ClubPhotoCropScreen";
    private static ClubPhotoCropScreen _cropScreen;

    private static ClubPhotoCropScreen CropScreen()
    {
        if (_cropScreen != null) return _cropScreen;   // destroyed with its scene → null again

        ClubPhotoCropScreen prefab = Resources.Load<ClubPhotoCropScreen>(CropScreenPrefab);
        if (prefab == null)
        {
            Debug.LogError($"[ClubPhotoPicker] Resources/{CropScreenPrefab} missing");
            return null;
        }

        _cropScreen = UnityEngine.Object.Instantiate(prefab);
        _cropScreen.gameObject.SetActive(false);
        return _cropScreen;
    }

    /// <summary>
    /// Opens the gallery, then the crop screen. On ✓ calls <paramref name="onPicked"/>
    /// with the square texture (caller owns it — Destroy when done) and its data URI.
    /// Cancel at either step → no call. Failures are reported to the player here.
    /// </summary>
    public static void Pick(Action<Texture2D, string> onPicked)
    {
        // Build the crop screen now, while the gallery is opening — the first
        // creation otherwise lands after the pick, as delay.
        if (CropScreen() == null)
        {
            ShowMessage("Photo picker unavailable");
            return;
        }

        NativeGallery.GetImageFromGallery(path =>
        {
            if (string.IsNullOrEmpty(path)) return;   // cancelled

            LoadAndCropAsync(path, onPicked).Forget();
        }, "Select Club Photo", "image/*");
    }

    /// <summary>
    /// Two steps, so the photo shows fast: the crop screen opens at once (spinner),
    /// a small preview drops in almost immediately (the player can already move /
    /// zoom it), then the sharp photo loads behind it and swaps in at the same
    /// position. ✓ unlocks when the sharp one is in.
    /// </summary>
    private static async UniTaskVoid LoadAndCropAsync(string path, Action<Texture2D, string> onPicked)
    {
        ClubPhotoCropScreen screen = CropScreen();
        if (screen == null) return;

        screen.ShowLoading(onCancel: null);

        var timer = System.Diagnostics.Stopwatch.StartNew();

        // 1. Preview — display only, so it can stay non-readable (less memory).
        Texture2D preview = await LoadAsync(path, PreviewMaxSize, readable: false);
        long previewMs = timer.ElapsedMilliseconds;

        if (preview == null)
        {
            if (screen.IsWaitingForPhoto) screen.Cancel();
            ShowMessage("Could not load image");
            return;
        }

        if (!screen.IsWaitingForPhoto)   // ✕ while it decoded
        {
            UnityEngine.Object.Destroy(preview);
            return;
        }

        Texture2D full = null;

        screen.Show(preview, MaxSize, cropped =>
        {
            // Screen closed (✓ or ✕) — both loaded textures are done with. (The
            // preview may already be gone, destroyed when the sharp one swapped in.)
            if (preview != null) UnityEngine.Object.Destroy(preview);
            if (full != null)    UnityEngine.Object.Destroy(full);

            if (cropped != null) Finish(cropped, onPicked);
        }, isFinal: false);

        // 2. Sharp, readable photo for the actual crop.
        full = await LoadAsync(path, LoadMaxSize, readable: true);

        Debug.Log($"[ClubPhotoPicker] preview {previewMs} ms, full {timer.ElapsedMilliseconds} ms" +
                  (full != null ? $" ({full.width}x{full.height})" : " (full failed)"));

        // Screen moved on (✕, or another photo) → this one isn't wanted.
        if (screen.CurrentSource != preview)
        {
            if (full != null) UnityEngine.Object.Destroy(full);
            return;
        }

        if (full == null)
        {
            screen.Cancel();   // → callback destroys the preview
            ShowMessage("Could not load image");
            return;
        }

        screen.UpgradeSource(full);
        UnityEngine.Object.Destroy(preview);
    }

    private static async UniTask<Texture2D> LoadAsync(string path, int maxSize, bool readable)
    {
        try
        {
            // Downscale (native, own thread) + decode both off the main thread.
            return await NativeGallery.LoadImageAtPathAsync(path, maxSize, markTextureNonReadable: !readable);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ClubPhotoPicker] load failed: {e.Message}");
            return null;
        }
    }

    // Cropped square → data URI → caller.
    private static void Finish(Texture2D cropped, Action<Texture2D, string> onPicked)
    {
        string dataUri = Encode(cropped);
        if (dataUri == null)
        {
            UnityEngine.Object.Destroy(cropped);
            ShowMessage("Image too large");
            return;
        }

        onPicked?.Invoke(cropped, dataUri);
    }

    // JPG, dropping quality until it fits. null = still too big.
    private static string Encode(Texture2D square)
    {
        int quality = 85;
        byte[] jpg = square.EncodeToJPG(quality);
        while (jpg.Length > MaxBytes && quality > 30)
        {
            quality -= 15;
            jpg = square.EncodeToJPG(quality);
        }

        return jpg.Length > MaxBytes ? null : ClubImageUtil.ToDataUri(jpg, "image/jpeg");
    }

    private static void ShowMessage(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
