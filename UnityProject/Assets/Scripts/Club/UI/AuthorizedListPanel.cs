using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

/// <summary>
/// "Authorized List" side drawer — the host's queue of pending buy-ins while
/// Auth-Buyin is on. Opened by the key button (BuyInAuthKeyButton sets this
/// active); slides in, loads the queue, and polls it while open.
///
/// Approve / Reject re-read the queue once the server answers; a failure hands
/// the row its buttons back.
/// </summary>
public class AuthorizedListPanel : MonoBehaviour
{
    [Header("Drawer")]
    [SerializeField] private RectTransform drawer;
    [Tooltip("Full-screen backdrop; tapping it closes the drawer — the only way out.")]
    [SerializeField] private Button dimmerButton;
    [Tooltip("Off = slides in from the left, like the table menu.")]
    [SerializeField] private bool  slideFromRight;
    [SerializeField] private float slideDuration = 0.25f;

    [Header("List")]
    [SerializeField] private Transform              content;
    [SerializeField] private BuyInRequestRowScript  rowPrefab;
    [Tooltip("Shown when there is nothing to review.")]
    [SerializeField] private GameObject             emptyState;

    // Interim: the server emits no socket event for new / resolved buy-in
    // requests, so the open list re-reads itself on a timer. Swap for a socket
    // subscription once the backend emits one.
    [Header("Refresh")]
    [SerializeField] private float pollSeconds = 5f;

    private const string ErrLoad    = "Couldn't load buy-in requests.";
    private const string ErrApprove = "Couldn't approve the request. Please try again.";
    private const string ErrReject  = "Couldn't reject the request. Please try again.";
    private const string MsgApproveTurnedReject =
        "Couldn't seat the player — request rejected and chips returned.";

    private readonly List<BuyInRequestRowScript> _rows = new List<BuyInRequestRowScript>();

    private float _openX;
    private bool  _measured;
    private Coroutine _slide;

    // Bumped on every load so a slow response can't overwrite a newer one.
    private int _loadVersion;

    // Approve / Reject calls in flight. A poll mid-call would rebuild the rows
    // and wipe the locked buttons, so polls skip while this is non-zero.
    private int _resolving;

    private CancellationTokenSource _pollCts;

    private void Awake()
    {
        if (dimmerButton != null) dimmerButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        Debug.Log($"[AuthorizedList] Opened (active in hierarchy: {gameObject.activeInHierarchy})");

        // This panel toggles as a whole (unlike the table menu, which toggles
        // its drawer and dimmer). Children copied from that menu start inactive,
        // so switch them on here rather than trusting the scene.
        if (dimmerButton != null) dimmerButton.gameObject.SetActive(true);
        if (drawer != null)       drawer.gameObject.SetActive(true);

        SlideIn();
        LoadAsync(silent: false).Forget();
        StartPolling();
    }

    private void OnDisable()
    {
        StopPolling();

        if (_slide != null)
        {
            StopCoroutine(_slide);
            _slide = null;
        }
    }

    public void Close()
    {
        if (!gameObject.activeInHierarchy) return;
        StartSlide(ClosedX, deactivateAtEnd: true);
    }

    // ── Loading ─────────────────────────────────────────────────────────────

    /// <param name="silent">Poll reloads: no error toast (one every 5s would be
    /// noise) and no rebuild when nothing changed.</param>
    private async UniTaskVoid LoadAsync(bool silent)
    {
        TableInfo info = TableContext.Info;
        if (info == null || string.IsNullOrEmpty(info.ClubTableRowId))
            return;

        int version = ++_loadVersion;

        List<BuyInRequestItem> requests;

        try
        {
            requests = await AuthManager.Instance.GetBuyInRequestsAsync(
                info.ClubId, info.ClubTableRowId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AuthorizedList] Load failed: {e.Message}");
            if (!silent && this != null && isActiveAndEnabled && version == _loadVersion)
            {
                RefreshEmptyState();
                ToastEvents.Show(ErrLoad);
            }
            return;
        }

        // Closed, or a newer load started, while this one was in flight.
        if (this == null || !isActiveAndEnabled || version != _loadVersion)
            return;

        // Rebuilding every poll would flicker the list and reset any half-scrolled
        // position for no reason.
        if (silent && SameAsShown(requests))
            return;

        Rebuild(requests);
    }

