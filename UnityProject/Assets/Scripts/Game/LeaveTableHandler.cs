using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;
using ClubPoker.Core;
using TMPro;

namespace ClubPoker.Game
{
    public class LeaveTableHandler : MonoBehaviour
    {
        public static LeaveTableHandler Instance { get; private set; }

        [Header("Buttons")]
        public Button LeaveTableButton;
        public Button ConfirmLeaveButton;
        public Button CancelLeaveButton;

        [Header("Popup UI")]
        public GameObject LeavePopupPanel;
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI ChipAmountText;
        public TextMeshProUGUI MidHandWarningText;

        private const string EVENT_LEAVE_TABLE = "player:leave_table";
        private const string EVENT_TABLE_CLOSED = "player:broadcast_table_closed";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        // Set for the Back-while-seated warning, which neither stands up nor leaves —
        // it sits out and goes back to the previous screen. Runs on confirm.
        private Action _onConfirmOverride;

        private void Start()
        {
            LeaveTableButton.onClick.AddListener(OpenLeaveDialog);
            ConfirmLeaveButton.onClick.AddListener(OnConfirm);
            CancelLeaveButton.onClick.AddListener(CloseLeaveDialog);

            LeavePopupPanel.SetActive(false);
        }

        /// <summary>
        /// Back while seated: the player keeps the seat but sits out, and the server
        /// now releases a sitting-out seat after 3 hands. That used to be free —
        /// sit-out had no limit — so leaving the screen parked the seat for as long
        /// as you liked. It doesn't any more, and a player who taps Back to look at
        /// the club has no table UI left to learn it from: they'd come back to a
        /// cashed-out stack and no seat.
        ///
        /// So it's said up front, on the tap, and they choose.
        /// </summary>
        public void OpenSitOutAndLeaveDialog(Action onConfirm)
        {
            _onConfirmOverride = onConfirm;

            if (TitleText != null) TitleText.text = "Leave the table screen?";

            ChipAmountText.text = GameMessages.SitOutHandLimitWarning;

            // The 3-hand limit IS the warning here, and it's already in the body —
            // a second line would say it twice.
            MidHandWarningText.gameObject.SetActive(false);

            LeavePopupPanel.SetActive(true);

            Debug.Log("[SitOut] Back-while-seated warning shown");
        }

        /// <summary>
        /// Open confirmation popup (full leave → exit)
        /// </summary>
        public void OpenLeaveDialog()
        {
            _onConfirmOverride = null;

            bool seated = TableJoinHandler.Instance == null || !TableJoinHandler.Instance.IsSpectator;

            if (TitleText != null) TitleText.text = "Leave Table";

            // One plain question, no chip figures or fold warnings: leaving mid-hand
            // isn't allowed any more (checked on Confirm), so there's nothing to warn
            // about here.
            ChipAmountText.text = seated
                ? GameMessages.ConfirmLeaveSeatAndExit
                : GameMessages.ConfirmExitTable;

            MidHandWarningText.gameObject.SetActive(false);

            LeavePopupPanel.SetActive(true);

            Debug.Log($"[LeaveTable] Popup Opened | Seated: {seated}");
        }

        // Confirm button → the override if one is set, otherwise full leave (→ exit).
        // Stand Up no longer uses this dialog — it goes straight through.
        private void OnConfirm()
        {
            LeavePopupPanel.SetActive(false);

            if (_onConfirmOverride != null)
            {
                var run = _onConfirmOverride;
                _onConfirmOverride = null;
                run();
                return;
            }

            // Still playing this hand → stay. Checked on Confirm, not when opening,
            // because the hand can end (or the player fold) while the popup is up.
            if (IsMeActiveInHand())
            {
                ToastEvents.Show(GameMessages.ExitBlockedInHand);
                Debug.Log("[LeaveTable] Blocked — still active in the hand");
                return;
            }

            ConfirmLeaveTable();
        }

        /// <summary>
        /// True while this player still has a live stake in the running hand: dealt
        /// in and not folded. All-in counts — they can't fold, so they wait for the
        /// hand to end. A spectator, someone who joined mid-hand (not dealt), or a
        /// player who folded can leave straight away.
        /// </summary>
        private bool IsMeActiveInHand()
        {
            if (!IsHandInProgress())
                return false;

            if (TableJoinHandler.Instance != null && TableJoinHandler.Instance.IsSpectator)
                return false;

            var players = GameStateManager.Instance != null ? GameStateManager.Instance.Players : null;
            string myId = Auth.AuthManager.Instance?.Session?.Id;

            if (players == null || string.IsNullOrEmpty(myId))
                return false;

            foreach (var player in players)
            {
                if (player.Id == myId)
                    return player.CardsDealt && !player.Folded;
            }

            return false;
        }

