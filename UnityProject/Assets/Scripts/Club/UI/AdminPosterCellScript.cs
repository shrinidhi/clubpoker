using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One uploaded poster in the Club Poster list: thumbnail (decoded from base64) + end-time
/// label + delete. Owns the texture/sprite it creates and frees them in OnDestroy.
/// </summary>
public class AdminPosterCellScript : MonoBehaviour
{
    public Image Thumbnail_Image;
    public TextMeshProUGUI DateTime_Text;    // End Time — "2026/07/21 13:04"
    public Button Delete_Button;

    private PosterData _poster;
    private Action<PosterData> _onDelete;
    private Texture2D _tex;
    private Sprite _sprite;

    public string PosterId => _poster != null ? _poster.Id : null;

    public void Setup(PosterData poster, Action<PosterData> onDelete)
    {
        _poster = poster;
        _onDelete = onDelete;

        if (DateTime_Text != null) DateTime_Text.text = FormatEndTime(poster.ExpiresAt);

        if (Thumbnail_Image != null)
        {
            _tex = ClubImageUtil.TextureFromDataUri(poster.Url);
            _sprite = ClubImageUtil.SpriteFromTexture(_tex);
            Thumbnail_Image.sprite = _sprite;
            Thumbnail_Image.enabled = _sprite != null;
        }

        if (Delete_Button != null)
        {
            Delete_Button.onClick.RemoveAllListeners();
            Delete_Button.onClick.AddListener(() => _onDelete?.Invoke(_poster));
        }
    }

    // "2026-07-21T13:04:..." → "2026/07/21 13:04". Null/blank expiry → "-".
    private static string FormatEndTime(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "-";
        return DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal, out DateTime dt)
            ? dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
            : iso;
    }

    private void OnDestroy()
    {
        if (_sprite != null) Destroy(_sprite);
        if (_tex != null) Destroy(_tex);
    }
}
