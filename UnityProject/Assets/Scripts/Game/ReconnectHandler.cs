
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
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ClubPoker.Core;
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

        #endregion

        #region Reconnect Token

        /// <summary>
        /// POST /api/reconnect/token
        /// Fetches a one-time reconnect token tied to the player's current table session.
        /// Token expires in 60 seconds (same as the backend grace period).
        /// Stored in memory — not persisted to disk.
        /// </summary>
        private async UniTaskVoid FetchReconnectTokenAsync(string tableId)
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

            if (string.IsNullOrEmpty(_reconnectToken))
            {
                Debug.LogWarning("[ReconnectHandler] No reconnect token available — navigating to Lobby.");
                HandleReconnectRejection();
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
            SocketManager.Instance.On("game:state_update", OnReconnectStateUpdate);
            SocketManager.Instance.On("game:error",        OnReconnectError);
           

            SocketManager.Instance.Emit("player:reconnect", payload);

            // Token is single-use — clear immediately after emit
            _reconnectToken = null;
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
            Debug.LogError("[ReconnectHandler] Grace period expired — navigating to Lobby.");
            HandleReconnectRejection();
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

        private void HandleReconnectRejection()
        {
            ClearReconnectState();
            SocketManager.Instance.ClearCurrentTable();

            OnReconnectRejected?.Invoke();

            // Navigate back to Lobby — player's seat has been forfeited
            GameSceneManager.Instance.LoadScene("Scene_Lobby");
        }

        #endregion



    }
}