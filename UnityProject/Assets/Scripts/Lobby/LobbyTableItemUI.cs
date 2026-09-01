using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Game;
using ClubPoker.Core;
using System;

namespace ClubPoker.Lobby
{
    public class LobbyTableItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI variantText;
        [SerializeField] private TextMeshProUGUI blindText;
        [SerializeField] private TextMeshProUGUI playersText;
        [SerializeField] private TextMeshProUGUI BuyinText;
        [SerializeField] private Image seatFillBar;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button watchButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI joinButtonText;

        private string _tableId;
        private bool _handInProgress;
        private int _minBuyIn;
        private int _maxBuyIn;
        private int _smallBlind;
        private int _bigBlind;
        private LobbyController _controller;
        private TableData _data;
        public void Setup(TableData data, LobbyController controller)
        {
            _controller = controller;
            _data = data;
            _tableId = data.TableId;
            _minBuyIn = data.MinBuyIn;
            _maxBuyIn = data.MaxBuyIn;
            _smallBlind = data.SmallBlind;
            _bigBlind = data.BigBlind;
            nameText.text = data.Name;
            
            if (variantText != null)
                variantText.text = VariantUtils.ToDisplayName(data.Variant);
                
            blindText.text =  $"Blinds: {data.SmallBlind}/{data.BigBlind}";
            playersText.text = $"{data.CurrentPlayers}/{data.MaxPlayers} Seated";
            BuyinText.text = $"Buy-in: {data.MinBuyIn} - {data.MaxBuyIn}";

            if (seatFillBar != null)
                seatFillBar.fillAmount = data.MaxPlayers > 0
                    ? (float)data.CurrentPlayers / data.MaxPlayers
                    : 0f;

            if (statusText != null)
            {
                if (data.HandInProgress)
                    statusText.text = "Hand in progress";
                else if (data.GameState == "ROUND_END" && data.CurrentPlayers > 0)
                    statusText.text = "Waiting for next hand";
                else
                    statusText.text = "Waiting for players";
            }

            bool isFull = data.CurrentPlayers >= data.MaxPlayers;
            bool isClosed = data.Status != "open";
            _handInProgress = data.HandInProgress;

            if (_handInProgress)
            {
                // Can't sit mid-hand → Join becomes Watch & Wait; hide separate watch btn.
                if (joinButtonText != null) joinButtonText.text = "Watch & Wait";
                joinButton.interactable = !isClosed;
                if (watchButton != null) watchButton.gameObject.SetActive(false);
            }
            else
            {
                if (joinButtonText != null) joinButtonText.text = "Join Now";
                joinButton.interactable = !isFull && !isClosed;
                if (watchButton != null) watchButton.gameObject.SetActive(true);
            }

            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() =>
            {
                OnJoinClicked().Forget();
            });

            if (watchButton != null)
            {
                watchButton.onClick.RemoveAllListeners();
                watchButton.onClick.AddListener(() =>
                {
                    OnWatchClicked().Forget();
                });
            }
    }

        private UniTaskVoid OnWatchClicked() => SpectateAsync(watchButton);

        // Spectate flow shared by the Watch button and the Join button's
        // Watch & Wait mode (hand in progress).
        private async UniTaskVoid SpectateAsync(Button trigger)
        {
            try
            {
                trigger.interactable = false;

                // Path B — Watch & Wait:
                // 1. Spectate (REST)
                var spectate = await AuthManager.Instance.SpectateTableAsync(_tableId);
                Debug.Log($"[Spectate] token={spectate.SpectatorToken} state={spectate.CurrentState?.GameState}");

                // 2. Enter as spectator + remember buy-in for when a seat frees.
                //    Buy-in amount = min for now; popup deferred. To restore the
                //    popup, choose the amount in the lobby up front and pass it in:
                //    _controller.ShowBuyIn(_tableId, _minBuyIn, _maxBuyIn, _smallBlind, _bigBlind,
                //        amount => { TableJoinHandler.Instance.BeginWatchAndWait(_tableId, amount); return UniTask.CompletedTask; });
                TableContext.EnterFromLobby(_tableId, _data);
                TableJoinHandler.Instance.BeginWatchAndWait(_tableId, _minBuyIn);

                // 3. Join the waiting list → server pushes table:seat_available when a seat frees.
                await AuthManager.Instance.JoinWaitingListAsync(_tableId);
            }
            catch (Exception e)
            {
                Debug.LogError("Spectate failed: " + e.Message);
                ToastEvents.Show(GameMessages.WatchFailed(e.Message));
                trigger.interactable = true;
            }
        }


        private UniTaskVoid OnJoinClicked()
        {
            // Hand in progress → Join acts as Watch & Wait.
            if (_handInProgress)
                return SpectateAsync(joinButton);

            // Buy-in confirmation popup temporarily skipped — join directly with
            // the minimum buy-in. TODO: restore the popup:
            // _controller.ShowBuyIn(_tableId, _minBuyIn, _maxBuyIn, _smallBlind, _bigBlind, JoinWithBuyIn);
            return JoinDirect();
        }

        private async UniTaskVoid JoinDirect()
        {
            try
            {
                joinButton.interactable = false;
                await JoinWithBuyIn(_minBuyIn);
            }
            catch (Exception e)
            {
                Debug.LogError("Join failed: " + e.Message);
                ToastEvents.Show(GameMessages.JoinFailed(e.Message));
                joinButton.interactable = true;
            }
        }

        // Runs inside the buy-in popup after the player confirms an amount.
        // Throwing surfaces the error in the popup; returning closes it.
        private async UniTask JoinWithBuyIn(int buyIn)
        {
            // 1. Economy buy-in: move chips from wallet to table stack.
            await AuthManager.Instance.BuyInAsync(_tableId, buyIn);

            // 2. Seat at the table.
            try
            {
                await AuthManager.Instance.JoinTableAsync(_tableId, buyIn);
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("Already seated"))
                    throw;
            }

            // 3. Open GameRoom → TableJoinHandler emits player:join_table after auth.
            //    Lobby origin: the in-game menu drops the club-only options and
            //    Back/Exit return here instead of a club.
            TableContext.EnterFromLobby(_tableId, _data);
            TableJoinHandler.Instance.JoinTable(_tableId);
        }
    }
}
