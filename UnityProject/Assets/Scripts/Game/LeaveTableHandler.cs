using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking;
using ClubPoker.Core;

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
        public Text ChipAmountText;
        public Text MidHandWarningText;

        private const string EVENT_LEAVE_TABLE = "player:leave_table";
        private const string SCENE_LOBBY = "Scene_Lobby";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            LeaveTableButton.onClick.AddListener(OpenLeaveDialog);
            ConfirmLeaveButton.onClick.AddListener(ConfirmLeaveTable);
            CancelLeaveButton.onClick.AddListener(CloseLeaveDialog);

            LeavePopupPanel.SetActive(false);
        }

      

        /// <summary>
        /// Open confirmation popup
        /// </summary>
        public void OpenLeaveDialog()
        {
            int chipsToReturn = GetMyCurrentTableChips();
            bool isMidHand = IsHandInProgress();

            ChipAmountText.text =
                $"Chips Returning To Wallet: {chipsToReturn}";

            MidHandWarningText.gameObject.SetActive(isMidHand);

            if (isMidHand)
            {
                MidHandWarningText.text =
                    "Warning: Leaving mid-hand will be treated as Fold + Leave";
            }

            LeavePopupPanel.SetActive(true);

            Debug.Log($"[LeaveTable] Popup Opened | Chips: {chipsToReturn}");
        }

        /// <summary>
        /// Confirm leave → emit socket
        /// </summary>
        public void ConfirmLeaveTable()
        {
            if (!SocketManager.Instance.IsConnected)
            {
                Debug.LogWarning("[LeaveTable] Socket not connected");
                return;
            }

            string tableId = SocketManager.Instance.CurrentTableId;

            if (string.IsNullOrEmpty(tableId))
            {
                Debug.LogWarning("[LeaveTable] No active table");
                return;
            }

            var payload = new Dictionary<string, object>()
            {
                { "tableId", tableId }
            };

            Debug.Log("[LeaveTable] Emit player:leave_table");

            SocketManager.Instance.Emit(EVENT_LEAVE_TABLE, payload);

            // Local cleanup
            GameStateManager.Instance.Clear();
            SocketManager.Instance.ClearCurrentTable();


            LeavePopupPanel.SetActive(false);
            GameSceneManager.Instance.LoadScene(SCENE_LOBBY);
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

            return state == "preflop" ||
                   state == "flop" ||
                   state == "turn" ||
                   state == "river";
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