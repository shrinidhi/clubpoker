//
// Responsibilities:
//   - Emit player:join_table after socket:authenticated
//   - Wait for game:state_update confirmation before navigating to GameTableScene
//   - Handle G005 (table full) and G007 (table not found) errors
//   - 10-second timeout if no confirmation received
//
// Join flow:
//   1. Buy-in REST call succeeds (Economy system)
//   2. JoinTable(tableId) called
//   3. socket:authenticated fires → emit player:join_table
//   4. Server broadcasts game:state_update to room
//   5. OnStateUpdateReceived → set current table → navigate to GameTableScene
//   6. G005/G007 → show error → stay in Lobby

using System;
using System.Collections;
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

        /// <summary>
        /// Fired when game:state_update is received after joining.
        /// GameTableScene subscribes to populate the initial game state.
        /// </summary>
        public static event Action<GameStateUpdatePayload> OnTableJoined;

        /// <summary>
        /// Fired when join fails due to a server error (G005, G007).
        /// Lobby UI subscribes to show the appropriate error message.
        /// </summary>
        public static event Action<string> OnJoinFailed;

        #endregion

        #region Constants

        private const float  JOIN_TIMEOUT_SECONDS   = 10f;
        private const string SCENE_GAME_TABLE        = "Scene_GameTable";
        private const string EVENT_JOIN_TABLE        = "player:join_table";
        private const string EVENT_STATE_UPDATE      = "game:state_update";
        private const string EVENT_GAME_ERROR        = "game:error";

        #endregion

        #region Private Fields

        private string    _pendingTableId;
        private bool      _waitingForConfirmation;
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
                SocketManager.Instance.OnAuthenticated += OnSocketAuthenticated;
            else
                SocketManager.OnInstanceReady += OnSocketManagerReady;

        }
        private void OnSocketManagerReady()
        {
            SocketManager.OnInstanceReady -= OnSocketManagerReady;
            SocketManager.Instance.OnAuthenticated += OnSocketAuthenticated;
        }


        private void OnDestroy()
        {
            if (SocketManager.Instance != null)
            {
                SocketManager.Instance.OnAuthenticated -= OnSocketAuthenticated;
                SocketManager.Instance.Off(EVENT_STATE_UPDATE);
                SocketManager.Instance.Off(EVENT_GAME_ERROR);
            }
            SocketManager.OnInstanceReady -= OnSocketManagerReady;
        }

        private void OnEnable()
        {

        }

        private void OnDisable()
        {
            
        }

        #endregion

        #region Public API

        /// <summary>
        /// Begin the table join flow.
        /// Call this after a successful buy-in REST call.
        /// If the socket is already connected, emits player:join_table immediately.
        /// If not yet connected, stores tableId and waits for socket:authenticated.
        /// </summary>
        public void JoinTable(string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
            {
                Debug.LogError("[TableJoinHandler] Cannot join — tableId is null.");
                return;
            }

            _pendingTableId        = tableId;
            _waitingForConfirmation = false;

            Debug.Log($"[TableJoinHandler] Joining table: {tableId}");

            if (SocketManager.Instance.IsConnected)
            {
                // Socket already authenticated — emit immediately
                EmitJoinTable(tableId);
            }
            else
            {
                // Socket not ready — will emit when socket:authenticated fires
                Debug.Log("[TableJoinHandler] Socket not connected — waiting for authentication.");
            }
        }

        #endregion

        #region Socket Event Handlers

        private void OnSocketAuthenticated(SocketAuthenticatedPayload payload)
        {
            Debug.Log($"[TableJoinHandler] Socket authenticated as player: {payload.Username} (ID: {payload.PlayerId})");
            SocketManager.Instance.On(EVENT_STATE_UPDATE, OnStateUpdateReceived);
            SocketManager.Instance.On(EVENT_GAME_ERROR,   OnGameErrorReceived);

            Debug.Log("[TableJoinHandler] Socket events registered"); // ← add this

            // Only handle if we have a pending table join
            if (string.IsNullOrEmpty(_pendingTableId)) return;

            EmitJoinTable(_pendingTableId);
        }

        private void EmitJoinTable(string tableId)
        {
            if (_waitingForConfirmation)
            {
                Debug.LogWarning("[TableJoinHandler] Already waiting for join confirmation.");
                return;
            }

            _waitingForConfirmation = true;

            var payload = new PlayerJoinTablePayload
            {
                TableId  = tableId,
                PlayerId = GetCurrentPlayerId()
            };

            Debug.Log($"[TableJoinHandler] TableId length: {tableId.Length}, Value: '{tableId}'");

            SocketManager.Instance.Emit(EVENT_JOIN_TABLE, payload);

            Debug.Log($"[TableJoinHandler] Emitted player:join_table for table: {tableId}");

            // Start timeout — if no game:state_update within 10s treat as failure
            StopTimeoutCoroutine();
            _timeoutCoroutine = StartCoroutine(JoinTimeoutCoroutine());
        }

        private void OnStateUpdateReceived(string json)
        {
            Debug.Log($"[TableJoinHandler] state_update received: {json}");
            if (!_waitingForConfirmation) return;

            try
            {
                var state = JsonConvert.DeserializeObject<GameStateUpdatePayload>(json);

                StopTimeoutCoroutine();
                _waitingForConfirmation = false;

                // Register the table with SocketManager for reconnect tracking
                SocketManager.Instance.SetCurrentTable(state.TableId);

                Debug.Log($"[TableJoinHandler] Join confirmed. Table: {state.TableId}, " +
                          $"State: {state.GameState}, Players: {state.Players?.Count}");

                // Fire event so GameTableScene can populate initial state
                OnTableJoined?.Invoke(state);

                // Navigate to game table
                GameSceneManager.Instance.LoadScene(SCENE_GAME_TABLE);

                // Clear pending join
                _pendingTableId = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TableJoinHandler] Failed to parse game:state_update: {e.Message}");
                HandleJoinFailure("Failed to load game state. Please try again.");
            }
        }

        private void OnGameErrorReceived(string json)
        {
            if (!_waitingForConfirmation) return;

            try
            {
                var error = JsonConvert.DeserializeObject<GameErrorPayload>(json);
                Debug.LogWarning($"[TableJoinHandler] Join failed — {error?.Code}: {error?.Message}");
                HandleJoinFailure(error?.Message ?? "Could not join table. Please try again.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TableJoinHandler] Failed to parse game:error: {e.Message}");
                HandleJoinFailure("Could not join table. Please try again.");
            }
        }

        #endregion

        #region Timeout

        private IEnumerator JoinTimeoutCoroutine()
        {
            yield return new WaitForSeconds(JOIN_TIMEOUT_SECONDS);

            if (_waitingForConfirmation)
            {
                Debug.LogError("[TableJoinHandler] Join confirmation timed out after " +
                               $"{JOIN_TIMEOUT_SECONDS}s.");
                HandleJoinFailure("Could not connect to the table. Please try again.");
            }
        }

        private void StopTimeoutCoroutine()
        {
            if (_timeoutCoroutine == null) return;
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        #endregion

        #region Failure Handling

        private void HandleJoinFailure(string message)
        {
            StopTimeoutCoroutine();
            _waitingForConfirmation = false;
            _pendingTableId         = null;

            SocketManager.Instance.ClearCurrentTable();
            OnJoinFailed?.Invoke(message);

            Debug.LogWarning($"[TableJoinHandler] Join failed: {message}");
        }

        #endregion

        #region Helpers

        private string GetCurrentPlayerId()
        {
            // Access session via AuthManager — always use this rather than
            // caching the ID locally to ensure it stays in sync
            return Auth.AuthManager.Instance?.Session?.Id ?? string.Empty;
        }

        #endregion
    }
}