        /// <summary>
        /// Confirm leave → emit socket
        /// </summary>
        public void ConfirmLeaveTable()
        {
            string tableId = SocketManager.Instance.CurrentTableId;

            if (SocketManager.Instance.IsConnected)
            {
                if (!string.IsNullOrEmpty(tableId))
                {
                    var payload = new Dictionary<string, object>()
                    {
                        { "tableId", tableId }
                    };

                    Debug.Log("[LeaveTable] Emit player:leave_table");
                    SocketManager.Instance.Emit(EVENT_LEAVE_TABLE, payload);

                    Debug.Log("[LeaveTable] Emit player:broadcast_table_closed");
                    SocketManager.Instance.Emit(EVENT_TABLE_CLOSED, payload);
                }
            }
            else
            {
                Debug.Log("[LeaveTable] Socket disconnected (game over) — skipping emit");
            }

            // REST /leave, then — last one out on a club table — unlink the row.
            // /leave frees the seat and returns chips server-side even when the
            // socket is already dead (emit above skipped), so the next join doesn't
            // hit a stale "already seated" state. The unlink has to wait for it:
            // clearing the row while we still hold a seat leaves the server with a
            // seated player on an unlinked table.
            // Fire-and-forget: exit must not block on a slow network. Context is
            // read synchronously inside, before TableExitRouter clears it.
            LeaveThenUnlinkAsync(tableId, leave: true).Forget();

            // Always clean up and navigate regardless of socket state
            GameStateManager.Instance.Clear();
            SocketManager.Instance.ClearCurrentTable();

            // Kill the local bots too — otherwise they keep playing the old
            // table and isRunning stays true, so the next StartBots is a no-op
            // ("waiting for players" forever on the next table).
            if (UnityBotRunner.Instance != null)
                UnityBotRunner.Instance.StopBots();

            // Close the game socket — we're leaving the table.
            if (SocketManager.Instance.IsConnected)
                SocketManager.Instance.Disconnect();

            LeavePopupPanel.SetActive(false);

            // The seat is released, so drop the table context and return to the
            // screen we came from — the club for a club table, home otherwise.
            TableExitRouter.GoBackAndClear();
        }

        /// <summary>
        /// game:player_busted — the server already eliminated us and freed the seat,
        /// so this is the leave path minus every "tell the server we're leaving"
        /// step: no player:leave_table, no broadcast, no REST /leave. Just tear the
        /// local table down and route back.
        /// </summary>
        public void ExitAfterBusted()
        {
            Debug.Log("[Busted] Closing table and exiting");

            // Busting out empties the table as much as leaving does — same unlink.
            if (SocketManager.Instance != null)
                LeaveThenUnlinkAsync(SocketManager.Instance.CurrentTableId, leave: false).Forget();

            TearDownAndRouteBack();
        }

        /// <summary>
        /// The host disbanded the table. The server has already closed it and
        /// deleted the club row, so there is nothing to leave or unlink — tear the
        /// local table down and route back, same as a bust-out.
        /// </summary>
        public void ExitAfterDisband()
        {
            Debug.Log("[Disband] Closing table and exiting");
            TearDownAndRouteBack();
        }

        private void TearDownAndRouteBack()
        {
            if (LeavePopupPanel != null)
                LeavePopupPanel.SetActive(false);

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.Clear();

            if (SocketManager.Instance != null)
                SocketManager.Instance.ClearCurrentTable();

            if (UnityBotRunner.Instance != null)
                UnityBotRunner.Instance.StopBots();

            if (SocketManager.Instance != null && SocketManager.Instance.IsConnected)
                SocketManager.Instance.Disconnect();

            TableExitRouter.GoBackAndClear();
        }

        /// <summary>
        /// REST /leave (when <paramref name="leave"/>), THEN — club tables only, and
        /// only if we're the last player — unlink the club row from the engine
        /// table (POST link-club-table with clear:true) so the row shows as an
        /// unstarted template again and the next member to tap it creates a fresh
        /// table. Strictly in that order: the seat has to be gone before the row
        /// is cleared.
        ///
        /// A failed /leave doesn't stop the unlink — the socket leave usually got
        /// through, and a row left pointing at an empty table is the worse outcome.
        ///
        /// "Last" is judged from the state we hold — seat count 1 (us) or 0. Two
        /// players leaving in the same instant can both read 2 and neither unlink;
        /// the row is repaired on the next join, which finds the dead table.
        ///
        /// Everything is read before the first await — the caller clears the table
        /// context and game state right after this returns.
        /// </summary>
        private async UniTaskVoid LeaveThenUnlinkAsync(string tableId, bool leave)
        {
            if (string.IsNullOrEmpty(tableId))
                return;

            string clubId = TableContext.ClubId;
            string rowId  = TableContext.Info?.ClubTableRowId;

            bool unlink = TableContext.IsClub &&
                          !string.IsNullOrEmpty(clubId) &&
                          !string.IsNullOrEmpty(rowId) &&
                          SeatedPlayerCount() <= 1;

            if (leave)
            {
                try
                {
                    await Auth.AuthManager.Instance.LeaveTableAsync(tableId);
                    Debug.Log("[LeaveTable] POST /leave OK");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LeaveTable] POST /leave failed: {e.Message}");
                }
            }

            if (!unlink)
                return;

            try
            {
                await Auth.AuthManager.Instance.UnlinkClubTableAsync(tableId, clubId, rowId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaveTable] Club row unlink failed: {e.Message}");
            }
        }

        private static int SeatedPlayerCount()
        {
            var players = GameStateManager.Instance != null
                ? GameStateManager.Instance.Players
                : null;

            return players?.Count ?? 0;
        }

        public void CloseLeaveDialog()
        {
            // Cancelled → the pending action is off, whatever it was. Leaving it set
            // would fire it from the next confirm of an unrelated dialog.
            _onConfirmOverride = null;

            LeavePopupPanel.SetActive(false);
        }

        /// <summary>
        /// Detect if current hand running
        /// </summary>
        private bool IsHandInProgress()
        {
            string state = GameStateManager.Instance.GameState;

            if (string.IsNullOrEmpty(state))
                return false;

            // Server sends uppercase states (PRE_FLOP/FLOP/TURN/RIVER); a hand is in
            // progress whenever we're not waiting or between rounds.
            string s = state.ToUpperInvariant();

            return s == "PRE_FLOP" ||
                   s == "FLOP" ||
                   s == "TURN" ||
                   s == "RIVER";
        }

       
    }
}