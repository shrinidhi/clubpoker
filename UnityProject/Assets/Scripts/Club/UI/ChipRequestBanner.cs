using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tappable banner that drops in from the top when a member asks for chips
/// (chips:request_received). Loads Resources/UI/CashierChipReq_Prefab on first
/// use into its own overlay canvas, kept across scene loads — no scene wiring.
///
/// Tap: already inside that club → open the Cashier on its requests tab;
/// anywhere else → open that club, which then opens the Cashier on arrival.
///
/// On the game table the banner still shows, but a tap only toasts — leaving
/// from there would walk the player out of their seat.
/// </summary>
public class ChipRequestBanner : MonoBehaviour
{
    private const string SCENE_CLUB = "Scene_Club";
    private const int    CountdownSeconds = 5;
    private const float  SlideSeconds = 0.3f;
    private const float  ShownY = -66f;
    private const string PrefabPath = "UI/CashierChipReq_Prefab";

    private static ChipRequestBanner _instance;

    private RectTransform _safeArea;
    private RectTransform _panel;
    private float _hiddenY;
    private TMP_Text _text;
    private Sequence _seq;
    private int _showVersion;
    private string _clubId;
    private ClubListData _club;
    private Rect _lastSafeArea;

    public static void Show(ChipRequestReceivedEvent payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.ClubId))
            return;

        var banner = Ensure();
        if (banner != null)
            banner.ShowAsync(payload).Forget();
    }

    // ── Build ─────────────────────────────────────────────────────────────

    private static ChipRequestBanner Ensure()
    {
        if (_instance != null)
            return _instance;

        var root = new GameObject("ChipRequestBanner");
        DontDestroyOnLoad(root);

        // Over scene canvases and popups, under the scene fade and the top inset bar.
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;

        root.AddComponent<GraphicRaycaster>();

        var banner = root.AddComponent<ChipRequestBanner>();

        // Safe-area holder so the banner sits under the notch, not behind it.
        banner._safeArea = NewRect("SafeArea", root.transform);

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ChipRequestBanner] Missing Resources/{PrefabPath}");
            Destroy(root);
            return null;
        }

        // Layout (top-anchored, parked above the screen) comes from the prefab.
        var panel = (RectTransform)Instantiate(prefab, banner._safeArea, false).transform;
        banner._panel = panel;
        banner._hiddenY = panel.anchoredPosition.y;

        // Reused friend-request row: its own script drives nothing here.
        var leftover = panel.GetComponent<Incomingreqprefab>();
        if (leftover != null) Destroy(leftover);

        banner._text = FindChild(panel, "requestText")?.GetComponent<TMP_Text>();
        if (banner._text != null) banner._text.richText = true;

        // Whole banner and the tick open the requests; the cross just dismisses.
        var bannerButton = panel.gameObject.AddComponent<Button>();
        bannerButton.transition = Selectable.Transition.None;
        bannerButton.onClick.AddListener(banner.OnTap);

        FindChild(panel, "Accept_Button")?.GetComponent<Button>()?.onClick.AddListener(banner.OnTap);
        FindChild(panel, "closeBtn")?.GetComponent<Button>()?.onClick.AddListener(banner.Hide);

        panel.gameObject.SetActive(false);

        _instance = banner;
        banner.ApplySafeArea();
        return banner;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name)
                return t;

        Debug.LogWarning($"[ChipRequestBanner] Child '{name}' not found in prefab");
        return null;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        _lastSafeArea = Screen.safeArea;

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect s = Screen.safeArea;
        _safeArea.anchorMin = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
        _safeArea.anchorMax = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);
    }

    // ── Show / hide ───────────────────────────────────────────────────────

    private async UniTaskVoid ShowAsync(ChipRequestReceivedEvent payload)
    {
        _clubId = payload.ClubId;
        _club = await FindClubAsync(payload.ClubId);

        if (this == null || _clubId != payload.ClubId)
            return;   // a newer request replaced this one while the list loaded

        // Only Cashier staff handle requests: Creator, or Manager that isn't a
        // Table Manager (a Manager server-side, but tables only).
        if (_club != null)
        {
            ClubRole role = ClubContext.ParseRole(_club.Role);
            bool cashierStaff = role == ClubRole.Creator ||
                                (role == ClubRole.Manager && !_club.IsTableManager);
            if (!cashierStaff)
                return;
        }

        string code = !string.IsNullOrWhiteSpace(payload.PlayerCode)
            ? payload.PlayerCode
            : await FindPlayerCodeAsync(payload.ClubId, payload.RequestId);

        if (this == null || _clubId != payload.ClubId)
            return;

        // "Player Arun (123456) applies chips : 100." — the (Ns) countdown is
        // appended below.
        string who = string.IsNullOrWhiteSpace(payload.MemberName) ? "-" : payload.MemberName;
        if (!string.IsNullOrWhiteSpace(code))
            who += $" ({code})";

        string line = Loc.Get("chipreq.banner_body",
                              ("name", who), ("amount", payload.Amount.ToString("N0")));

        int version = ++_showVersion;

        _seq?.Kill();
        _panel.gameObject.SetActive(true);
        _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, _hiddenY);
        SetCountdown(line, CountdownSeconds);

        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_panel.DOAnchorPosY(ShownY, SlideSeconds).SetEase(Ease.OutCubic));

        // 3 → 2 → 1, then slide away. A newer banner or a tap bumps the version
        // and this loop just stops.
        for (int s = CountdownSeconds; s > 0; s--)
        {
            SetCountdown(line, s);
            await UniTask.Delay(1000, ignoreTimeScale: true);

            if (this == null || version != _showVersion)
                return;
        }

        _seq?.Kill();
        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_panel.DOAnchorPosY(_hiddenY, SlideSeconds).SetEase(Ease.InCubic))
            .OnComplete(() => _panel.gameObject.SetActive(false));
    }

    private void SetCountdown(string line, int seconds)
    {
        if (_text != null)
            _text.text = $"{line} ({seconds}s)";
    }

    private void Hide()
    {
        _showVersion++;
        _seq?.Kill();
        _panel.gameObject.SetActive(false);
    }

    /// <summary>The event carries only the username — the player code comes from
    /// the pending list. Empty on any failure; the banner just shows the name.</summary>
    private static async UniTask<string> FindPlayerCodeAsync(string clubId, string requestId)
    {
        if (string.IsNullOrEmpty(requestId) || ApiClient.Instance == null)
            return null;

        try
        {
            var data = await ApiClient.Instance.Get<ChipRequestsData>(
                $"/api/clubs/{clubId}/chips/requests?status=PENDING&limit=50");

            return data?.Requests?.Find(r => r != null && r.Id == requestId)?.PlayerCode;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ChipRequestBanner] Player code lookup failed: {e.Message}");
            return null;
        }
    }

    // ── Tap ───────────────────────────────────────────────────────────────

    private void OnTap()
    {
        string clubId = _clubId;
        ClubListData club = _club;
        Hide();

        if (string.IsNullOrEmpty(clubId))
            return;

        // On a table: leaving from here would walk the player out of their seat.
        // Point them at the Cashier for later instead.
        if (SceneManager.GetActiveScene().name.Contains("GameTable"))
        {
            ToastEvents.Show(Loc.Get("chipreq.review_after_game"));
            return;
        }

        // Same club already open → Cashier, requests tab.
        var view = ClubViewController.Instance;
        if (view != null && view.isActiveAndEnabled && view.CurrentClubId == clubId)
        {
            view.OpenCashierRequests();
            return;
        }

        OpenClubAsync(clubId, club).Forget();
    }

    private static async UniTaskVoid OpenClubAsync(string clubId, ClubListData club)
    {
        if (club == null)
            club = await FindClubAsync(clubId);

        if (club == null || GameSceneManager.Instance == null)
            return;

        // Same steps as picking the club on the main menu, plus the Cashier flag.
        if (ClubSocketHandler.Instance != null)
            ClubSocketHandler.Instance.JoinClubPage(club.ClubId);

        ClubContext.SelectClub(club);
        ClubContext.OpenCashierOnEntry = true;
        GameSceneManager.Instance.LoadScene(SCENE_CLUB);
    }

    private static async UniTask<ClubListData> FindClubAsync(string clubId)
    {
        if (ClubContext.SelectedClub != null && ClubContext.SelectedClub.ClubId == clubId)
            return ClubContext.SelectedClub;

        if (AuthManager.Instance == null)
            return null;

        List<ClubListData> clubs = await AuthManager.Instance.GetClubsAsync();
        return clubs?.Find(c => c != null && c.ClubId == clubId);
    }
}
