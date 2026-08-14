
// Responsibilities:
//   - Fetch one-time reconnect token from POST /api/reconnect/token on disconnect
//   - Emit player:reconnect with token after socket re-establishes
//   - Handle A005 rejection (invalid token or grace period expired)
//   - Provide countdown seconds to UI overlay during reconnect
//
// Reconnect flow:
//   1. App backgrounds or socket drops while at a table
//   2. OnAppBackgrounded() → POST /api/reconnect/token → store token
//   3. SocketManager reconnects → socket:authenticated fires
//   4. OnAuthenticated() → emit player:reconnect { tableId, reconnectToken }
//   5. Server responds with game:state_update + game:your_cards
//   6. A005 → clear table state → navigate to Lobby

using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ClubPoker.Core;
using ClubPoker.Auth;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class ReconnectHandler : MonoBehaviour
    {
        public static ReconnectHandler Instance { get; private set; }

        #region Events

        /// <summary>
        /// Fired during reconnection with the number of seconds remaining.
        /// UI countdown overlay subscribes to this to update the display.
        /// </summary>
        public event Action<int> OnCountdownUpdated;

        /// <summary>
        /// Fired when server accepts player:reconnect and sends game:state_update.
        /// Subscribers should restore game UI from the new state.
        /// </summary>
        public event Action<GameStateUpdatePayload> OnReconnectSuccess;

        /// <summary>
        /// Fired when server rejects player:reconnect with A005.
        /// Subscribers should clear table state and navigate to Lobby.
        /// </summary>
        public event Action OnReconnectRejected;

        #endregion

        #region Private Fields

        private string _reconnectToken;
        private bool   _isReconnecting;

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
        }

        private void OnEnable()
        {
            if (SocketManager.Instance != null)
            {
                SocketManager.Instance.OnAuthenticated   += OnSocketAuthenticated;
                SocketManager.Instance.OnReconnectFailed += OnGracePeriodExpired;
                SocketManager.Instance.OnReconnecting    += OnSocketReconnecting;
                SocketManager.OnAppBackgrounded += OnAppBackgrounded;
                SocketManager.OnCountdownTick   += UpdateCountdown;

            }
        }

        private void OnDisable()
        {
            if (SocketManager.Instance != null)
            {
                SocketManager.OnAppBackgrounded -= OnAppBackgrounded;
                SocketManager.OnCountdownTick   -= UpdateCountdown;
                SocketManager.Instance.OnAuthenticated   -= OnSocketAuthenticated;
                SocketManager.Instance.OnReconnectFailed -= OnGracePeriodExpired;
                SocketManager.Instance.OnReconnecting    -= OnSocketReconnecting;
            }
        }

        #endregion

        #region Called by SocketManager

        /// <summary>
        /// Called by SocketManager when the app is backgrounded while seated at a table.
        /// Fetches the reconnect token immediately while the JWT is still valid.
        /// </summary>
        public void OnAppBackgrounded(string tableId)
        {
            FetchReconnectTokenAsync(tableId).Forget();
        }

        /// <summary>
        /// Called by SocketManager during reconnection to update the UI countdown.
        /// </summary>
        public void UpdateCountdown(int secondsRemaining)
        {
            OnCountdownUpdated?.Invoke(secondsRemaining);
        }

        /// <summary>
        /// Fired when SocketManager starts reconnecting after an unexpected mid-game
        /// disconnect. The cached handshake JWT may have expired during the hand, so
        /// refresh it here — this rotates ApiClient.AccessToken, which SocketManager
        /// reads on its next reconnect attempt. Networking cannot call AuthManager
        /// directly (no assembly reference), so the refresh is driven from here.
        /// </summary>
        private void OnSocketReconnecting()
        {
            // Arm the sequence. Without this _isReconnecting stays false and
            // OnSocketAuthenticated returns early, so player:reconnect was never
            // emitted — the socket came back but the seat was never re-claimed.
            BeginReconnect();

            // Try to pre-fetch the one-time token. On a real network drop this call
            // fails (we're offline) — OnSocketAuthenticated retries it once the
            // connection is actually back. On a clean drop or app-background the
            // network is still up and this succeeds immediately.
            string tableId = SocketManager.Instance != null
                ? SocketManager.Instance.CurrentTableId
                : null;

            if (!string.IsNullOrEmpty(tableId))
                FetchReconnectTokenAsync(tableId).Forget();

            RefreshTokenForReconnectAsync().Forget();
        }

        private async UniTaskVoid RefreshTokenForReconnectAsync()
        {
            try
            {
                if (AuthManager.Instance == null) return;

                Debug.Log("[ReconnectHandler] Refreshing JWT before socket reconnect.");
                bool ok = await AuthManager.Instance.RefreshSessionAsync();
                if (!ok)
                    Debug.LogWarning("[ReconnectHandler] JWT refresh failed — reconnect will retry with existing token.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReconnectHandler] JWT refresh error during reconnect: {e.Message}");
            }
        }

        #endregion

        #region Reconnect Token

        /// <summary>
        /// POST /api/reconnect/token
        /// Fetches a one-time reconnect token tied to the player's current table session.
        /// Token expires in 60 seconds (same as the backend grace period).
        /// Stored in memory — not persisted to disk.
        /// </summary>
        private async UniTask FetchReconnectTokenAsync(string tableId)
        {
            try
            {
                Debug.Log($"[ReconnectHandler] Fetching reconnect token for table: {tableId}");

                var request  = new ReconnectTokenRequest { TableId = tableId };
                var response = await ApiClient.Instance.Post<ReconnectTokenResponse>(
                    "/api/reconnect/token", request);

                _reconnectToken = response.ReconnectToken;
                Debug.Log($"[ReconnectHandler] Reconnect token stored. Expires: {response.ExpiresAt}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReconnectHandler] Failed to fetch reconnect token: {e.Message}");
                _reconnectToken = null;
            }
        }

        #endregion

        #region Socket Event Handlers

        /// <summary>
        /// Called when socket:authenticated fires during a reconnect attempt.
        /// If a reconnect token is available, emits player:reconnect immediately.
        /// If no token is available (e.g. token fetch failed), navigates to Lobby.
        /// </summary>
        private void OnSocketAuthenticated(SocketAuthenticatedPayload payload)
        {
            string tableId = SocketManager.Instance.CurrentTableId;

            // Only handle reconnect if we were previously at a table
            if (string.IsNullOrEmpty(tableId) || !_isReconnecting)
                return;

            ReconnectWithTokenAsync(tableId).Forget();
        }

        /// <summary>
        /// A network drop leaves us with no reconnect token — the pre-fetch runs
        /// while we're still offline and fails. By the time socket:authenticated
        /// fires the connection is genuinely back, so fetch it here before giving up.
        /// </summary>
        private async UniTaskVoid ReconnectWithTokenAsync(string tableId)
        {
            if (string.IsNullOrEmpty(_reconnectToken))
            {
                Debug.Log("[ReconnectHandler] No reconnect token yet — fetching now that we're online.");
                await FetchReconnectTokenAsync(tableId);
            }

            if (string.IsNullOrEmpty(_reconnectToken))
            {
                // Do NOT bail to Lobby. A failed token fetch says nothing about
                // whether the seat is still ours — the endpoint may be down or the
                // network still settling. The socket is authenticated and
                // TableJoinHandler requests a fresh snapshot, so carry on. Only an
                // explicit A005 forfeits the seat.
                Debug.LogWarning("[ReconnectHandler] No reconnect token — continuing without player:reconnect.");
                _isReconnecting = false;
                return;
            }

            EmitPlayerReconnect(tableId);
        }

        /// <summary>
        /// Emit player:reconnect with the stored one-time token.
        /// Server responds with game:state_update + game:your_cards on success,
        /// or game:error A005 on rejection.
        /// </summary>
        private void EmitPlayerReconnect(string tableId)
        {
            Debug.Log($"[ReconnectHandler] Emitting player:reconnect for table: {tableId}");

            var payload = new PlayerReconnectPayload
            {
                TableId        = tableId,
                ReconnectToken = _reconnectToken
            };

            // Subscribe to server response before emitting
            // NOT subscribing to game:state_update here. SocketManager allows one
            // handler per event and On() replaces it, so this stole every snapshot
            // from TableJoinHandler — which is what renders round, pot and seats.
            // The result was a reconnected client whose table never updated again.
            // TableJoinHandler keeps the event; it requests a fresh snapshot on
            // re-auth anyway.
            // Not subscribing to game:error either. TableJoinHandler owns it, and
            // On() replaces rather than adds — whichever registered last wins, so
            // A005 handling was silently lost after the next re-auth. It routes
            // A005 here through NotifyReconnectRejected instead.
           

            SocketManager.Instance.Emit("player:reconnect", payload);

            // Token is single-use — clear immediately after emit
            _reconnectToken = null;

            // game:your_turn fired while we were offline and the server does not
            // replay it, so state_update alone leaves TurnManager stale: reconnect
            // on your own turn and the action buttons never appear. The web client
            // asks for it explicitly — do the same.
            RequestTurn(tableId);
        }

        /// <summary>
        /// player:request_turn — re-sync whose turn it is after a reconnect.
        /// </summary>
        private void RequestTurn(string tableId)
        {
            Debug.Log($"[ReconnectHandler] Emitting player:request_turn for table: {tableId}");

            var payload = new Dictionary<string, object>
            {
                { "tableId", tableId }
            };

            SocketManager.Instance.Emit("player:request_turn", payload);
        }

        private void OnReconnectStateUpdate(string json)
        {
            try
            {
                var state = JsonConvert.DeserializeObject<GameStateUpdatePayload>(json);
                Debug.Log($"[ReconnectHandler] Reconnect accepted. Table: {state?.TableId}");

                _isReconnecting = false;
                OnReconnectSuccess?.Invoke(state);
                GameStateManager.Instance.SetFullState(state);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReconnectHandler] Failed to parse game:state_update: {e.Message}");
            }
        }

     

        private void OnReconnectError(string json)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<GameErrorPayload>(json);

                if (error?.Code == "A005")
                {
                    Debug.LogWarning("[ReconnectHandler] Reconnect rejected — A005: " +
                                     "token invalid or grace period expired.");
                    HandleReconnectRejection();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReconnectHandler] Failed to parse game:error: {e.Message}");
            }
        }

        /// <summary>
        /// Called by SocketManager when all reconnect attempts are exhausted.
        /// </summary>
        private void OnGracePeriodExpired()
        {
            // Don't bail to Lobby here. The client's 60s starts when the network
            // dies; the SERVER's only starts when its heartbeat times out, 20-45s
            // later, and it then holds the seat for 3 more sit-out hands. Leaving
            // now abandons a seat the server is still keeping — and strands the
            // player, since they can't retry from the Lobby.
            //
            // Stay on the table and let PokerTableUI offer a manual retry. Lobby is
            // for a real rejection (A005), which HandleReconnectRejection covers.
            Debug.LogWarning(
                "[ReconnectHandler] Grace period expired — staying on table, awaiting manual retry.");
        }

        #endregion

        #region Reconnect State

        /// <summary>
        /// Call this when the player disconnects unexpectedly during a game.
        /// Sets the reconnecting flag so OnSocketAuthenticated knows to emit player:reconnect.
        /// </summary>
        public void BeginReconnect()
        {
            _isReconnecting = true;
            Debug.Log("[ReconnectHandler] Reconnect sequence begun.");
        }

        /// <summary>
        /// Clear reconnect state after a successful reconnect or rejection.
        /// </summary>
        public void ClearReconnectState()
        {
            _isReconnecting = false;
            _reconnectToken = null;
        }

        #endregion

        #region Rejection Handling

        /// <summary>
        /// Called by TableJoinHandler's game:error handler when the server rejects a
        /// reconnect (A005 — token invalid or grace period expired). A direct call
        /// rather than a subscription: only one component may own a socket event.
        /// </summary>
        public void NotifyReconnectRejected()
        {
            Debug.LogWarning("[ReconnectHandler] Reconnect rejected — A005: " +
                             "token invalid or grace period expired.");
            HandleReconnectRejection();
        }

        private void HandleReconnectRejection()
        {
            ClearReconnectState();
            SocketManager.Instance.ClearCurrentTable();

            OnReconnectRejected?.Invoke();

            // Seat forfeited → back to where this table was entered from. A cold
            // start has no in-memory context, so pull it off disk first.
            TableContext.Restore();
            TableExitRouter.GoBackAndClear();
        }

        #endregion



    }
}