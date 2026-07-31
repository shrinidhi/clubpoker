using System.Collections.Generic;
using UnityEngine;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class HandHistoryManager : MonoBehaviour
    {
        public static HandHistoryManager Instance;

        public List<HandHistoryRecord> HandLogs =
            new List<HandHistoryRecord>();

        private List<HandActionRecord> currentActions =
            new List<HandActionRecord>();

        private Dictionary<string, int> startChipSnapshot =
            new Dictionary<string, int>();

        private List<StreetBoardRecord> currentBoardHistory =
    new List<StreetBoardRecord>();

        private int currentRoundNumber = -1;

        private string currentStreet = "PRE_FLOP";

        private string currentTableId;
        private string currentVariant;
        private int currentSmallBlindSeat;
        private int currentBigBlindSeat;
        private string currentStartDateTime;

        private int currentDealerSeat;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
             //   DontDestroyOnLoad(gameObject);
            }
            else
            {
              //  Destroy(gameObject);
            }
        }

        public void StartNewHand(GameStateUpdatePayload state)
        {
            if (currentRoundNumber == state.RoundNumber)
                return;

            currentRoundNumber = state.RoundNumber;

            currentActions.Clear();
            currentBoardHistory.Clear();
            startChipSnapshot.Clear();

            currentStreet = "PRE_FLOP";

            currentTableId = state.TableId;
            currentVariant = state.Variant;
            currentSmallBlindSeat = state.SmallBlindSeat ?? -1;
            currentBigBlindSeat = state.BigBlindSeat ?? -1;
            currentStartDateTime = System.DateTime.Now.ToString("MM/dd HH:mm");
            currentDealerSeat = state.DealerSeat ?? -1;
            foreach (var player in state.Players)
            {
                startChipSnapshot[player.Id] =
                    player.Chips;
            }

            Debug.Log(
                $"Hand Start Round : {currentRoundNumber}");
        }

        public void SetStreet(string street)
        {
            currentStreet = street;
        }

        public void AddAction(PlayerActedPayload payload)
        {
            if (payload == null)
                return;

            HandActionRecord action =
                new HandActionRecord();

            action.Street = currentStreet;
            action.PlayerId = payload.PlayerId;
            action.Action = payload.Action;
            action.Amount = payload.Amount;
            action.PotAfter = payload.Pot;

            // An action can arrive for a player who's no longer in the snapshot —
            // the server auto-acts for disconnected seats, and we may have pruned
            // them already. Every read of `player` past here must tolerate null.
            List<GamePlayer> players =
                GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState != null
                    ? GameStateManager.Instance.CurrentState.Players
                    : null;

            GamePlayer player = players != null
                ? players.Find(x => x.Id == payload.PlayerId)
                : null;

            action.Username = player != null ? player.Username : "";
            action.ChipsAfter = player != null ? player.Chips : 0;

            currentActions.Add(action);

            Debug.Log(
                $"Action : {action.Username} {payload.Action}");
        }

        public void CompleteHand(
            RoundEndPayload payload,
            GameStateUpdatePayload currentState)
        {
            HandHistoryRecord record =
                new HandHistoryRecord();

            record.RoundNumber =
                payload.roundNumber;

            record.WinningHand =
                payload.hand != null
                    ? payload.hand.name
                    : "";

            record.PotAmount =
                payload.potWon;
            record.TableId = currentTableId;
            record.Variant = currentVariant;
            record.SmallBlindSeat = currentSmallBlindSeat;
            record.BigBlindSeat = currentBigBlindSeat;
            record.StartDateTime = currentStartDateTime;
            record.DealerSeat = currentDealerSeat;

            if (payload.communityCards != null)
            {
                record.BoardCards =
                    new List<string>(
                        payload.communityCards);
            }

            record.Actions =
                new List<HandActionRecord>(
                    currentActions);

            record.StreetBoards =
    new List<StreetBoardRecord>(
        currentBoardHistory);

            foreach (var player in currentState.Players)
            {
                HandHistoryPlayer historyPlayer =
                    new HandHistoryPlayer();

                historyPlayer.PlayerId =
                    player.Id;

                historyPlayer.Username =
                    player.Username;
                historyPlayer.Seat = player.Seat;
                historyPlayer.IsWinner =
                    payload.winner != null &&
                    payload.winner.id == player.Id;

                int startChips = 0;

                if (startChipSnapshot.ContainsKey(player.Id))
                {
                    startChips =
                        startChipSnapshot[player.Id];
                }

                int finalChips = player.Chips;

                if (payload.updatedChipBalances != null &&
                    payload.updatedChipBalances.ContainsKey(player.Id))
                {
                    finalChips =
                        payload.updatedChipBalances[player.Id];
                }

                historyPlayer.ChipDifference =
                    finalChips - startChips;

                if (payload.showdownCards != null)
                {
                    var showdown =
                        payload.showdownCards.Find(
                            x => x.playerId == player.Id);

                    if (showdown != null)
                    {
                        historyPlayer.HoleCards =
                            showdown.holeCards;

                        historyPlayer.HandName =
                            showdown.handName;

                       historyPlayer.BestHandCards =  showdown.bestHandCards;
                    }
                }

                record.Players.Add(historyPlayer);
            }

            HandLogs.Add(record);

            Debug.Log(
                $"Hand Saved Round {record.RoundNumber}");

            Debug.Log(
                $"Total Hands = {HandLogs.Count}");
        }

        public void AddBoardCards(List<string> cards)
        {
            if (cards == null || cards.Count == 0)
                return;

            StreetBoardRecord board =
                new StreetBoardRecord();

            board.Street = currentStreet;

            board.Cards =
                new List<string>(cards);

            currentBoardHistory.Add(board);

            Debug.Log(
                $"Board Saved : {currentStreet} -> {string.Join(",", cards)}");
        }
        public string GetCurrentStreet()
        {
            return currentStreet;
        }
        public HandHistoryRecord GetHand(int index)
        {
            if (index < 0 ||
                index >= HandLogs.Count)
                return null;

            return HandLogs[index];
        }

        public HandHistoryRecord GetLatestHand()
        {
            if (HandLogs.Count == 0)
                return null;

            return HandLogs[HandLogs.Count - 1];
        }

        public int GetHandCount()
        {
            return HandLogs.Count;
        }

        public void ClearHistory()
        {
            HandLogs.Clear();

            currentActions.Clear();

            startChipSnapshot.Clear();

            currentRoundNumber = -1;
        }
    }
}