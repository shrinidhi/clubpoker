using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;
using ClubPoker.Core;

/// <summary>
/// Club scene ▸ Club Profile (read-only) — what members and Table Managers see when
/// they tap the club icon, name, ID or notice in the header: icon (photo or badge),
/// name, ID, the full notice, and the club's posters in a horizontal scroll row.
/// View only — nothing is editable and posters don't open on tap.
///
/// The creator (and full Managers) get the editable ClubProfilePanelScript instead.
/// </summary>
public class ClubProfileInfoMembersPanel : MonoBehaviour
{
    public Button Close_Button;

    [Header("Icon")]
    [Tooltip("Badge logo.")]
    public Image ClubBadgeImage;
    [Tooltip("Optional: club photo (e.g. a child of the badge, inside its padding).")]
    public Image ExternalPhotoImage;
    public ClubBadgeSO ClubBadgeSO;

    [Header("Info")]
    public TextMeshProUGUI Name_Text;
    public TextMeshProUGUI Id_Text;
    [Tooltip("The full notice — the header may cut it short with \"…\".")]
    public TextMeshProUGUI Notice_Text;

    [Header("Posters")]
    [Tooltip("Scroll view Content (Horizontal Layout Group + Content Size Fitter).")]
    public RectTransform PosterContent;
    [Tooltip("PosterImg prefab (from Assets/Prefabs/Club) — the frame cell spawned for every " +
             "poster. Its child Image shows the poster. Any PosterImg placed under Content " +
             "for layout preview is removed at runtime.")]
    public RectTransform PosterTemplate;
    [Tooltip("Gap between the frame edge and the poster, in pixels. 0 = edge to edge.")]
    [Min(0f)] float PosterPadding = 2.0f;

    // Textures/sprites decoded for the current posters — freed on clear / destroy.
    private readonly List<Object> _posterAssets = new List<Object>();

    // Bumped on every open, so posters from an earlier open that land late are dropped.
    private int _loadVersion;

