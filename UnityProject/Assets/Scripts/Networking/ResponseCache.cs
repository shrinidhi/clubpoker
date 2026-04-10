using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Networking
{
    public class CacheEntry
    {
        public string Data { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public class ResponseCache : MonoBehaviour
    {
        #region Singleton

        public static ResponseCache Instance { get; private set; }

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

        #endregion

        #region Constants

        private const int MAX_CACHE_SIZE = 50;
        private const int LOBBY_TTL_SECONDS = 30;
        private const int LEADERBOARD_TTL_SECONDS = 60;

        #endregion

        #region Private Fields

        private Dictionary<string, CacheEntry> _cache = 
            new Dictionary<string, CacheEntry>();

        #endregion

        #region Public Methods

        public void Set(string key, string data, int ttlSeconds)
        {
            // LRU eviction if cache is full
            if (_cache.Count >= MAX_CACHE_SIZE)
            {
                EvictLRU();
            }

            _cache[key] = new CacheEntry
            {
                Data = data,
                ExpiresAt = DateTime.UtcNow.AddSeconds(ttlSeconds),
                LastAccessed = DateTime.UtcNow
            };

            Debug.Log($"[ResponseCache] Cached: {key} (TTL: {ttlSeconds}s)");
        }

        public bool TryGet(string key, out string data)
        {
            if (_cache.TryGetValue(key, out CacheEntry entry))
            {
                if (!entry.IsExpired)
                {
                    // Update last accessed for LRU
                    entry.LastAccessed = DateTime.UtcNow;
                    data = entry.Data;
                    Debug.Log($"[ResponseCache] Cache hit: {key}");
                    return true;
                }
                else
                {
                    // Expired - remove from cache
                    _cache.Remove(key);
                    Debug.Log($"[ResponseCache] Cache expired: {key}");
                }
            }

            data = null;
            return false;
        }

        public void Invalidate(string key)
        {
            if (_cache.ContainsKey(key))
            {
                _cache.Remove(key);
                Debug.Log($"[ResponseCache] Invalidated: {key}");
            }
        }

        public void InvalidateAll()
        {
            _cache.Clear();
            Debug.Log("[ResponseCache] Cache cleared");
        }

        public int GetLobbyTTL() => LOBBY_TTL_SECONDS;
        public int GetLeaderboardTTL() => LEADERBOARD_TTL_SECONDS;

        #endregion

        #region Private Methods

        private void EvictLRU()
        {
            string lruKey = null;
            DateTime oldestAccess = DateTime.MaxValue;

            foreach (var entry in _cache)
            {
                if (entry.Value.LastAccessed < oldestAccess)
                {
                    oldestAccess = entry.Value.LastAccessed;
                    lruKey = entry.Key;
                }
            }

            if (lruKey != null)
            {
                _cache.Remove(lruKey);
                Debug.Log($"[ResponseCache] LRU evicted: {lruKey}");
            }
        }

        #endregion
    }
}