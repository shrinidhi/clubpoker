using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

/// <summary>
/// Scene-root controller for ClubScene. Opens the tapped club (passed via
/// ClubContext) as the home screen and owns the bottom-bar buttons.
/// </summary>
public class ClubViewController : MonoBehaviour
{
    public static ClubViewController Instance { get; private set; }

    [Header("Sub-screen Scripts")]
    public ShowClubTableScreenScript ShowClubTableScreenScript;

    [Header("Navigation")]
    public Button BackButton;

    [Header("Bottom Button Screens")]
    public GameObject ClubCashierPanel;
    public GameObject MemberManagementScreen;
    public GameObject MessagesScreen;

    [Header("Bottom Buttons")]
    [SerializeField] Button MessagesButton;
    [SerializeField] Button MembersButton;
    [SerializeField] Button CashierButton;
    [SerializeField] Button DataButton;
    [SerializeField] Button AdminButton;

    [Header("Bottom Bar Slide")]
    [SerializeField] RectTransform bottomBar;     // panel (horizontal layout) holding the buttons
    [SerializeField] Button openBarButton;        // menu icon (bottom-right)
    [SerializeField] Button closeBarButton;       // X icon — same spot as menu, outside the bar
    [SerializeField] float barShownX = 0f;        // anchored X when open (full visible)
    [SerializeField] float barHiddenX = 600f;     // anchored X when closed (parked at menu button)
    [SerializeField] float barSlideDuration = 0.3f;

    private const string SCENE_MAIN_MENU = "Scene_MainMenu";

    // Club currently shown on the home screen — set by OpenClubTable.
    private ClubListData _currentClub;
    private Tween _barTween;
    private bool _barOpen;

    private void Awake()
    {
        Instance = this;

        if (BackButton != null)
            BackButton.onClick.AddListener(BackToMainMenu);

        InitBottomBar();
    }

    private void Start()
    {
        // Entry from MainMenu: ShowClubPanelScript stored the tapped club in
        // ClubContext before loading this scene. Open it as the home screen.
        if (ClubContext.SelectedClub != null)
            OpenClubTable(ClubContext.SelectedClub);
    }

    private void OnEnable()
    {
        MessagesButton.onClick.AddListener(MessagesButtonOnTap);
        MembersButton.onClick.AddListener(MembersButtonOnTap);
        CashierButton.onClick.AddListener(CashierButtonOnTap);
        DataButton.onClick.AddListener(DataButtonOnTap);
        AdminButton.onClick.AddListener(AdminButtonOnTap);

        if (openBarButton != null)  openBarButton.onClick.AddListener(OpenBottomBar);
        if (closeBarButton != null) closeBarButton.onClick.AddListener(CloseBottomBar);
    }

    private void OnDisable()
    {
        MessagesButton.onClick.RemoveListener(MessagesButtonOnTap);
        MembersButton.onClick.RemoveListener(MembersButtonOnTap);
        CashierButton.onClick.RemoveListener(CashierButtonOnTap);
        DataButton.onClick.RemoveListener(DataButtonOnTap);
        AdminButton.onClick.RemoveListener(AdminButtonOnTap);

        if (openBarButton != null)  openBarButton.onClick.RemoveListener(OpenBottomBar);
        if (closeBarButton != null) closeBarButton.onClick.RemoveListener(CloseBottomBar);
    }

    private void OnDestroy()
    {
        _barTween?.Kill();

        if (Instance == this)
            Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Show a club on the home screen and gate creator-only options.</summary>
    public void OpenClubTable(ClubListData club)
    {
        _currentClub = club;

        if (ShowClubTableScreenScript != null)
            ShowClubTableScreenScript.ShowData(club);

        // The whole bottom bar is creator-only.
        bool isCreator = ClubContext.ParseRole(club.Role) == ClubRole.Creator;
        SetBottomBarCreatorOnly(isCreator);
    }

    public void BackToMainMenu() => GameSceneManager.Instance.LoadScene(SCENE_MAIN_MENU);

    // ── Bottom bar slide ──────────────────────────────────────────────────

    private void InitBottomBar()
    {
        if (bottomBar == null)
            return;

        // Start closed: panel parked at the menu button and disabled, menu icon shown.
        bottomBar.anchoredPosition = new Vector2(barHiddenX, bottomBar.anchoredPosition.y);
        bottomBar.gameObject.SetActive(false);
        _barOpen = false;
        ShowMenuIcon(open: false);
    }

    /// <summary>
    /// Whole bottom bar is creator-only. Collapse it for everyone, then only show
    /// the hamburger for the creator — members can't open it, so it stays hidden.
    /// </summary>
    private void SetBottomBarCreatorOnly(bool isCreator)
    {
        InitBottomBar();              // collapse + show hamburger

        if (!isCreator)
        {
            if (openBarButton != null)  openBarButton.gameObject.SetActive(false);
            if (closeBarButton != null) closeBarButton.gameObject.SetActive(false);
        }
    }

    public void OpenBottomBar()  => ExpandBottomBar(true);
    public void CloseBottomBar() => ExpandBottomBar(false);
    public void ToggleBottomBar() => ExpandBottomBar(!_barOpen);

    private void ExpandBottomBar(bool open)
    {
        if (bottomBar == null || _barOpen == open)
            return;

        _barOpen = open;

        // Menu icon when closed, X when open (separate buttons, same spot).
        ShowMenuIcon(open);

        _barTween?.Kill();

        if (open)
        {
            // Enable the panel, then slide it out from the menu button.
            bottomBar.gameObject.SetActive(true);
            _barTween = bottomBar
                .DOAnchorPosX(barShownX, barSlideDuration)
                .SetEase(Ease.OutCubic);
        }
        else
        {
            // Slide back toward the menu button, then disable the panel.
            _barTween = bottomBar
                .DOAnchorPosX(barHiddenX, barSlideDuration)
                .SetEase(Ease.InCubic)
                .OnComplete(() => bottomBar.gameObject.SetActive(false));
        }
    }

    // Menu (open) and X (close) share the same spot — swap which one shows.
    private void ShowMenuIcon(bool open)
    {
        if (openBarButton != null)  openBarButton.gameObject.SetActive(!open);
        if (closeBarButton != null) closeBarButton.gameObject.SetActive(open);
    }

    // ── Bottom bar ────────────────────────────────────────────────────────

    void MessagesButtonOnTap()
    {
        if (MessagesScreen != null)
            MessagesScreen.SetActive(true);
    }

    void MembersButtonOnTap()
    {
        if (MemberManagementScreen != null)
            MemberManagementScreen.SetActive(true);
    }

    void CashierButtonOnTap()
    {
        if (_currentClub == null || ClubCashierPanel == null)
            return;

        ClubContext.Set(
            _currentClub.ClubId,
            _currentClub.Name,
            ClubContext.ParseRole(_currentClub.Role),
            0, 0, 0);

        ClubCashierPanel.SetActive(true);
        var cashier = ClubCashierPanel.GetComponent<CashierPanelScript>();
        if (cashier != null)
            cashier.Init();
    }

    void DataButtonOnTap()
    {
        // TODO: club data/stats screen
    }

    void AdminButtonOnTap()
    {
        // TODO: club admin screen
    }
}