    private void Awake()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(Close);
    }

    /// <summary>Show the current club and open.</summary>
    public void Open()
    {
        // Latest cached detail if it has loaded, else the club as it was opened.
        ClubDetailData detail = ClubContext.ClubDetail;
        ClubListData club     = ClubContext.SelectedClub;

        string name    = detail?.Name ?? club?.Name ?? ClubContext.ClubName ?? "";
        string code    = detail?.ClubCode ?? club?.ClubCode ?? "";
        string badge   = (detail?.Badge ?? club?.Badge ?? "").ToLower();
        string notice  = detail?.Description ?? club?.Description;
        string logoUrl = detail != null ? detail.LogoUrl : club?.LogoUrl;

        if (Name_Text != null)   Name_Text.text   = name;
        if (Id_Text != null)     Id_Text.text     = "ID: " + code;
        if (Notice_Text != null) Notice_Text.text = string.IsNullOrEmpty(notice)
                                                    ? Loc.Get("welcome_club_poker")
                                                    : notice;

        ClubLogo.Show(ClubBadgeImage, ExternalPhotoImage, BadgeSprite(badge), logoUrl);

        gameObject.SetActive(true);

        // Fresh posters every open — the creator may have posted since.
        LoadPostersAsync(ClubContext.ClubId).Forget();
    }

    public void Close()
    {
        _loadVersion++;   // drop any poster load still in flight
        gameObject.SetActive(false);
    }

    #region Posters

    private async UniTaskVoid LoadPostersAsync(string clubId)
    {
        int version = ++_loadVersion;

        ClearPosters();

        if (ClubManager.Instance == null || string.IsNullOrEmpty(clubId)) return;

        List<PosterData> posters = new List<PosterData>();
        try
        {
            var res = await ClubManager.Instance.GetPostersAsync(clubId);
            if (res?.Posters != null)
                foreach (PosterData p in res.Posters)
                    if (p != null && !string.IsNullOrEmpty(p.Url))
                        posters.Add(p);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ClubProfileInfoMembersPanel] posters fetch failed: {e.Message}");
            return;
        }

        // Closed or reopened while loading → this result is stale.
        if (version != _loadVersion || !gameObject.activeInHierarchy) return;

        posters.Sort((a, b) => a.Order.CompareTo(b.Order));

        if (posters.Count == 0 || PosterContent == null || PosterTemplate == null)
            return;   // no posters — the panel stays, just empty

        // Content must grow rightwards from the left edge. With stretched anchors the
        // Content Size Fitter's width is added to the viewport's, leaving empty
        // scroll space after the last poster.
        PosterContent.anchorMin = new Vector2(0f, PosterContent.anchorMin.y);
        PosterContent.anchorMax = new Vector2(0f, PosterContent.anchorMax.y);
        PosterContent.pivot     = new Vector2(0f, PosterContent.pivot.y);

        foreach (PosterData poster in posters)
            AddPoster(poster.Url);

        // Start scrolled to the first poster.
        PosterContent.anchoredPosition = new Vector2(0f, PosterContent.anchoredPosition.y);
    }

    // Clones the PosterImg frame and fits the poster inside it at its own aspect:
    // landscape → full width, shorter; portrait → full height, narrower; square → fills.
    // Never cropped or stretched.
    private void AddPoster(string dataUri)
    {
        Texture2D tex = ClubImageUtil.TextureFromDataUri(dataUri);
        if (tex == null) return;   // undecodable — skip it rather than show an empty frame

        Sprite sprite = ClubImageUtil.SpriteFromTexture(tex);
        _posterAssets.Add(tex);
        _posterAssets.Add(sprite);

        RectTransform cell = Instantiate(PosterTemplate, PosterContent);
        cell.gameObject.SetActive(true);

        // View only — a Button on the frame would still tint on press.
        Button button = cell.GetComponent<Button>();
        if (button != null) button.enabled = false;

        Image poster = PosterImageIn(cell);
        if (poster == null) return;

        poster.sprite         = sprite;
        poster.color          = Color.white;
        poster.preserveAspect = true;
        poster.enabled        = true;

        // Centre it and size it to the largest box of the poster's aspect that fits the
        // frame inside PosterPadding: portrait touches top + bottom, landscape left + right.
        RectTransform rt = poster.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Vector2 box   = cell.rect.size - Vector2.one * (PosterPadding * 2f);
        if (box.x <= 0f || box.y <= 0f) box = cell.rect.size;   // padding bigger than the frame
        float   scale = Mathf.Min(box.x / tex.width, box.y / tex.height);
        rt.sizeDelta  = new Vector2(tex.width * scale, tex.height * scale);
    }

    // The poster Image is the frame's first child Image (the frame itself is skipped).
    private static Image PosterImageIn(RectTransform cell)
    {
        foreach (Image img in cell.GetComponentsInChildren<Image>(true))
            if (img.transform != cell)
                return img;
        return null;
    }

    private void ClearPosters()
    {
        if (PosterContent != null)
            for (int i = PosterContent.childCount - 1; i >= 0; i--)
            {
                Transform child = PosterContent.GetChild(i);
                if (child == PosterTemplate) continue;   // template placed in the scene — keep
                Destroy(child.gameObject);
            }

        // A template living in the scene is hidden, not destroyed. A prefab asset is
        // never touched — SetActive on it would change the asset itself.
        if (PosterTemplate != null && PosterTemplate.gameObject.scene.IsValid())
            PosterTemplate.gameObject.SetActive(false);

        foreach (Object asset in _posterAssets)
            if (asset != null) Destroy(asset);
        _posterAssets.Clear();
    }

    private void OnDestroy()
    {
        foreach (Object asset in _posterAssets)
            if (asset != null) Destroy(asset);
        _posterAssets.Clear();
    }

    #endregion

    private Sprite BadgeSprite(string badgeKey)
    {
        if (ClubBadgeSO == null || string.IsNullOrEmpty(badgeKey)) return null;

        foreach (ClubBadgeData data in ClubBadgeSO.ClubBadges)
            if (data.BadgeName.ToLower() == badgeKey)
                return data.BadgeImage;

        return null;
    }
}
