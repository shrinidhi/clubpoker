
using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using System.Collections.Generic;
using System.Linq;
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

                string verifyRefresh = TokenStore.LoadRefreshToken();
                Debug.Log($"[AuthManager] Refresh token stored verify: {!string.IsNullOrEmpty(verifyRefresh)}");

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
            // Snapshot access token before waiting — if it changes while we wait,
            // another caller already refreshed and we can skip.
            string tokenBeforeWait = ApiClient.Instance.AccessToken;

            bool gotLock = await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10));
            if (!gotLock)
            {
                Debug.LogWarning("[AuthManager] Refresh lock timed out.");
                return false;
            }

            // If another caller refreshed while we were waiting, the token changed.
            // Reuse their result — don't burn the freshly-issued refresh token again.
            string tokenAfterWait = ApiClient.Instance.AccessToken;
            if (!string.IsNullOrEmpty(tokenAfterWait) && tokenAfterWait != tokenBeforeWait)
            {
                _refreshLock.Release();
                Debug.Log("[AuthManager] Token already refreshed by concurrent caller — reusing.");
                return true;
            }

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

                // Only auto-connect when the socket is idle. If it is already
                // Reconnecting/Connecting (mid-game reconnect drives the refresh),
                // don't spawn a second socket — that path re-reads the fresh token
                // from ApiClient on its next attempt.
                if (SocketManager.Instance != null &&
                    SocketManager.Instance.State == SocketConnectionState.Disconnected)
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
            GameSceneManager.Instance.LoadScene("Scene_Login");
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
                TokenStore.SaveGuestProfile(data.Player.Id, data.Player.Username, data.Player.WalletChips);
                ApiClient.Instance.SetTokens(data.Tokens.AccessToken, null);

                Session = UserSession.FromGuest(data.Player, expiresAt);

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
                GuestRestrictedFeature.CreateTable => true,
                GuestRestrictedFeature.Transaction => true,
                GuestRestrictedFeature.CreateClub => true,
                GuestRestrictedFeature.SearchClub => true,
                _ => false
            };
        }

        // ── Profile ─────────────────────────────────────────────────────
        public async UniTask<PlayerFullProfileData> GetPlayerProfileAsync()
        {
            try
            {
                PlayerFullProfileData profile =
                    await ApiClient.Instance.Get<PlayerFullProfileData>(
                        "/api/player/profile/full"
                    );

                if (profile == null)
                {
                    Debug.LogError("❌ Profile Data Null");
                    return null;
                }

                Debug.Log("✅ Profile Loaded: " + profile.Username);
                return profile;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Full Profile Failed: " + e.Message);
                return null;
            }
        }

        public async UniTask<UpdateProfileData> UpdatePlayerProfileAsync(
      string username,
      string avatar)
        {
            try
            {
                var body = new
                {
                    username = username,
                    avatar = avatar
                };

                UpdateProfileData data =
                    await ApiClient.Instance.Put<UpdateProfileData>(
                        "/api/player/profile/update",
                        body
                    );

                if (data == null)
                    return null;

                Session.Username = data.Username;
                Session.Avatar = data.Avatar;

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Update Profile Failed: " + e.Message);
                throw;
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
            if (Session.IsGuest)
            {
                return new ChipsData
                {
                    WalletChips = Session.WalletChips,
                    LockedInTables = 0,
                    AvailableChips = Session.WalletChips
                };
            }

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

                // Update session so AutoShowDailyBonusAsync won't re-show popup this session
                if (Session != null)
                    Session.LastDailyBonus = DateTime.UtcNow;

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
        public async UniTask<List<TableData>> GetTablesAsync(string variant)
        {
            string endpoint = "/api/lobby/tables?limit=50&status=all";

            if (!string.IsNullOrEmpty(variant) && variant != "all")
                endpoint += $"&variant={variant}";

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

                // Persistent tables only, highest seat count first.
                var tables = data.Items
                    .Where(t => t.IsPersistent)
                    .OrderByDescending(t => t.CurrentPlayers)
                    .ToList();

                Debug.Log($"✅ Tables Count: {data.Items.Count} (persistent: {tables.Count})");

                return tables;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ GetTables Error: " + e.Message);
                return new List<TableData>();
            }
        }

        // Single table detail (incl. maxPlayers): GET /api/lobby/tables/{id}
        public async UniTask<TableData> GetTableDetailAsync(string tableId)
        {
            string endpoint = $"/api/lobby/tables/{tableId}";

            try
            {
                return await ApiClient.Instance.Get<TableData>(endpoint);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ GetTableDetail Error ({tableId}): " + e.Message);
                return null;
            }
        }

        // Per-table live hand status: GET /api/lobby/tables/{id}/active
        public async UniTask<TableActiveData> GetTableActiveAsync(string tableId)
        {
            string endpoint = $"/api/lobby/tables/{tableId}/active";

            try
            {
                return await ApiClient.Instance.Get<TableActiveData>(endpoint);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ GetTableActive Error ({tableId}): " + e.Message);
                return null;
            }
        }

        // Spectate a table: POST /api/lobby/tables/{id}/spectate
        // Throws (e.g. GameException G006) on failure so the caller can surface it.
        public async UniTask<SpectateData> SpectateTableAsync(string tableId)
        {
            string endpoint = $"/api/lobby/tables/{tableId}/spectate";
            return await ApiClient.Instance.Post<SpectateData>(endpoint, new { });
        }

        // Join the waiting list for a table: POST /api/lobby/tables/{id}/waiting-list
        // Used in the Watch & Wait flow so the server notifies via table:seat_available.
        public async UniTask JoinWaitingListAsync(string tableId)
        {
            string endpoint = $"/api/lobby/tables/{tableId}/waiting-list";
            await ApiClient.Instance.Post<object>(endpoint, new { });
        }

        // Stand up / leave a table: POST /api/lobby/tables/{id}/leave — chips returned.
        public async UniTask LeaveTableAsync(string tableId)
        {
            string endpoint = $"/api/lobby/tables/{tableId}/leave";
            await ApiClient.Instance.Post<object>(endpoint, new { });
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



        public async UniTask LinkClubTableAsync(string tableId, string clubId, string clubTableId)
        {
            var body = new { clubId, clubTableId };
            await ApiClient.Instance.Post<object>(
                $"/api/lobby/tables/{tableId}/link-club-table",
                body
            );
            Debug.Log($"✅ Club table linked: tableId={tableId} clubTableId={clubTableId}");
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


        public async UniTask<JoinTableResponse> JoinTableAsync(string tableId, int buyIn)
        {
            try
            {
                var body = new
                {
                    buyInAmount = buyIn
                };

                var result = await ApiClient.Instance.Post<JoinTableResponse>(
                    $"/api/lobby/tables/{tableId}/join",
                    body
                );

                Debug.Log($"✅ Joined Table → Seat: {result.seat}");

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ JoinTable Error: " + e.Message);
                throw;
            }
        }


        public async UniTask<JoinTableResponse> JoinByCodeAsync(string shareCode, int buyIn)
        {
            try
            {
                var body = new { shareCode = shareCode, buyIn = buyIn };

                var result = await ApiClient.Instance.Post<JoinTableResponse>(
                    "/api/lobby/tables/join-by-code",
                    body
                );

                Debug.Log($"Joined by code → Table: {result.tableId}, Seat: {result.seat}");

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("JoinByCode Error: " + e.Message);
                throw;
            }
        }

        public async UniTask StartTableAsync(string tableId, int rounds)
        {
            var body = new
            {
                rounds = rounds
            };

            await ApiClient.Instance.Post<object>(
                $"/api/lobby/tables/{tableId}/start",
                body
            );

            Debug.Log("Game started");
        }




        public async UniTask<ClubData> CreateClubAsync(string name, string badge, string description)
        {
            try
            {
                var request = new CreateClubRequest
                {
                    Name = name,
                    Badge = badge,
                    Description = description
                };

                Debug.Log("📤 CREATE CLUB REQUEST:");
                Debug.Log(JsonConvert.SerializeObject(request, Formatting.Indented));

                var response = await ApiClient.Instance.Post<CreateClubApiResponse>(
                    "/api/clubs",
                    request
                );

                Debug.Log("✅ CLUB CREATED: " + response.Club.Name);
                Debug.Log("Club Code: " + response.Club.ClubCode);

                return response.Club;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Create Club Failed: " + e.Message);
                throw;
            }
        }


        public async UniTask<List<ClubListData>> GetClubsAsync()
        {
            try
            {
                var response = await ApiClient.Instance
                    .Get<ClubListApiResponse>("/api/clubs");

                if (response == null || response.Clubs == null)
                {
                    Debug.LogWarning("No Clubs Found");
                    return new List<ClubListData>();
                }

                Debug.Log("✅ Clubs Count: " + response.Clubs.Count);

                return response.Clubs;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Clubs Failed: " + e.Message);
                return new List<ClubListData>();
            }
        }


        public async UniTask<ClubTableData> CreateClubTableAsync(
      string clubId,
      CreateClubTableRequest request)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/tables";

                Debug.Log("📤 CREATE CLUB TABLE REQUEST:");
                Debug.Log(JsonConvert.SerializeObject(request, Formatting.Indented));

                CreateClubTableApiResponse response =
                    await ApiClient.Instance.Post<CreateClubTableApiResponse>(
                        endpoint,
                        request
                    );

                if (response == null || response.Table == null)
                {
                    Debug.LogError("❌ Create Club Table Response Null");
                    return null;
                }

                Debug.Log("✅ Club Table Created: " + response.Table.Name);
                Debug.Log("Table ID: " + response.Table.Id);

                return response.Table;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Create Club Table Failed: " + e.Message);
                throw;
            }
        }



        public async UniTask<List<ClubTableData>> GetClubTablesAsync(string clubId)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/tables";

                ClubTablesApiResponse response =
                    await ApiClient.Instance.Get<ClubTablesApiResponse>(endpoint);

                if (response == null || response.Tables == null)
                    return new List<ClubTableData>();

                Debug.Log("✅ Club Tables Count: " + response.Tables.Count);

                return response.Tables;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Club Tables Failed: " + e.Message);
                return new List<ClubTableData>();
            }
        }


        public async UniTask<(ClubSearchData club, string errorCode, string errorMessage)>
        SearchClubAsync(string clubCode)
        {
            try
            {
                string endpoint = $"/api/clubs/find?code={clubCode}";

                ClubSearchApiResponse response =
                    await ApiClient.Instance.Get<ClubSearchApiResponse>(endpoint);

                if (response == null || response.Club == null)
                    return (null, "NOT_FOUND", "Club not found");

                return (response.Club, null, null);
            }
            catch (ApiException e)
            {
                Debug.LogError($"❌ Club Search Failed: {e.Code} - {e.Message}");

                return (null, e.Code, e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Club Search Failed: " + e.Message);

                return (null, "UNKNOWN", "Something went wrong");
            }
        }

        public async UniTask<(bool success, string errorMessage)> ApplyToClubAsync(
       string clubId,
       string message)
        {
            try
            {
                var body = new
                {
                    message = message
                };

                await ApiClient.Instance.Post<object>(
                    $"/api/clubs/{clubId}/apply",
                    body
                );

                Debug.Log("✅ Apply request sent");

                return (true, "");
            }
            catch (ApiException e)
            {
                Debug.LogError("========== APPLY ERROR ==========");
                Debug.LogError("Code : " + e.Code);
                Debug.LogError("Message : " + e.Message);

                return (false, e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Apply Failed : " + e.Message);

                return (false, e.Message);
            }
        }



        public async UniTask<ClubTableTemplateData> SaveClubTableTemplateAsync(
    string clubId,
    SaveClubTableTemplateRequest request)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/tables/template";

                Debug.Log("📤 SAVE TEMPLATE REQUEST:");
                Debug.Log(JsonConvert.SerializeObject(request, Formatting.Indented));

                SaveClubTableTemplateApiResponse response =
                    await ApiClient.Instance.Post<SaveClubTableTemplateApiResponse>(
                        endpoint,
                        request
                    );

                if (response == null || response.Template == null)
                    return null;

                Debug.Log("✅ Template Saved: " + response.Template.Name);

                return response.Template;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Save Template Failed: " + e.Message);
                throw;
            }
        }


        public async UniTask<List<ClubTableTemplateData>> GetClubTableTemplatesAsync(string clubId)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/tables/templates";

                ClubTableTemplatesApiResponse response =
                    await ApiClient.Instance.Get<ClubTableTemplatesApiResponse>(endpoint);

                if (response == null || response.Templates == null)
                    return new List<ClubTableTemplateData>();

                response.Templates.RemoveAll(template => template == null);

                Debug.Log("✅ Templates Count: " + response.Templates.Count);

                return response.Templates;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Templates Failed: " + e.Message);
                return new List<ClubTableTemplateData>();
            }
        }


        public async UniTask<BulkCreateClubTablesApiResponse> BulkCreateClubTablesAsync(
    string clubId,
    BulkCreateClubTablesRequest request)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/tables/bulk";

                Debug.Log("📤 BULK CREATE TABLE REQUEST:");
                Debug.Log(JsonConvert.SerializeObject(request, Formatting.Indented));

                BulkCreateClubTablesApiResponse response =
                    await ApiClient.Instance.Post<BulkCreateClubTablesApiResponse>(
                        endpoint,
                        request
                    );

                Debug.Log("✅ Bulk Tables Created: " + response.Created);

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Bulk Create Tables Failed: " + e.Message);
                throw;
            }
        }





        public async UniTask<DeleteClubTableApiResponse> DeleteClubTableAsync(
    string clubId,
    string tableId)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/tables/{tableId}";

                DeleteClubTableApiResponse response =
                    await ApiClient.Instance.Delete<DeleteClubTableApiResponse>(endpoint);

                Debug.Log("✅ Table Disbanded: " + response.Name);

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Delete Club Table Failed: " + e.Message);
                throw;
            }
        }


        public async UniTask<ExtendTableResponse> ExtendTableAsync(
      string clubId,
      string tableId)
        {
            try
            {
                string endpoint =
                    $"/api/clubs/{clubId}/tables/{tableId}/extend";

                ExtendTableResponse response =
                    await ApiClient.Instance.Post<ExtendTableResponse>(
                        endpoint,
                        new { }
                    );

                if (response == null)
                {
                    Debug.LogError("❌ Extend Table Response Null");
                    return null;
                }

                Debug.Log(
                    $"✅ Table Extended: {response.TableId}, +{response.AddedMinutes} min"
                );

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Extend Table Failed: " + e.Message);
                throw;
            }
        }

        public async UniTask<List<ClubApplicationData>> GetClubApplicationsAsync(string clubId)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/applications";

                ClubApplicationsApiResponse response =
                    await ApiClient.Instance.Get<ClubApplicationsApiResponse>(endpoint);

                if (response == null || response.Applications == null)
                    return new List<ClubApplicationData>();

                return response.Applications;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Applications Failed: " + e.Message);
                return new List<ClubApplicationData>();
            }
        }

        public async UniTask<bool> ApproveClubApplicationAsync(
            string clubId,
            string applicationId)
        {
            try
            {
                await ApiClient.Instance.Post<object>(
                    $"/api/clubs/{clubId}/applications/{applicationId}/approve",
                    null
                );

                Debug.Log("✅ Application Approved");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Approve Failed: " + e.Message);
                return false;
            }
        }

        public async UniTask<bool> RejectClubApplicationAsync(
            string clubId,
            string applicationId)
        {
            try
            {
                await ApiClient.Instance.Post<object>(
                    $"/api/clubs/{clubId}/applications/{applicationId}/reject",
                    null
                );

                Debug.Log("✅ Application Rejected");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Reject Failed: " + e.Message);
                return false;
            }
        }


        public async UniTask<List<ClubMemberData>> GetClubMembersAsync(string clubId)
        {
            try
            {
                string endpoint =
                    $"/api/clubs/{clubId}/members?role=ALL&sortBy=chips&limit=50";

                ClubMembersApiResponse response =
                    await ApiClient.Instance.Get<ClubMembersApiResponse>(endpoint);

                if (response == null || response.Members == null)
                    return new List<ClubMemberData>();

                Debug.Log("✅ Club Members Count: " + response.Members.Count);

                return response.Members;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Club Members Failed: " + e.Message);
                return new List<ClubMemberData>();
            }
        }


        public async UniTask<List<ClubMemberData>> GetClubMembersAsync(
    string clubId,
    string role = "ALL")
        {
            try
            {
                string endpoint =
                    $"/api/clubs/{clubId}/members?role={role}&sortBy=chips&limit=50";

                ClubMembersApiResponse response =
                    await ApiClient.Instance.Get<ClubMembersApiResponse>(endpoint);

                if (response == null || response.Members == null)
                    return new List<ClubMemberData>();

                return response.Members;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Club Members Failed: " + e.Message);
                return new List<ClubMemberData>();
            }
        }

        public async UniTask<AgentDataApiResponse> GetAgentDataAsync(
            string clubId,
            string agentUserId)
        {
            try
            {
                string endpoint =
                    $"/api/clubs/{clubId}/members/{agentUserId}/agent-data";

                AgentDataApiResponse response =
                    await ApiClient.Instance.Get<AgentDataApiResponse>(endpoint);

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Agent Data Failed: " + e.Message);
                return null;
            }
        }


        public async UniTask<ClubMemberData> GetMemberDetailAsync(
     string clubId,
     string userId)
        {
            try
            {
                string endpoint =
                    $"/api/clubs/{clubId}/members/{userId}";

                MemberDetailResponse response =
                    await ApiClient.Instance.Get<MemberDetailResponse>(
                        endpoint
                    );

                return response.Member;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return null;
            }
        }


        public async UniTask<bool> UpdateMemberRoleAsync(
        string clubId,
        string userId,
        string role)
        {
            try
            {
                await ApiClient.Instance.Put<object>(
                    $"/api/clubs/{clubId}/members/{userId}/role",
                    new
                    {
                        role = role
                    });

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }
        }

        public async UniTask<DeleteClubMemberResponse> DeleteClubMemberAsync(
    string clubId,
    string userId)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/members/{userId}";

                DeleteClubMemberResponse response =
                    await ApiClient.Instance.Delete<DeleteClubMemberResponse>(endpoint);

                Debug.Log("✅ Member Removed: " + response.UserId);

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Delete Member Failed: " + e.Message);
                throw;
            }
        }



        public async UniTask<List<ClubMemberData>> GetClubMembersAsync(
    string clubId,
    string role = "ALL",
    string sortBy = "chips")
        {
            try
            {
                string endpoint =
                    $"/api/clubs/{clubId}/members?role={role}&sortBy={sortBy}&limit=50";

                ClubMembersApiResponse response =
                    await ApiClient.Instance.Get<ClubMembersApiResponse>(endpoint);

                if (response == null || response.Members == null)
                    return new List<ClubMemberData>();

                return response.Members;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Club Members Failed: " + e.Message);
                return new List<ClubMemberData>();
            }
        }

        public async UniTask<List<string>> GetClubOnlineMembersAsync(string clubId)
        {
            try
            {
                string endpoint = $"/api/clubs/{clubId}/online";

                ClubOnlineApiResponse response =
                    await ApiClient.Instance.Get<ClubOnlineApiResponse>(endpoint);

                if (response == null || response.Online == null)
                    return new List<string>();

                Debug.Log("✅ Online Count: " + response.Count);

                return response.Online;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Online Members Failed: " + e.Message);
                return new List<string>();
            }
        }



        public async UniTask<bool> UpdateTableManagerAsync(
    string clubId,
    string userId,
    bool isTableManager)
        {
            try
            {
                await ApiClient.Instance.Put<object>(
                    $"/api/clubs/{clubId}/members/{userId}/table-manager",
                    new
                    {
                        isTableManager = isTableManager
                    });

                Debug.Log("✅ Table Manager Updated : " + isTableManager);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Table Manager Update Failed : " + e.Message);
                return false;
            }
        }





        public async UniTask<PlayerStatsData> GetPlayerStatsAsync()
        {
            try
            {
                PlayerStatsData data =
                    await ApiClient.Instance.Get<PlayerStatsData>(
                        "/api/player/stats/self"
                    );

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get Player Stats Failed: " + e.Message);
                return null;
            }
        }

        public async UniTask<AllInStatsData> GetPlayerAllInStatsAsync()
        {
            try
            {
                AllInStatsData data =
                    await ApiClient.Instance.Get<AllInStatsData>(
                        "/api/player/stats/allin"
                    );

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Get All-In Stats Failed: " + e.Message);
                return null;
            }
        }

        public async UniTask<KickPlayerResponse> KickPlayerAsync(
      string tableId,
      string playerId)
        {
            try
            {
                string endpoint =
                    $"/api/lobby/tables/{tableId}/kick/{playerId}";

                KickPlayerResponse response =
                    await ApiClient.Instance.Post<KickPlayerResponse>(
                        endpoint,
                        null
                    );

                Debug.Log("✅ Player Kicked : " + response.PlayerId);
                Debug.Log("Socket Count : " + response.SocketCount);

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Kick Player Failed : " + e.Message);
                return null;
            }
        }


        public async UniTask<KickListResponse> GetKickListAsync(string tableId)
        {
            try
            {
                return await ApiClient.Instance.Get<KickListResponse>(
                    $"/api/lobby/tables/{tableId}/kick-list"
                );
            }
            catch (Exception e)
            {
                Debug.LogError("GetKickList Error : " + e.Message);
                return null;
            }
        }

        public async UniTask<WaitingListResponse> GetWaitingListAsync(string tableId)
        {
            try
            {
                string endpoint =
                    $"/api/lobby/tables/{tableId}/waiting-list";

                WaitingListResponse response =
                    await ApiClient.Instance.Get<WaitingListResponse>(
                        endpoint);

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("Get Waiting List Failed : " + e.Message);
                return null;
            }
        }

        public async UniTask<JoinWaitingListResponse> JoinWaitingList(string tableId)
        {
            try
            {
                string endpoint =
                    $"/api/lobby/tables/{tableId}/waiting-list";

                JoinWaitingListResponse response =
                    await ApiClient.Instance.Post<JoinWaitingListResponse>(
                        endpoint,
                        new { }
                    );

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("Join Waiting List Failed : " + e.Message);
                return null;
            }
        }

        public async UniTask<LeaveWaitingListResponse> LeaveWaitingListAsync(string tableId)
        {
            try
            {
                string endpoint =
                    $"/api/lobby/tables/{tableId}/waiting-list";

                LeaveWaitingListResponse response =
                    await ApiClient.Instance.Delete<LeaveWaitingListResponse>(
                        endpoint);

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("Leave Waiting List Failed : " + e.Message);
                return null;
            }
        }


        public async UniTask<List<GameChatPayload>> GetTableChatMessagesAsync(string tableId, int limit = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(tableId))
                {
                    Debug.LogError("[Chat API] Table ID is empty");
                    return new List<GameChatPayload>();
                }
                string endpoint = $"/api/chat/table/{tableId}?limit={limit}";

                Debug.Log("[Chat API] GET: " + endpoint);

                ChatMessagesResponse response = await ApiClient.Instance.Get<ChatMessagesResponse>(endpoint);

                if (response == null)
                {
                    Debug.LogError("[Chat API] Response is null");
                    return new List<GameChatPayload>();
                }

                if (!string.Equals(response.status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[Chat API] Invalid response status: " + response.status);
                    return new List<GameChatPayload>();
                }

                if (response.data == null || response.data.messages == null)
                {
                    Debug.Log("[Chat API] No messages found");
                    return new List<GameChatPayload>();
                }

                Debug.Log("[Chat API] Messages loaded: " + response.data.messages.Count
                );

                return response.data.messages;
            }
            catch (Exception e)
            {
                Debug.LogError("[Chat API] GetTableChatMessagesAsync failed: " + e.Message);
                return new List<GameChatPayload>();
            }
        }


        public async UniTask<JoinByCodeResponse> JoinByCodeAsync(string shareCode)
        {
            try
            {
                JoinByCodeRequest request = new JoinByCodeRequest
                {
                    ShareCode = shareCode
                };

                JoinByCodeResponse response =
                    await ApiClient.Instance.Post<JoinByCodeResponse>(
                        "/api/lobby/tables/join-by-code",
                        request
                    );

                Debug.Log(
                    $"✅ Join code valid | " +
                    $"Table: {response.TableId} | " +
                    $"Variant: {response.Variant} | " +
                    $"Seats Left: {response.SeatsLeft}"
                );

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError("❌ JoinByCode Error: " + e.Message);
                throw;
            }
        }



        public async UniTask<CareerOverviewData> GetCareerOverviewAsync(string period = "30d", string variant = "ALL")
        {
            try
            {
                string endpoint = $"/api/player/career/overview?period={period}&variant={variant}";
                CareerOverviewData data = await ApiClient.Instance.Get<CareerOverviewData>(endpoint);
                Debug.Log($"Career loaded | Period: {period} | Sessions: {data?.Sessions?.Count ?? 0}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Get Career Overview Failed: " + e.Message);
                return null;
            }
        }



        public async UniTask<CareerHandHistoryData> GetCareerHandHistoryAsync(string tableId, int limit = 50)
        {
            try
            {
                string endpoint = $"/api/history/hands?tableId={tableId}&limit={limit}";
                CareerHandHistoryData data = await ApiClient.Instance.Get<CareerHandHistoryData>(endpoint);
                Debug.Log($"Career hand history loaded | Table: {tableId} | Hands: {data?.Items?.Count ?? 0}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Get career hand history failed: " + e.Message);
                return null;
            }
        }


        public async UniTask<CareerHandDetailData> GetCareerHandDetailAsync(string handId)
        {
            try
            {
                string endpoint = "/api/history/hands/" + handId;
                CareerHandDetailData data = await ApiClient.Instance.Get<CareerHandDetailData>(endpoint);

                Debug.Log(
                    "Career hand detail loaded | Hand ID: " +
                    handId +
                    " | Actions: " +
                    (data?.Actions?.Count ?? 0)
                );

                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Career hand detail failed: " + e.Message);
                return null;
            }
        }

    }

}