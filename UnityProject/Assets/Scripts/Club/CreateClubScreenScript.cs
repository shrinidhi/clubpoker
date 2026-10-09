using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

/// <summary>
/// Main menu ▸ Create A Club — club name + icon only. The notice isn't asked for here:
/// every new club starts with the default one, and the creator lands straight in the
/// new club with Club Profile open (ClubProfilePanelScript) to change name,
/// notice and icon.
/// </summary>
public class CreateClubScreenScript : MonoBehaviour
{
    public const string DefaultNotice = "Welcome to Club Poker";

    public Button Close_Button;
    public TMP_InputField ClubName_InputField;
    public Button CreateClub_Button;

    public GameObject ClubBadge_Prefab;
    public Transform ClubBadge_Content;

    public ClubBadgeSO ClubBadgeSO;

    private List<ClubBadgePrefabScript> badgeItems = new List<ClubBadgePrefabScript>();

    private string selectedBadge = "";
    public ShowClubPanelScript ShowClubPanelScript;

    public RectTransform popupRect;
    private Vector2 originalPos;
    private bool isMovedUp = false;

    public float moveUpY = 250f;

    [Tooltip("The logo image in the top preview (ClubLogo) — NOT its background.")]
    public Image ClubBadgeImage;
    public Button LeftButton;
    public Button RightButton;
    public ScrollRect ScrollRect;
    private int selectedBadgeIndex = 0;

    [Header("Custom photo")]
    [Tooltip("Camera button on the preview — picks a photo from the device gallery.")]
    public Button CameraButton;
    [Tooltip("Shows the picked photo in the preview, in place of the badge logo (ClubExternalPhoto).")]
    public Image ExternalPhotoImage;

    private string    _photoDataUri;
    private Texture2D _photoTex;
    private Sprite    _photoSprite;

    void Start()
    {
        Close_Button.onClick.AddListener(CloseButtonOnTap);
        CreateClub_Button.onClick.AddListener(CreateClubButtonOnTap);

        LeftButton.onClick.AddListener(LeftButtonOnTap);
        RightButton.onClick.AddListener(RightButtonOnTap);

        if (CameraButton != null) CameraButton.onClick.AddListener(OnCameraTap);

        GenerateBadges();

        originalPos = popupRect.anchoredPosition;
    }

    private void OnEnable()
    {
        ClubName_InputField.text = "";
        ClearPhoto();
    }

    private void OnDestroy()
    {
        FreePhoto();
    }

    #region Custom photo

    private void OnCameraTap()
    {
        ClubPhotoPicker.Pick((square, dataUri) =>
        {
            FreePhoto();
            _photoTex     = square;
            _photoSprite  = ClubImageUtil.SpriteFromTexture(square);
            _photoDataUri = dataUri;

            ShowPhoto(true);
        });
    }

    /// Photo on → preview shows it and no badge is ticked; off → back to the badge.
    private void ShowPhoto(bool on)
    {
        if (ExternalPhotoImage != null)
        {
            ExternalPhotoImage.gameObject.SetActive(on);
            if (on) ExternalPhotoImage.sprite = _photoSprite;
        }

        if (ClubBadgeImage != null) ClubBadgeImage.gameObject.SetActive(!on);

        for (int i = 0; i < badgeItems.Count; i++)
            badgeItems[i].SetSelected(!on && i == selectedBadgeIndex);
    }

    // Back to the selected badge (a badge was tapped, or the popup reopened).
    private void ClearPhoto()
    {
        FreePhoto();
        ShowPhoto(false);
    }

    private void FreePhoto()
    {
        _photoDataUri = null;
        if (_photoSprite != null) { Destroy(_photoSprite); _photoSprite = null; }
        if (_photoTex != null)    { Destroy(_photoTex);    _photoTex = null; }
    }

    #endregion

    void GenerateBadges()
    {
        badgeItems.Clear();

        for (int i = 0; i < ClubBadge_Content.childCount; i++)
        {
            Destroy(ClubBadge_Content.GetChild(i).gameObject);
        }

        foreach (ClubBadgeData data in ClubBadgeSO.ClubBadges)
        {
            GameObject obj = Instantiate(
                ClubBadge_Prefab,
                ClubBadge_Content
            );

            ClubBadgePrefabScript badgePrefab =
                obj.GetComponent<ClubBadgePrefabScript>();

            badgePrefab.Setup(data, SelectBadge);

            badgeItems.Add(badgePrefab);
        }

        if (ClubBadgeSO.ClubBadges.Count > 0)
        {
            selectedBadgeIndex = 0;

            SelectBadge(
                badgeItems[0],
                ClubBadgeSO.ClubBadges[0].BadgeName
            );
        }

        UpdateArrowButtons();
    }

