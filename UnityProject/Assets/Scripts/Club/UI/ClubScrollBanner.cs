using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays club scrolling messages (club:scroll_message) over every scene. Loads
/// Resources/UI/ScrollMessageStrip on first use into its own overlay canvas, kept
/// across scene loads, so opening a table doesn't cut a message short — no scene wiring.
///
/// Messages play one at a time, in order. Go: already in a game → a message only;
/// that club open → join the table; anywhere else → open the club, then join.
/// </summary>
public class ClubScrollBanner : MonoBehaviour
{
    private const string SCENE_CLUB = "Scene_Club";
    private const string PrefabPath = "UI/ScrollMessageStrip";

    private static ClubScrollBanner _instance;

    private readonly Queue<ClubScrollMessagePayload> _queue = new Queue<ClubScrollMessagePayload>();

    private RectTransform _safeArea;
    private GameObject _panel;
    private ClubScrollingMessageMarquee _marquee;
    private ClubScrollMessagePayload _current;
    private Rect _lastSafeArea;

    public static void Show(ClubScrollMessagePayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Message))
            return;

        if (ChipRequestBanner.IsPreLoginScene())
            return;

        var banner = Ensure();
        if (banner != null)
            banner.Enqueue(payload);
    }

    // ── Build ─────────────────────────────────────────────────────────────

    private static ClubScrollBanner Ensure()
    {
        if (_instance != null)
            return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ClubScrollBanner] Missing Resources/{PrefabPath}");
            return null;
        }

        var root = new GameObject("ClubScrollBanner");
        DontDestroyOnLoad(root);

        // One under the chip request banner, which is the one asking for a decision.
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 3;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;

        root.AddComponent<GraphicRaycaster>();

        var banner = root.AddComponent<ClubScrollBanner>();

        // Safe-area holder so the strip sits under the notch, not behind it.
        var safeArea = new GameObject("SafeArea", typeof(RectTransform));
        safeArea.transform.SetParent(root.transform, false);
        banner._safeArea = (RectTransform)safeArea.transform;
        banner._safeArea.anchorMin = Vector2.zero;
        banner._safeArea.anchorMax = Vector2.one;
        banner._safeArea.offsetMin = Vector2.zero;
        banner._safeArea.offsetMax = Vector2.zero;

        // Layout (full width, top-anchored) comes from the prefab.
        banner._panel = Instantiate(prefab, banner._safeArea, false);
        banner._marquee = banner._panel.GetComponent<ClubScrollingMessageMarquee>();
        banner._marquee.OnGoTapped += banner.OnGoTapped;
        banner._marquee.OnFinished += banner.OnFinished;

        // It runs over live screens, so only Go may catch taps — the strip must not
        // block the table's own top bar while it passes.
        foreach (Graphic g in banner._panel.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        if (banner._marquee.GoButton != null)
            foreach (Graphic g in banner._marquee.GoButton.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = true;

        banner._panel.SetActive(false);

        SceneManager.activeSceneChanged += banner.OnActiveSceneChanged;

        _instance = banner;
        banner.ApplySafeArea();
        return banner;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
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

    // ── Play ──────────────────────────────────────────────────────────────

    private void Enqueue(ClubScrollMessagePayload payload)
    {
        // The sender plays their own message straight away and then gets the socket
        // echo of it, which shouldn't play it a second time.
        if (IsSame(_current, payload))
            return;

        foreach (ClubScrollMessagePayload queued in _queue)
            if (IsSame(queued, payload))
                return;

        _queue.Enqueue(payload);

        if (_current == null)
            PlayNextAsync().Forget();
    }

    private static bool IsSame(ClubScrollMessagePayload a, ClubScrollMessagePayload b) =>
        a != null && a.ClubId == b.ClubId && a.Message == b.Message && a.TableId == b.TableId;

    private async UniTaskVoid PlayNextAsync()
    {
        if (_queue.Count == 0)
        {
            _current = null;
            _panel.SetActive(false);
            return;
        }

        ClubScrollMessagePayload payload = _queue.Dequeue();
        _current = payload;

        string clubName = await FindClubNameAsync(payload);

        // Logged out while the club list loaded.
        if (this == null || _current != payload)
            return;

        _panel.SetActive(true);
        _marquee.SetMessage(clubName, payload.Message, payload.TableId);
    }

    private void OnFinished() => PlayNextAsync().Forget();

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        if (!ChipRequestBanner.IsPreLoginScene())
            return;

        _queue.Clear();
        _current = null;
        _panel.SetActive(false);
    }

    private static async UniTask<string> FindClubNameAsync(ClubScrollMessagePayload payload)
    {
        if (!string.IsNullOrEmpty(payload.ClubName))
            return payload.ClubName;

        if (payload.ClubId == ClubContext.ClubId)
            return ClubContext.ClubName;

        ClubListData club = await FindClubAsync(payload.ClubId);
        return club?.Name;
    }

    // ── Go ────────────────────────────────────────────────────────────────

    private void OnGoTapped(string tableId)
    {
        string clubId = _current?.ClubId;

        if (string.IsNullOrEmpty(tableId) || string.IsNullOrEmpty(clubId))
            return;

        // Leaving from here would walk the player out of their seat.
        if (SceneManager.GetActiveScene().name.Contains("GameTable"))
        {
            ToastEvents.Show(GameMessages.InGameCantJoin);
            return;
        }

        // Same club already open → straight to the table.
        var view = ClubViewController.Instance;
        if (view != null && view.isActiveAndEnabled && view.CurrentClubId == clubId)
        {
            view.JoinTable(tableId);
            return;
        }

        OpenClubAsync(clubId, tableId).Forget();
    }

    private static async UniTaskVoid OpenClubAsync(string clubId, string tableId)
    {
        ClubListData club = await FindClubAsync(clubId);

        if (club == null || GameSceneManager.Instance == null)
            return;

        // Same steps as picking the club on the main menu, plus the join on arrival.
        if (ClubSocketHandler.Instance != null)
            ClubSocketHandler.Instance.JoinClubPage(club.ClubId);

        ClubContext.SelectClub(club);
        ClubContext.JoinTableOnEntry = tableId;
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
