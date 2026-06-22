using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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
        private int lastRoundNumber = -1;


        #region Events

        public static event Action<GameStateUpdatePayload> OnTableJoined;
        public static event Action<string> OnJoinFailed;
        public static event Action<string> OnStateSyncFailed;

        #endregion

        #region Constants

        private const float JOIN_TIMEOUT_SECONDS = 10f;

        private const string SCENE_GAME_TABLE = "Scene_GameTable";
        private const string SCENE_MAIN_MENU = "Scene_MainMenu";

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
        private const string EVENT_SEAT_AVAILABLE = "table:seat_available";
        private const string EVENT_WAITING_LIST_UPDATED = "table:waiting_list_updated";
        private const string EVENT_LEAVE_TABLE = "player:leave_table";


        #endregion

        #region Private Fields

        public string _pendingTableId;
        private bool _pendingIsSpectator;

        // Watch & Wait (Path B): the table we're spectating and the buy-in to use
        // when a seat frees (table:seat_available).
        private string _watchWaitTableId;
        private int _watchWaitBuyIn;

        // True while converting spectator → seated in the GameTable scene, so the
        // join's confirming state_update renders in place instead of reloading.
        private bool _convertingInPlace;

        // Stand Up  set when the player chooses to stand up mid-hand —
        // auto-fold every turn, then leave at round_end and become a spectator.
        public bool IsStoodUp { get; private set; }

        // Current role at the table: true = watching (observer), false = seated player.
        public bool IsSpectator { get; private set; }
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
                SocketManager.Instance.Off(EVENT_SEAT_AVAILABLE);
                SocketManager.Instance.Off(EVENT_WAITING_LIST_UPDATED);
            }

            SocketManager.OnInstanceReady -= OnSocketManagerReady;
        }

        #endregion

        #region Public API

        public void JoinTable(string tableId, bool isSpectator = false)
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
            _pendingIsSpectator = isSpectator;

            Debug.Log($"[TableJoinHandler] Joining table: {tableId} (spectator: {isSpectator})");

            if (SocketManager.Instance.IsConnected)
            {
                EmitJoinTable(tableId);
            }
            else if (!SocketManager.Instance.IsReconnecting)
            {
                // Socket was intentionally disconnected (e.g. after game over) — reconnect now
                Debug.Log("[TableJoinHandler] Socket disconnected — reconnecting before join");
                string token = Networking.ApiClient.Instance != null ? Networking.ApiClient.Instance.AccessToken : null;
                if (!string.IsNullOrEmpty(token))
                    SocketManager.Instance.Connect(token);
                // EmitJoinTable fires from OnSocketAuthenticated once connected
            }
            else
            {
                Debug.Log("[TableJoinHandler] Waiting for socket authentication");
            }
        }

        // ── Watch & Wait (Path B) ────────────────────────────────────────────

        /// <summary>
        /// Enter a table as a spectator and wait for a seat. Remembers the buy-in
        /// to use when table:seat_available fires, then seats the player.
        /// </summary>
        public void BeginWatchAndWait(string tableId, int buyIn)
        {
            _watchWaitTableId = tableId;
            _watchWaitBuyIn = buyIn;

            JoinTable(tableId, isSpectator: true);
        }

        // ── Stand Up  ─────────────────────────────────────────────

        /// <summary>
        /// Stand up from the table. Between hands → leave immediately. Mid-hand →
        /// fold now (if it's our turn), auto-fold the rest, then leave at round_end.
        /// </summary>
        public void RequestStandUp()
        {
            // Can't stand up if we're already watching or already standing up.
            if (IsSpectator || IsStoodUp)
                return;

            string gs = GameStateManager.Instance != null ? GameStateManager.Instance.GameState : null;
            bool handInProgress = gs == "PRE_FLOP" || gs == "FLOP" || gs == "TURN" || gs == "RIVER";

            if (handInProgress)
            {
                IsStoodUp = true;
                ToastEvents.Show("You will stand up after this hand.");

                // Fold now only if the server state says it's actually our turn
                // (TurnManager.IsMyTurn can be stale → emits a fold the server rejects).
                string myId = Auth.AuthManager.Instance.Session.Id;
                bool myTurn = GameStateManager.Instance != null &&
                              GameStateManager.Instance.CurrentTurnPlayerId == myId;

                if (myTurn)
                    Fold();

                // Hide the action buttons — we auto-fold from here, no manual play.
                if (TurnManager.Instance != null)
                    TurnManager.Instance.DisableAllActions();
            }
            else
            {
                ExecuteStandUp().Forget();
            }
        }

        // Emit leave, call POST /leave, show chips toast. If a game can still run for
        // the others, watch as a spectator; otherwise leave the table entirely.
        private async UniTaskVoid ExecuteStandUp()
        {
            string tableId = SocketManager.Instance != null ? SocketManager.Instance.CurrentTableId : null;
            int chips = GetMyTableChips();

            // After we leave, can the others keep playing (need ≥2)? If not, there's
            // nothing to spectate → leave the table fully.
            bool worthSpectating = CountOtherPlayers() >= 2;

            IsStoodUp = false;

            if (string.IsNullOrEmpty(tableId))
                return;

            if (SocketManager.Instance.IsConnected)
                SocketManager.Instance.Emit(EVENT_LEAVE_TABLE,
                    new Dictionary<string, object> { { "tableId", tableId } });

            try
            {
                await Auth.AuthManager.Instance.LeaveTableAsync(tableId);
                ToastEvents.Show($"Chips returned to wallet: {chips}");
            }
            catch (Exception e)
            {
                Debug.LogError("[StandUp] /leave failed: " + e.Message);
            }

            if (worthSpectating)
            {
                // Close the player socket so the re-join handshakes fresh as an observer
                // (same as the spectator→seat conversion). beJoinAsSpectator → watch.
                if (SocketManager.Instance != null && SocketManager.Instance.IsConnected)
                    SocketManager.Instance.Disconnect();

                _convertingInPlace = true;
                JoinTable(tableId, isSpectator: true);
            }
            else
            {
                // No live game to watch → leave the table entirely.
                Debug.Log("[StandUp] No players to spectate — leaving table.");
                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.Clear();

                if (SocketManager.Instance != null)
                {
                    SocketManager.Instance.ClearCurrentTable();
                    if (SocketManager.Instance.IsConnected)
                        SocketManager.Instance.Disconnect();
                }

                if (GameSceneManager.Instance != null)
                    GameSceneManager.Instance.LoadScene(SCENE_MAIN_MENU);
            }
        }

        private int CountOtherPlayers()
        {
            if (GameStateManager.Instance == null || GameStateManager.Instance.Players == null)
                return 0;

            string myId = Auth.AuthManager.Instance.Session.Id;
            int count = 0;
            foreach (var p in GameStateManager.Instance.Players)
                if (p.Id != myId) count++;
            return count;
        }

        private int GetMyTableChips()
        {
            string myId = Auth.AuthManager.Instance.Session.Id;
            var me = GameStateManager.Instance != null ? GameStateManager.Instance.GetPlayerById(myId) : null;
            return me?.Chips ?? 0;
        }

        private void OnSeatAvailableReceived(string json)
        {
            Debug.Log("[TableJoinHandler] table:seat_available ← " + json);

            try
            {
                var payload = JsonConvert.DeserializeObject<SeatAvailablePayload>(json);

                // Only act if we're waiting on this table.
                if (payload == null || payload.TableId != _watchWaitTableId)
                    return;

                if (!string.IsNullOrEmpty(payload.Message))
                    ToastEvents.Show(payload.Message);

                ConvertSpectatorToSeated().Forget();
            }
            catch (Exception e)
            {
                Debug.LogError("[TableJoinHandler] seat_available parse failed: " + e.Message);
            }
        }

        // TODO  drive the spectator waiting-list badge / queue position.
        private void OnWaitingListUpdatedReceived(string json)
        {
            Debug.Log("[TableJoinHandler] table:waiting_list_updated ← " + json);
        }

        // A seat opened → run Path A (buy-in + join) to convert spectator → player.
        private async UniTaskVoid ConvertSpectatorToSeated()
        {
            string tableId = _watchWaitTableId;
            int buyIn = _watchWaitBuyIn;

            // Clear first so a duplicate seat_available can't double-convert.
            _watchWaitTableId = null;

            try
            {
                await Auth.AuthManager.Instance.BuyInAsync(tableId, buyIn);

                try
                {
                    await Auth.AuthManager.Instance.JoinTableAsync(tableId, buyIn);
                }
                catch (Exception e)
                {
                    if (!e.Message.Contains("Already seated"))
                        throw;
                }

                // The socket was authenticated as a spectator — close it so the
                // re-join handshakes fresh as a seated player. JoinTable sees the
                // socket down and reconnects, then emits join_table (isSpectator:false).
                if (SocketManager.Instance != null && SocketManager.Instance.IsConnected)
                    SocketManager.Instance.Disconnect();

                // Already in GameTable — render the seated state in place, no reload.
                _convertingInPlace = true;
                JoinTable(tableId, isSpectator: false);
            }
            catch (Exception e)
            {
                Debug.LogError("[TableJoinHandler] Seat conversion failed: " + e.Message);
                Core.ToastEvents.Show("Failed to take seat: " + e.Message);
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
            SocketManager.Instance.On(EVENT_COMMUNITY_CARDS, OnCommunityCardsReceived);
            SocketManager.Instance.On(EVENT_YOUR_TURN, OnYourTurnReceived);
            SocketManager.Instance.On(EVENT_TIMER_TICK, OnTimerTickReceived);
            SocketManager.Instance.On(EVENT_TIMER_START, OnTimerStartReceived);
            SocketManager.Instance.On(EVENT_PLAYER_ACTED, OnPlayerActedReceived);
            SocketManager.Instance.On(EVENT_GAME_ROUND_END, OnRoundEndReceived);
            SocketManager.Instance.On(EVENT_GAME_POT_UPDATE, OnPotUpdateReceived);
            SocketManager.Instance.On(EVENT_GAME_DEALER_MOVED, OnDealerMovedReceived);
            SocketManager.Instance.On(EVENT_PLAYER_JOINED, OnPlayerJoinedReceived);
            SocketManager.Instance.On(EVENT_PLAYER_LEFT, OnPlayerLeftReceived);
            SocketManager.Instance.On(EVENT_PLAYER_DISCONNECTED, OnPlayerDisconnectedReceived);
            SocketManager.Instance.On(EVENT_PLAYER_RECONNECTED, OnPlayerReconnectedReceived);
            SocketManager.Instance.On(EVENT_GAME_PAUSED, OnGamePausedReceived);
            SocketManager.Instance.On(EVENT_GAME_RESUMED, OnGameResumedReceived);
            SocketManager.Instance.On(EVENT_PLAYER_SITTING_OUT, OnPlayerSittingOutReceived);
            SocketManager.Instance.On(EVENT_PLAYER_CAME_BACK, OnPlayerCameBackReceived);
            SocketManager.Instance.On(EVENT_GAME_CHAT, OnGameChatReceived);
            SocketManager.Instance.On(EVENT_TIME_BANK, OnTimeBankActivated);
            SocketManager.Instance.On(EVENT_SEAT_AVAILABLE, OnSeatAvailableReceived);
            SocketManager.Instance.On(EVENT_WAITING_LIST_UPDATED, OnWaitingListUpdatedReceived);
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
                PlayerId = GetCurrentPlayerId(),
                IsSpectator = _pendingIsSpectator
            };

            // Current role reflects this join (seated player vs observer).
            IsSpectator = _pendingIsSpectator;
            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.SetSpectatorMode(IsSpectator);

            Debug.Log($"[TableJoinHandler] Emit join_table: {tableId} (spectator: {_pendingIsSpectator})");

            SocketManager.Instance.Emit(EVENT_JOIN_TABLE, payload);

            StopTimeoutCoroutine();
            _timeoutCoroutine = StartCoroutine(JoinTimeoutCoroutine());
        }

        private void OnStateUpdateReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] state_update: {json}");

            try
            {
                var state = JsonConvert.DeserializeObject<GameStateUpdatePayload>(json);

                if (state == null)
                {
                    Debug.LogError("[StateUpdate] state null");
                    return;
                }

                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.SetFullState(state);
                }

                if (SocketManager.Instance != null)
                {
                    SocketManager.Instance.SetCurrentTable(state.TableId);
                }

                if (_waitingForConfirmation)
                {
                    StopTimeoutCoroutine();
                    _waitingForConfirmation = false;
                    _pendingTableId = null;

                    if (_convertingInPlace)
                    {
                        // Spectator → seated while already in GameTable: don't reload
                        // the scene, just fall through and render the new state in place.
                        _convertingInPlace = false;
                    }
                    else
                    {
                        OnTableJoined?.Invoke(state);

                        if (GameSceneManager.Instance != null)
                            GameSceneManager.Instance.LoadScene(SCENE_GAME_TABLE);
                        else
                            Debug.LogError("[StateUpdate] GameSceneManager.Instance is null");

                        return;
                    }
                }

                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.RenderFullTable(state);
                    PokerTableUI.Instance.SetGameStatus($"Round {state.RoundNumber}:{state.GameState}");
                    PokerTableUI.Instance.UpdateDealerButton(state.DealerSeat ?? -1);
                    // Set blinds from the state itself (state_update carries them) —
                    // ReapplyBlindIndicators alone keeps stale -1 until a dealer_moved.
                    PokerTableUI.Instance.UpdateBlindIndicators(state.SmallBlindSeat ?? -1, state.BigBlindSeat ?? -1);

                    if (!string.IsNullOrEmpty(state.CurrentTurnPlayerId))
                    {
                        if (TurnManager.Instance == null || !TurnManager.Instance.IsMyTurn)
                        {
                            PokerTableUI.Instance.ShowThinkingAndTimer(
                                state.CurrentTurnPlayerId,
                                30f,
                                state.RoundNumber
                            );
                        }
                    }
                    else
                    {
                        PokerTableUI.Instance.HideAllThinkingAndTimers();
                    }


                    if (state.RoundNumber != lastRoundNumber)
                    {
                        lastRoundNumber = state.RoundNumber;

                        if (PokerTableUI.Instance != null)
                            PokerTableUI.Instance.ClearAllPlayerActions();

                        if (CommunityCardsUI.Instance != null)
                            CommunityCardsUI.Instance.ClearBoard();
                    }

                    PokerTableUI.Instance.UpdateMainPot(state.Pot);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"state_update failed: {e}");
            }
        }
        private void OnGameErrorReceived(string json)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<GameErrorPayload>(json);

                if (_waitingForConfirmation)
                {
                    HandleJoinFailure(error?.Message ?? "Could not join table");
                    return;
                }

                // While standing up we auto-fold every turn; a "Not your turn" (G001)
                // race is expected and harmless — don't surface it to the player.
                if (IsStoodUp && error?.Code == "G001")
                {
                    Debug.Log("[StandUp] Ignored 'Not your turn' during auto-fold");
                    return;
                }

                // Gameplay error — show toast, don't alter join state
                Core.ToastEvents.Show(GameErrorMessage(error?.Code, error?.Message));
            }
            catch
            {
                if (_waitingForConfirmation)
                    HandleJoinFailure("Could not join table");
            }
        }

        private static string GameErrorMessage(string code, string fallback)
        {
            return code switch
            {
                "G001" => "Not your turn",
                "G002" => "Invalid action",
                "G009" => "Raise amount too low",
                "G010" => "Already folded",
                "G011" => "Already all-in",
                "G015" => "Rule violation",
                _      => fallback ?? "Game error"
            };
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

                if (string.IsNullOrEmpty(payload.Variant))
                {
                    payload.Variant = GameStateManager.Instance.CurrentState?.Variant;

                    if (string.IsNullOrEmpty(payload.Variant))
                        payload.Variant = "texas_holdem";
                }
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

                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.ShowMyPrivateCards(payload.Cards);
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
            if (string.IsNullOrEmpty(variant))
            {
                variant = GameStateManager.Instance.CurrentState?.Variant;

                if (string.IsNullOrEmpty(variant))
                    variant = "texas_holdem";
            }

            switch (variant)
            {
                case "texas_holdem":
                    return count == 2;

                case "omaha":
                case "plo4":
                    return count == 4;

                case "plo6":
                case "omaha_six":
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

                // PLO only: highlight best 2 hole cards after flop/turn/river
                string variant = GameStateManager.Instance.Variant
                              ?? GameStateManager.Instance.CurrentState?.Variant;
                bool isPLO = variant == "omaha" || variant == "omaha_six" || variant == "plo4" || variant == "plo6";

                if (isPLO)
                {
                    List<string> allCommunity = GameStateManager.Instance.CommunityCards
                        .Distinct().ToList();
                    List<string> holeCards    = GameStateManager.Instance.YourCards;
                    string localId            = GetCurrentPlayerId();

                    if (holeCards != null && holeCards.Count > 0 && allCommunity != null && allCommunity.Count >= 3)
                    {
                        List<string> bestHole = PLOHandEvaluator.GetBestHoleCards(holeCards, allCommunity);
                        Debug.Log($"[PLO] hole={string.Join(",", holeCards)} community={string.Join(",", allCommunity)} best={string.Join(",", bestHole)}");

                        if (PokerTableUI.Instance != null)
                            PokerTableUI.Instance.HighlightLocalPlayerBestCards(localId, holeCards, bestHole);
                    }
                }

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

                // Standing up → auto-fold every turn until the round ends (CLUB-1010).
                if (IsStoodUp)
                {
                    Debug.Log("[StandUp] Auto-folding (stood up)");
                    Fold();
                    return;
                }

                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.StartYourTurn(payload);
                }


                Debug.Log(
                    $"[YourTurn] actions={string.Join(",", payload.ValidActions)} " +
                    $"canCheck={payload.CanCheck} callAmount={payload.CallAmount} " +
                    $"minRaise={payload.MinimumRaise} chips={payload.YourChips} gameState={payload.GameState}"
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
                var payload = JsonConvert.DeserializeObject<PlayerActedPayload>(json);

                if (payload == null)
                {
                    Debug.LogError("[PlayerActed] payload null");
                    return;
                }

                GamePlayer player = null;

                if (GameStateManager.Instance != null)
                {
                    player = GameStateManager.Instance.ApplyPlayerAction(payload);
                }

                if (player != null && PokerTableUI.Instance != null)
                {
                   //PokerTableUI.Instance.UpdateSeatAction(player.Seat, payload.Action);

                 
                   // PokerTableUI.Instance.UpdateSeatChips(player.Seat, payload.UpdatedChips);

                   
                    if (payload.Pot > 0)
                        PokerTableUI.Instance.UpdateMainPot(payload.Pot);

                    Debug.Log($"[PlayerActed123] UI updated → {player.Username}: {player.Chips}");
                    // Debug.Log($"[PlayerActed123] UI updated → {player.Username}: {payload.UpdatedChips}");
                    Debug.Log(
         $"[PlayerActed123] {player.Username} | StateChips: {player.Chips} | PayloadChips: {payload.UpdatedChips}"

     );
                    PokerTableUI.Instance.UpdateSeatChips(player.Seat, player.Chips);
                }
                else
                {
                    Debug.LogWarning("[PlayerActed] Player not found after action");
                }
                    PlayerActionUI.Instance.HandlePlayerAction(payload);
                
                RequestState();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerActed] parse failed: {e.Message}");
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

                if (payload.communityCards == null || payload.communityCards.Count == 0)
                {
                    var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);

                    payload.communityCards = jObj["communityCards"]?
                        .ToObject<List<string>>() ?? new List<string>();
                }

                Debug.Log("Community Cards Count: " + payload.communityCards.Count);
                Debug.Log("Community Cards: " + string.Join(", ", payload.communityCards));
                if (payload == null)
                {
                    Debug.LogError("[RoundEnd] Payload NULL");
                    return;
                }
                if (payload.winner != null && PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.LockWinnerChipText(payload.winner.id);
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

               

                //------------------------------------------------------
                // STEP 5 : Clear action labels
                //------------------------------------------------------
                if (PlayerActionUI.Instance != null)
                {
                    PlayerActionUI.Instance.ClearAllActionLabels();
                }

                if (payload.winner != null &&
     payload.updatedChipBalances != null &&
     payload.updatedChipBalances.ContainsKey(payload.winner.id))
                {
                    int finalWinnerChips = payload.updatedChipBalances[payload.winner.id];
                    StartCoroutine(
                       PokerTableUI.Instance.PlayPotToWinnerAndUpdateChips(
                           payload.winner.id,
                           finalWinnerChips
                       ));

                   StartCoroutine(AnimateWinnerChipsAfterCoinMove(payload.winner.id, finalWinnerChips));
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
                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.ResetCardsForNewRound();
                }


                if (PokerTableUI.Instance != null)
                {
                    PokerTableUI.Instance.SetGameStatus($"Round {payload.roundNumber} Finished");

                    if (payload.winner != null)
                        PokerTableUI.Instance.ShowWinner(payload.winner.username, payload.potWon, payload.hand?.name);

                    if (payload.showdown && payload.showdownCards != null)
                    {
                        PokerTableUI.Instance.ShowAllShowdownCards(payload.showdownCards);
                    }

                    if (payload.showdown)
                    {
                        StartCoroutine(HighlightWinnerCardsDelayed(payload ,json));
                    }
                    // Debug.Log("ShowWinnerCards  : "+ string.Join(", ", payload.winner.holeCards));

                }

                // Persistent tables run continuously — no game-over after N rounds.
                // if (payload.roundNumber >= 4)
                // {
                //     StartCoroutine(ShowGameOverDelayed(3.5f));
                // }

                // Stand Up pending → the hand has finished, now leave + spectate (CLUB-1010).
                if (IsStoodUp)
                    ExecuteStandUp().Forget();

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
        private IEnumerator AnimateWinnerChipsAfterCoinMove(string winnerId, int finalChips)
        {
            yield return new WaitForSeconds(0.5f);

            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.AnimateWinnerChipText(winnerId, finalChips);
        }


        private IEnumerator HighlightWinnerCardsDelayed(RoundEndPayload payload ,string json)
        {
            yield return new WaitForSeconds(0.6f);

            if (payload == null ||
                payload.winner == null ||
                payload.showdownCards == null ||
                payload.communityCards == null)
                yield break;

            ShowdownCardData winnerData = payload.showdownCards.Find(
                x => x.playerId == payload.winner.id
            );

            if (winnerData == null || winnerData.holeCards == null)
                yield break;

            if (payload.communityCards == null || payload.communityCards.Count == 0)
            {
                var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);

                payload.communityCards = jObj["communityCards"]?
                    .ToObject<List<string>>() ?? new List<string>();
            }
            Debug.Log("Community Cards1: " + string.Join(", ", payload.communityCards));
            Debug.Log("Hole Cards1: " + string.Join(", ", winnerData.holeCards));
            string roundVariant = GameStateManager.Instance.Variant
                               ?? GameStateManager.Instance.CurrentState?.Variant;
            bool roundIsPLO = roundVariant == "omaha" || roundVariant == "omaha_six" || roundVariant == "plo4" || roundVariant == "plo6";

            List<string> highlightCards = roundIsPLO
                ? PLOHandEvaluator.GetBestFiveCards(winnerData.holeCards, payload.communityCards)
                : PokerBestHandHighlighter.GetHighlightCards(winnerData.holeCards, payload.communityCards);


            PokerTableUI.Instance.ShowHandName(payload.hand.name);
            Debug.Log("Highlight Cards: " + string.Join(", ", highlightCards));

            if (PokerTableUI.Instance != null)
            {
                PokerTableUI.Instance.HighlightWinnerCards(
                    payload.winner.id,
                    highlightCards,
                    payload.showdownCards
                );
            }

            if (CommunityCardsUI.Instance != null)
            {
                CommunityCardsUI.Instance.HighlightCommunityCards(highlightCards);
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
                var payload = JsonConvert.DeserializeObject<PlayerJoinedPayload>(json);

                if (payload == null || string.IsNullOrEmpty(payload.PlayerId))
                {
                    Debug.LogWarning("[PlayerJoined] Invalid payload");
                    return;
                }

                if (payload.IsSpectator)
                {
                    Debug.Log($"[PlayerJoined] Spectator joined: {payload.PlayerId}");
                    return;
                }

                // ❌ REMOVE THIS LINE
                GameStateManager.Instance.RefreshPlayerSeats();

                // ❌ DO NOT TOUCH UI HERE

                Debug.Log($"[PlayerJoined] Notification only → waiting for state_update");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerJoined] Parse Failed: {e.Message}");
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

                // Mid-hand leave = fold + stand AFTER the round ends. The server keeps
                // the player (folded) in state until then, so removing now would just
                // make them reappear on the next state_update. Let the state_update
                // prune remove them when the server actually drops them (round end).
                string gs = GameStateManager.Instance != null ? GameStateManager.Instance.GameState : null;
                bool handInProgress = gs == "PRE_FLOP" || gs == "FLOP" || gs == "TURN" || gs == "RIVER";
                if (handInProgress)
                {
                    Debug.Log($"[PlayerLeft] {payload.username} leaving mid-hand — defer removal to round end.");
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
                // "waiting_for_players" is handled by the count-based
                // waitingForPlayersOverlay in RenderFullTable — don't also flash the
                // pause overlay for it (avoids the duplicate "Waiting for players…").
                //--------------------------------------------------
                if (PokerTableUI.Instance != null)
                {
                    if (payload.reason == "waiting_for_players")
                        PokerTableUI.Instance.HidePauseOverlay();
                    else
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

        private IEnumerator ShowGameOverDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.ShowGameOver();
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