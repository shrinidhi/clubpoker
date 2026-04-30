using System;
using UnityEngine;
using ClubPoker.Networking;

namespace ClubPoker.Game
{
    public class StateSyncHandler : MonoBehaviour
    {
        public static StateSyncHandler Instance { get; private set; }

        private const string EVENT_REQUEST_STATE = "player:request_state";
        private const float DUPLICATE_BLOCK_SECONDS = 5f;
        private const float BACKGROUND_THRESHOLD_SECONDS = 30f;

        private float _lastRequestTime = -999f;
        private float _backgroundStartTime;
        private bool _wasBackgrounded;

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
            if (ReconnectHandler.Instance != null)
            {
                ReconnectHandler.Instance.OnReconnectSuccess += OnReconnectSuccess;
            }
        }

        private void OnDisable()
        {
            if (ReconnectHandler.Instance != null)
            {
                ReconnectHandler.Instance.OnReconnectSuccess -= OnReconnectSuccess;
            }
        }

        /// <summary>
        /// Called automatically after reconnect success
        /// </summary>
        private void OnReconnectSuccess(ClubPoker.Networking.Models.GameStateUpdatePayload state)
        {
            Debug.Log("[StateSync] Reconnect success → requesting full sync");
            RequestState();
        }

        /// <summary>
        /// Detect app background / foreground
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _backgroundStartTime = Time.realtimeSinceStartup;
                _wasBackgrounded = true;

                Debug.Log("[StateSync] App moved to background");
            }
            else
            {
                if (!_wasBackgrounded)
                    return;

                float backgroundDuration =
                    Time.realtimeSinceStartup - _backgroundStartTime;

                Debug.Log($"[StateSync] App resumed after {backgroundDuration} sec");

                if (backgroundDuration >= BACKGROUND_THRESHOLD_SECONDS)
                {
                    Debug.Log("[StateSync] Long background detected → requesting state sync");
                    RequestState();
                }

                _wasBackgrounded = false;
            }
        }

        /// <summary>
        /// Public manual full sync request
        /// </summary>
        public void RequestState()
        {
            if (SocketManager.Instance == null)
            {
                Debug.LogError("[StateSync] SocketManager NULL");
                return;
            }

            if (!SocketManager.Instance.IsConnected)
            {
                Debug.LogWarning("[StateSync] Cannot request state → socket disconnected");
                return;
            }

            string tableId = SocketManager.Instance.CurrentTableId;

            if (string.IsNullOrEmpty(tableId))
            {
                Debug.LogWarning("[StateSync] No active table");
                return;
            }

            if (Time.time - _lastRequestTime < DUPLICATE_BLOCK_SECONDS)
            {
                Debug.LogWarning("[StateSync] Duplicate request blocked (5 sec rule)");
                return;
            }

            _lastRequestTime = Time.time;

            var payload = new System.Collections.Generic.Dictionary<string, object>()
            {
                { "tableId", tableId }
            };

            Debug.Log($"[StateSync] Emit player:request_state → Table: {tableId}");

            SocketManager.Instance.Emit(EVENT_REQUEST_STATE, payload);
        }
    }
}