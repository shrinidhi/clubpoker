using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class TableJoinHandler : MonoBehaviour
    {
        public static TableJoinHandler Instance { get; private set; }

        #region Events

        public static event Action<GameStateUpdatePayload> OnTableJoined;
        public static event Action<string> OnJoinFailed;
        public static event Action<string> OnStateSyncFailed;

        #endregion

        #region Constants

        private const float JOIN_TIMEOUT_SECONDS = 10f;

        private const string SCENE_GAME_TABLE = "Scene_GameTable";

        private const string EVENT_JOIN_TABLE = "player:join_table";
        private const string EVENT_STATE_UPDATE = "game:state_update";
        private const string EVENT_GAME_ERROR = "game:error";
        private const string EVENT_REQUEST_STATE = "player:request_state";

        // NEW
        private const string EVENT_YOUR_CARDS = "game:your_cards";
        private const string EVENT_COMMUNITY_CARDS = "game:community_cards";
        private const string EVENT_YOUR_TURN = "game:your_turn";
        private const string EVENT_TIMER_TICK = "game:timer_tick";
        private const string EVENT_TIMER_START = "game:timer_start";
        private const string EVENT_PLAYER_ACTED = "game:player_acted";
        private const string EVENT_GAME_ROUND_END = "game:round_end";
        private const string EVENT_GAME_POT_UPDATE = "game:pot_update";
        private const string EVENT_GAME_DEALER_MOVED = "game:dealer_moved";
        private const string EVENT_PLAYER_JOINED = "game:player_joined";
        private const string EVENT_PLAYER_LEFT = "game:player_left";
        private const string EVENT_PLAYER_DISCONNECTED = "game:player_disconnected";
        private const string EVENT_PLAYER_RECONNECTED = "game:player_reconnected";
        private const string EVENT_GAME_PAUSED = "game:game_paused";
        private const string EVENT_GAME_RESUMED = "game:game_resumed";
        private const string EVENT_PLAYER_SITTING_OUT = "game:player_sitting_out";
        private const string EVENT_PLAYER_CAME_BACK = "game:player_came_back";
        private const string EVENT_GAME_CHAT = "game:chat";
        private const string EVENT_TIME_BANK = "game:time_bank_activated";


        #endregion

        #region Private Fields

        private string _pendingTableId;
        private bool _waitingForConfirmation;
        private Coroutine _timeoutCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (SocketManager.Instance != null)
            {
                SocketManager.Instance.OnAuthenticated += OnSocketAuthenticated;
            }
            else
            {
                SocketManager.OnInstanceReady += OnSocketManagerReady;
            }
        }

        private void OnSocketManagerReady()
        {
            SocketManager.OnInstanceReady -= OnSocketManagerReady;

            if (SocketManager.Instance != null)
            {
                SocketManager.Instance.OnAuthenticated += OnSocketAuthenticated;
            }
        }

        private void OnDestroy()
        {
            if (SocketManager.Instance != null)
            {
                SocketManager.Instance.OnAuthenticated -= OnSocketAuthenticated;

                SocketManager.Instance.Off(EVENT_STATE_UPDATE);
                SocketManager.Instance.Off(EVENT_GAME_ERROR);
                SocketManager.Instance.Off(EVENT_YOUR_CARDS);
                SocketManager.Instance.Off(EVENT_COMMUNITY_CARDS);
                SocketManager.Instance.Off(EVENT_YOUR_TURN);
                SocketManager.Instance.Off(EVENT_TIMER_TICK);
                SocketManager.Instance.Off(EVENT_TIMER_START);
                SocketManager.Instance.Off(EVENT_PLAYER_ACTED);
                SocketManager.Instance.Off(EVENT_GAME_ROUND_END);
                SocketManager.Instance.Off(EVENT_GAME_POT_UPDATE);
                SocketManager.Instance.Off(EVENT_GAME_DEALER_MOVED);
                SocketManager.Instance.Off(EVENT_PLAYER_JOINED);
                SocketManager.Instance.Off(EVENT_PLAYER_LEFT);
                SocketManager.Instance.Off(EVENT_PLAYER_DISCONNECTED);
                SocketManager.Instance.Off(EVENT_PLAYER_RECONNECTED);
                SocketManager.Instance.Off(EVENT_GAME_PAUSED);
                SocketManager.Instance.Off(EVENT_GAME_RESUMED);
                SocketManager.Instance.Off(EVENT_PLAYER_SITTING_OUT);
                SocketManager.Instance.Off(EVENT_PLAYER_CAME_BACK);
                SocketManager.Instance.Off(EVENT_GAME_CHAT);
                SocketManager.Instance.Off(EVENT_TIME_BANK);
            }

            SocketManager.OnInstanceReady -= OnSocketManagerReady;
        }

        #endregion

        #region Public API

        public void JoinTable(string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
            {
                Debug.LogError("[TableJoinHandler] tableId is null");
                return;
            }

            if (_waitingForConfirmation)
            {
                Debug.LogWarning("[TableJoinHandler] Previous join cancelled");

                StopTimeoutCoroutine();
                _waitingForConfirmation = false;
            }

            _pendingTableId = tableId;

            Debug.Log($"[TableJoinHandler] Joining table: {tableId}");

            if (SocketManager.Instance.IsConnected)
            {
                EmitJoinTable(tableId);
            }
            else
            {
                Debug.Log("[TableJoinHandler] Waiting for socket authentication");
            }
        }

        /// <summary>
        /// Manual full state re-sync
        /// </summary>
        public void RequestState()
        {
            if (SocketManager.Instance == null)
            {
                Debug.LogError("[StateSync] SocketManager null");
                return;
            }

            if (!SocketManager.Instance.IsConnected)
            {
                Debug.LogError("[StateSync] Socket disconnected");
                OnStateSyncFailed?.Invoke("Socket disconnected");
                return;
            }

            string tableId = SocketManager.Instance.CurrentTableId;

            if (string.IsNullOrEmpty(tableId))
            {
                Debug.LogError("[StateSync] No current table");
                OnStateSyncFailed?.Invoke("No active table");
                return;
            }

            EmitRequestState(tableId);
        }

        #endregion

        #region Authentication

        private void OnSocketAuthenticated(SocketAuthenticatedPayload payload)
        {
            Debug.Log($"[TableJoinHandler] Authenticated: {payload.Username}");

            // subscribe once
            SocketManager.Instance.On(EVENT_STATE_UPDATE, OnStateUpdateReceived);
            SocketManager.Instance.On(EVENT_GAME_ERROR, OnGameErrorReceived);

            // NEW
            SocketManager.Instance.On(EVENT_YOUR_CARDS, OnYourCardsReceived);
            SocketManager.Instance.On(EVENT_COMMUNITY_CARDS,OnCommunityCardsReceived);
            SocketManager.Instance.On(EVENT_YOUR_TURN,OnYourTurnReceived);
            SocketManager.Instance.On( EVENT_TIMER_TICK, OnTimerTickReceived);
            SocketManager.Instance.On(EVENT_TIMER_START,OnTimerStartReceived);
            SocketManager.Instance.On(EVENT_PLAYER_ACTED,OnPlayerActedReceived);
            SocketManager.Instance.On(EVENT_GAME_ROUND_END, OnRoundEndReceived);
            SocketManager.Instance.On(EVENT_GAME_POT_UPDATE, OnPotUpdateReceived);
            SocketManager.Instance.On(EVENT_GAME_DEALER_MOVED, OnDealerMovedReceived);
            SocketManager.Instance.On(EVENT_PLAYER_JOINED, OnPlayerJoinedReceived);
            SocketManager.Instance.On(EVENT_PLAYER_LEFT, OnPlayerLeftReceived);
            SocketManager.Instance.On(EVENT_PLAYER_DISCONNECTED, OnPlayerDisconnectedReceived);
            SocketManager.Instance.On(EVENT_PLAYER_RECONNECTED, OnPlayerReconnectedReceived);
            SocketManager.Instance.On(EVENT_GAME_PAUSED, OnGamePausedReceived);
            SocketManager.Instance.On(EVENT_GAME_RESUMED, OnGameResumedReceived);
            SocketManager.Instance.On( EVENT_PLAYER_SITTING_OUT,OnPlayerSittingOutReceived);
            SocketManager.Instance.On( EVENT_PLAYER_CAME_BACK,OnPlayerCameBackReceived);
            SocketManager.Instance.On( EVENT_GAME_CHAT, OnGameChatReceived);
            SocketManager.Instance.On(EVENT_TIME_BANK, OnTimeBankActivated);
            if (string.IsNullOrEmpty(_pendingTableId))
                return;

            EmitJoinTable(_pendingTableId);
        }

        #endregion

        #region Join Table

        private void EmitJoinTable(string tableId)
        {
            if (_waitingForConfirmation)
            {
                Debug.LogWarning("[TableJoinHandler] Already waiting");
                return;
            }

            _waitingForConfirmation = true;

            var payload = new PlayerJoinTablePayload
            {
                TableId = tableId,
                PlayerId = GetCurrentPlayerId()
            };

            Debug.Log($"[TableJoinHandler] Emit join_table: {tableId}");

            SocketManager.Instance.Emit(EVENT_JOIN_TABLE, payload);

            StopTimeoutCoroutine();
            _timeoutCoroutine = StartCoroutine(JoinTimeoutCoroutine());
        }

        private void OnStateUpdateReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] state_update: {json}");

            try
            {
                var state =
                    JsonConvert.DeserializeObject<GameStateUpdatePayload>(json);

                if (state == null)
                {
                    Debug.LogError("[TableJoinHandler] state null");
                    return;
                }

                // FULL STATE APPLY
                GameStateManager.Instance.SetFullState(state);

                SocketManager.Instance.SetCurrentTable(state.TableId);

                if (_waitingForConfirmation)
                {
                    StopTimeoutCoroutine();
                    _waitingForConfirmation = false;

                    Debug.Log($"[TableJoinHandler] Join confirmed: {state.TableId}");

                    OnTableJoined?.Invoke(state);

                    GameSceneManager.Instance.LoadScene(SCENE_GAME_TABLE);

                    _pendingTableId = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"state_update parse failed: {e.Message}");
            }
        }

        private void OnGameErrorReceived(string json)
        {
            if (!_waitingForConfirmation)
                return;

            try
            {
                var error =
                    JsonConvert.DeserializeObject<GameErrorPayload>(json);

                HandleJoinFailure(
                    error?.Message ?? "Could not join table"
                );
            }
            catch
            {
                HandleJoinFailure("Could not join table");
            }
        }

        #endregion

        #region YOUR CARDS (PRIVATE)

        /// <summary>
        /// SERVER → CLIENT
        /// game:your_cards
        ///
        /// texas_holdem = 2
        /// plo4 = 4
        /// plo6 = 6
        /// </summary>
        private void OnYourCardsReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] your_cards: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<YourCardsPayload>(json);

                if (payload == null || payload.Cards == null)
                {
                    Debug.LogError("[YourCards] Invalid payload");
                    return;
                }

                if (!ValidateCardCount(payload.Variant, payload.Cards.Count))
                {
                    Debug.LogError(
                        $"[YourCards] Invalid card count. Variant: {payload.Variant}, Count: {payload.Cards.Count}"
                    );
                    return;
                }

                // Store cards only for local player
                GameStateManager.Instance.SetYourCards(
                    payload.Cards,
                    payload.Variant
                );

                if (PlayerCardHandUI.Instance != null)
                {
                    PlayerCardHandUI.Instance.PlayDealAnimation(payload.Cards);
                }
                else
                {
                    Debug.LogWarning("[YourCards] PlayerCardHandUI not found");
                }

                // encrypted save for reconnect restore
                SaveEncryptedCards(payload);

             

                Debug.Log(
                    $"[YourCards] Applied successfully ({payload.Cards.Count} cards)"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[YourCards] Parse failed: {e.Message}");
            }
        }

        private bool ValidateCardCount(string variant, int count)
        {
            switch (variant)
            {
                case "texas_holdem":
                    return count == 2;

                case "plo4":
                    return count == 4;

                case "plo6":
                    return count == 6;

                default:
                    Debug.LogWarning($"Unknown variant: {variant}");
                    return false;
            }
        }

        private void SaveEncryptedCards(YourCardsPayload payload)
        {
            string json = JsonConvert.SerializeObject(payload);

            // basic example encryption
            string encrypted =
                Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(json)
                );

            PlayerPrefs.SetString("PLAYER_PRIVATE_CARDS", encrypted);
            PlayerPrefs.Save();

            Debug.Log("[YourCards] Encrypted cards saved");
        }


        #endregion

        #region COMMUNITY CARD

        private void OnCommunityCardsReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] community_cards received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<CommunityCardsPayload>(json);

                if (payload == null || payload.Cards == null)
                {
                    Debug.LogError("Community cards payload null");
                    return;
                }

                GameStateManager.Instance.AppendCommunityCards(
                 payload.Cards,
                payload.Street
                );

                CommunityCardsUI.Instance.ShowCommunityCards(
                    payload.Cards,
                    payload.Street
                );


                /*  CommunityCardsUI.Instance.ShowCommunityCards(
                      payload.Cards,
                      payload.Street
                  );

                 BestHandCalculator.Instance.Recalculate();

                  SoundManager.Instance.PlayCardFlip();*/

                Debug.Log(
                    $"Community cards updated | Street: {payload.Street}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"Community cards parse failed: {e.Message}"
                );
            }
        }

        #endregion

        #region YOUR TURN


        private void OnYourTurnReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] your_turn received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<YourTurnPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("your_turn payload null");
                    return;
                }

                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.StartYourTurn(payload);
                }

                
                Debug.Log(
                    $"Your turn started | ValidActions: " +
                    string.Join(", ", payload.ValidActions)
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"your_turn parse failed: {e.Message}"
                );
            }
        }

        #endregion

        #region   TIMER TICK
     
        private void OnTimerTickReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] timer_tick received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<TimerTickPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("timer_tick payload null");
                    return;
                }

                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.ApplyTimerTick(
                    payload.RemainingMs,
                    payload.ServerTime
                     );
                }

                Debug.Log(
                    $"Timer Sync | Remaining: {payload.RemainingMs}ms"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"timer_tick parse failed: {e.Message}"
                );
            }
        }
        #endregion

        #region TIMER START

        private void OnTimerStartReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] timer_start received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<TimerStartPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("timer_start payload null");
                    return;
                }

                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.StartPlayerTimer(
                        payload.PlayerId,
                        payload.DurationMs,
                        payload.ServerTime
                    );
                }

                Debug.Log(
                    $"Timer Started | Player: {payload.PlayerId} | Duration: {payload.DurationMs}ms"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"timer_start parse failed: {e.Message}"
                );
            }
        }

        #endregion

        #region PLAYER ACTED

        private void OnPlayerActedReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] player_acted received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<PlayerActedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[PlayerActed] payload null");
                    return;
                }

                // 1. Game state update
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.ApplyPlayerAction(payload);
                }

                // 2. UI + Animation
                if (PlayerActionUI.Instance != null)
                {
                    PlayerActionUI.Instance.HandlePlayerAction(payload);
                }
                else
                {
                    Debug.LogWarning("[PlayerActed] PlayerActionUI not found");
                }

                Debug.Log(
                    $"[PlayerActed] {payload.Username} -> {payload.Action} | " +
                    $"Amount: {payload.Amount} | Pot: {payload.Pot}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[PlayerActed] parse failed: {e.Message}"
                );
            }
        }

        #endregion

        #region  GAME ROUND END

        private void OnRoundEndReceived(string json)
        {
            Debug.Log($"[RoundEnd] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<RoundEndPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[RoundEnd] Payload NULL");
                    return;
                }

                //------------------------------------------------------
                // STEP 1 : Final board sync
                //------------------------------------------------------
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.UpdateCommunityCards(
                        payload.communityCards
                    );
                }

                //------------------------------------------------------
                // STEP 2 : Pot → Winner animation
                //------------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    if (payload.updatedChipBalances != null &&
                        payload.updatedChipBalances.Count > 1)
                    {
                        PokerTableUI.Instance.AnimateSplitPotToWinners(
                            payload.updatedChipBalances,
                            payload.potWon
                        );
                    }
                    else
                    {
                        PokerTableUI.Instance.AnimatePotToWinner(
                            payload.winner.id,
                            payload.potWon
                        );
                    }
                }

                //------------------------------------------------------
                // STEP 3 : Showdown reveal
                //------------------------------------------------------
                if (payload.showdown)
                {
                    if (PokerTableUI.Instance != null)
                    {
                        if (payload.winner != null &&
                            payload.winner.holeCards != null &&
                            payload.winner.holeCards.Count > 0)
                        {
                            PokerTableUI.Instance.RevealPlayerCards(
                                payload.winner.id,
                                payload.winner.holeCards
                            );
                        }

                        if (payload.hand != null)
                        {
                            PokerTableUI.Instance.ShowHandRank(
                                payload.winner.id,
                                payload.hand.name
                            );
                        }
                    }
                }

                //------------------------------------------------------
                // STEP 4 : Final chip sync
                //------------------------------------------------------
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.ApplyRoundEndBalances(
                        payload.updatedChipBalances
                    );
                }

                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.UpdateAllPlayerChips(
                        payload.updatedChipBalances
                    );
                }

                //------------------------------------------------------
                // STEP 5 : Clear action labels
                //------------------------------------------------------
                if (PlayerActionUI.Instance != null)
                {
                    PlayerActionUI.Instance.ClearAllActionLabels();
                }

                //------------------------------------------------------
                // STEP 6 : Prepare next round
                //------------------------------------------------------
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.ResetForNextRound(
                        payload.roundNumber
                    );
                }

                Debug.Log(
                    $"[RoundEnd] Completed → Winner: " +
                    $"{payload.winner.username}, Pot: {payload.potWon}"
                );
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    $"[RoundEnd] Parse Failed: {e.Message}"
                );
            }
        }

        #endregion

        #region DEALER MOVED

        private void OnDealerMovedReceived(string json)
        {
            Debug.Log($"[DealerMoved] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<DealerMovedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[DealerMoved] Payload NULL");
                    return;
                }

                //------------------------------------------------------
                // STEP 1 : Game State Sync
                //------------------------------------------------------
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.UpdateDealerState(
                        payload.dealerSeat,
                        payload.smallBlindSeat,
                        payload.bigBlindSeat
                    );
                }

                //------------------------------------------------------
                // STEP 2 : Dealer Button Animation
                //------------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    // Animate dealer token to new seat
                    PokerTableUI.Instance.MoveDealerButton(
                        payload.dealerSeat
                    );

                    //--------------------------------------------------
                    // STEP 3 : Small Blind + Big Blind Indicators
                    //--------------------------------------------------
                    PokerTableUI.Instance.UpdateBlindIndicators(
                        payload.smallBlindSeat,
                        payload.bigBlindSeat
                    );

                    //--------------------------------------------------
                    // STEP 4 : Heads-Up Special Handling
                    // dealer = SB in heads-up
                    //--------------------------------------------------
                    PokerTableUI.Instance.HandlePreFlopFirstActor(
                        payload.preFlopFirstActorSeat
                    );
                }

                //------------------------------------------------------
                // STEP 5 : Debug Logs
                //------------------------------------------------------
                Debug.Log(
                    $"[DealerMoved] Completed | " +
                    $"Dealer: {payload.dealerSeat} | " +
                    $"SB: {payload.smallBlindSeat} | " +
                    $"BB: {payload.bigBlindSeat} | " +
                    $"First Actor: {payload.preFlopFirstActorSeat}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[DealerMoved] Parse Failed: {e.Message}"
                );
            }
        }

        #endregion

        #region POT UPDATE

        private void OnPotUpdateReceived(string json)
        {
            Debug.Log($"[PotUpdate] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<PotUpdatePayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[PotUpdate] Payload NULL");
                    return;
                }

                //------------------------------------------------------
                // STEP 1 : Main Pot + Side Pot GameState Sync
                //------------------------------------------------------
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.UpdatePotState(
                        payload.pot,
                        payload.sidePots
                    );
                }

                //------------------------------------------------------
                // STEP 2 : Real-time Chip Movement Animation
                // Player bet positions → Center Pot
                //------------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.AnimateChipsToPot();

                    //--------------------------------------------------
                    // STEP 3 : Main Pot Total Display
                    //--------------------------------------------------
                    PokerTableUI.Instance.UpdateMainPot(
                        payload.pot
                    );

                    //--------------------------------------------------
                    // STEP 4 : Side Pots Display Separately
                    // Example:
                    // Side Pot 1 → 300
                    // Side Pot 2 → 40
                    //--------------------------------------------------
                    if (payload.sidePots != null &&
                        payload.sidePots.Count > 0)
                    {
                        PokerTableUI.Instance.ShowSidePots(
                            payload.sidePots
                        );
                    }
                    else
                    {
                        PokerTableUI.Instance.HideSidePots();
                    }

                    //--------------------------------------------------
                    // STEP 5 : Rake Amount Display
                    // Show only when applicable
                    //--------------------------------------------------
                   /* if (payload.rake > 0)
                    {
                       PokerTableUI.Instance.ShowRake(
                            payload.rake
                        );
                    }
                    else
                    {
                        PokerTableUI.Instance.HideRake();
                    }*/
                }

                //------------------------------------------------------
                // STEP 6 : Debug Logs
                //------------------------------------------------------
                Debug.Log(
                    $"[PotUpdate] Completed | " +
                    $"Main Pot: {payload.pot} | " +
                    $"Side Pots: {(payload.sidePots != null ? payload.sidePots.Count : 0)} | "// +
                //    $"Rake: {payload.rake}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[PotUpdate] Parse Failed: {e.Message}"
                );
            }
        }

        #endregion

        #region PLAYER JOINED

        private void OnPlayerJoinedReceived(string json)
        {
            Debug.Log($"[PlayerJoined] Received: {json}");

            try
            {
                //--------------------------------------------------
                // JSON Parse
                //--------------------------------------------------
                var payload =
                    JsonConvert.DeserializeObject<PlayerJoinedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[PlayerJoined] Payload NULL");
                    return;
                }

                //--------------------------------------------------
                // STEP 1 : Validate Data
                //--------------------------------------------------
                if (payload.player == null)
                {
                    Debug.LogError("[PlayerJoined] player object NULL");
                    return;
                }

                if (payload.seat < 0)
                {
                    Debug.LogError(
                        $"[PlayerJoined] Invalid Seat: {payload.seat}"
                    );
                    return;
                }

                //--------------------------------------------------
                // STEP 2 : Update Game State
                //--------------------------------------------------
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.RefreshPlayerSeats();
                }

                //--------------------------------------------------
                // STEP 3 : UI Animation
                //--------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    // New player panel animation
                    PokerTableUI.Instance.ShowPlayerJoinAnimation(
                        payload.seat
                    );

                    // Total player count update
                    PokerTableUI.Instance.UpdatePlayerCount();

                    // Empty seat available indicator refresh
                    PokerTableUI.Instance.RefreshSeatAvailability();
                }

                //--------------------------------------------------
                // STEP 4 : Debug
                //--------------------------------------------------
                Debug.Log(
                    $"[PlayerJoined] Completed → " +
                    $"Player: {payload.player.username} | " +
                    $"Seat: {payload.seat} | " +
                    $"Chips: {payload.player.chips}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[PlayerJoined] Parse Failed: {e.Message}"
                );
            }
        }

        #endregion

        #region PLAYER LEFT

        private void OnPlayerLeftReceived(string json)
        {
            Debug.Log($"[PlayerLeft] Received: {json}");

            try
            {
                //--------------------------------------------------
                // JSON Parse
                //--------------------------------------------------
                var payload =
                    JsonConvert.DeserializeObject<PlayerLeftPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[PlayerLeft] Payload NULL");
                    return;
                }

                //--------------------------------------------------
                // STEP 1 : Find Seat Before Remove
                //--------------------------------------------------
                int seat = -1;

                if (GameStateManager.Instance != null)
                {
                    // first find player's seat
                    seat = GameStateManager.Instance.GetPlayerSeat(
                        payload.playerId
                    );

                    // remove player from state
                    GameStateManager.Instance.RemovePlayer(
                        payload.playerId
                    );
                }

                //--------------------------------------------------
                // STEP 2 : UI Leave Animation
                //--------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    if (seat >= 0)
                    {
                        // animate player panel leaving
                        PokerTableUI.Instance.ShowPlayerLeaveAnimation(
                            seat
                        );
                    }

                    // total player count update
                    PokerTableUI.Instance.UpdatePlayerCount();

                    // empty seat available indicator
                    PokerTableUI.Instance.RefreshSeatAvailability();
                }

                //--------------------------------------------------
                // STEP 3 : Debug
                //--------------------------------------------------
                Debug.Log(
                    $"[PlayerLeft] Completed → " +
                    $"Player: {payload.username} | " +
                    $"Seat: {seat} | " +
                    $"Chips Returned: {payload.chipsReturned}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[PlayerLeft] Parse Failed: {e.Message}"
                );
            }
        }

        #endregion

        #region PLAYER DISCONNECTED

        private void OnPlayerDisconnectedReceived(string json)
        {
            Debug.Log($"[PlayerDisconnected] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<PlayerDisconnectedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[PlayerDisconnected] Payload NULL");
                    return;
                }

                int seat = -1;

                if (GameStateManager.Instance != null)
                {
                    seat = GameStateManager.Instance.GetPlayerSeat(
                        payload.playerId
                    );
                }

                if (PokerTableUI.Instance != null && seat >= 0)
                {
                    PokerTableUI.Instance.ShowDisconnectedIndicator(
                        seat,
                        payload.gracePeriodSeconds
                    );
                }

                Debug.Log(
                    $"[PlayerDisconnected] Completed → " +
                    $"{payload.username} | Seat: {seat} | " +
                    $"Grace: {payload.gracePeriodSeconds}s"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[PlayerDisconnected] Parse Failed: {e.Message}"
                );
            }
        }

        #endregion


        #region PLAYER RECONNECTED

        private void OnPlayerReconnectedReceived(string json)
        {
            Debug.Log($"[PlayerReconnected] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<PlayerReconnectedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[PlayerReconnected] Payload NULL");
                    return;
                }

                int seat = -1;

                if (GameStateManager.Instance != null)
                {
                    seat = GameStateManager.Instance.GetPlayerSeat(
                        payload.playerId
                    );
                }

                if (PokerTableUI.Instance != null && seat >= 0)
                {
                    PokerTableUI.Instance.HideDisconnectedIndicator(
                        seat
                    );
                }

                Debug.Log(
                    $"[PlayerReconnected] Completed → " +
                    $"{payload.username} | Seat: {seat}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[PlayerReconnected] Parse Failed: {e.Message}"
                );
            }
        }

        #endregion

        #region GAME PAUSE

        private void OnGamePausedReceived(string json)
        {
            Debug.Log($"[GamePaused] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<GamePausedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[GamePaused] Payload NULL");
                    return;
                }

                //--------------------------------------------------
                // STEP 1 : Disable player actions
                //--------------------------------------------------
                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.DisableAllActions();
                }

                //--------------------------------------------------
                // STEP 2 : Show Pause Overlay
                //--------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.ShowPauseOverlay(
                        payload.reason,
                        payload.countdownSeconds
                    );
                }

                Debug.Log(
                    $"[GamePaused] Reason: {payload.reason} | Countdown: {payload.countdownSeconds}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[GamePaused] Parse Failed: {e.Message}");
            }
        }

        #endregion


        #region GAME RESUME

        private void OnGameResumedReceived(string json)
        {
            Debug.Log($"[GameResumed] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<GameResumedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[GameResumed] Payload NULL");
                    return;
                }

                //--------------------------------------------------
                // STEP 1 : Enable player actions
                //--------------------------------------------------
                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.EnableAllActions();
                }

                //--------------------------------------------------
                // STEP 2 : Hide Pause Overlay
                //--------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.HidePauseOverlay();
                }

                Debug.Log(
                    $"[GameResumed] Player Count: {payload.playerCount}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameResumed] Parse Failed: {e.Message}");
            }
        }

        #endregion


        #region  TIME BANK
        private void OnTimeBankActivated(string json)
        {
            Debug.Log($"[TimeBank] server confirm: {json}");

            var ui = FindObjectOfType<TimeBankButtonHandler>();
            if (ui != null)
            {
                ui.OnTimeBankConfirmed();
            }
        }

        #endregion

        #region PLAYER SITTING OUT

        private void OnPlayerSittingOutReceived(string json)
        {
            Debug.Log($"[SittingOut] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<PlayerSittingOut_CameBackPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[SittingOut] Payload NULL");
                    return;
                }

                int seat = -1;

                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.SetPlayerSitOut(payload.playerId, true);

                    seat = GameStateManager.Instance.GetPlayerSeat(
                        payload.playerId
                    );
                }

                if (PokerTableUI.Instance != null && seat >= 0)
                {
                    PokerTableUI.Instance.ShowSittingOutState(
                        seat
                    );
                }

                Debug.Log(
                    $"[SittingOut] Completed → {payload.username} | Seat: {seat}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[SittingOut] Parse Error: {e.Message}");
            }
        }

        #endregion


        #region PLAYER CAME BACK

        private void OnPlayerCameBackReceived(string json)
        {
            Debug.Log($"[CameBack] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<PlayerSittingOut_CameBackPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[CameBack] Payload NULL");
                    return;
                }

                int seat = -1;

                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.SetPlayerSitOut(payload.playerId, false);

                    seat = GameStateManager.Instance.GetPlayerSeat(
                        payload.playerId
                    );
                }

                if (PokerTableUI.Instance != null && seat >= 0)
                {
                    PokerTableUI.Instance.HideSittingOutState(
                        seat
                    );
                }

                Debug.Log(
                    $"[CameBack] Completed → {payload.username} | Seat: {seat}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[CameBack] Parse Error: {e.Message}");
            }
        }

        #endregion

        #region GAME CHAT

        private void OnGameChatReceived(string json)
        {
            Debug.Log($"[GameChat] Received: {json}");

            try
            {
                var payload =
                    JsonConvert.DeserializeObject<GameChatPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[GameChat] Payload NULL");
                    return;
                }

                if (ChatHandler.Instance != null)
                {
                    ChatHandler.Instance.AppendChatMessage(payload);
                }

                Debug.Log(
                    $"[GameChat] {payload.username}: {payload.text}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameChat] Parse Error: {e.Message}");
            }
        }

        #endregion

        #region Request State

        private void EmitRequestState(string tableId)
        {
            var payload = new RequestStatePayload
            {
                TableId = tableId
            };

            Debug.Log($"[StateSync] Request state: {tableId}");

            SocketManager.Instance.Emit(EVENT_REQUEST_STATE, payload);
        }

        #endregion



        //   CLINT TO SERVER

        #region PLAYER ACTION

        public void Fold() => SendPlayerAction("fold");

        public void Check() => SendPlayerAction("check");

        public void Call() => SendPlayerAction("call");

        public void AllIn() => SendPlayerAction("all_in");

        public void Raise(int amount)
        {
            SendPlayerAction("raise", amount);
        }


        private void SendPlayerAction(string type, int amount = 0)
        {
            if (!SocketManager.Instance.IsConnected)
                return;

            var payload = new Dictionary<string, object>
         {
          { "tableId", SocketManager.Instance.CurrentTableId },
          { "type", type }
          };

            if (type == "raise")
            {
                payload.Add("amount", amount);
            }

            SocketManager.Instance.Emit("player:action", payload);

            Debug.Log($"[Action] {type} emitted");
        }


        #endregion





        #region Timeout

        private IEnumerator JoinTimeoutCoroutine()
        {
            yield return new WaitForSeconds(JOIN_TIMEOUT_SECONDS);

            if (_waitingForConfirmation)
            {
                HandleJoinFailure(
                    "Could not connect to the table. Please try again."
                );
            }
        }

        private void StopTimeoutCoroutine()
        {
            if (_timeoutCoroutine == null)
                return;

            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        #endregion

        #region Failure

        private void HandleJoinFailure(string message)
        {
            StopTimeoutCoroutine();

            _waitingForConfirmation = false;
            _pendingTableId = null;

            SocketManager.Instance.ClearCurrentTable();

            OnJoinFailed?.Invoke(message);

            Debug.LogWarning($"[TableJoinHandler] Failed: {message}");
        }

        #endregion

        #region Helpers

        private string GetCurrentPlayerId()
        {
            var mgr = Auth.AuthManager.Instance;

            return mgr != null
                ? mgr.Session.Id ?? string.Empty
                : string.Empty;
        }

        #endregion
    }
}