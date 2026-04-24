//
// Responsibilities:
//   - Socket.io client connection with JWT authentication handshake
//   - Connection state machine (Disconnected, Connecting, Connected, Reconnecting)
//   - Automatic reconnection within the 60-second backend grace period
//
// Usage:
//   SocketManager.Instance.Connect(accessToken)    ← call after login
//   SocketManager.Instance.Disconnect()            ← call on logout
//   SocketManager.Instance.On("event", handler)    ← subscribe to server events
//   SocketManager.Instance.Emit("event", payload)  ← send events to server
//
// Subscribe to lifecycle events:
//   SocketManager.Instance.OnStateChanged    += YourHandler
//   SocketManager.Instance.OnAuthenticated   += YourHandler
//   SocketManager.Instance.OnReconnectFailed += YourHandler

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SocketIOClient;
using Newtonsoft.Json;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

namespace ClubPoker.Networking
{
    public class SocketManager : MonoBehaviour
    {
        public static SocketManager Instance { get; private set; }

        #region Public Events

        /// <summary>Fired whenever the connection state changes.</summary>
        public event Action<SocketConnectionState> OnStateChanged;

        public static event Action<string> OnAppBackgrounded;
        public static event Action<int>    OnCountdownTick;

        /// <summary>
        /// Fired when socket:authenticated is received from the server.
        /// Subscribers should emit player:join_table or player:reconnect at this point.
        /// </summary>
        public event Action<SocketAuthenticatedPayload> OnAuthenticated;

        /// <summary>
        /// Fired when all reconnection attempts are exhausted (60-second grace period expired).
        /// Subscribers should navigate the player back to Lobby and clear table state.
        /// </summary>
        public event Action OnReconnectFailed;

        #endregion

        #region Constants

        private const int    RECONNECT_INTERVAL_SECONDS = 5;
        private const int    RECONNECT_MAX_ATTEMPTS      = 12;  // 12 × 5s = 60s
        private const string EVENT_AUTHENTICATED         = "socket:authenticated";

        #endregion

        #region Private Fields

        private SocketIOUnity         _socket;
        private SocketConnectionState _state                = SocketConnectionState.Disconnected;
        private string                _accessToken;
        private string                _currentTableId;
        private bool                  _intentionalDisconnect;
        private int                   _reconnectAttempts;
        private Coroutine             _reconnectCoroutine;

        private readonly Dictionary<string, Action<string>> _eventHandlers = new();

        #endregion

        #region Public Properties

        /// <summary>Current connection state. Read-only — changed only via SetState().</summary>
        public SocketConnectionState State          => _state;
        public bool                  IsConnected    => _state == SocketConnectionState.Connected;
        public bool                  IsReconnecting => _state == SocketConnectionState.Reconnecting;

        public static event Action OnInstanceReady;
        /// <summary>
        /// The tableId the player is currently seated at.
        /// Set by TableJoinHandler after a successful join.
        /// Used by the reconnect flow to know which table to return to.
        /// </summary>
        public string CurrentTableId => _currentTableId;

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

            OnInstanceReady?.Invoke();
        }

        private void OnDestroy()
        {
            DisconnectInternal();
        }

        private void OnApplicationPause(bool paused)
        {
            // App backgrounded while seated at a table.
            // Notify ReconnectHandler immediately so it can fetch the reconnect token
            // while the JWT is still valid — before the socket drops.
            if (paused && IsConnected && !string.IsNullOrEmpty(_currentTableId))
            {
                Debug.Log("[SocketManager] App backgrounded — notifying ReconnectHandler.");
                OnAppBackgrounded?.Invoke(_currentTableId);
            }
        }

        #endregion

        #region Public API — Connection

        /// <summary>
        /// Connect to the Socket.io server with JWT authentication.
        /// JWT is sent in the Socket.io handshake auth object:
        ///   { auth: { token: accessToken } }
        /// On success the server emits socket:authenticated.
        /// On failure (A001) the socket is immediately disconnected by the server.
        /// </summary>
        public void Connect(string accessToken)
        {
            Debug.Log("[SocketManager] Connect called.");
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.LogError("[SocketManager] Cannot connect — access token is null.");
                return;
            }

            if (_state == SocketConnectionState.Connected ||
                _state == SocketConnectionState.Connecting)
            {
                Debug.LogWarning("[SocketManager] Already connected or connecting — ignoring.");
                return;
            }

            _accessToken           = accessToken;
            _intentionalDisconnect = false;

