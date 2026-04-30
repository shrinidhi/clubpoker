using System;
using System.Collections.Generic;
using UnityEngine;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        public string TableId { get; private set; }
        public string GameState { get; private set; }
        public int RoundNumber { get; private set; }
        public int Pot { get; private set; }
        
        public List<string> CommunityCards { get; private set; }
        public int DealerSeat { get; private set; }
        public string CurrentTurnPlayerId { get; private set; }
        public List<GamePlayer> Players { get; private set; }
        public List<string> YourCards { get; private set; }
        public string Variant { get; private set; }

        public int SmallBlindSeat { get; private set; }
        public int BigBlindSeat { get; private set; }


        public List<SidePots> SidePots { get; private set; }

        public event Action OnStateUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetFullState(GameStateUpdatePayload state)
        {
            if (state == null)
            {
                Debug.LogError("[GameStateManager] NULL state");
                return;
            }

            TableId = state.TableId;
            GameState = state.GameState;
            RoundNumber = state.RoundNumber;
            Pot = state.Pot;
            SidePots = state.SidePots ?? new List<SidePots>();
            CommunityCards = state.CommunityCards ?? new List<string>();
            DealerSeat = state.DealerSeat;
            CurrentTurnPlayerId = state.CurrentTurnPlayerId;
            Players = state.Players ?? new List<GamePlayer>();

            Debug.Log($"[GameStateManager] State Applied | Table: {TableId}");

            OnStateUpdated?.Invoke();
        }


        public void SetYourCards(List<string> cards, string variant)
        {
            YourCards = cards ?? new List<string>();
            Variant = variant;

            Debug.Log($"[GameStateManager] YourCards updated: {YourCards.Count}");

            OnStateUpdated?.Invoke();
        }


        public void AppendCommunityCards( List<string> newCards,string street
)
        {
            if (CommunityCards == null)
                CommunityCards = new List<string>();

            CommunityCards.AddRange(newCards);

            Debug.Log(
                $"[GameStateManager] Community cards updated: " +
                string.Join(", ", CommunityCards)
            );

            OnStateUpdated?.Invoke();
        }


        public void ApplyPlayerAction( PlayerActedPayload payload
)
        {
            Pot = payload.Pot;

            if (Players == null)
                return;

            foreach (var player in Players)
            {
                if (player.Id == payload.PlayerId)
                {
                    player.Chips = payload.UpdatedChips;
                    player.LastAction = payload.Action;
                    break;
                }
            }

            Debug.Log(
                $"Pot Updated: {Pot} | Player Chips Updated"
            );

            OnStateUpdated?.Invoke();
        }



        public void UpdateCommunityCards(List<string> cards)
        {
            CommunityCards = cards ?? new List<string>();

            Debug.Log($"[GameStateManager] Community Cards Updated: {CommunityCards.Count}");

            OnStateUpdated?.Invoke();
        }


        public void UpdatePotState(int pot, List<SidePots> sidePots)
        {
            Pot = pot;
            SidePots = sidePots ?? new List<SidePots>();

            Debug.Log($"[GameStateManager] Pot Updated → {Pot}");

            OnStateUpdated?.Invoke();
        }



        public void UpdateDealerState(int dealerSeat, int sbSeat, int bbSeat)
        {
            DealerSeat = dealerSeat;
            SmallBlindSeat = sbSeat;
            BigBlindSeat = bbSeat;

            Debug.Log(
                $"[GameStateManager] Dealer Updated → D:{DealerSeat}, SB:{SmallBlindSeat}, BB:{BigBlindSeat}"
            );

            OnStateUpdated?.Invoke();
        }


        public void RefreshPlayerSeats()
        {
            Debug.Log("[PlayerJoined] Refreshing player seats...");

           
               OnStateUpdated?.Invoke();
           
        }

        public void RemovePlayer(string playerId)
        {
            if (Players == null) return;

            Players.RemoveAll(p => p.Id == playerId);

            Debug.Log($"[GameStateManager] Player Removed → {playerId}");

            OnStateUpdated?.Invoke();
        }


        public int GetPlayerSeat(string playerId)
        {
            if (Players == null)
                return -1;

            var player = Players.Find(p => p.Id == playerId);

            if (player == null)
                return -1;

            return player.Seat;
        }


        public void SetPlayerSitOut(string playerId, bool isSittingOut)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("[GameStateManager] SitOut invalid playerId");
                return;
            }

            if (Players == null)
            {
                Debug.LogWarning("[GameStateManager] Players list null");
                return;
            }

            var player = Players.Find(p => p.Id == playerId);

            if (player == null)
            {
                Debug.LogWarning($"[GameStateManager] Player not found for SitOut: {playerId}");
                return;
            }

         //   if (player.IsSittingOut == isSittingOut)
              //  return; // no change

           // player.IsSittingOut = isSittingOut;

            Debug.Log(
                $"[GameStateManager] SitOut Updated → " +
                $"{player.Username} = {(isSittingOut ? "SIT OUT" : "ACTIVE")}"
            );

            OnStateUpdated?.Invoke();
        }

        public void Clear()
        {
            TableId = null;
            Players?.Clear();
            CommunityCards?.Clear();
            SidePots?.Clear();
        }



        public void ApplyRoundEndBalances(
           Dictionary<string, int> updatedChipBalances
)
        {
            if (updatedChipBalances == null || Players == null)
            {
                Debug.LogWarning("[GameStateManager] ApplyRoundEndBalances skipped");
                return;
            }

            foreach (var player in Players)
            {
                if (updatedChipBalances.ContainsKey(player.Id))
                {
                    player.Chips = updatedChipBalances[player.Id];
                }
            }

            Debug.Log("[GameStateManager] Final chip balances applied");

            OnStateUpdated?.Invoke();
        }

        public void ResetForNextRound(int roundNumber)
        {
            RoundNumber = roundNumber;

            Pot = 0;

            if (CommunityCards != null)
                CommunityCards.Clear();

            if (SidePots != null)
                SidePots.Clear();

            if (Players != null)
            {
                foreach (var player in Players)
                {
                    player.LastAction = string.Empty;
                }
            }

            Debug.Log(
                $"[GameStateManager] Ready for next round → Round {RoundNumber}"
            );

            OnStateUpdated?.Invoke();
        }
    }
}