using System;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Game;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "Take Seat" button (return to a held seat) — shown on the menu / club screens while the server says
/// we still hold a seat somewhere (GET /api/player/active-seat): after Back to
/// Club, an app restart, a crash, or a seat taken on another device.
///
/// Checks on enable (so after login and session restore, which both land on
/// these screens), on app resume, and whenever the socket authenticates. Hidden
/// when there's no seat — including once the server releases an idle seat
/// (5 min sat out / disconnected), when active-seat goes back to null.
///
/// Tap: club seat → open the club and its table row; the seat is remembered as
/// held, so the table rejoins it without a buy-in popup. Lobby seat → straight
/// into the table as a player.
///
/// Drop it on a banner object in each screen that should show it, and wire
/// Root (the part to show/hide) and Button. Label / ChipsText are optional.
/// </summary>
public class ReturnToTableBanner : MonoBehaviour
{
    [Tooltip("Shown only while a held seat exists.")]
    public GameObject Root;
    [Tooltip("Tapping it opens the table.")]
    public Button Button;
    [Tooltip("Optional — filled with \"Take Seat\" (localized).")]
    public TextMeshProUGUI Label;
    [Tooltip("Optional — the stack waiting on that table.")]
    public TextMeshProUGUI ChipsText;

    private ActiveSeat _seat;
    private bool _opening;

    private void Awake()
    {
        if (Button != null)
            Button.onClick.AddListener(OnTap);
        SetVisible(false);
    }

    private void OnEnable()
    {
        ActiveSeatTracker.Changed += Apply;
        if (SocketManager.Instance != null)
            SocketManager.Instance.OnAuthenticated += OnSocketAuthenticated;

        Apply(ActiveSeatTracker.Current);   // last known, until the fetch lands
        Refresh();
        _poll = StartCoroutine(PollWhileShown());
    }

    // The server releases an idle seat after 5 min with no event, so while we're
    // showing one, re-check now and then. Nothing to poll when there's no seat.
    private const float POLL_SECONDS = 30f;
    private Coroutine _poll;

    private System.Collections.IEnumerator PollWhileShown()
    {
        var wait = new WaitForSecondsRealtime(POLL_SECONDS);
        while (true)
        {
            yield return wait;
            if (ActiveSeatTracker.Current != null)
                Refresh();
        }
    }

    private void OnDisable()
    {
        if (_poll != null)
        {
            StopCoroutine(_poll);
            _poll = null;
        }
        ActiveSeatTracker.Changed -= Apply;
        if (SocketManager.Instance != null)
            SocketManager.Instance.OnAuthenticated -= OnSocketAuthenticated;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
            Refresh();
    }

    private void OnSocketAuthenticated(SocketAuthenticatedPayload _) => Refresh();

    /// Fetch again; every listener (this button, table-row badges) updates.
    public void Refresh() => ActiveSeatTracker.RefreshAsync().Forget();

    private void Apply(ActiveSeat seat)
    {
        _seat = seat;

        // Never on the table screen itself.
        bool show = seat != null && !SceneManager.GetActiveScene().name.Contains("GameTable");

        if (show)
        {
            if (Label != null) Label.text = GameMessages.TakeSeatButton;
            if (ChipsText != null) ChipsText.text = seat.Chips.ToString("N0");
        }

        SetVisible(show);
    }

    private void SetVisible(bool visible)
    {
        GameObject target = Root != null ? Root : (Button != null ? Button.gameObject : null);
        if (target == null)
            return;

        if (target != gameObject)
        {
            target.SetActive(visible);
            return;
        }

        // Root is this object: deactivating it would stop the checks that bring
        // it back, so hide it with a CanvasGroup instead.
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    private void OnTap() => OpenAsync().Forget();

    private async UniTaskVoid OpenAsync()
    {
        if (_seat == null || _opening)
            return;

        _opening = true;
        SetVisible(false);

        try
        {
            // What we show can be up to one poll old. The server may have let the
            // seat go since (idle release, busted, closed) — ask before walking
            // the player into a table they no longer sit at.
            string shownTableId = _seat.TableId;
            ActiveSeat seat = await ActiveSeatTracker.RefreshAsync();
            if (this == null)
                return;

            if (seat == null || seat.TableId != shownTableId)
            {
                Debug.Log($"[ReturnToTable] Seat at {shownTableId} released before the tap");
                HeldClubSeat.Forget(shownTableId);
                ToastEvents.Show(GameMessages.HeldSeatReleased);
                // A seat somewhere else still shows via Changed → Apply.
                return;
            }

            if (!string.IsNullOrEmpty(seat.ClubId) && !string.IsNullOrEmpty(seat.ClubTableRowId))
            {
                // Rejoin the held seat on arrival instead of offering a buy-in.
                HeldClubSeat.Remember(seat.TableId);
                ClubScrollBanner.OpenClubTable(seat.ClubId, seat.ClubTableRowId);
            }
            else if (TableJoinHandler.Instance != null)
            {
                TableContext.EnterFromLobby(seat.TableId);
                TableJoinHandler.Instance.JoinTable(seat.TableId, isSpectator: false);
            }
            else
            {
                Debug.LogWarning("[ReturnToTable] No TableJoinHandler — can't open the table");
            }
        }
        finally
        {
            _opening = false;
        }
    }
}