            SetState(SocketConnectionState.Connecting);
            InitialiseSocket();
        }

        /// <summary>
        /// Cleanly disconnect. Call on logout or when leaving a table permanently.
        /// Prevents automatic reconnection from triggering.
        /// </summary>
        public void Disconnect()
        {
            _intentionalDisconnect = true;
            _currentTableId        = null;
            StopReconnectCoroutine();
            DisconnectInternal();
            SetState(SocketConnectionState.Disconnected);
        }

        /// <summary>
        /// Set the table the player is currently seated at.
        /// Called by TableJoinHandler after game:state_update confirms the join.
        /// </summary>
        public void SetCurrentTable(string tableId)
        {
            _currentTableId = tableId;
            Debug.Log($"[SocketManager] Current table: {tableId}");
        }

        /// <summary>Clear the current table context when leaving a table.</summary>
        public void ClearCurrentTable() => _currentTableId = null;

        #endregion

        #region Public API — Events

        /// <summary>
        /// Subscribe to a Socket.io server event.
        /// The handler receives the raw JSON string.
        /// Parse with JsonConvert.DeserializeObject on the calling side.
        /// Callback is guaranteed to run on the Unity main thread.
        /// </summary>
        public void On(string eventName, Action<string> handler)
        {
            if (_socket == null)
            {
                Debug.LogError($"[SocketManager] Cannot subscribe to '{eventName}' — socket not initialised.");
                return;
            }
            _socket.Off(eventName);
            _eventHandlers[eventName] = handler;

            _socket.On(eventName, response =>
            {
                try
                {
                    string json = response.GetValue().ToString() ?? "null";
                    Debug.Log($"[SocketManager] ← {eventName}: {json}");
                    UnityThread.ExecuteInUpdate(() => handler?.Invoke(json));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SocketManager] Error handling '{eventName}': {e.Message}");
                }
            });
        }

        /// <summary>Unsubscribe from a Socket.io server event.</summary>
        public void Off(string eventName)
        {
            if (_socket == null) return;
            _socket.Off(eventName);
            _eventHandlers.Remove(eventName);
        }

        /// <summary>
        /// Emit a Socket.io event with a payload object.
        /// The payload is serialised to JSON automatically.
        /// </summary>
        public void Emit(string eventName, object payload)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[SocketManager] Cannot emit '{eventName}' — not connected.");
                return;
            }

            string json = JsonConvert.SerializeObject(payload);
            var data    = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            _socket.Emit(eventName, data);
            Debug.Log($"[SocketManager] → {eventName}: {json}");
        }

        /// <summary>Emit a Socket.io event with no payload.</summary>

        public void Emit(string eventName)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[SocketManager] Cannot emit '{eventName}' — not connected.");
                return;
            }
            _socket.Emit(eventName);
        }

        #endregion

        #region Socket Initialisation

        private void InitialiseSocket()
        {
            DisconnectInternal();

            string serverUrl = ConfigManager.Instance.Config.webSocketUrl;

            var options = new SocketIOOptions
            {
                // JWT sent in Socket.io handshake auth object
                // Server reads: socket.handshake.auth.token
                Auth = new Dictionary<string, string>
                {
                    { "token", _accessToken }
                },
                Transport            = SocketIOClient.Transport.TransportProtocol.WebSocket,
                ReconnectionAttempts = 0,  // Manual reconnection for full grace period control
                ConnectionTimeout    = TimeSpan.FromSeconds(10)
            };

            _socket = new SocketIOUnity(serverUrl, options);

            BindSocketEvents();
            ReapplyEventHandlers();
            _socket.Connect();

            Debug.Log($"[SocketManager] Connecting to {serverUrl}...");
        }

        // Re-registers all handlers that were subscribed via On() onto the new socket.
        // Required after reconnect — InitialiseSocket creates a fresh _socket instance.
        private void ReapplyEventHandlers()
        {
            foreach (var kvp in _eventHandlers)
            {
                string eventName = kvp.Key;
                Action<string> handler = kvp.Value;
                _socket.On(eventName, response =>
                {
                    try
                    {
                        string json = response.GetValue().ToString();
                        UnityThread.ExecuteInUpdate(() => handler?.Invoke(json));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SocketManager] Error handling '{eventName}': {e.Message}");
                    }
                });
            }
        }

        #endregion

        #region Socket Event Binding

        private void BindSocketEvents()
        {
            _socket.OnConnected += (sender, args) =>
            {
                UnityThread.ExecuteInUpdate(() =>
                    Debug.Log("[SocketManager] Transport connected — awaiting authentication."));
            };

            _socket.On(EVENT_AUTHENTICATED, response =>
            {
                UnityThread.ExecuteInUpdate(() =>
                {
                    try
                    {
                       string json = response.GetValue().ToString();
                        Debug.Log($"[SocketManager] socket:authenticated raw: {json}");

                        var payload = JsonConvert.DeserializeObject<SocketAuthenticatedPayload>(json);

                        _reconnectAttempts = 0;
                        SetState(SocketConnectionState.Connected);

                        Debug.Log($"[SocketManager] Authenticated. " +
                                $"PlayerId: {payload?.PlayerId}, " +
                                $"Username: {payload?.Username}");

                        OnAuthenticated?.Invoke(payload);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SocketManager] Failed to parse socket:authenticated: {e.Message}");
                    }
                });
            });

            _socket.OnDisconnected += (sender, reason) =>
            {
                UnityThread.ExecuteInUpdate(() =>
                {
                    Debug.LogWarning($"[SocketManager] Disconnected — reason: {reason}");

                    if (_intentionalDisconnect)
                    {
                        SetState(SocketConnectionState.Disconnected);
                        return;
                    }

                    // Unexpected disconnect while at a table — begin reconnect sequence
                    if (!string.IsNullOrEmpty(_currentTableId))
                    {
                        Debug.Log("[SocketManager] Unexpected disconnect during game — starting reconnect.");
                        StartReconnection();
                    }
                    else
                    {
                        SetState(SocketConnectionState.Disconnected);
                    }
                });
            };

            _socket.OnError += (sender, error) =>
            {
                UnityThread.ExecuteInUpdate(() =>
                {
                    Debug.LogError($"[SocketManager] Error: {error}");

                    if (!_intentionalDisconnect &&
                        _state == SocketConnectionState.Connecting)
                    {
                        Debug.LogError("[SocketManager] Auth failed (A001) — JWT invalid or expired.");
                        SetState(SocketConnectionState.Disconnected);
                    }
                });
            };

        }

        #endregion

        #region Reconnection

        private void StartReconnection()
        {
            SetState(SocketConnectionState.Reconnecting);
            _reconnectAttempts = 0;
            StopReconnectCoroutine();
            _reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
        }

        private IEnumerator ReconnectCoroutine()
        {
            Debug.Log($"[SocketManager] Reconnect started — " +
                      $"{RECONNECT_MAX_ATTEMPTS} attempts × {RECONNECT_INTERVAL_SECONDS}s = 60s grace period.");

            while (_reconnectAttempts < RECONNECT_MAX_ATTEMPTS)
            {
                _reconnectAttempts++;

                int secondsRemaining = (RECONNECT_MAX_ATTEMPTS - _reconnectAttempts + 1)
                                       * RECONNECT_INTERVAL_SECONDS;

                Debug.Log($"[SocketManager] Reconnect attempt " +
                          $"{_reconnectAttempts}/{RECONNECT_MAX_ATTEMPTS} " +
                          $"(~{secondsRemaining}s remaining)");

                // Notify UI of remaining seconds for countdown overlay
                OnCountdownTick?.Invoke(secondsRemaining);

                InitialiseSocket();

                // Wait up to RECONNECT_INTERVAL_SECONDS for connection
                float elapsed = 0f;
                while (elapsed < RECONNECT_INTERVAL_SECONDS)
                {
                    if (_state == SocketConnectionState.Connected)
                    {
                        Debug.Log("[SocketManager] Reconnect successful.");
                        _reconnectCoroutine = null;
                        yield break;
                    }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // Grace period exhausted
            Debug.LogError("[SocketManager] All reconnect attempts failed. Grace period expired.");
            SetState(SocketConnectionState.Disconnected);
            OnReconnectFailed?.Invoke();
            _reconnectCoroutine = null;
        }

        private void StopReconnectCoroutine()
        {
            if (_reconnectCoroutine == null) return;
            StopCoroutine(_reconnectCoroutine);
            _reconnectCoroutine = null;
        }

        #endregion

        #region State Machine

        /// <summary>
        /// The only method that changes connection state.
        /// Never set _state directly — always go through SetState().
        /// Fires OnStateChanged so all subscribers stay in sync.
        /// </summary>
        private void SetState(SocketConnectionState newState)
        {
            if (_state == newState) return;
            SocketConnectionState previous = _state;
            _state = newState;
            Debug.Log($"[SocketManager] {previous} → {newState}");
            OnStateChanged?.Invoke(newState);
        }

        #endregion

        #region Internal Helpers

        private void DisconnectInternal()
        {
            if (_socket == null) return;
            try
            {
                _socket.Disconnect();
                _socket.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SocketManager] Disconnect error: {e.Message}");
            }
            finally
            {
                _socket = null;
            }
        }

        #endregion
    }
}