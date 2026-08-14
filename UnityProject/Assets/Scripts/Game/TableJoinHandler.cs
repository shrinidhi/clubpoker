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
using ClubPoker.Auth;

namespace ClubPoker.Game
{
    public class TableJoinHandler : MonoBehaviour
    {
        public static TableJoinHandler Instance { get; private set; }
        private int lastRoundNumber = -1;

        private GameStateUpdatePayload currentGameState;
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

        /// <summary>
        /// Call right after emitting player:sit_out. Applies the sit-out locally
        /// without waiting for the server broadcast: flag myself sitting out (so
        /// the your_turn auto-fold works even if the broadcast is late), fold if
        /// it's currently my turn, and hide the action buttons.
        /// </summary>
        public void NotifySitOutRequested()
        {
            string myId = Auth.AuthManager.Instance.Session.Id;

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.SetPlayerSitOut(myId, true);

                if (GameStateManager.Instance.CurrentTurnPlayerId == myId)
                    Fold();
            }

            if (TurnManager.Instance != null)
                TurnManager.Instance.DisableAllActions();
        }

        /// <summary>
        /// Call right after emitting player:come_back — clears the local sit-out
        /// flag so the next your_turn isn't auto-folded while the server
        /// confirmation is still in flight.
        /// </summary>
        public void NotifyComeBackRequested()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetPlayerSitOut(
                    Auth.AuthManager.Instance.Session.Id, false);
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

                // Seat gone → back to the entry screen (club or home) and clear.
                if (GameSceneManager.Instance != null)
                    TableExitRouter.GoBackAndClear();
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
            {
                // Re-authentication after a reconnect — the original join already
                // happened, so there's no pending join to emit. Pull a fresh
                // snapshot instead: without it the Come Back button, seat badges
                // and sit-out flags stay frozen at whatever they were when the
                // connection dropped, and the player has no way back into play.
                if (SocketManager.Instance != null &&
                    !string.IsNullOrEmpty(SocketManager.Instance.CurrentTableId))
                {
                    Debug.Log("[TableJoinHandler] Re-authenticated at table — requesting fresh state.");
                    RequestState();
                }

                return;
            }

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
                currentGameState = state;
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
                        {
                            PokerTableUI.Instance.ClearAllPlayerActions();

                            // Release the showdown-reveal guard so opponents get
                            // fresh card backs, and drop last hand's highlight.
                            PokerTableUI.Instance.EndCardRevealForAllSeats();
                        }

