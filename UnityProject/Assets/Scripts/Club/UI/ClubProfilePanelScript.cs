using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;

/// <summary>
/// Club scene ▸ Club Profile — the current club's icon, name and notice. Opened from
/// the club header and automatically right after the club is created.
///
/// Matches the reference: name and notice aren't edited in place — tapping either row
/// opens its own small popup, and its Confirm saves that one field straight away (a
/// rename may cost diamonds, so it's its own explicit step). The icon — stock badge or
/// a photo from the gallery — is picked on the panel and saved with Save.
///
/// Everything goes through PUT /api/clubs/{clubId}; the saved club comes back and is
/// handed to ClubContext.SetClubDetail, which repaints the club header.
/// </summary>
public class ClubProfilePanelScript : MonoBehaviour
{
    private const int NameMax = 20;

    // The header only shows a couple of lines (then "…"), so anything much longer is
    // never read — cap it where a real welcome message ends.
    private const int NoticeMax = 150;

    [Header("Panel")]
    public Button Close_Button;

    [Tooltip("The panel's own content (header, icon, rows, Save) — everything except " +
             "the two popups. Hidden when only the notice is opened from the club header.")]
    public GameObject PanelBody;

    [Tooltip("Saves the icon (badge or photo). Only enabled once the icon changed.")]
    [FormerlySerializedAs("CreateClub_Button")]
    public Button Save_Button;

    [Header("Icon")]
    [Tooltip("Badge logo in the preview (the logo, not its background).")]
    public Image ClubBadgeImage;
    [Tooltip("Photo in the preview, shown in place of the badge logo. Same setup as " +
             "Create Club's ClubExternalPhoto. Empty → the photo shows in ClubBadgeImage.")]
    public Image ExternalPhotoImage;
    public Button CameraButton;
    public GameObject ClubBadge_Prefab;
    public Transform ClubBadge_Content;
    public ClubBadgeSO ClubBadgeSO;
    public Button LeftButton;
    public Button RightButton;
    public ScrollRect ScrollRect;

    [Header("Rows (tap to edit)")]
    public TextMeshProUGUI Name_Text;
    public Button Name_Button;
    public TextMeshProUGUI Notice_Text;
    public Button Notice_Button;

    [Header("Club Name popup")]
    public GameObject NamePopup;
    public TMP_InputField NamePopup_Input;
    [Tooltip("\"First time change name is free\" / \"Change name cost N diamonds\".")]
    public TextMeshProUGUI NamePopup_HintText;
    public Button NamePopup_Confirm;
    public Button NamePopup_Close;

    [Header("Notice popup")]
    public GameObject NoticePopup;
    public TMP_InputField NoticePopup_Input;
    public Button NoticePopup_Confirm;
    public Button NoticePopup_Close;

    private readonly List<ClubBadgePrefabScript> _badgeItems = new List<ClubBadgePrefabScript>();
    private int _selectedBadgeIndex = -1;

    // What the club has now.
    private string _currentName = "";
    private string _currentNotice = "";
    private string _currentBadge = "";
    private string _currentLogoUrl;

    // Icon picked on the panel, not saved yet.
    private string    _pendingBadge;
    private string    _pendingPhotoDataUri;
    private Texture2D _pendingPhotoTex;
    private Sprite    _pendingPhotoSprite;

    private bool _built;
    private bool _busy;

    private void Start()
    {
        Close_Button.onClick.AddListener(Close);
        Save_Button.onClick.AddListener(OnSaveIconTap);
        LeftButton.onClick.AddListener(() => SelectBadgeByIndex(_selectedBadgeIndex - 1));
        RightButton.onClick.AddListener(() => SelectBadgeByIndex(_selectedBadgeIndex + 1));
        if (CameraButton != null) CameraButton.onClick.AddListener(OnCameraTap);

        Name_Button.onClick.AddListener(OpenNamePopup);
        Notice_Button.onClick.AddListener(OpenNoticePopup);

        NamePopup_Confirm.onClick.AddListener(OnNameConfirm);
        NamePopup_Close.onClick.AddListener(() => NamePopup.SetActive(false));
        NamePopup_Input.characterLimit = NameMax;
        NoticePopup_Input.characterLimit = NoticeMax;

        NoticePopup_Confirm.onClick.AddListener(OnNoticeConfirm);
        NoticePopup_Close.onClick.AddListener(CloseNoticePopup);

        GenerateBadges();
        _built = true;
        LoadFromClub();
    }

