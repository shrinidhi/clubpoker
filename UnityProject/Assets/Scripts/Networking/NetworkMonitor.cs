using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace ClubPoker.Networking
{
    public enum NetworkState
    {
        Online,
        Offline
    }

    public class NetworkMonitor : MonoBehaviour
    {
        #region Singleton

        public static NetworkMonitor Instance { get; private set; }

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

        private void Start() 
        {
            StartMonitoring();
        }

        #endregion

        #region Constants

        private const float CHECK_INTERVAL_SECONDS = 2f;

        #endregion

        #region Properties

        public NetworkState CurrentState { get; private set; } = NetworkState.Online;
        public bool IsOnline => CurrentState == NetworkState.Online;

        #endregion

        #region Events

        public event Action OnWentOffline;
        public event Action OnCameOnline;

        #endregion

        #region Private Fields

        private Queue<Func<UniTask>> _requestQueue = new Queue<Func<UniTask>>();
        private bool _isMonitoring = false;

        #endregion

        #region Public Methods

        public void StartMonitoring()
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
            MonitorNetworkAsync().Forget();
            Debug.Log("[NetworkMonitor] Started monitoring network state");
        }

        // Queue non-critical requests (lobby refresh, leaderboard etc)
        public void QueueRequest(Func<UniTask> request)
        {
            if (IsOnline)
            {
                // Online - execute immediately
                request().Forget();
            }
            else
            {
                // Offline - queue for later
                _requestQueue.Enqueue(request);
                Debug.Log($"[NetworkMonitor] Request queued. Queue size: {_requestQueue.Count}");
            }
        }

        // Block game-critical requests
        public bool CanMakeGameCriticalRequest()
        {
            if (!IsOnline)
            {
                Debug.LogWarning("[NetworkMonitor] Game critical request blocked - offline!");
                return false;
            }
            return true;
        }

        #endregion

        #region Private Methods

        private async UniTaskVoid MonitorNetworkAsync()
        {
            while (_isMonitoring)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS));

                CheckNetworkState();
            }
        }

        private void CheckNetworkState()
        {
            bool isReachable = Application.internetReachability 
                != NetworkReachability.NotReachable;

            if (isReachable && CurrentState == NetworkState.Offline)
            {
                // Came back online
                CurrentState = NetworkState.Online;
                Debug.Log("[NetworkMonitor] Network restored!");
                OnCameOnline?.Invoke();
                DrainQueueAsync().Forget();
            }
            else if (!isReachable && CurrentState == NetworkState.Online)
            {
                // Went offline
                CurrentState = NetworkState.Offline;
                Debug.LogWarning("[NetworkMonitor] Network lost!");
                OnWentOffline?.Invoke();
            }
        }

        private async UniTaskVoid DrainQueueAsync()
        {
            Debug.Log($"[NetworkMonitor] Draining queue - {_requestQueue.Count} requests");

            while (_requestQueue.Count > 0 && IsOnline)
            {
                Func<UniTask> request = _requestQueue.Dequeue();
                try
                {
                    await request();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkMonitor] Queued request failed: {e.Message}");
                }
            }

            Debug.Log("[NetworkMonitor] Queue drained!");
        }

        #endregion
    }
}