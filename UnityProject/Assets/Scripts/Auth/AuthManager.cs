
using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Globalization;

namespace ClubPoker.Auth
{
    /// <summary>
    /// Singleton MonoBehaviour that owns all authentication flows:
    /// registration, login, logout, silent token refresh, and guest sessions.
    ///
    /// Views call AuthManager methods and react to the returned result objects
    /// defined in AuthViewModels.cs.
    ///
    /// AuthManager is the ONLY class that reads and writes TokenStore.
    /// ApiClient is the ONLY class that makes HTTP calls.
    /// </summary>
    public class AuthManager : MonoBehaviour, IAuthProvider
    {
        public static AuthManager Instance { get; private set; }

        /// <summary>
        /// The current player's runtime session.
        /// Read by any system that needs identity (lobby, game, UI chips display).
        /// </summary>

        public UserSession Session { get; set; } = new UserSession();

        // Ensures only one token refresh runs at a time.
        // If multiple requests hit 401 simultaneously, the first acquires the
        // lock and refreshes. The rest wait, then return true since the token
        // is already fresh by the time they proceed.
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private bool _isRefreshing;

        // ── Lifecycle ─────────────────────────────────────────────────────────

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
            ApiClient.Instance.SetAuthProvider(this);
            RestoreSessionFromStorage();
        }

        /// <summary>
        /// On cold start, check TokenStore for a persisted token and push it
        /// into ApiClient so authenticated requests work immediately without
        /// the user needing to log in again.
        /// </summary>
        private void RestoreSessionFromStorage()
        {
            string accessToken = TokenStore.LoadAccessToken();
            if (!string.IsNullOrEmpty(accessToken))
            {
                string refreshToken = TokenStore.LoadRefreshToken();
                ApiClient.Instance.SetTokens(accessToken, refreshToken);
                Debug.Log("[AuthManager] Session restored from storage.");

                Session = new UserSession { Id = "restored" };
                return;
            }

            string guestToken = TokenStore.LoadGuestToken();
            if (!string.IsNullOrEmpty(guestToken))
            {
                ApiClient.Instance.SetTokens(guestToken, null);
                Session.IsGuest = true;
                Debug.Log("[AuthManager] Guest session restored from storage.");
            }
        }

