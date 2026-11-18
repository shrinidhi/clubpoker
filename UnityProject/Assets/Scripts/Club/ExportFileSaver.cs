using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Saves a downloaded file (club data export .xlsx) where the player can reach it.
///
///   Android 10+   → Downloads, through MediaStore (no storage permission).
///                   Open() then hands it to an app with ACTION_VIEW.
///   iOS           → app Documents folder, then the share sheet (Save to Files,
///                   Mail, …).
///   Android 7–9   → app storage, then the share sheet. Writing to the public
///                   Downloads folder there would need WRITE_EXTERNAL_STORAGE.
///
/// The export has already been paid for when this runs and the server can't
/// send it again, so Save() checks the write (size on disk == bytes received)
/// and falls back to app storage + share sheet if Downloads fails.
/// </summary>
public static class ExportFileSaver
{
    public const string XlsxMime =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public class Result
    {
        public bool Saved;
        /// Where the player will find it, for the message ("Download/x.xlsx").
        public string Location;
        /// Saved in the public Downloads folder (Android) — Open() works.
        public bool InDownloads;
        /// Android content:// uri when InDownloads.
        public string ContentUri;
        /// File in app storage (iOS / fallback) — shared via the share sheet.
        public string LocalPath;
        public string Error;
    }

    public static Result Save(byte[] bytes, string fileName, string mime = XlsxMime)
    {
        if (bytes == null || bytes.Length == 0)
            return new Result { Error = "empty file" };

        fileName = SafeFileName(fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidSdkInt() >= 29)
        {
            Result downloads = SaveToAndroidDownloads(bytes, fileName, mime);
            if (downloads.Saved)
                return downloads;

            Debug.LogWarning($"[ExportFileSaver] Downloads save failed ({downloads.Error}) — " +
                             "falling back to app storage + share sheet");
        }
#endif
        return SaveToAppStorage(bytes, fileName);
    }

    /// <summary>
    /// Show the saved file: Android Downloads → open it in an app (false if no
    /// app handles .xlsx); app storage → the share sheet.
    /// </summary>
    public static bool Open(Result saved, string mime = XlsxMime)
    {
        if (saved == null || !saved.Saved)
            return false;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (saved.InDownloads)
            return OpenAndroidUri(saved.ContentUri, mime);
#endif
        if (!string.IsNullOrEmpty(saved.LocalPath))
        {
#if UNITY_EDITOR
            Debug.Log($"[ExportFileSaver] Saved to {saved.LocalPath}");
            return true;
#else
            new NativeShare().AddFile(saved.LocalPath, mime).Share();
            return true;
#endif
        }

        return false;
    }

    // ── App storage (iOS, older Android, editor, fallback) ──────────────────

    private static Result SaveToAppStorage(byte[] bytes, string fileName)
    {
        try
        {
            // persistentDataPath is Documents on iOS.
            string dir = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(dir);

            string path = UniquePath(Path.Combine(dir, fileName));
            File.WriteAllBytes(path, bytes);

            var info = new FileInfo(path);
            if (!info.Exists || info.Length != bytes.Length)
                return new Result { Error = $"wrote {(info.Exists ? info.Length : 0)} of {bytes.Length} bytes" };

            return new Result
            {
                Saved = true,
                LocalPath = path,
                Location = Path.GetFileName(path),
            };
        }
        catch (Exception e)
        {
            return new Result { Error = e.Message };
        }
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        string dir = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);

        for (int i = 1; ; i++)
        {
            string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static string SafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"ClubExport_{DateTime.Now:yyyy-MM-dd_HHmmss}.xlsx";

        var sb = new StringBuilder(fileName.Length);
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in fileName)
            sb.Append(Array.IndexOf(invalid, c) >= 0 || c == '/' || c == '\\' || c == ':' ? '_' : c);

        string safe = sb.ToString().Trim();
        if (!safe.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            safe += ".xlsx";
        return safe;
    }

    // ── Android ─────────────────────────────────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR
    private static int AndroidSdkInt()
    {
        using var version = new AndroidJavaClass("android.os.Build$VERSION");
        return version.GetStatic<int>("SDK_INT");
    }

    private static AndroidJavaObject CurrentActivity()
    {
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        return player.GetStatic<AndroidJavaObject>("currentActivity");
    }

    private static Result SaveToAndroidDownloads(byte[] bytes, string fileName, string mime)
    {
        AndroidJavaObject uri = null;
        try
        {
            using AndroidJavaObject activity = CurrentActivity();
            using AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            using var values = new AndroidJavaObject("android.content.ContentValues");

            // MediaStore.MediaColumns.DISPLAY_NAME / MIME_TYPE / RELATIVE_PATH
            values.Call("put", "_display_name", fileName);
            values.Call("put", "mime_type", mime);
            values.Call("put", "relative_path", "Download/");

            using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
            using AndroidJavaObject collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");

            uri = resolver.Call<AndroidJavaObject>("insert", collection, values);
            if (uri == null)
                return new Result { Error = "MediaStore insert returned null" };

            // Java takes signed bytes.
            var signed = new sbyte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, signed, 0, bytes.Length);

            using (AndroidJavaObject stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
            {
                if (stream == null)
                    return new Result { Error = "openOutputStream returned null" };

                stream.Call("write", signed);
                stream.Call("flush");
                stream.Call("close");
            }

            // Read the size back — success only if all of it landed.
            long size;
            using (AndroidJavaObject fd = resolver.Call<AndroidJavaObject>("openFileDescriptor", uri, "r"))
            {
                size = fd.Call<long>("getStatSize");
                fd.Call("close");
            }

            if (size != bytes.Length)
            {
                resolver.Call<int>("delete", uri, null, null);
                return new Result { Error = $"wrote {size} of {bytes.Length} bytes" };
            }

            // MediaStore may rename on a clash ("x (1).xlsx") — show the real name.
            string savedName = fileName;
            try { savedName = QueryDisplayName(resolver, uri) ?? fileName; }
            catch { /* keep the requested name */ }

            return new Result
            {
                Saved = true,
                InDownloads = true,
                ContentUri = uri.Call<string>("toString"),
                Location = "Download/" + savedName,
            };
        }
        catch (Exception e)
        {
            return new Result { Error = e.Message };
        }
        finally
        {
            uri?.Dispose();
        }
    }

    private static string QueryDisplayName(AndroidJavaObject resolver, AndroidJavaObject uri)
    {
        string[] projection = { "_display_name" };
        using AndroidJavaObject cursor = resolver.Call<AndroidJavaObject>(
            "query", uri, projection, null, null, null);
        if (cursor == null)
            return null;

        try
        {
            return cursor.Call<bool>("moveToFirst") ? cursor.Call<string>("getString", 0) : null;
        }
        finally
        {
            cursor.Call("close");
        }
    }

    private static bool OpenAndroidUri(string contentUri, string mime)
    {
        if (string.IsNullOrEmpty(contentUri))
            return false;

        try
        {
            using AndroidJavaObject activity = CurrentActivity();
            using var uriClass = new AndroidJavaClass("android.net.Uri");
            using AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", contentUri);
            using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW");

            intent.Call<AndroidJavaObject>("setDataAndType", uri, mime);
            intent.Call<AndroidJavaObject>("addFlags", 1);   // FLAG_GRANT_READ_URI_PERMISSION

            activity.Call("startActivity", intent);
            return true;
        }
        catch (Exception e)
        {
            // ActivityNotFoundException: nothing installed opens .xlsx.
            Debug.LogWarning($"[ExportFileSaver] Open failed: {e.Message}");
            return false;
        }
    }
#endif
}
