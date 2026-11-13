using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClubMembershipApprovedBanner : MonoBehaviour
{
    private const string SCENE_LOAD_KEY = "Scene_Club";

    private const string PREFAB_PATH = "UI/ClubMembershipApproved";

    private const float SlideSeconds = 0.3f;
    private const float ShownY = -66f;

    private const int CountdownSeconds = 5;

    private static ClubMembershipApprovedBanner _instance;

    private RectTransform _safeArea;
    private RectTransform _panel;

    private TMP_Text _text;

    private Button _closeButton;
    private Button _gotoButton;

    private float _hiddenY;

    private Sequence _sequence;

    private string _clubId;
    private string _clubName;

    private int _showVersion;

    private bool _isNavigating;

    private Rect _lastSafeArea;

    // ==================================================
    // SHOW BANNER
    // ==================================================

    public static void Show(ClubMembershipApprovedPayload payload)
    {
        if (payload == null ||
            string.IsNullOrWhiteSpace(payload.ClubId))
        {
            Debug.LogWarning(
                "[ClubMembershipApprovedBanner] Invalid payload!"
            );
            return;
        }

        string scene = SceneManager.GetActiveScene().name;

        if (scene.Contains("Login") ||
            scene.Contains("Register") ||
            scene.Contains("Splash") ||
            scene.Contains("Bootstrap"))
        {
            Debug.Log(
                "[ClubMembershipApprovedBanner] Banner skipped in scene: " + scene
            );
            return;
        }

        ClubMembershipApprovedBanner banner = Ensure();

        if (banner == null)
            return;

        banner.ShowBanner(payload);
    }

    // ==================================================
    // CREATE BANNER
    // ==================================================

    private static ClubMembershipApprovedBanner Ensure()
    {
        if (_instance != null)
            return _instance;

        GameObject root = new GameObject("ClubMembershipApprovedBanner");

        DontDestroyOnLoad(root);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;

        root.AddComponent<GraphicRaycaster>();

        ClubMembershipApprovedBanner banner =
            root.AddComponent<ClubMembershipApprovedBanner>();

        banner._safeArea = NewRect("SafeArea", root.transform);

        GameObject prefab = Resources.Load<GameObject>(PREFAB_PATH);

        if (prefab == null)
        {
            Debug.LogError(
                "[ClubMembershipApprovedBanner] Prefab missing: Resources/" +
                PREFAB_PATH
            );

            Destroy(root);
            return null;
        }

        GameObject panelObject =
            Instantiate(prefab, banner._safeArea, false);

        banner._panel = panelObject.GetComponent<RectTransform>();

        if (banner._panel == null)
        {
            Debug.LogError(
                "[ClubMembershipApprovedBanner] Prefab RectTransform missing!"
            );

            Destroy(root);
            return null;
        }

        banner._hiddenY = banner._panel.anchoredPosition.y;

        banner._text = FindChild(banner._panel, "requestText")
            ?.GetComponent<TMP_Text>();

        banner._closeButton = FindChild(banner._panel, "closeBtn")
            ?.GetComponent<Button>();

        banner._gotoButton = FindChild(banner._panel, "Accept_Button")
            ?.GetComponent<Button>();

        if (banner._gotoButton != null)
        {
            banner._gotoButton.onClick.RemoveAllListeners();
            banner._gotoButton.onClick.AddListener(banner.OnTap);
        }
        else
        {
            Debug.LogWarning(
                "[ClubMembershipApprovedBanner] Accept_Button not found!"
            );
        }

        if (banner._closeButton != null)
        {
            banner._closeButton.onClick.RemoveAllListeners();
            banner._closeButton.onClick.AddListener(banner.Hide);
        }

        banner._panel.gameObject.SetActive(false);

        _instance = banner;

        banner.ApplySafeArea();

        Debug.Log(
            "[ClubMembershipApprovedBanner] Initialized successfully!"
        );

        return banner;
    }

    // ==================================================
    // FIND CHILD
    // ==================================================

    private static Transform FindChild(
        Transform root,
        string childName)
    {
        if (root == null)
            return null;

        foreach (Transform t in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
                return t;
        }

        Debug.LogWarning(
            "[ClubMembershipApprovedBanner] Child missing: " + childName
        );

        return null;
    }

    // ==================================================
    // CREATE RECT
    // ==================================================

    private static RectTransform NewRect(
        string name,
        Transform parent)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform)
        );

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    // ==================================================
    // SAFE AREA
    // ==================================================

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (_safeArea == null ||
            Screen.width <= 0 ||
            Screen.height <= 0)
            return;

        _lastSafeArea = Screen.safeArea;

        Rect safe = Screen.safeArea;

        _safeArea.anchorMin = new Vector2(
            safe.xMin / Screen.width,
            safe.yMin / Screen.height
        );

        _safeArea.anchorMax = new Vector2(
            safe.xMax / Screen.width,
            safe.yMax / Screen.height
        );
    }

    // ==================================================
    // SHOW BANNER
    // ==================================================

    private void ShowBanner(ClubMembershipApprovedPayload payload)
    {
        if (_panel == null)
            return;

        // Previous countdown cancel.
        int version = ++_showVersion;

        _sequence?.Kill();

        _clubId = payload.ClubId;
        _clubName = payload.ClubName;

        _isNavigating = false;

        if (string.IsNullOrWhiteSpace(_clubName))
            _clubName = "Club";

        Debug.Log(
            "[ClubMembershipApprovedBanner] Showing banner for Club: " +
            _clubName + " | ClubId: " + _clubId
        );

        _panel.gameObject.SetActive(true);

        _panel.anchoredPosition = new Vector2(
            _panel.anchoredPosition.x,
            _hiddenY
        );

        _sequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(
                _panel.DOAnchorPosY(
                    ShownY,
                    SlideSeconds
                ).SetEase(Ease.OutCubic)
            );

        ShowCountdownAsync(_clubName, version).Forget();
    }

    // ==================================================
    // COUNTDOWN
    // ==================================================

    private async UniTaskVoid ShowCountdownAsync(
        string clubName,
        int version)
    {
        string message = Loc.Get(
            "club.approved_to_join",
            ("club", clubName)
        );

        for (int i = CountdownSeconds; i > 0; i--)
        {
            if (this == null ||
                version != _showVersion)
                return;

            if (_text != null)
            {
                _text.text = $"{message} ({i}s)";
            }

            await UniTask.Delay(
                1000,
                ignoreTimeScale: true
            );
        }

        if (this == null ||
            version != _showVersion)
            return;

        Hide();
    }

    // ==================================================
    // HIDE BANNER
    // ==================================================

    private void Hide()
    {
        _showVersion++;

        _sequence?.Kill();
        _sequence = null;

        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    // ==================================================
    // ACCEPT BUTTON
    // ==================================================

    private void OnTap()
    {
        if (_isNavigating)
            return;

        string clubId = _clubId;

        if (string.IsNullOrWhiteSpace(clubId))
        {
            Debug.LogError(
                "[ClubMembershipApprovedBanner] Club ID is empty!"
            );
            return;
        }

        // On a table: loading the club from here walks the player out of the
        // game screen without leaving the seat. Tell them, don't go.
        if (SceneManager.GetActiveScene().name.Contains("GameTable"))
        {
            Hide();
            ToastEvents.Show(Loc.Get("club.banner_review_after_game"));
            return;
        }

        _isNavigating = true;

        Debug.Log(
            "[ClubMembershipApprovedBanner] Accept clicked. Opening Club: " +
            clubId
        );

        Hide();

        OpenClubAsync(clubId).Forget();
    }

    // ==================================================
    // OPEN APPROVED CLUB
    // ==================================================

    private static async UniTaskVoid OpenClubAsync(string clubId)
    {
        Debug.Log(
            "[ClubMembershipApprovedBanner] Finding club data: " + clubId
        );

        // Find exact club using the ClubId from approval payload.
        ClubListData club = await FindClubAsync(clubId);

        await UniTask.SwitchToMainThread();

        if (club == null)
        {
            Debug.LogError(
                "[ClubMembershipApprovedBanner] Club data not found for ID: " +
                clubId
            );

            if (_instance != null)
                _instance._isNavigating = false;

            return;
        }

        if (GameSceneManager.Instance == null)
        {
            Debug.LogError(
                "[ClubMembershipApprovedBanner] GameSceneManager is null!"
            );

            if (_instance != null)
                _instance._isNavigating = false;

            return;
        }

        // Verify that the found club is the approved club.
        if (club.ClubId != clubId)
        {
            Debug.LogError(
                "[ClubMembershipApprovedBanner] Club ID mismatch!"
            );

            if (_instance != null)
                _instance._isNavigating = false;

            return;
        }

        // Set the exact approved club as selected club.
        ClubContext.SelectClub(club);

        Debug.Log(
            "[ClubMembershipApprovedBanner] Selected Club: " +
            club.ClubId
        );

        // Join the socket room for the same club.
        if (ClubSocketHandler.Instance != null)
        {
            ClubSocketHandler.Instance.JoinClubPage(clubId);
        }
        else
        {
            Debug.LogWarning(
                "[ClubMembershipApprovedBanner] ClubSocketHandler is null!"
            );
        }

        Debug.Log(
            "[ClubMembershipApprovedBanner] Loading Club Scene..."
        );

        // Load scene only once.
        GameSceneManager.Instance.LoadScene(SCENE_LOAD_KEY);
    }

    // ==================================================
    // FIND CLUB BY ID
    // ==================================================

    private static async UniTask<ClubListData> FindClubAsync(
        string clubId)
    {
        if (string.IsNullOrWhiteSpace(clubId))
            return null;

        // First check currently selected club.
        if (ClubContext.SelectedClub != null &&
            ClubContext.SelectedClub.ClubId == clubId)
        {
            return ClubContext.SelectedClub;
        }

        if (AuthManager.Instance == null)
        {
            Debug.LogError(
                "[ClubMembershipApprovedBanner] AuthManager is null!"
            );

            return null;
        }

        // Fetch clubs and find the exact approved club.
        List<ClubListData> clubs =
            await AuthManager.Instance.GetClubsAsync();

        await UniTask.SwitchToMainThread();

        if (clubs == null)
        {
            Debug.LogWarning(
                "[ClubMembershipApprovedBanner] Club list is null!"
            );

            return null;
        }

        ClubListData club = clubs.Find(
            c => c != null && c.ClubId == clubId
        );

        if (club == null)
        {
            Debug.LogWarning(
                "[ClubMembershipApprovedBanner] Approved club not found in club list: " +
                clubId
            );
        }

        return club;
    }

    // ==================================================
    // DESTROY
    // ==================================================

    private void OnDestroy()
    {
        _sequence?.Kill();

        if (_instance == this)
            _instance = null;
    }
}