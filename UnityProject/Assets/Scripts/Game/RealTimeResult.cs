using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ClubPoker.Networking.Models;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace ClubPoker.Game
{
    public class RealTimeResult : MonoBehaviour
    {
        public TextMeshProUGUI RunningTime;
        public TextMeshProUGUI TableName;
        public TextMeshProUGUI ExtensionTime;
        public TextMeshProUGUI VariantName;
        public TextMeshProUGUI Blinds;

        public Transform Content;
        public GameObject RealTimeResultPlayerPrefab;

        private DateTime sessionStartTime;

        private Dictionary<string, RealTimeResultPlayerPrefab> playerItems =
            new Dictionary<string, RealTimeResultPlayerPrefab>();

        public Button WaitingButton;
        public Button KickedButton;
        public Button ObserverButton;
        public GameObject WaitingSelectBG;
        public GameObject KickedSelectBG;
        public GameObject ObserVerSelectBG;

        public GameObject WaitingPanel;
        public GameObject KickedPanel;
        public GameObject ObserverPanel;
        public string tableId;

        public Transform KickedContent;
        public GameObject KickedPlayerPrefab;
        public Text KickedPlayerCount;

        public Transform WaitingContent;
        public GameObject WaitingPlayerPrefab;
        public Text WaitingPlayerCount;

        public Button WaitingPlayerButton;

        public GameObject KickedMsg;
        public GameObject waitingMsg;


       /* private async void Start()
        {
            WaitingButton.onClick.AddListener(WaitingButtonOnTap);
            KickedButton.onClick.AddListener(KickedButtonOnTap);
            ObserverButton.onClick.AddListener(ObserverButtonOnTap);
            WaitingPlayerButton.onClick.AddListener(OnJoinWaitingListClicked);
            await LoadKickedPlayers();
            await LoadWaitingPlayers();
            ObserverButtonOnTap();
        }

        private void OnEnable()
        {
            sessionStartTime = DateTime.Now;
            tableId = TableContext.tableId;
            LoadTableInfo();
            CreatePlayers();

            TableJoinHandler.OnTableJoined += OnTableJoined;
        }

        private void OnDisable()
        {
            TableJoinHandler.OnTableJoined -= OnTableJoined;
        }

        private void Update()
        {
            TimeSpan span = DateTime.Now - sessionStartTime;

            RunningTime.text =
                string.Format("{0:00}:{1:00}:{2:00}",
                span.Hours,
                span.Minutes,
                span.Seconds);
        }

        private async void OnJoinWaitingListClicked()
        {
            JoinWaitingListResponse response =
                await Auth.AuthManager.Instance
                .JoinWaitingListAsync(tableId);

            if (response != null && response.Joined)
            {
                Debug.Log(
                    "Joined Waiting List. Position : "
                    + response.Position);

                await LoadWaitingPlayers();
            }
        }
        async void  WaitingButtonOnTap()
        {
            WaitingPanel.SetActive(true);
            KickedPanel.SetActive(false);
            ObserverPanel.SetActive(false);
            WaitingSelectBG.SetActive(true);
            KickedSelectBG.SetActive(false);
            ObserVerSelectBG.SetActive(false);

            await LoadWaitingPlayers();
        }

        private async UniTask LoadWaitingPlayers()
        {
            foreach (Transform child in WaitingContent)
            {
                Destroy(child.gameObject);
            }

            WaitingListResponse response =
                await Auth.AuthManager.Instance.GetWaitingListAsync(tableId);

            if (response == null)
                return;

            WaitingPlayerCount.text =
                "Waiting(" + response.Count + ")";

            if (response.Count > 0)
            {
                waitingMsg.SetActive(false);
                WaitingPlayerButton.gameObject.SetActive(false);
            }

             if (response.Count == 0)
            {
                waitingMsg.SetActive(true);
                WaitingPlayerButton.gameObject.SetActive(true);
            }

            string myPlayerId =
                Auth.AuthManager.Instance.Session.Id;

            foreach (WaitingPlayerData player in response.WaitingList)
            {
                GameObject obj =
                    Instantiate(
                        WaitingPlayerPrefab,
                        WaitingContent);

                WaitingPanelPrefab item =
                    obj.GetComponent<WaitingPanelPrefab>();

                string timeText = "";
                Debug.Log("Server Time : " + player.WaitingSince);

                DateTime waitingAt;
                if (DateTime.TryParse(
                        player.WaitingSince,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out waitingAt))
                {
                    Debug.Log("Parsed Time : " + waitingAt);
                    Debug.Log("UTC Now : " + DateTime.UtcNow);

                    TimeSpan diff = DateTime.UtcNow - waitingAt;

                    Debug.Log("Diff : " + diff);

                    timeText = string.Format(
                        "Since {0:00}:{1:00}:{2:00}",
                        (int)diff.TotalHours,
                        diff.Minutes,
                        diff.Seconds
                    );
                }

                bool showRemoveButton =
                    player.PlayerId == myPlayerId;

                item.SetData(
                    player.PlayerId,
                    player.Username,
                    GetSinceTime(player.WaitingSince),
                    player.Position,
                    showRemoveButton);
               
                item.OnRemoveAction += OnRemoveWaitingPlayer;
            }
        }
        
        private string GetSinceTime(string waitingSince)
        {
            if (string.IsNullOrEmpty(waitingSince))
                return "Since --";

            if (DateTime.TryParse(
                waitingSince,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime joinedTime))
            {
                return "Since " +
                       joinedTime.ToLocalTime().ToString("HH:mm:ss");
            }

            return "Since --";
        }
        private async void OnRemoveWaitingPlayer(string playerId)
        {
            LeaveWaitingListResponse response =
                await Auth.AuthManager.Instance
                .LeaveWaitingListAsync(tableId);

            if (response != null && response.Left)
            {
                Debug.Log("Waiting list removed");

                await LoadWaitingPlayers();
            }
        }

        private async void KickedButtonOnTap()
        {
            WaitingPanel.SetActive(false);
            KickedPanel.SetActive(true);
            ObserverPanel.SetActive(false);
            WaitingSelectBG.SetActive(false);
            KickedSelectBG.SetActive(true);
            ObserVerSelectBG.SetActive(false);
            await LoadKickedPlayers();
        }
        private async UniTask LoadKickedPlayers()
        {
            foreach (Transform child in KickedContent)
            {
                Destroy(child.gameObject);
            }

            KickListResponse response =
                await Auth.AuthManager.Instance.GetKickListAsync(tableId);

            if (response == null)
                return;

            KickedPlayerCount.text = "Kicked(" + response.Count.ToString() + ")";
            if (response.Count > 0)
            {
                KickedMsg.SetActive(false);
            }
            else
            {
                KickedMsg.SetActive(true);
            }
            foreach (KickedPlayerData player in response.KickList)
            {
                GameObject obj =
                    Instantiate(
                        KickedPlayerPrefab,
                        KickedContent);

                KickedPlayerPrefab item =
                    obj.GetComponent<KickedPlayerPrefab>();

                string timeText = "";

                if (DateTime.TryParse(player.KickedAt, out DateTime kickedAt))
                {
                    timeText = kickedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                }

                item.SetData(
                    player.Username,
                    timeText);
            }
        }
        void ObserverButtonOnTap()
        {
            WaitingPanel.SetActive(false);
            KickedPanel.SetActive(false);
            ObserverPanel.SetActive(true);
            WaitingSelectBG.SetActive(false);
            KickedSelectBG.SetActive(false);
            ObserVerSelectBG.SetActive(true);
        }
        private void LoadTableInfo()
        {
            if (TableContext.CurrentTable == null)
                return;

            var table = TableContext.CurrentTable;

            TableName.text = table.Name;
            VariantName.text = table.Variant.Replace("_", " ").ToUpper();
            Blinds.text = $"{table.SmallBlind}/{table.BigBlind}";
        }

        private void CreatePlayers()
        {
            foreach (Transform child in Content)
            {
                Destroy(child.gameObject);
            }

            playerItems.Clear();

            var state = GameStateManager.Instance.CurrentState;

            if (state == null || state.Players == null)
                return;

            var table = TableContext.CurrentTable;

            string myPlayerId =
                Auth.AuthManager.Instance.Session.Id;

            foreach (GamePlayer player in state.Players)
            {
                GameObject obj =
                    Instantiate(
                        RealTimeResultPlayerPrefab,
                        Content);

                RealTimeResultPlayerPrefab item =
                    obj.GetComponent<RealTimeResultPlayerPrefab>();

                bool showKickButton =
                    player.Id != myPlayerId;
                item.SetData(
                    player.Id,
                    player.Username,
                    table.BuyInMin,
                    player.Chips,
                    showKickButton);

                item.OnKickAction += OnKickPlayerClicked;

                playerItems[player.Id] = item;
            }
        }
        private async void OnKickPlayerClicked(string playerId)
        {
            KickPlayerResponse response =
                await Auth.AuthManager.Instance.KickPlayerAsync(
                    tableId,
                    playerId);

            if (response != null && response.Kicked)
            {
                Debug.Log("Player kicked successfully : " + playerId);

                if (playerItems.ContainsKey(playerId))
                {
                    Destroy(playerItems[playerId].gameObject);
                    playerItems.Remove(playerId);
                    await LoadKickedPlayers();
                }
            }
        }
        private void OnTableJoined(GameStateUpdatePayload state)
        {
            RefreshPlayers(state);
        }

        private void RefreshPlayers(GameStateUpdatePayload state)
        {
            if (state == null || state.Players == null)
                return;

            foreach (GamePlayer player in state.Players)
            {
                if (!playerItems.ContainsKey(player.Id))
                    continue;

                playerItems[player.Id].UpdateStack(player.Chips);
            }
        }*/
    }
}