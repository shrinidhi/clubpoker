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

        private bool _standUpMode;

        private void Start()
        {
            LeaveTableButton.onClick.AddListener(OpenLeaveDialog);
            ConfirmLeaveButton.onClick.AddListener(OnConfirm);
            CancelLeaveButton.onClick.AddListener(CloseLeaveDialog);

            LeavePopupPanel.SetActive(false);
        }

        /// <summary>
        /// Open the Stand Up confirmation popup (CLUB-1010). Same dialog, but on
        /// confirm it stands the player up (→ spectator) instead of exiting.
        /// </summary>
        /// <summary>Where the stack goes: a club seat was funded from club chips and
        /// settles back there, not into the global wallet.</summary>
        private static string BalanceName => TableContext.IsClub ? "club chips" : "wallet";

        public void OpenStandUpDialog()
        {
            // Already watching or already standing up → nothing to stand up from.
            if (TableJoinHandler.Instance != null &&
                (TableJoinHandler.Instance.IsSpectator || TableJoinHandler.Instance.IsStoodUp))
            {
                ToastEvents.Show(GameMessages.NotSeated);
                return;
            }

            _standUpMode = true;

            int chipsToReturn = GetMyCurrentTableChips();
            bool isMidHand = IsHandInProgress();

            if (TitleText != null) TitleText.text = "Stand Up";

            // Mid-hand the stack is still moving: the player keeps playing this hand
            // and leaves at round end, so today's figure is not what comes back.
            // Naming an amount that the next bet invalidates reads as a promise, so
            // only state it when it can't change.
            ChipAmountText.text = isMidHand
                ? $"Stand up? Your remaining chips will be returned to your {BalanceName}."
                : $"Stand up? Your chips ({chipsToReturn}) will be returned to your {BalanceName}.";

            MidHandWarningText.gameObject.SetActive(isMidHand);
            if (isMidHand)
                MidHandWarningText.text = "You will stand up after this hand completes.";

            LeavePopupPanel.SetActive(true);

            Debug.Log($"[StandUp] Popup Opened | Chips: {chipsToReturn}");
        }

        /// <summary>
        /// Open confirmation popup (full leave → exit)
        /// </summary>
        public void OpenLeaveDialog()
        {
            _standUpMode = false;

            int chipsToReturn = GetMyCurrentTableChips();
            bool isMidHand = IsHandInProgress();

            if (TitleText != null) TitleText.text = "Leave Table";

            // Full leave folds immediately, so the stack can't move after this — the
            // exact figure is safe to name here.
            ChipAmountText.text =
                $"Chips Returning To {BalanceName}: {chipsToReturn}";

            MidHandWarningText.gameObject.SetActive(isMidHand);

            if (isMidHand)
            {
                MidHandWarningText.text =
                    "Warning: Leaving mid-hand will be treated as Fold + Leave";
            }

            LeavePopupPanel.SetActive(true);

            Debug.Log($"[LeaveTable] Popup Opened | Chips: {chipsToReturn}");
        }

        // Confirm button → route by mode: Stand Up (→ spectator) or full leave (→ exit).
        private void OnConfirm()
        {
            LeavePopupPanel.SetActive(false);

            if (_standUpMode)
            {
                if (TableJoinHandler.Instance != null)
                    TableJoinHandler.Instance.RequestStandUp();
            }
            else
            {
                ConfirmLeaveTable();
            }
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

            // REST /leave as well — frees the seat and returns chips server-side
            // even when the socket is already dead (emit above skipped), so the
            // next join doesn't hit a stale "already seated" state. Fire-and-forget:
            // exit must not block on a slow network.
            if (!string.IsNullOrEmpty(tableId))
                LeaveViaRestAsync(tableId).Forget();

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

        private async UniTaskVoid LeaveViaRestAsync(string tableId)
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

        public void CloseLeaveDialog()
        {
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

        /// <summary>
        /// Get current player chips from table state
        /// </summary>
        private int GetMyCurrentTableChips()
        {
            string myPlayerId =
                Auth.AuthManager.Instance.Session.Id;

            if (GameStateManager.Instance.Players == null)
                return 0;

            foreach (var player in GameStateManager.Instance.Players)
            {
                if (player.Id == myPlayerId)
                {
                    return player.Chips;
                }
            }

            return 0;
        }

       
    }
}