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
        public string AccessToken => _accessToken;

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

            // A token we can already see is expired would just earn a 401 (and count
            // as a failed auth on the server). Refresh first. Auth endpoints are
            // skipped — the refresh call itself goes through here. If the refresh
            // fails, the request still goes out and the normal 401 path handles it.
            if (!isRetry && !endpoint.StartsWith("/api/auth/") &&
                !string.IsNullOrEmpty(_refreshToken) && IsTokenExpired(_accessToken))
            {
                Debug.Log("[ApiClient] Access token expired — refreshing before request.");
                await RefreshAccessTokenAsync();
            }

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

                // No HTTP response at all (server down, DNS, timeout, SSL) — the body
                // is empty, so log Unity's reason or the log says nothing useful.
                if (request.result == UnityWebRequest.Result.ConnectionError)
                    NetworkLogger.LogError(method, url, request.error);

                string json = request.downloadHandler.text;

                // Handle 401
                if (request.responseCode == 401 && !isRetry)
                {
                    ApiResponse<object> errorResponse =
                        JsonConvert.DeserializeObject<ApiResponse<object>>(json);

                    string errorCode = errorResponse?.Error?.Code ?? "";

                    Debug.Log($"[ApiClient] 401 received. ErrorCode: '{errorCode}'"); // ← add this
                    // A009: forgot-password reset token invalid; A010: Google token not
                    // verified — sign-in failures, not an expired session.
                    bool isCredentialError = errorCode == "A006" || errorCode == "A007" ||
                                             errorCode == "A009" || errorCode == "A010";

                    Debug.Log($"[ApiClient] isCredentialError: {isCredentialError}"); // ← add this

                    if (!isCredentialError)
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
                            // Don't navigate here. A refresh can fail because the
                            // token is dead OR because the network is down, and this
                            // layer can't tell them apart. AuthManager logs out (and
                            // routes to Login) only when the server actually rejects
                            // the token; an offline blip must not end the session.
                            NetworkLogger.LogTokenRefreshFailed();
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

        /// <summary>
        /// Refresh the session now. For SocketManager, which can't reference Auth —
        /// used when the server rejects the socket handshake's token.
        /// </summary>
        public async UniTask<bool> RefreshAccessTokenAsync()
        {
            bool ok = await RefreshTokenAsync();

            // A token the server just issued can't really be expired — the device
            // clock is off. Stop judging expiry locally for this run, or every
            // request / socket connect would refresh again (and Connect would loop).
            if (ok && !_deviceClockUntrusted && IsTokenExpired(_accessToken))
            {
                _deviceClockUntrusted = true;
                Debug.LogWarning("[ApiClient] Fresh token looks expired — device clock is off. " +
                                 "Local expiry checks disabled; relying on server 401s.");
            }

            return ok;
        }

        private static bool _deviceClockUntrusted;

        /// <summary>
        /// True when the JWT's exp claim has passed (with a small margin for clock
        /// skew and request time). Unreadable or non-JWT tokens count as NOT
        /// expired, so this can only skip a doomed request, never block a good one.
        /// Always false once the device clock has been found to be wrong.
        /// </summary>
        public static bool IsTokenExpired(string jwt, int skewSeconds = 30)
        {
            if (_deviceClockUntrusted || string.IsNullOrEmpty(jwt))
                return false;

            string[] parts = jwt.Split('.');
            if (parts.Length != 3)
                return false;

            try
            {
                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var exp = Newtonsoft.Json.Linq.JObject.Parse(json)["exp"];
                if (exp == null)
                    return false;

                return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp.ToObject<long>() - skewSeconds;
            }
            catch
            {
                return false;
            }
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
                string message = error?.Error?.Message ?? Loc.Get("something_went_wrong");
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