                        if (CommunityCardsUI.Instance != null)
                            CommunityCardsUI.Instance.ClearBoard();
                    }

                    // First snapshot after a reconnect: rebuild the board from it.
                    // Uses the same call game:community_cards does, so there's no
                    // second rendering path to keep working. Skipped entirely when
                    // there are no cards, which is what made this unsafe before.
                    if (PokerTableUI.Instance.NeedsBoardResync)
                    {
                        PokerTableUI.Instance.ConsumeBoardResync();

                        if (CommunityCardsUI.Instance != null &&
                            state.CommunityCards != null &&
                            state.CommunityCards.Count > 0)
                        {
                            Debug.Log($"[Reconnect] Rebuilding board: {state.CommunityCards.Count} cards.");
                            CommunityCardsUI.Instance.ShowCommunityCards(
                                state.CommunityCards, state.GameState);
                        }
                    }

                    PokerTableUI.Instance.UpdateMainPot(state.Pot);

                    SyncSitOutLifecycleUI(state);
                    FlushInactivityToasts(state);

                    HandHistoryManager.Instance
   .StartNewHand(state);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"state_update failed: {e}");
            }
        }
        /// <summary>
        /// Reconcile the disconnect / sit-out UI against the state snapshot. The
        /// broadcasts (player_disconnected, player_sitting_out, player_came_back)
        /// are best-effort notifications — this is what keeps the table honest if
        /// one is missed, and it's the only path that runs on a fresh join or a
        /// reconnect into an already-running hand.
        /// </summary>
        private void SyncSitOutLifecycleUI(GameStateUpdatePayload state)
        {
            if (state?.Players == null || PokerTableUI.Instance == null)
                return;

            string myId = AuthManager.Instance.Session.Id;
            bool anyoneDisconnected = false;

            foreach (var player in state.Players)
            {
                if (player.Disconnected)
                    anyoneDisconnected = true;

                if (player.Id != myId)
                    continue;

                // My own sit-out state drives the Come Back button. The server is
                // the authority — a missed player_came_back would otherwise leave
                // the button stuck on (or off, which strands me sitting out).
                if (PokerTableUI.Instance.ComeBackButton != null)
                    PokerTableUI.Instance.ComeBackButton.gameObject
                        .SetActive(player.SittingOut);

                // Server-set hand count means it sat me out after a drop, so the
                // button reads "I'm back". A voluntary sit-out keeps "Come Back".
                PokerTableUI.Instance.SetComeBackLabel(
                    player.SitOutHandsRemaining.HasValue);

                if (player.SittingOut && TurnManager.Instance != null)
                    TurnManager.Instance.DisableAllActions();
            }

        }

        // A drop the server detects late — often only when the player reconnects —
        // arrives as player_disconnected and player_reconnected almost together.
        // Announcing immediately gives the observer "lost connection" followed by
        // "reconnected" for something they never saw break, or a lone "reconnected"
        // out of nowhere. Hold the first toast briefly and drop both if the player
        // is already back.
        private const float DISCONNECT_TOAST_DELAY_SECONDS = 2f;

        private readonly Dictionary<string, Coroutine> disconnectToastPending =
            new Dictionary<string, Coroutine>();
        private readonly HashSet<string> disconnectToastShown = new HashSet<string>();

        private void QueueDisconnectToast(string playerId, string username)
        {
            if (string.IsNullOrEmpty(playerId))
                return;

            if (disconnectToastPending.TryGetValue(playerId, out Coroutine existing) && existing != null)
                StopCoroutine(existing);

            disconnectToastPending[playerId] =
                StartCoroutine(DisconnectToastRoutine(playerId, username));
        }

        private IEnumerator DisconnectToastRoutine(string playerId, string username)
        {
            yield return new WaitForSeconds(DISCONNECT_TOAST_DELAY_SECONDS);

            string who = string.IsNullOrEmpty(username) ? "Opponent" : username;
            Core.ToastEvents.Show($"{who} lost connection");

            disconnectToastShown.Add(playerId);
            disconnectToastPending.Remove(playerId);
        }

        /// <summary>
        /// Only announce a reconnect if the drop was actually announced. Otherwise
        /// the observer gets "reconnected" for a blip they never saw.
        /// </summary>
        private void ResolveDisconnectToast(string playerId, string username)
        {
            if (string.IsNullOrEmpty(playerId))
                return;

            if (disconnectToastPending.TryGetValue(playerId, out Coroutine pending))
            {
                if (pending != null)
                    StopCoroutine(pending);

                disconnectToastPending.Remove(playerId);
                Debug.Log($"[Disconnect] {username} back before the toast fired — staying silent.");
                return;
            }

            if (!disconnectToastShown.Remove(playerId))
                return;

            string who = string.IsNullOrEmpty(username) ? "Opponent" : username;
            Core.ToastEvents.Show($"{who} reconnected");
        }

        // Players the server dropped for inactivity, keyed to the name we announce.
        // Held until the seat is actually gone from state_update.players[] — a
        // mid-hand player_left defers the removal, and toasting on the event would
        // announce a removal the player can still see on the table.
        private readonly Dictionary<string, string> removedForInactivityNames =
            new Dictionary<string, string>();

        private void ShowInactivityToast(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
                return;

            if (!removedForInactivityNames.TryGetValue(playerId, out string name))
                return;

            removedForInactivityNames.Remove(playerId);
            Core.ToastEvents.Show($"{name} removed for inactivity");
        }

        /// <summary>
        /// Fire any deferred inactivity toasts whose player has now actually left
        /// the snapshot. Called from every state_update.
        /// </summary>
        private void FlushInactivityToasts(GameStateUpdatePayload state)
        {
            if (removedForInactivityNames.Count == 0)
                return;

            var stillSeated = new HashSet<string>();

            if (state?.Players != null)
                foreach (var player in state.Players)
                    stillSeated.Add(player.Id);

            var gone = new List<string>();
            foreach (var id in removedForInactivityNames.Keys)
                if (!stillSeated.Contains(id))
                    gone.Add(id);

            foreach (var id in gone)
                ShowInactivityToast(id);
        }

        private void OnGameErrorReceived(string json)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<GameErrorPayload>(json);

                // A005 — reconnect token invalid or grace period expired. The seat is
                // forfeit, so ReconnectHandler has to see it even though it arrives
                // on the shared error channel.
                if (error?.Code == "A005" && ReconnectHandler.Instance != null)
                {
                    ReconnectHandler.Instance.NotifyReconnectRejected();
                    return;
                }

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
                    List<string> holeCards = GameStateManager.Instance.YourCards;
                    string localId = GetCurrentPlayerId();

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

                if (payload.Cards.Count == 3)
                {
                    HandHistoryManager.Instance.SetStreet("FLOP");
                    HandHistoryManager.Instance.AddBoardCards(payload.Cards);
                }
                else if (payload.Cards.Count == 4)
                {
                    HandHistoryManager.Instance.SetStreet("TURN");

                    HandHistoryManager.Instance.AddBoardCards(
                        new List<string> { payload.Cards[3] });
                }
                else if (payload.Cards.Count == 5)
                {
                    HandHistoryManager.Instance.SetStreet("RIVER");

                    HandHistoryManager.Instance.AddBoardCards(
                        new List<string> { payload.Cards[4] });
                }

                HandHistoryManager.Instance.AddBoardCards(payload.Cards);
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
                string myId = AuthManager.Instance.Session.Id;

                if (GameStateManager.Instance.IsPlayerSittingOut(myId))
                {
                    Fold();
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

                // The server omits playerId on the auto-actions it generates for
                // disconnected seats, e.g. {"action":"check","amount":0,"pot":20}.
                // It only ever auto-acts for the player whose turn it is, so fall
                // back to that — otherwise the action can't be attributed at all and
                // the gesture plays on nobody.
                if (string.IsNullOrEmpty(payload.PlayerId) &&
                    GameStateManager.Instance != null &&
                    !string.IsNullOrEmpty(GameStateManager.Instance.CurrentTurnPlayerId))
                {
                    payload.PlayerId = GameStateManager.Instance.CurrentTurnPlayerId;
                    Debug.LogWarning(
                        $"[PlayerActed] No playerId in payload — attributing to current turn: {payload.PlayerId}");
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
                // Both of these fire even when the seat wasn't found above — an action
                // can land while the table scene is tearing down, or for a player the
                // server auto-acted for after we'd already pruned them. Unguarded,
                // that NREs and gets misreported as a parse failure.
                if (PlayerActionUI.Instance != null)
                    PlayerActionUI.Instance.HandlePlayerAction(payload);

                RequestState();

                if (HandHistoryManager.Instance != null)
                    HandHistoryManager.Instance.AddAction(payload);
            }
            catch (Exception e)
            {
                // Not only parsing — this catch covers the whole handler, so log the
                // type and stack or the next occurrence is just as opaque.
                Debug.LogError($"[PlayerActed] handler failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
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

                // Drop a round_end for a hand we've already moved past. Nothing
                // orders these against state_update, and on reconnect the server can
                // deliver a late round_end for the previous hand — which then
                // overwrites "Round 26:PRE_FLOP" with "Round 25 Finished" and shows
                // that hand's winner over the live one.
                if (payload != null && GameStateManager.Instance != null &&
                    payload.roundNumber > 0 &&
                    payload.roundNumber < GameStateManager.Instance.RoundNumber)
                {
                    Debug.LogWarning(
                        $"[RoundEnd] Ignoring stale round {payload.roundNumber} " +
                        $"(current is {GameStateManager.Instance.RoundNumber}).");
                    return;
                }

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
                HandHistoryManager.Instance
   .CompleteHand(
       payload,
       currentGameState);
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
                    // Deliberately NOT writing the round text here. Two writers with
                    // no ordering guarantee meant a late round_end could overwrite a
                    // newer state_update and leave the previous round on screen.
                    // state_update carries roundNumber + gameState and is the only
                    // authority now.

                    if (payload.winner != null)
                        PokerTableUI.Instance.ShowWinner(payload.winner.username, payload.potWon, payload.hand?.name);

                    if (payload.showdown && payload.showdownCards != null && payload.winner != null)
                    {
                        // Reveal only the winner's cards — losing opponents stay hidden.
                        PokerTableUI.Instance.ShowWinnerShowdownCards(
                            payload.showdownCards, payload.winner.id);
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

            // Prefer the server's answer. It sends the full five-card hand, which is
            // the convention every poker client follows — the pair AND its kickers,
            // since kickers are what decide a split. The local calculators are the
            // fallback: PokerBestHandHighlighter filters down to ImportantRanks, so
            // One Pair highlighted only the two aces and dropped K/Q/10.
            List<string> highlightCards =
                winnerData.bestHandCards != null && winnerData.bestHandCards.Count > 0
                    ? winnerData.bestHandCards
                    : (roundIsPLO
                        ? PLOHandEvaluator.GetBestFiveCards(winnerData.holeCards, payload.communityCards)
                        : PokerBestHandHighlighter.GetHighlightCards(winnerData.holeCards, payload.communityCards));


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

                // A sitting-out player being dropped is the inactivity removal at the
                // end of the 3-hand sit-out window, not a voluntary leave — say so.
                bool removedForInactivity =
                    GameStateManager.Instance != null &&
                    GameStateManager.Instance.IsPlayerSittingOut(payload.playerId);

                if (removedForInactivity && payload.playerId != AuthManager.Instance.Session.Id)
                {
                    removedForInactivityNames[payload.playerId] =
                        string.IsNullOrEmpty(payload.username) ? "Opponent" : payload.username;
                }

                // player_left means gone — remove the seat now. This used to be
                // deferred while a hand was in progress, on the theory that the
                // server kept the player in state until round end and would re-add
                // them. In practice the server sends this once it has genuinely
                // dropped them, and the deferral left seats stuck on the table.
                // If the server disagrees, the next state_update puts them back.
                ShowInactivityToast(payload.playerId);

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

                    // Heads-up opponent gone → no hand can run. Clear the reconnect
                    // countdown and go back to waiting instead of leaving the overlay
                    // up until the next state_update.
                    if (GameStateManager.Instance != null &&
                        GameStateManager.Instance.SeatedPlayerCount < 2)
                    {
                        PokerTableUI.Instance.SetWaitingForPlayers(true);
                    }
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

                // My own drop is handled by ReconnectHandler (I can't see this
                // broadcast about myself anyway) — this is always someone else.
                string myId = AuthManager.Instance.Session.Id;
                if (payload.playerId == myId)
                    return;

                int seat = -1;
                bool headsUp = false;

                if (GameStateManager.Instance != null)
                {
                    seat = GameStateManager.Instance.GetPlayerSeat(payload.playerId);
                    headsUp = GameStateManager.Instance.IsHeadsUp;
                    GameStateManager.Instance.SetPlayerDisconnected(payload.playerId, true);
                }

                // Nothing on the seat for a mid-hand drop. They are still in the
                // hand — cards live, chips in the pot, and they can win it while the
                // server acts for them. The seat greys only once the server actually
                // deals them out (sittingOut), which Bind() picks up from the
                // snapshot. No overlay either: it only got in the way of the action
                // buttons when the turn moved on.
                //
                // PokerTableUI.ShowDisconnectedIndicator stays available if a subtler
                // connection icon is wanted here later.

                // Connection toasts disabled — the greyed seat already says it, and a
                // late-detected drop produced "lost connection" and "reconnected"
                // back to back for an outage nobody saw.
                // QueueDisconnectToast(payload.playerId, payload.username);

                Debug.Log(
                    $"[PlayerDisconnected] Completed → " +
                    $"{payload.username} | Seat: {seat} | " +
                    $"HeadsUp: {headsUp}"
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
                    GameStateManager.Instance.SetPlayerDisconnected(payload.playerId, false);
                }

                if (PokerTableUI.Instance != null && seat >= 0)
                {
                    // Reconnecting restores the CONNECTION, not the seat's playing
                    // state — the server keeps them sitting out until player:come_back.
                    // Clearing the badge here un-greyed them for a moment, then the
                    // next state_update greyed them again. Re-derive instead of
                    // assuming they're back in play.
                    bool stillSittingOut =
                        GameStateManager.Instance != null &&
                        GameStateManager.Instance.IsPlayerSittingOut(payload.playerId);

                    if (stillSittingOut)
                    {
                        PokerTableUI.Instance.ShowSittingOutState(
                            seat,
                            GameStateManager.Instance.GetSitOutHandsRemaining(payload.playerId));
                    }
                    else
                    {
                        PokerTableUI.Instance.HideDisconnectedIndicator(seat);
                    }
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
                int handsRemaining = -1;

                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.SetPlayerSitOut(payload.playerId, true);

                    // This is a broadcast about ANY player — only show MY comeback
                    // button when it's me who sat out.
                    if (payload.playerId == Auth.AuthManager.Instance.Session.Id)
                        PokerTableUI.Instance.ComeBackButton.gameObject.SetActive(true);

                    seat = GameStateManager.Instance.GetPlayerSeat(
                        payload.playerId
                    );

                    handsRemaining = GameStateManager.Instance
                        .GetSitOutHandsRemaining(payload.playerId);

                    // Sit-out supersedes the reconnect countdown — the server now
                    // plays this seat, so it's no longer "waiting to reconnect".
                    GameStateManager.Instance.SetPlayerDisconnected(payload.playerId, false);
                }

                if (PokerTableUI.Instance != null && seat >= 0)
                {
                    PokerTableUI.Instance.ShowSittingOutState(
                        seat,
                        handsRemaining > 0 ? handsRemaining : (int?)null
                    );

                    // A disconnected player who is now sitting out is played by the
                    // server (auto check/fold) — the sit-out badge takes over.
                    PokerTableUI.Instance.HideDisconnectedIndicator(seat);
                }

                Debug.Log(
                    $"[SittingOut] Completed → {payload.username} | Seat: {seat} | " +
                    $"HandsRemaining: {handsRemaining}"
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
            var payload =
                JsonConvert.DeserializeObject<PlayerSittingOut_CameBackPayload>(json);

            if (payload == null)
                return;

            GameStateManager.Instance.SetPlayerSitOut(
                payload.playerId,
                false
            );
            GameStateManager.Instance.SetPlayerDisconnected(payload.playerId, false);

            // Broadcast about ANY player — another player's comeback must not
            // hide MY button (it left the local player stuck sitting out with
            // no way to return).
            if (payload.playerId == Auth.AuthManager.Instance.Session.Id)
                PokerTableUI.Instance.ComeBackButton.gameObject.SetActive(false);

            // Clear the seat badges now rather than waiting for the next
            // state_update, which only arrives when the next street resolves.
            int seat = GameStateManager.Instance.GetPlayerSeat(payload.playerId);

            if (PokerTableUI.Instance != null && seat >= 0)
            {
                PokerTableUI.Instance.HideSittingOutState(seat);
                PokerTableUI.Instance.HideDisconnectedIndicator(seat);
            }

            Debug.Log($"[CameBack] {payload.username} back in play | Seat: {seat}");
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