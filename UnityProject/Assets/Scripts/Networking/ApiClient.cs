using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

namespace ClubPoker.Networking
{
    public class ApiClient : MonoBehaviour
    {
        public static ApiClient Instance { get; private set; }

        private const int REQUEST_TIMEOUT_SECONDS = 10;
        private string _accessToken;
        private string _refreshToken;
        private IAuthProvider _authProvider;

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

        public void SetAuthProvider(IAuthProvider provider)
        {
            _authProvider = provider;
        }   

        // ── Token Management ────────────────────────────────
        public void SetTokens(string accessToken, string refreshToken)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            Debug.Log("[ApiClient] Tokens set successfully");
        }

        public void ClearTokens()
        {
            _accessToken = null;
            _refreshToken = null;
            Debug.Log("[ApiClient] Tokens cleared");
        }

        // ── Public HTTP Methods ──────────────────────────────
        public async UniTask<T> Get<T>(string endpoint)
        {
            return await SendWithRetryAsync<T>(endpoint, "GET", null);
        }
        public async UniTask<T> Get<T>(string endpoint, int cacheTTL = 0)
        {
            // Check cache first
            if (cacheTTL > 0 && ResponseCache.Instance != null)
            {
                if (ResponseCache.Instance.TryGet(endpoint, out string cachedData))
                {
                    Debug.Log($"[ApiClient] Returning cached data for: {endpoint}");
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(cachedData);
                }

                // Cache miss - fetch from server and cache response
                T result = await SendWithRetryAsync<T>(endpoint, "GET", null);

                // Cache the raw response
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                ResponseCache.Instance.Set(endpoint, json, cacheTTL);

                return result;
            }

            // No caching - fetch directly
            return await SendWithRetryAsync<T>(endpoint, "GET", null);
        }

        public async UniTask<T> Post<T>(string endpoint, object body)
        {
            return await SendRequest<T>(endpoint, "POST", body);
        }

        public async UniTask<T> Put<T>(string endpoint, object body)
        {
            return await SendRequest<T>(endpoint, "PUT", body);
        }

        public async UniTask<T> Delete<T>(string endpoint)
        {
            return await SendRequest<T>(endpoint, "DELETE", null);
        }
        