    private bool SameAsShown(List<BuyInRequestItem> requests)
    {
        if (requests.Count != _rows.Count) return false;

        for (int i = 0; i < requests.Count; i++)
            if (_rows[i] == null || _rows[i].RequestId != requests[i].Id)
                return false;

        return true;
    }

    private void Rebuild(List<BuyInRequestItem> requests)
    {
        foreach (BuyInRequestRowScript row in _rows)
            if (row != null) Destroy(row.gameObject);
        _rows.Clear();

        foreach (BuyInRequestItem item in requests)
        {
            BuyInRequestRowScript row = Instantiate(rowPrefab, content);
            row.Setup(item,
                      r => ResolveAsync(r, approve: true).Forget(),
                      r => ResolveAsync(r, approve: false).Forget());
            _rows.Add(row);
        }

        RefreshEmptyState();
    }

    private void RefreshEmptyState()
    {
        if (emptyState != null) emptyState.SetActive(_rows.Count == 0);
    }

    // ── Approve / Reject ────────────────────────────────────────────────────

    private async UniTaskVoid ResolveAsync(BuyInRequestRowScript row, bool approve)
    {
        TableInfo info = TableContext.Info;
        if (info == null || row == null) return;

        ResolveBuyInResponse result;

        _resolving++;
        try
        {
            result = approve
                ? await AuthManager.Instance.ApproveBuyInRequestAsync(
                      info.ClubId, info.ClubTableRowId, row.RequestId)
                : await AuthManager.Instance.RejectBuyInRequestAsync(
                      info.ClubId, info.ClubTableRowId, row.RequestId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AuthorizedList] {(approve ? "Approve" : "Reject")} failed: {e.Message}");
            ToastEvents.Show(approve ? ErrApprove : ErrReject);

            if (row != null) row.SetButtonsInteractable(true);
            return;
        }
        finally
        {
            _resolving--;
        }

        // The server may refuse to seat an approved player and reject instead
        // (chips refunded) — tell the host rather than implying it went through.
        if (approve && result != null && !result.Approved)
            ToastEvents.Show(MsgApproveTurnedReject);

        // Re-read the queue rather than just dropping the row: the server is the
        // one that knows what's still pending, and other requests may have moved.
        if (this != null && isActiveAndEnabled)
            LoadAsync(silent: false).Forget();
    }

    // ── Polling (interim, until the server emits a socket event) ────────────

    private void StartPolling()
    {
        StopPolling();
        _pollCts = new CancellationTokenSource();
        PollAsync(_pollCts.Token).Forget();
    }

    private void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async UniTaskVoid PollAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(pollSeconds),
                                    ignoreTimeScale: true, cancellationToken: token);

                if (_resolving == 0)
                    LoadAsync(silent: true).Forget();
            }
        }
        catch (OperationCanceledException) { /* panel closed */ }
    }

    // ── Slide ───────────────────────────────────────────────────────────────

    private float ClosedX => slideFromRight
        ? _openX + drawer.rect.width
        : _openX - drawer.rect.width;

    private void SlideIn()
    {
        if (drawer == null) return;

        // The design-time spot is the open position — capture it before the
        // drawer is ever moved.
        if (!_measured)
        {
            Canvas.ForceUpdateCanvases();
            _openX    = drawer.anchoredPosition.x;
            _measured = true;
        }

        drawer.anchoredPosition = new Vector2(ClosedX, drawer.anchoredPosition.y);
        StartSlide(_openX, deactivateAtEnd: false);
    }

    private void StartSlide(float targetX, bool deactivateAtEnd)
    {
        if (drawer == null)
        {
            if (deactivateAtEnd) gameObject.SetActive(false);
            return;
        }

        if (_slide != null) StopCoroutine(_slide);
        _slide = StartCoroutine(SlideTo(targetX, deactivateAtEnd));
    }

    private IEnumerator SlideTo(float targetX, bool deactivateAtEnd)
    {
        float startX = drawer.anchoredPosition.x;
        float t = 0f;

        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / slideDuration);
            drawer.anchoredPosition =
                new Vector2(Mathf.Lerp(startX, targetX, p), drawer.anchoredPosition.y);
            yield return null;
        }

        drawer.anchoredPosition = new Vector2(targetX, drawer.anchoredPosition.y);
        _slide = null;

        if (deactivateAtEnd)
            gameObject.SetActive(false);
    }
}
