using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Shows a club's icon: its custom photo (logoUrl) when it has one, otherwise its
/// stock badge. logoUrl may be a base64 data URI (as uploaded) or an http(s) URL
/// (if the server ever moves photos to storage) — both are handled.
///
/// Sprites are cached by logoUrl for the session, so the carousel and header don't
/// decode or download the same photo twice.
/// </summary>
public static class ClubLogo
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    // Last logo asked for per Image: a slow download mustn't overwrite a newer one
    // (e.g. a carousel cell reused for another club).
    private static readonly Dictionary<Image, string> Pending = new Dictionary<Image, string>();

    /// <summary>
    /// Badge right away, then the photo once it's ready (instant if cached).
    /// </summary>
    public static void Apply(Image target, Sprite badgeSprite, string logoUrl)
    {
        if (target == null) return;

        if (string.IsNullOrEmpty(logoUrl))
        {
            Pending.Remove(target);
            if (badgeSprite != null) target.sprite = badgeSprite;
            return;
        }

        if (Cache.TryGetValue(logoUrl, out Sprite cached) && cached != null)
        {
            Pending.Remove(target);
            target.sprite = cached;
            return;
        }

        if (badgeSprite != null) target.sprite = badgeSprite;

        Pending[target] = logoUrl;
        LoadAsync(target, logoUrl, sprite => target.sprite = sprite).Forget();
    }

    /// <summary>
    /// Two-image icon: the badge in <paramref name="badgeImage"/>, a photo in its own
    /// <paramref name="photoImage"/> (e.g. inside the badge's padding). Only one shows.
    ///
    /// The badge is hidden by switching off its Image component, not its GameObject,
    /// so a photo placed as a CHILD of the badge stays visible — sibling setups work
    /// the same. The photo stays hidden until it has loaded (an Image with no sprite
    /// draws as a white square). No photoImage → same as Apply on the badge image.
    /// </summary>
    public static void Show(Image badgeImage, Image photoImage, Sprite badgeSprite, string logoUrl)
    {
        if (photoImage == null)
        {
            Apply(badgeImage, badgeSprite, logoUrl);
            return;
        }

        if (string.IsNullOrEmpty(logoUrl))
        {
            Pending.Remove(photoImage);
            ShowBadgeOnly(badgeImage, photoImage, badgeSprite);
            return;
        }

        if (Cache.TryGetValue(logoUrl, out Sprite cached) && cached != null)
        {
            Pending.Remove(photoImage);
            ShowPhotoSprite(badgeImage, photoImage, cached);
            return;
        }

        // Badge until the photo is ready.
        ShowBadgeOnly(badgeImage, photoImage, badgeSprite);

        Pending[photoImage] = logoUrl;
        LoadAsync(photoImage, logoUrl, sprite => ShowPhotoSprite(badgeImage, photoImage, sprite)).Forget();
    }

    /// <summary>Two-image icon, showing a sprite already in hand (a photo just picked).</summary>
    public static void ShowPhotoSprite(Image badgeImage, Image photoImage, Sprite photo)
    {
        if (photoImage == null) { if (badgeImage != null) badgeImage.sprite = photo; return; }

        Pending.Remove(photoImage);
        photoImage.sprite = photo;
        photoImage.enabled = true;
        photoImage.gameObject.SetActive(true);
        if (badgeImage != null) badgeImage.enabled = false;
    }

    /// <summary>Two-image icon, showing the badge.</summary>
    public static void ShowBadgeOnly(Image badgeImage, Image photoImage, Sprite badgeSprite)
    {
        if (photoImage != null) photoImage.gameObject.SetActive(false);

        if (badgeImage == null) return;
        badgeImage.enabled = true;
        if (badgeSprite != null) badgeImage.sprite = badgeSprite;
    }

    /// <summary>A photo just uploaded — cache it so the screens showing it next
    /// don't have to decode it again.</summary>
    public static void Remember(string logoUrl, Sprite sprite)
    {
        if (!string.IsNullOrEmpty(logoUrl) && sprite != null)
            Cache[logoUrl] = sprite;
    }

    private static async UniTaskVoid LoadAsync(Image target, string logoUrl, System.Action<Sprite> onLoaded)
    {
        Sprite sprite = await LoadSpriteAsync(logoUrl);

        if (sprite != null) Cache[logoUrl] = sprite;

        // Still wanted, and the Image still exists?
        if (target == null || !Pending.TryGetValue(target, out string wanted) || wanted != logoUrl)
            return;

        Pending.Remove(target);

        // Failed → whatever is showing (the badge) stays.
        if (sprite != null) onLoaded(sprite);
    }

    private static async UniTask<Sprite> LoadSpriteAsync(string logoUrl)
    {
        if (logoUrl.StartsWith("data:"))
            return ClubImageUtil.SpriteFromTexture(ClubImageUtil.TextureFromDataUri(logoUrl));

        try
        {
            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(logoUrl))
            {
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ClubLogo] download failed: {req.error}");
                    return null;
                }
                return ClubImageUtil.SpriteFromTexture(DownloadHandlerTexture.GetContent(req));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ClubLogo] download failed: {e.Message}");
            return null;
        }
    }
}