        // ── Register ──────────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/register
        ///
        /// Request:  { username, email, password }
        /// Response: { status: "ok", data: { player: {...}, tokens: {...} } }
        ///
        /// Success → tokens stored, session populated, 1000 chip bonus returned.
        /// U001    → duplicate username, view highlights username field.
        /// U002    → duplicate email, view highlights email field.
        /// V001    → validation error e.g. weak password.
        /// A007    → rate limited, too many register attempts.
        /// </summary>
        public async UniTask<RegisterResult> RegisterAsync(
            string username, string email, string password, bool rememberMe = true)
        {
            try
            {
                var request = new RegisterRequest
                {
                    Username = username,
                    Email = email,
                    Password = password
                };

                // Register returns the same player + tokens shape as login
                LoginResponse data =
                    await ApiClient.Instance.Post<LoginResponse>(
                        "/api/auth/register", request);

                TokenStore.SaveTokens(
                    data.Tokens.AccessToken,
                    data.Tokens.RefreshToken,
                    rememberMe);

                ApiClient.Instance.SetTokens(
                    data.Tokens.AccessToken,
                    data.Tokens.RefreshToken);

                Session = UserSession.From(data.Player);

                SocketManager.Instance.Connect(data.Tokens.AccessToken);

                Debug.Log($"[AuthManager] Register success. User: {data.Player.Username}");

                return new RegisterResult { Success = true };
            }
            catch (ValidationException e)
            {
                // V001 weak password — U001/U002 duplicate field errors
                // View switches on ErrorCode to highlight the correct field
                Debug.LogWarning($"[AuthManager] Register validation error: {e.Code} — {e.Message}");
                return new RegisterResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (AuthException e) when (e.Code == "A007")
            {
                Debug.LogWarning("[AuthManager] Register rate limited.");
                return new RegisterResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (ApiException e)
            {
                Debug.LogError($"[AuthManager] Register failed: {e.Code} — {e.Message}");
                return new RegisterResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthManager] Register unexpected error: {e.Message}");
                return new RegisterResult
                {
                    Success = false,
                    ErrorCode = "N001",
                    ErrorMessage = "Network error. Please try again."
                };
            }
        }

        // ── Login ─────────────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/login
        ///
        /// Request:  { email, password }
        /// Response: { status: "success", data: { player: {...}, tokens: {...} } }
        ///
        /// Success → tokens stored, session populated.
        /// A006    → wrong password, view shows inline error on password field.
        /// A007    → account locked, view shows lockout countdown timer.
        /// </summary>
        public async UniTask<LoginResult> LoginAsync(
            string email, string password, bool rememberMe = true)
        {
            try
            {
                var request = new LoginRequest
                {
                    Email = email,
                    Password = password
                };

                LoginResponse data =
                    await ApiClient.Instance.Post<LoginResponse>(
                        "/api/auth/login", request);

                TokenStore.SaveTokens(
                    data.Tokens.AccessToken,
                    data.Tokens.RefreshToken,
                    rememberMe);

                ApiClient.Instance.SetTokens(
                    data.Tokens.AccessToken,
                    data.Tokens.RefreshToken);

                Session = UserSession.From(data.Player);

                Debug.Log($"[AuthManager] Login success. User: {data.Player.Username}");

                if (SocketManager.Instance != null)
                    SocketManager.Instance.Connect(data.Tokens.AccessToken);

                return new LoginResult { Success = true };
            }
            catch (AuthException e) when (e.Code == "A007")
            {
                // Account locked — pass remaining seconds to view for countdown
                Debug.LogWarning($"[AuthManager] Account locked. Remaining: {e.LockoutRemainingSeconds}s");
                return new LoginResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message,
                    LockoutRemainingSeconds = e.LockoutRemainingSeconds
                };
            }
            catch (AuthException e) when (e.Code == "A006")
            {
                Debug.LogWarning("[AuthManager] Login failed: wrong password.");
                return new LoginResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (ApiException e)
            {
                Debug.LogError($"[AuthManager] Login failed: {e.Code} — {e.Message}");
                return new LoginResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthManager] Login unexpected error: {e.Message}");
                return new LoginResult
                {
                    Success = false,
                    ErrorCode = "N001",
                    ErrorMessage = "Network error. Please try again."
                };
            }
        }