    private void OnEnable()
    {
        // First open is loaded from Start, once the badges exist.
        if (_built) LoadFromClub();
    }

    private void OnDestroy()
    {
        FreePendingPhoto();
    }

    /// <summary>Open the editor for the current club.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
    }

    #region Load

    /// Show the club as it is now and drop any unsaved icon pick.
    private void LoadFromClub()
    {
        // Latest cached detail if it has loaded, else the club as it was opened (the
        // detail fetch may still be in flight right after creating the club).
        ClubDetailData detail = ClubContext.ClubDetail;
        ClubListData club     = ClubContext.SelectedClub;

        _currentName    = detail?.Name ?? club?.Name ?? ClubContext.ClubName ?? "";
        _currentBadge   = (detail?.Badge ?? club?.Badge ?? "").ToLower();
        _currentNotice  = detail?.Description ?? club?.Description ?? "";
        _currentLogoUrl = detail != null ? detail.LogoUrl : club?.LogoUrl;

        if (string.IsNullOrEmpty(_currentNotice))
            _currentNotice = CreateClubScreenScript.DefaultNotice;

        if (Name_Text != null)   Name_Text.text   = _currentName;
        if (Notice_Text != null) Notice_Text.text = _currentNotice;

        if (NamePopup != null)   NamePopup.SetActive(false);
        if (NoticePopup != null) NoticePopup.SetActive(false);

        ResetIcon();

        // Opened from the club header's notice → straight to the notice popup. Done
        // here, after the load, because the first open loads from Start (a frame
        // after activation) and would otherwise close the popup again.
        if (_noticeOnly) ShowNoticePopup();
    }

    #endregion

    #region Icon

    // Back to the saved icon: its photo if it has one (no badge ticked), else its badge.
    private void ResetIcon()
    {
        FreePendingPhoto();
        _pendingBadge = null;

        int index = _badgeItems.FindIndex(b => b.BadgeKey == _currentBadge);
        _selectedBadgeIndex = index >= 0 ? index : 0;

        bool hasPhoto = !string.IsNullOrEmpty(_currentLogoUrl);
        TickBadge(hasPhoto ? -1 : _selectedBadgeIndex);
        ScrollToBadge(_selectedBadgeIndex);
        UpdateArrowButtons();

        if (hasPhoto) ShowSavedPhoto(_currentLogoUrl);
        else          ShowBadge(BadgeSprite(_selectedBadgeIndex));

        RefreshSaveButton();
    }

    private void GenerateBadges()
    {
        _badgeItems.Clear();

        for (int i = ClubBadge_Content.childCount - 1; i >= 0; i--)
            Destroy(ClubBadge_Content.GetChild(i).gameObject);

        foreach (ClubBadgeData data in ClubBadgeSO.ClubBadges)
        {
            GameObject obj = Instantiate(ClubBadge_Prefab, ClubBadge_Content);
            var badge = obj.GetComponent<ClubBadgePrefabScript>();
            badge.Setup(data, SelectBadge);
            _badgeItems.Add(badge);
        }
    }

    // A badge tapped (or reached with the arrows) — replaces any photo.
    public void SelectBadge(ClubBadgePrefabScript selectedItem, string badgeKey)
    {
        if (selectedItem == null) return;

        FreePendingPhoto();

        _selectedBadgeIndex = _badgeItems.IndexOf(selectedItem);
        _pendingBadge = badgeKey.ToLower();

        TickBadge(_selectedBadgeIndex);
        UpdateArrowButtons();

        ShowBadge(BadgeSprite(_selectedBadgeIndex));

        RefreshSaveButton();
    }

    private void SelectBadgeByIndex(int index)
    {
        if (index < 0 || index >= _badgeItems.Count) return;

        SelectBadge(_badgeItems[index], _badgeItems[index].BadgeKey);
        ScrollToBadge(index);
    }

    private void OnCameraTap()
    {
        ClubPhotoPicker.Pick((square, dataUri) =>
        {
            FreePendingPhoto();
            _pendingPhotoTex     = square;
            _pendingPhotoSprite  = ClubImageUtil.SpriteFromTexture(square);
            _pendingPhotoDataUri = dataUri;
            _pendingBadge        = null;

            ShowPickedPhoto(_pendingPhotoSprite);
            TickBadge(-1);   // a photo, not a badge

            RefreshSaveButton();
        });
    }

    // ── Preview: badge logo or photo, one visible at a time ──────────────

    private void ShowBadge(Sprite badgeSprite)
    {
        SetPhotoVisible(false);
        if (ClubBadgeImage != null && badgeSprite != null) ClubBadgeImage.sprite = badgeSprite;
    }

    // The club's saved photo — decoded / downloaded by ClubLogo (cached).
    private void ShowSavedPhoto(string logoUrl)
    {
        SetPhotoVisible(true);
        ClubLogo.Apply(PhotoTarget, null, logoUrl);
    }

    // A photo just picked from the gallery, not saved yet.
    private void ShowPickedPhoto(Sprite photo)
    {
        SetPhotoVisible(true);
        if (PhotoTarget != null) PhotoTarget.sprite = photo;
    }

    // Where photos go: their own image if wired, else the badge image.
    private Image PhotoTarget => ExternalPhotoImage != null ? ExternalPhotoImage : ClubBadgeImage;

    private void SetPhotoVisible(bool photo)
    {
        if (ExternalPhotoImage == null) return;   // single-image setup: nothing to swap

        ExternalPhotoImage.gameObject.SetActive(photo);
        if (ClubBadgeImage != null) ClubBadgeImage.gameObject.SetActive(!photo);
    }

    private void TickBadge(int index)
    {
        for (int i = 0; i < _badgeItems.Count; i++)
            _badgeItems[i].SetSelected(i == index);
    }

    private Sprite BadgeSprite(int index) =>
        index >= 0 && index < ClubBadgeSO.ClubBadges.Count
            ? ClubBadgeSO.ClubBadges[index].BadgeImage
            : null;

    private void ScrollToBadge(int index)
    {
        if (ScrollRect == null || _badgeItems.Count <= 1) return;

        Canvas.ForceUpdateCanvases();
        ScrollRect.horizontalNormalizedPosition = (float)index / (_badgeItems.Count - 1);
    }

    private void UpdateArrowButtons()
    {
        if (LeftButton != null)  LeftButton.interactable  = _selectedBadgeIndex > 0;
        if (RightButton != null) RightButton.interactable = _selectedBadgeIndex < _badgeItems.Count - 1;
    }

    private bool IconChanged()
    {
        if (_pendingPhotoDataUri != null) return true;
        if (_pendingBadge == null) return false;

        // A badge differs from the saved icon if it's another badge, or the club
        // currently shows a photo (picking a badge removes it).
        return _pendingBadge != _currentBadge || !string.IsNullOrEmpty(_currentLogoUrl);
    }

    private void RefreshSaveButton()
    {
        Save_Button.interactable = !_busy && IconChanged();
    }

    private void OnSaveIconTap()
    {
        if (_busy || !IconChanged()) return;

        var fields = new Dictionary<string, object>();

        if (_pendingPhotoDataUri != null)
        {
            fields["logoUrl"] = _pendingPhotoDataUri;
        }
        else
        {
            if (_pendingBadge != _currentBadge) fields["badge"] = _pendingBadge;

            // Badge chosen over a photo → remove the photo so the badge shows.
            if (!string.IsNullOrEmpty(_currentLogoUrl)) fields["logoUrl"] = null;
        }

        // Keep the decoded photo for the screens that show it next.
        string uploaded = _pendingPhotoDataUri;
        Sprite uploadedSprite = _pendingPhotoSprite;

        SaveAsync(fields, "Club icon updated", () =>
        {
            // The cache now owns the sprite — don't destroy it with the pending pick.
            if (uploaded != null)
            {
                ClubLogo.Remember(uploaded, uploadedSprite);
                _pendingPhotoSprite = null;
                _pendingPhotoTex = null;
            }
        }).Forget();
    }

    private void FreePendingPhoto()
    {
        _pendingPhotoDataUri = null;
        if (_pendingPhotoSprite != null) { Destroy(_pendingPhotoSprite); _pendingPhotoSprite = null; }
        if (_pendingPhotoTex != null)    { Destroy(_pendingPhotoTex);    _pendingPhotoTex = null; }
    }

    #endregion

    #region Name popup

    private void OpenNamePopup()
    {
        NamePopup_Input.text = _currentName;

        // The server says what the next rename costs; until it does, no hint.
        int? cost = ClubContext.ClubDetail?.NameChangeCost;
        if (NamePopup_HintText != null)
        {
            NamePopup_HintText.gameObject.SetActive(cost.HasValue);
            if (cost.HasValue)
                NamePopup_HintText.text = cost.Value <= 0
                    ? "First time change name is free"
                    : $"Change name cost {cost.Value} diamonds";
        }

        NamePopup.SetActive(true);
    }

    private void OnNameConfirm()
    {
        if (_busy) return;

        string name = NamePopup_Input.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            ShowMessage("Please Enter Club Name");
            return;
        }

        if (name == _currentName)
        {
            NamePopup.SetActive(false);
            return;
        }

        SaveAsync(new Dictionary<string, object> { { "name", name } }, "Club name updated",
            () => NamePopup.SetActive(false)).Forget();
    }

    #endregion

    #region Notice popup

    // Notice-only mode: opened from the club header, not from the panel's notice row.
    // The panel body stays hidden and closing the popup closes everything.
    private bool _noticeOnly;
    private bool _noticeEditable = true;

    /// <summary>
    /// Open just the Notice popup (club header tap). Editable for the creator;
    /// read-only for everyone else, so members can still read a notice the header
    /// cut short with "…".
    /// </summary>
    public void OpenNoticeOnly(bool canEdit)
    {
        _noticeOnly = true;
        _noticeEditable = canEdit;

        if (PanelBody != null) PanelBody.SetActive(false);

        if (gameObject.activeSelf) ShowNoticePopup();
        else gameObject.SetActive(true);   // OnEnable / Start → LoadFromClub → ShowNoticePopup
    }

    // Notice row inside the panel — always the creator.
    private void OpenNoticePopup()
    {
        _noticeEditable = true;
        ShowNoticePopup();
    }

    private void ShowNoticePopup()
    {
        NoticePopup_Input.text = _currentNotice;
        NoticePopup_Input.readOnly = !_noticeEditable;
        NoticePopup_Confirm.gameObject.SetActive(_noticeEditable);
        NoticePopup.SetActive(true);
    }

    private void CloseNoticePopup()
    {
        NoticePopup.SetActive(false);

        if (!_noticeOnly) return;

        // Opened on its own → close the whole panel and put it back to normal.
        _noticeOnly = false;
        _noticeEditable = true;
        if (PanelBody != null) PanelBody.SetActive(true);
        gameObject.SetActive(false);
    }

    private void OnNoticeConfirm()
    {
        if (_busy || !_noticeEditable) return;

        // An emptied notice goes back to the default rather than a blank header.
        string notice = NoticePopup_Input.text.Trim();
        if (string.IsNullOrEmpty(notice))
            notice = CreateClubScreenScript.DefaultNotice;

        if (notice == _currentNotice)
        {
            CloseNoticePopup();
            return;
        }

        SaveAsync(new Dictionary<string, object> { { "description", notice } }, "Notice updated",
            CloseNoticePopup).Forget();
    }

    #endregion

    #region Save

    private async UniTaskVoid SaveAsync(Dictionary<string, object> fields, string successMessage,
                                       System.Action onSaved)
    {
        _busy = true;
        RefreshSaveButton();

        try
        {
            var res = await ClubManager.Instance.UpdateClubAsync(ClubContext.ClubId, fields);

            onSaved?.Invoke();

            // Full club comes back → cache it. SetClubDetail fires OnClubDetailChanged,
            // which repaints the club header (name, icon, notice).
            if (res?.Club != null) ClubContext.SetClubDetail(res.Club);

            ShowMessage(successMessage);
        }
        catch (System.Exception e)
        {
            // e.g. not enough diamonds for a paid rename — the server's message says so.
            Debug.LogError($"[ClubProfilePanelScript] save error: {e.Message}");
            ShowMessage(string.IsNullOrEmpty(e.Message) ? "Failed to save" : e.Message);
            _busy = false;
            RefreshSaveButton();
            return;
        }

        _busy = false;

        // Show what the server now has (and clear the saved icon pick).
        LoadFromClub();
    }

    #endregion

    private void Close()
    {
        _noticeOnly = false;
        _noticeEditable = true;
        if (PanelBody != null) PanelBody.SetActive(true);
        gameObject.SetActive(false);
    }

    private static void ShowMessage(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
