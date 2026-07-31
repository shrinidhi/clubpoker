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
        public GameStateUpdatePayload CurrentState { get; private set; }

        private readonly Dictionary<string, bool> sittingOutPlayers = new Dictionary<string, bool>();
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
            CurrentState = state;
            TableId = state.TableId;
            // Keep Variant in sync with the current table. Without this it only ever
            // reflects the last game:your_cards, so switching format (e.g. PLO4 → PLO6)
            // left the stale variant and the wrong rules tooltip was shown.
            if (!string.IsNullOrEmpty(state.Variant))
                Variant = state.Variant;
            GameState = state.GameState;
            RoundNumber = state.RoundNumber;
            Pot = state.Pot;
            SidePots = state.SidePots ?? new List<SidePots>();
            CommunityCards = state.CommunityCards ?? new List<string>();
            DealerSeat = state.DealerSeat ?? -1;
            CurrentTurnPlayerId = state.CurrentTurnPlayerId;
            Players = state.Players ?? new List<GamePlayer>();

            // state_update is the authority for sit-out. The player_sitting_out /
            // player_came_back broadcasts can be missed (that's the whole point of
            // the disconnect flow), so re-derive the flags from the snapshot —
            // otherwise a stale local flag auto-folds a player who is back in play.
            SyncSitOutFromState();

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


        public void AppendCommunityCards(List<string> newCards, string street
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


        public GamePlayer ApplyPlayerAction(PlayerActedPayload payload)
        {
            if (payload == null || Players == null)
                return null;

            if (payload.Pot >= 0)
                Pot = payload.Pot;

            GamePlayer updatedPlayer = null;

            foreach (var player in Players)
            {
                if (player.Id == payload.PlayerId)
                {
                    player.LastAction = payload.Action;

                    updatedPlayer = player;
                    break;
                }
            }

            OnStateUpdated?.Invoke();
            return updatedPlayer;
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


        

        public void Clear()
        {
            TableId = null;
            Variant = null;   // reset so a stale variant can't leak into the next table
            Players?.Clear();
            CommunityCards?.Clear();
            SidePots?.Clear();

            // Stale sit-out flags on a new table auto-fold the player every turn
            // and grey out their seat — must not leak across tables.
            sittingOutPlayers.Clear();
            pendingSitOut.Clear();
        }


        public GamePlayer GetPlayerById(string playerId)
        {
            if (Players == null)
                return null;

            return Players.Find(p => p.Id == playerId);
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
            // round_end carries the round that just FINISHED. Assigning it directly
            // rolled RoundNumber backwards whenever a state_update for the next hand
            // had already arrived, so the UI showed the previous round until the
            // following snapshot. state_update is the authority — only ever advance.
            if (roundNumber > RoundNumber)
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



        public bool IsPlayerSittingOut(string playerId)
        {
            return sittingOutPlayers.TryGetValue(playerId, out bool value) && value;
        }

        // Sit-outs the server has told us about but hasn't applied to its own
        // snapshot yet. A voluntary sit-out takes effect from the NEXT hand, so
        // state_update keeps reporting sittingOut:false for the rest of the current
        // one — clearing the flag from that snapshot would drop the sit-out.
        private readonly HashSet<string> pendingSitOut = new HashSet<string>();

        public void SetPlayerSitOut(string playerId, bool isSittingOut)
        {
            if (string.IsNullOrEmpty(playerId))
                return;

            sittingOutPlayers[playerId] = isSittingOut;

            if (isSittingOut)
                pendingSitOut.Add(playerId);
            else
                pendingSitOut.Remove(playerId);

            // Keep the seat model in step so a re-render (which binds from Players)
            // doesn't immediately undo the sit-out.
            var player = GetPlayerById(playerId);
            if (player != null)
                player.SittingOut = isSittingOut;

            OnStateUpdated?.Invoke();
        }

        private void SyncSitOutFromState()
        {
            foreach (var player in Players)
            {
                // Server says sitting out — authoritative, and the pending flag has
                // served its purpose.
                if (player.SittingOut)
                {
                    sittingOutPlayers[player.Id] = true;
                    pendingSitOut.Remove(player.Id);
                    continue;
                }

                // Server says active, but we're still waiting for a sit-out it hasn't
                // applied yet — hold the flag rather than flapping it back on next hand.
                if (pendingSitOut.Contains(player.Id))
                {
                    sittingOutPlayers[player.Id] = true;
                    player.SittingOut = true;
                    continue;
                }

                sittingOutPlayers[player.Id] = false;
            }
        }

        /// <summary>
        /// Mark the seat model locally on the player_disconnected broadcast, which
        /// lands before the state_update carrying disconnected:true. Without it a
        /// state_update in between re-binds the seat as connected and wipes the
        /// reconnect countdown.
        /// </summary>
        public void SetPlayerDisconnected(string playerId, bool disconnected)
        {
            var target = GetPlayerById(playerId);
            if (target != null)
                target.Disconnected = disconnected;
        }

        public bool IsPlayerDisconnected(string playerId)
        {
            var player = GetPlayerById(playerId);
            return player != null && player.Disconnected;
        }

        /// <summary>
        /// Hands left before the server removes a sitting-out player for inactivity.
        /// -1 when the player isn't on the sit-out countdown.
        /// </summary>
        public int GetSitOutHandsRemaining(string playerId)
        {
            var player = GetPlayerById(playerId);
            return player?.SitOutHandsRemaining ?? -1;
        }

        /// <summary>
        /// Heads-up = exactly two players seated. The disconnect grace period is
        /// shorter here because there is nobody else to keep the hand moving.
        /// </summary>
        public bool IsHeadsUp => Players != null && Players.Count == 2;

        public int SeatedPlayerCount => Players != null ? Players.Count : 0;
    }
}