        // ── Silent Token Refresh ──────────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/refresh
        ///
        /// Request:  { refreshToken }
        /// Response: { status: "ok", data: { accessToken, refreshToken } }
        ///
        /// Called by ApiClient's 401 interceptor — never by views directly.
        /// SemaphoreSlim guarantees only one refresh runs at a time.
        /// On failure, LogoutAsync(callServer: false) is called automatically.
        ///
        /// Error codes from this endpoint:
        ///   A001 — token invalid or malformed
        ///   A002 — refresh token expired
        /// Both mean the session is unrecoverable — force logout in both cases.
        /// </summary>
        public async UniTask<bool> RefreshSessionAsync()
        {
            bool gotLock = await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10));
            if (!gotLock)
            {
                Debug.LogWarning("[AuthManager] Refresh lock timed out.");
                return false;
            }

            // If another caller already completed the refresh while we waited,
            // the token is already fresh — return true without refreshing again
            if (_isRefreshing)
            {
                _refreshLock.Release();
                return !string.IsNullOrEmpty(TokenStore.LoadRefreshToken());
            }

            _isRefreshing = true;
            try
            {
                string storedRefreshToken = TokenStore.LoadRefreshToken();
                if (string.IsNullOrEmpty(storedRefreshToken))
                {
                    Debug.LogWarning("[AuthManager] No refresh token in storage.");
                    return false;
                }

                var request = new RefreshTokenRequest
                {
                    RefreshToken = storedRefreshToken
                };

                // Refresh response is flat on data — not nested under player/tokens
                RefreshTokenResponse data =
                    await ApiClient.Instance.Post<RefreshTokenResponse>(
                        "/api/auth/refresh", request);

                bool rememberMe = TokenStore.HasRememberMe();
                TokenStore.SaveTokens(data.AccessToken, data.RefreshToken, rememberMe);
                ApiClient.Instance.SetTokens(data.AccessToken, data.RefreshToken);

                if (SocketManager.Instance != null && !SocketManager.Instance.IsConnected)
                        SocketManager.Instance.Connect(data.AccessToken);
                Debug.Log("[AuthManager] Token refreshed successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthManager] Token refresh failed: {e.Message}");
                await LogoutAsync(callServer: false);
                return false;
            }
            finally
            {
                _isRefreshing = false;
                _refreshLock.Release();
            }
        }

        // ── Logout ────────────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/logout
        ///
        /// Request:  { refreshToken }
        /// Response: { status: "ok", data: { message: "Logged out successfully" } }
        ///
        /// Full logout sequence:
        ///   1. POST /api/auth/logout — blacklists token server-side (best effort)
        ///   2. TokenStore.ClearAll() — wipes all encrypted tokens from PlayerPrefs
        ///   3. ApiClient.ClearTokens() — clears in-memory tokens
        ///   4. ResponseCache.Clear() — clears cached response data
        ///   5. Session reset
        ///   6. Navigate to LoginScene
        ///
        /// Pass callServer: false when triggered by a failed refresh — the token
        /// is already invalid server-side so the network call is pointless.
        /// </summary>
        public async UniTask LogoutAsync(bool callServer = true)
        {
            Debug.Log("[AuthManager] Logging out...");

            if (callServer)
            {
                try
                {
                    string refreshToken = TokenStore.LoadRefreshToken();
                    var request = new LogoutRequest { RefreshToken = refreshToken };
                    await ApiClient.Instance.Post<object>("/api/auth/logout", request);
                }
                catch (Exception e)
                {
                    // Server call failing must never block local cleanup
                    Debug.LogWarning($"[AuthManager] Server logout failed (continuing): {e.Message}");
                }
            }

            TokenStore.ClearAll();
            ApiClient.Instance.ClearTokens();

            if (ResponseCache.Instance != null)
                ResponseCache.Instance.InvalidateAll();

            Session = new UserSession();

            SocketManager.Instance.Disconnect();
            Debug.Log("[AuthManager] Logout complete.");
            GameSceneManager.Instance.LoadScene("LoginScene");
        }

        // ── Guest Session ─────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/guest
        ///
        /// Response: { status: "success", data: {
        ///     player: { id, username, avatar, walletChips, isGuest: true },
        ///     tokens: { accessToken, expiresIn: 7200 },   ← no refreshToken
        ///     guestId, guestChips, temporary: true
        /// }}
        ///
        /// Guest sessions cannot be refreshed — expiry is final.
        /// Expiry is calculated from server-provided expiresIn seconds.
        /// Restricted features: Leaderboard, HandHistory, ProfileEdit.
        /// </summary>
        public async UniTask<AuthResult> LoginAsGuestAsync()
        {
            try
            {
                GuestResponseData data =
                    await ApiClient.Instance.Post<GuestResponseData>(
                        "/api/auth/guest", null);

                // Calculate expiry from server-provided seconds rather than hardcoding
                DateTime expiresAt = DateTime.UtcNow.AddSeconds(data.Tokens.ExpiresIn);

                TokenStore.SaveGuestToken(data.Tokens.AccessToken, expiresAt);
                ApiClient.Instance.SetTokens(data.Tokens.AccessToken, null);

                Session = UserSession.FromGuest(data.Player);

                SocketManager.Instance.Connect(data.Tokens.AccessToken);

                Debug.Log($"[AuthManager] Guest session created. " +
                          $"User: {data.Player.Username}, Expires: {expiresAt:u}");

                return AuthResult.Ok();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthManager] Guest login failed: {e.Message}");
                return AuthResult.Fail("N001", "Could not create guest session. Please try again.");
            }
        }

        // ── Guest helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the current guest session has expired.
        /// Call this on app resume or before navigating to any restricted feature.
        /// </summary>
        public bool IsGuestSessionExpired()
        {
            if (!Session.IsGuest) return false;
            return TokenStore.GuestTimeRemaining() == TimeSpan.Zero;
        }

        /// <summary>
        /// Returns time remaining on the guest session.
        /// Used by the UI to display a countdown timer.
        /// </summary>
        public TimeSpan GuestTimeRemaining() => TokenStore.GuestTimeRemaining();

        /// <summary>
        /// Returns true if the feature is blocked for guest users.
        /// Views call this before rendering a screen — if true, show the
        /// upgrade prompt instead of the actual feature content.
        /// </summary>
        public bool IsFeatureRestrictedForGuest(GuestRestrictedFeature feature)
        {
            if (!Session.IsGuest) return false;
            return feature switch
            {
                GuestRestrictedFeature.Leaderboard => true,
                GuestRestrictedFeature.HandHistory => true,
                GuestRestrictedFeature.ProfileEdit => true,
                _ => false
            };
        }

        // ── Profile ─────────────────────────────────────────────────────
        public async UniTask<PlayerData> GetProfileAsync()
        {
            try
            {
                var profile = await ApiClient.Instance.Get<PlayerData>("/api/player/profile");

                Debug.Log("[AuthManager] Profile Loaded: " + profile.Username);

                return profile;
            }
            catch (Exception e)
            {
                Debug.LogError("[AuthManager] Profile Error: " + e.Message);
                throw;
            }
        }


        // ── Update Profile ─────────────────────────────────────────────────────

        public async UniTask<UpdateProfileResult> UpdateProfileAsync(string username, string avatarKey)
        {
            try
            {
                var body = new UpdateProfileRequest
                {
                    Username = username,
                    Avatar = avatarKey
                };

                await ApiClient.Instance.Put<object>("/api/player/profile", body);

                Session.Username = username;
                Session.Avatar = avatarKey;

                Debug.Log("[AuthManager] Profile Updated");

                return new UpdateProfileResult { Success = true };
            }
            catch (ValidationException e)
            {
                Debug.LogWarning("[AuthManager] Validation Error: " + e.Message);

                return new UpdateProfileResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (ApiException e)
            {
                Debug.LogError("[AuthManager] API Error: " + e.Message);

                return new UpdateProfileResult
                {
                    Success = false,
                    ErrorCode = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (Exception e)
            {
                Debug.LogError("[AuthManager] Unknown Error: " + e.Message);

                return new UpdateProfileResult
                {
                    Success = false,
                    ErrorCode = "N001",
                    ErrorMessage = "Network error"
                };
            }
        }


        // ── Get All Avtar ─────────────────────────────────────────────────────
        public async UniTask<List<AvatarData>> GetAvatarsAsync()
        {
            try
            {
                var res = await ApiClient.Instance.Get<AvatarListResponse>("/api/player/avatars");
                return res.Avatars;
            }
            catch (Exception e)
            {
                Debug.LogError("[AuthManager] Avatar Error: " + e.Message);
                return new List<AvatarData>();
            }
        }

        // ── HUD Chips Data ─────────────────────────────────────────────────────

        public async UniTask<ChipsData> GetChipsAsync()
        {
            try
            {
                var data = await ApiClient.Instance
                    .Get<ChipsData>("/api/player/chips");

                if (data == null)
                {
                    Debug.LogError("[AuthManager] Chips NULL");
                    return null;
                }

                Debug.Log("[AuthManager] Chips Loaded: " + data.WalletChips);

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("[AuthManager] Chips Error: " + e.Message);
                throw;
            }
        }



        // ── Buy In Data ─────────────────────────────────────────────────────

        public async UniTask<BuyInResponse> BuyInAsync(string tableId, int amount)
        {
            try
            {
                var body = new
                {
                    tableId = tableId,
                    amount = amount
                };

                var result = await ApiClient.Instance
                    .Post<BuyInResponse>("/api/economy/buyin", body);



                Debug.Log("[AuthManager] BuyIn Success: " + amount);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("[AuthManager] BuyIn Error: " + e.Message);
                throw;
            }
        }



        // ── ClaimDailyBonus ─────────────────────────────────────────────────────
        public async UniTask<DailyBonusResult> ClaimDailyBonusAsync()
        {
            DailyBonusResult result = new DailyBonusResult();

            try
            {
                var data = await ApiClient.Instance.Post<DailyBonusData>(
                    "/api/economy/daily-bonus", null
                );

                if (data == null)
                {
                    Debug.LogError("❌ Data NULL");
                    result.Success = false;
                    return result;
                }

                Debug.Log("✅ BONUS RECEIVED: " + data.BonusAmount);

                result.Success = true;
                result.ChipsGranted = data.BonusAmount;
                result.NewBalance = data.NewBalance;

                if (!string.IsNullOrEmpty(data.NextBonusAt))
                {
                    result.NextBonusTime = DateTime.Parse(
                        data.NextBonusAt,
                        null,
                        DateTimeStyles.RoundtripKind
                    );
                }

                return result;
            }
            catch (ApiException ex)
            {
                Debug.LogError("❌ API ERROR: " + ex.Message);

                result.Success = false;
                result.ErrorCode = ex.Code;
                result.ErrorMessage = ex.Message;

                // ✅ 409 E001 handle
                if (ex.Code == "E001" && ex.Extra != null)
                {
                    if (ex.Extra.ContainsKey("nextBonusAvailableAt"))
                    {
                        string next = ex.Extra["nextBonusAvailableAt"].ToString();

                        result.NextBonusTime = DateTime.Parse(
                            next,
                            null,
                            DateTimeStyles.RoundtripKind
                        );
                    }
                }

                return result;
            }
        }


        // ── Lobby Table ─────────────────────────────────────────────────────
        public async UniTask<List<TableData>> GetTablesAsync(string variant, int minBlind, int maxBlind)
        {
            string endpoint = $"/api/lobby/tables?variant={variant}&minBlind={minBlind}&maxBlind={maxBlind}&status=open&page=1&limit=20";

            Debug.Log("🌐 API CALL: " + endpoint);

            try
            {
                var data = await ApiClient.Instance.Get<TablesData>(endpoint);

                if (data == null)
                {
                    Debug.LogError("❌ TablesData NULL");
                    return new List<TableData>();
                }

                if (data.Items == null)
                {
                    Debug.LogError("❌ Items NULL");
                    return new List<TableData>();
                }

                Debug.Log("✅ Tables Count: " + data.Items.Count);

                return data.Items;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ GetTables Error: " + e.Message);
                return new List<TableData>();
            }
        }

        // ── Create Table ─────────────────────────────────────────────────────
        public async UniTask<CreateTableResponse> CreateTableAsync(CreateTableRequest request)
        {
            string endpoint = "/api/lobby/tables";

            Debug.Log("📤 CREATE TABLE REQUEST:");
            Debug.Log(JsonConvert.SerializeObject(request, Formatting.Indented));

            try
            {
                var result = await ApiClient.Instance.Post<CreateTableResponse>(endpoint, request);

                Debug.Log("✅ TABLE CREATED: " + result.TableId);
                return result;
            }
            catch (ValidationException e)
            {
                Debug.LogError($"❌ Validation Error: {e.Code} - {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ CreateTable Failed: " + e.Message);
                throw;
            }
        }




        // ── Quick Join ─────────────────────────────────────────────────────

        public async UniTask<TableData> QuickJoinAsync(string variant = null)
        {
            try
            {
                Debug.Log("🚀 Quick Join Started...");

                var request = new QuickJoinRequest
                {
                    Variant = string.IsNullOrEmpty(variant) ? null : variant
                };

                var response = await ApiClient.Instance.Post<QuickJoinResponse>(
                    "/api/lobby/quickjoin",
                    request
                );

                Debug.Log("✅ Quick Join Success");
                Debug.Log("🎯 Table ID: " + response.TableId);

                return response.Table;
            }
            catch (LobbyException ex)
            {
                if (ex.Code == "L001")
                {
                    Debug.LogWarning("⚠️ No Tables Available (L001)");
                    throw;
                }

                Debug.LogError($"❌ Lobby Error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError("❌ QuickJoin Failed: " + ex.Message);
                throw;
            }
        }


        // ── Leaderboard ─────────────────────────────────────────────────────

        public async UniTask<GlobalLeaderboardData> GetGlobalLeaderboard(int page, int limit)
        {
            return await ApiClient.Instance
                .Get<GlobalLeaderboardData>($"/api/leaderboard/global?page={page}&limit={limit}");
        }

        public async UniTask<WeeklyLeaderboardData> GetWeeklyLeaderboard(int page, int limit)
        {
            return await ApiClient.Instance
                .Get<WeeklyLeaderboardData>($"/api/leaderboard/weekly?page={page}&limit={limit}");
        }



        // ── Transaction ─────────────────────────────────────────────────────
        public async UniTask<TransactionHistoryData> GetTransactions(int page, int limit, string type = "all")
        {
            string url = $"/api/economy/transactions?page={page}&limit={limit}&type={type}";

            var res = await ApiClient.Instance.Get<ApiResponse<TransactionHistoryData>>(url);

            return res.Data;
        }

    }



}