    public void SelectBadge(
    ClubBadgePrefabScript selectedItem,
    string badgeKey)
    {
        if (selectedItem == null)
            return;

        // Picking a badge replaces a custom photo.
        if (_photoDataUri != null)
            ClearPhoto();

        selectedBadge = badgeKey.ToLower();

        selectedBadgeIndex =
            badgeItems.IndexOf(selectedItem);

        for (int i = 0; i < badgeItems.Count; i++)
        {
            badgeItems[i].SetSelected(
                i == selectedBadgeIndex
            );
        }

        if (ClubBadgeImage != null &&
            selectedBadgeIndex >= 0 &&
            selectedBadgeIndex < ClubBadgeSO.ClubBadges.Count)
        {
            ClubBadgeImage.sprite =
                ClubBadgeSO
                .ClubBadges[selectedBadgeIndex]
                .BadgeImage;
        }

        UpdateArrowButtons();
    }
    private void LeftButtonOnTap()
    {
        if (badgeItems.Count == 0)
            return;

        if (selectedBadgeIndex <= 0)
            return;

        selectedBadgeIndex--;

        SelectBadgeByIndex(selectedBadgeIndex);
    }

    private void RightButtonOnTap()
    {
        if (badgeItems.Count == 0)
            return;

        if (selectedBadgeIndex >= badgeItems.Count - 1)
            return;

        selectedBadgeIndex++;

        SelectBadgeByIndex(selectedBadgeIndex);
    }

    private void SelectBadgeByIndex(int index)
    {
        if (index < 0 || index >= badgeItems.Count)
            return;

        ClubBadgePrefabScript item =
            badgeItems[index];

        SelectBadge(
            item,
            item.BadgeKey
        );

        ScrollToBadge(index);
    }

    private void ScrollToBadge(int index)
    {
        if (ScrollRect == null ||
            badgeItems.Count <= 1)
            return;

        Canvas.ForceUpdateCanvases();

        float normalizedPosition =
            (float)index /
            (badgeItems.Count - 1);

        ScrollRect.horizontalNormalizedPosition =
            normalizedPosition;
    }

    private void UpdateArrowButtons()
    {
        if (LeftButton != null)
        {
            LeftButton.interactable =
                selectedBadgeIndex > 0;
        }

        if (RightButton != null)
        {
            RightButton.interactable =
                selectedBadgeIndex <
                badgeItems.Count - 1;
        }
    }

    async void CreateClubButtonOnTap()
    {
        string clubName = ClubName_InputField.text.Trim();

        if (string.IsNullOrEmpty(clubName))
        {
            InformationPrefabScript.Instance.ShowMessage("Please Enter Club Name");
            return;
        }

        if (string.IsNullOrEmpty(selectedBadge))
        {
            InformationPrefabScript.Instance.ShowMessage("Please Select Club Badge");
            return;
        }

        CreateClub_Button.interactable = false;

        try
        {
            // The selected badge is always sent too — it's the fallback wherever
            // the photo isn't shown.
            ClubData club = await AuthManager.Instance.CreateClubAsync(
                clubName,
                selectedBadge.ToLower(),
                DefaultNotice,
                _photoDataUri
            );

            Debug.Log("Club Created: " + club.Name);
            Debug.Log("Club Code: " + club.ClubCode);

            await EnterNewClubAsync(club.Id);
        }
        catch
        {
            Debug.LogError("Create club failed");
        }

        CreateClub_Button.interactable = true;
    }

    /// <summary>
    /// Go straight into the new club, with Club Profile opening on arrival. The
    /// club is taken from the club list rather than built from the create response,
    /// so it carries everything the club screen reads (role, chips, member count…) —
    /// the same object a carousel tap would open.
    /// </summary>
    private async UniTask EnterNewClubAsync(string clubId)
    {
        List<ClubListData> clubs = await AuthManager.Instance.GetClubsAsync();
        ClubListData created = clubs?.Find(c => c.ClubId == clubId);

        if (created == null || ShowClubPanelScript == null)
        {
            // Couldn't find it — fall back to the old behaviour: refresh the carousel.
            Debug.LogWarning("[CreateClub] New club not in list — staying on main menu");
            if (ShowClubPanelScript != null) ShowClubPanelScript.LoadClubs().Forget();
            gameObject.SetActive(false);
            return;
        }

        ClubContext.OpenProfileOnEntry = true;
        MovePopupDown();
        gameObject.SetActive(false);

        // Same path as tapping the club in the carousel (joins the club socket room,
        // selects it, loads the club scene).
        ShowClubPanelScript.OnClubSelected(created);
    }

    void CloseButtonOnTap()
    {
        MovePopupDown();
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (ClubName_InputField.isFocused && TouchScreenKeyboard.visible)
        {
            MovePopupUp();
        }
        else
        {
            MovePopupDown();
        }
    }

    private void MovePopupUp()
    {
        if (isMovedUp)
            return;

        isMovedUp = true;

        popupRect.anchoredPosition =
            originalPos + new Vector2(0, moveUpY);
    }

    private void MovePopupDown()
    {
        if (!isMovedUp)
            return;

        isMovedUp = false;

        popupRect.anchoredPosition = originalPos;
    }
}
