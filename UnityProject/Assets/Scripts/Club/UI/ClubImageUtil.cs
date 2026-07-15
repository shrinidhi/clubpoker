using System;
using UnityEngine;

/// <summary>
/// Base64 data-URI ⇄ texture helpers for club posters (images travel as base64 JSON).
/// </summary>
public static class ClubImageUtil
{
    /// Decode a "data:image/...;base64,XXXX" (or bare base64) string into a Texture2D.
    /// Returns null on any failure. Caller owns the texture — Destroy it when done.
    public static Texture2D TextureFromDataUri(string dataUri)
    {
        if (string.IsNullOrEmpty(dataUri)) return null;
        try
        {
            int comma = dataUri.IndexOf(',');
            string b64 = comma >= 0 ? dataUri.Substring(comma + 1) : dataUri;
            byte[] bytes = Convert.FromBase64String(b64);

            var tex = new Texture2D(2, 2);
            return tex.LoadImage(bytes) ? tex : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ClubImageUtil] decode failed: {e.Message}");
            return null;
        }
    }

    public static Sprite SpriteFromTexture(Texture2D tex)
    {
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    /// Wrap raw JPEG/PNG bytes as a data URI for upload.
    public static string ToDataUri(byte[] bytes, string mime = "image/jpeg")
    {
        return $"data:{mime};base64," + Convert.ToBase64String(bytes);
    }
}