        private async UniTask<T> SendWithRetryAsync<T>(string endpoint, string method, object body)
        {
            const int MAX_RETRIES = 3;
            int attempt = 0;

            while (attempt < MAX_RETRIES)
            {
                try
                {
                    return await SendRequest<T>(endpoint, method, body);
                }
                catch (NetworkException e)
                {
                    attempt++;

                    if (attempt >= MAX_RETRIES)
                    {
                        Debug.LogError($"[ApiClient] Final failure after {MAX_RETRIES} attempts: {e.Message}");
                        throw;
                    }

                    // Exponential backoff: 1s, 2s, 4s
                    int delaySeconds = (int)Math.Pow(2, attempt - 1);
                    Debug.LogWarning($"[ApiClient] Retry {attempt}/{MAX_RETRIES} in {delaySeconds}s...");
                    await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
                catch (ApiException)
                {
                    // Don't retry API errors - throw immediately
                    throw;
                }
            }

            throw new NetworkException("N001", "Request failed after maximum retries");
        }

        // ── Core Request ─────────────────────────────────────
        private async UniTask<T> SendRequest<T>(string endpoint, string method, object body, bool isRetry = false)
        {
            string url = $"{ConfigManager.Instance.Config.apiBaseUrl}{endpoint}";

            NetworkLogger.LogRequest(method, url, body);

            using UnityWebRequest request = CreateRequest(url, method, body);

            if (!string.IsNullOrEmpty(_accessToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
            }

            request.timeout = REQUEST_TIMEOUT_SECONDS;

            try
            {
                // Ignore UnityWebRequest exceptions - we handle manually
                try
                {
                    await request.SendWebRequest();
                }
                catch (UnityWebRequestException)
                {
                    // Handled below
                }

                NetworkLogger.LogResponse(method, url, request.responseCode, request.downloadHandler.text);

                string json = request.downloadHandler.text;

                // Handle 401
                if (request.responseCode == 401 && !isRetry)
                {
                    ApiResponse<object> errorResponse =
                        JsonConvert.DeserializeObject<ApiResponse<object>>(json);

                    string errorCode = errorResponse?.Error?.Code ?? "";
                    bool isCredentialError = errorCode == "A006" || errorCode == "A007";

                    if (isCredentialError)
                    {
                        NetworkLogger.LogTokenRefresh();
                        bool refreshed = await RefreshTokenAsync();
                        if (refreshed)
                        {
                            NetworkLogger.LogTokenRefreshSuccess();
                            return await SendRequest<T>(endpoint, method, body, isRetry: true);
                        }
                        else
                        {
                            NetworkLogger.LogTokenRefreshFailed();
                            GameSceneManager.Instance.LoadScene("Scene_Login");
                            throw new AuthException("A002", "Session expired.");
                        }
                    }
                    else
                    {
                        // A006, A007 etc - HandleError throws correct AuthException
                        HandleError(json, request.responseCode);
                    }
                }

                // Handle other errors
                if (request.result != UnityWebRequest.Result.Success)
                {
                    HandleError(json, request.responseCode);
                }

                // Deserialize response
                ApiResponse<T> response = JsonConvert.DeserializeObject<ApiResponse<T>>(json);

                if (!response.IsSuccess)
                {
                    HandleError(json, request.responseCode);
                }

                return response.Data;
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception e)
            {
                NetworkLogger.LogError(method, url, e.Message);
                throw new NetworkException("N001", e.Message);
            }
        }

        private async UniTask<bool> RefreshTokenAsync()
        {
            if (_authProvider == null)
            {
                Debug.LogError("[ApiClient] No auth provider registered.");
                return false;
            }
            return await _authProvider.RefreshSessionAsync();
        }
       

        // ── Token Refresh ────────────────────────────────────
        // private async UniTask<bool> RefreshTokenAsync()
        // {
        //     try
        //     {
        //         string url = $"{ConfigManager.Instance.Config.apiBaseUrl}/api/auth/refresh";
        //         var body = new { refreshToken = _refreshToken };
        //         string json = JsonConvert.SerializeObject(body);

        //         using UnityWebRequest request = new UnityWebRequest(url, "POST");
        //         byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        //         request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        //         request.downloadHandler = new DownloadHandlerBuffer();
        //         request.SetRequestHeader("Content-Type", "application/json");
        //         request.timeout = 10;

        //         await request.SendWebRequest();

        //         if (request.result == UnityWebRequest.Result.Success)
        //         {
        //             string responseJson = request.downloadHandler.text;
        //             ApiResponse<TokenPair> response = 
        //                 JsonConvert.DeserializeObject<ApiResponse<TokenPair>>(responseJson);

        //             if (response.IsSuccess)
        //             {
        //                 SetTokens(response.Data.AccessToken, response.Data.RefreshToken);
        //                 Debug.Log("[ApiClient] Token refreshed successfully!");
        //                 NetworkLogger.LogTokenRefreshSuccess();
        //                 return true;
        //             }
        //         }

        //         return false;
        //     }
        //     catch (Exception e)
        //     {
        //         NetworkLogger.LogTokenRefreshFailed();
        //         return false;
        //     }
        // }

        // ── Helper Methods ───────────────────────────────────
        private UnityWebRequest CreateRequest(string url, string method, object body)
        {
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                string json = JsonConvert.SerializeObject(body);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        private void HandleError(string json, long responseCode)
        {
            try
            {
                ApiResponse<object> error = 
                    JsonConvert.DeserializeObject<ApiResponse<object>>(json);

                string code = error?.Error?.Code ?? "N001";
                string message = error?.Error?.Message ?? "Unknown error";
                int? lockout = error?.Error?.LockoutRemainingSeconds;

                // Map error code prefix to typed exception
                string prefix = code.Length > 0 ? code[0].ToString() : "N";

                switch (prefix)
                {
                    case "A":
                        throw new AuthException(code, message);
                    case "G":
                        throw new GameException(code, message, lockout);
                    case "V":
                        throw new ValidationException(code, message);
                    case "E":
                        throw new EconomyException(code, message);
                    case "L":
                        throw new LobbyException(code, message);
                    case "S":
                        throw new SystemApiException(code, message);
                    default:
                        throw new NetworkException(code, message);
                }
            }
            catch (ApiException)
            {
                throw;
            }
            catch
            {
                throw new NetworkException("N001", $"HTTP {responseCode} error");
            }
        }
    }
}

// Lobby - 30 second cache
// var tables = await ApiClient.Instance
//     .Get<LobbyData>("/api/lobby/tables", 
//         ResponseCache.Instance.GetLobbyTTL());

// // Leaderboard - 60 second cache
// var leaderboard = await ApiClient.Instance
//     .Get<LeaderboardData>("/api/leaderboard", 
//         ResponseCache.Instance.GetLeaderboardTTL());

// // Profile - no cache
// var profile = await ApiClient.Instance
//     .Get<PlayerData>("/api/player/profile");