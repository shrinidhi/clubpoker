using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;

/// <summary>
/// Club scene ▸ Club Info — the read-only club profile everyone but the creator sees
/// when tapping the club icon, name, ID or notice in the header: icon (photo or
/// badge), name, ID, the full notice, and the club's posters.
///
/// The creator gets the editable Club Profile (ClubProfilePanelScript) instead.
/// </summary>
public class ClubInfoPopupScript : MonoBehaviour
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
    [Tooltip("The poster area (title + scroll view). Hidden when the club has no posters.")]
    public GameObject PosterSection;
    [Tooltip("Scroll view Content — give it a Vertical Layout Group + Content Size Fitter.")]
    public Transform PosterContent;
    [Tooltip("One poster thumbnail — a prefab with PosterImageShowPrefab on it.")]
    public PosterImageShowPrefab PosterItemPrefab;
    [Tooltip("Full-size viewer a tapped thumbnail opens (the club's existing poster " +
             "popup). It must draw above this popup.")]
    public PosterShowManager PosterViewer;

    private readonly List<PosterData> _posters = new List<PosterData>();

    // Bumped on every open, so posters from an earlier open that land late are dropped.
    private int _loadVersion;

    private void Awake()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(() => gameObject.SetActive(false));
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
                                                    ? CreateClubScreenScript.DefaultNotice
                                                    : notice;

        ClubLogo.Show(ClubBadgeImage, ExternalPhotoImage, BadgeSprite(badge), logoUrl);

        gameObject.SetActive(true);

        // Fresh posters every open — the creator may have posted since.
        LoadPostersAsync(ClubContext.ClubId).Forget();
    }

    #region Posters

    private async UniTaskVoid LoadPostersAsync(string clubId)
    {
        int version = ++_loadVersion;

        ClearPosters();
        if (PosterSection != null) PosterSection.SetActive(false);

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
            Debug.LogWarning($"[ClubInfoPopupScript] posters fetch failed: {e.Message}");
            return;
        }

        // Closed or reopened while loading → this result is stale.
        if (version != _loadVersion || !gameObject.activeInHierarchy) return;

        posters.Sort((a, b) => a.Order.CompareTo(b.Order));

        if (posters.Count == 0 || PosterContent == null || PosterItemPrefab == null)
            return;   // section stays hidden

        _posters.Clear();
        _posters.AddRange(posters);

        for (int i = 0; i < posters.Count; i++)
        {
            PosterImageShowPrefab item = Instantiate(PosterItemPrefab, PosterContent);
            item.Setup(posters[i].Url);

            // Tap → full-size viewer, opened at this poster.
            int index = i;
            Button button = item.GetComponent<Button>();
            if (button == null) button = item.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => OpenViewer(index));
        }

        if (PosterSection != null) PosterSection.SetActive(true);
    }

    private void OpenViewer(int index)
    {
        if (PosterViewer != null) PosterViewer.ShowPosters(_posters, index);
    }

    private void ClearPosters()
    {
        _posters.Clear();

        if (PosterContent == null) return;

        for (int i = PosterContent.childCount - 1; i >= 0; i--)
            Destroy(PosterContent.GetChild(i).gameObject);
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
