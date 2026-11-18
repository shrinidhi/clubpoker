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
    /// <summary>A file returned by <see cref="ApiClient.PostForFile"/>.</summary>
    public class ApiFile
    {
        public byte[] Bytes;
        public string FileName;      // from Content-Disposition; null if missing
        public string ContentType;
    }

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

        /// <summary>
        /// POST that answers with a file (e.g. .xlsx) instead of JSON. 2xx → the raw
        /// bytes plus the file name from Content-Disposition; anything else → the
        /// usual JSON error, thrown as the matching ApiException.
        ///
        /// Never retried: these calls can cost the player (club data export spends
        /// diamonds), so a retry could charge twice. The long timeout is for the
        /// same reason — a big file can take a while, and giving up early would
        /// still be charged.
        /// </summary>
        public async UniTask<ApiFile> PostForFile(string endpoint, object body,
                                                  int timeoutSeconds = 120, bool isRetry = false)
        {
            string url = $"{ConfigManager.Instance.Config.apiBaseUrl}{endpoint}";

            NetworkLogger.LogRequest("POST", url, body);

            if (!isRetry && !string.IsNullOrEmpty(_refreshToken) && IsTokenExpired(_accessToken))
                await RefreshAccessTokenAsync();

            using UnityWebRequest request = CreateRequest(url, "POST", body);
            request.SetRequestHeader("Accept",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, application/json");

            if (!string.IsNullOrEmpty(_accessToken))
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            request.timeout = timeoutSeconds;

            try
            {
                try { await request.SendWebRequest(); }
                catch (UnityWebRequestException) { /* handled below */ }

                long status = request.responseCode;
                bool ok = request.result == UnityWebRequest.Result.Success && status >= 200 && status < 300;

                if (!ok)
                {
                    string json = request.downloadHandler.text;
                    NetworkLogger.LogResponse("POST", url, status, json);

                    if (request.result == UnityWebRequest.Result.ConnectionError)
                        NetworkLogger.LogError("POST", url, request.error);

                    // Expired session: refresh once and resend. A 401 means the
                    // server didn't run the export, so nothing was charged.
                    if (status == 401 && !isRetry)
                    {
                        string code = JsonConvert.DeserializeObject<ApiResponse<object>>(json)?.Error?.Code ?? "";
                        bool credentialError = code == "A006" || code == "A007" ||
                                               code == "A009" || code == "A010";

                        if (!credentialError)
                        {
                            if (await RefreshTokenAsync())
                                return await PostForFile(endpoint, body, timeoutSeconds, isRetry: true);

                            throw new AuthException("A002", "Session expired.");
                        }

                        if (code == "A007")
                            NotifySessionRevoked("superseded");
                    }

                    HandleError(json, status);
                }

                byte[] bytes = request.downloadHandler.data;
                NetworkLogger.LogResponse("POST", url, status, $"<file {bytes?.Length ?? 0} bytes>");

                return new ApiFile
                {
                    Bytes = bytes,
                    FileName = ParseFileName(request.GetResponseHeader("Content-Disposition")),
                    ContentType = request.GetResponseHeader("Content-Type"),
                };
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception e)
            {
                NetworkLogger.LogError("POST", url, e.Message);
                throw new NetworkException("N001", e.Message);
            }
        }

        /// <summary>
        /// File name from a Content-Disposition header. Prefers the RFC 5987
        /// filename*=UTF-8''… form (non-ASCII club names), then filename="…".
        /// Null when the header has neither.
        /// </summary>
        public static string ParseFileName(string contentDisposition)
        {
            if (string.IsNullOrEmpty(contentDisposition))
                return null;

            foreach (string rawPart in contentDisposition.Split(';'))
            {
                string part = rawPart.Trim();
                if (part.StartsWith("filename*=", StringComparison.OrdinalIgnoreCase))
                {
                    string value = part.Substring("filename*=".Length).Trim('"');
                    int quote = value.IndexOf("''", StringComparison.Ordinal);
                    if (quote >= 0) value = value.Substring(quote + 2);
                    try { return Uri.UnescapeDataString(value); }
                    catch { /* fall back to the plain form */ }
                }
            }

            foreach (string rawPart in contentDisposition.Split(';'))
            {
                string part = rawPart.Trim();
                if (part.StartsWith("filename=", StringComparison.OrdinalIgnoreCase))
                    return part.Substring("filename=".Length).Trim().Trim('"');
            }

            return null;
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
                        // CP-337: on sign-in calls A007 is a lockout; anywhere else it
                        // means the account logged in on another device.
                        if (errorCode == "A007" && !endpoint.StartsWith("/api/auth/"))
                            NotifySessionRevoked("superseded");

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

        /// <summary>
        /// The server ended this session. For SocketManager too, which can't reference
        /// Auth. Retrying a revoked session never works, so this logs out instead.
        /// </summary>
        public void NotifySessionRevoked(string reason)
        {
            _authProvider?.HandleSessionRevoked(reason);
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