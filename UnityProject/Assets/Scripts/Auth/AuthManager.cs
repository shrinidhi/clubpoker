
using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;

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
                    Email    = email,
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
                    Success      = false,
                    ErrorCode    = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (AuthException e) when (e.Code == "A007")
            {
                Debug.LogWarning("[AuthManager] Register rate limited.");
                return new RegisterResult
                {
                    Success      = false,
                    ErrorCode    = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (ApiException e)
            {
                Debug.LogError($"[AuthManager] Register failed: {e.Code} — {e.Message}");
                return new RegisterResult
                {
                    Success      = false,
                    ErrorCode    = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthManager] Register unexpected error: {e.Message}");
                return new RegisterResult
                {
                    Success      = false,
                    ErrorCode    = "N001",
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
                    Email    = email,
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
                return new LoginResult { Success = true };
            }
            catch (AuthException e) when (e.Code == "A007")
            {
                // Account locked — pass remaining seconds to view for countdown
                Debug.LogWarning($"[AuthManager] Account locked. Remaining: {e.LockoutRemainingSeconds}s");
                return new LoginResult
                {
                    Success                 = false,
                    ErrorCode               = e.Code,
                    ErrorMessage            = e.Message,
                    LockoutRemainingSeconds = e.LockoutRemainingSeconds
                };
            }
            catch (AuthException e) when (e.Code == "A006")
            {
                Debug.LogWarning("[AuthManager] Login failed: wrong password.");
                return new LoginResult
                {
                    Success      = false,
                    ErrorCode    = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (ApiException e)
            {
                Debug.LogError($"[AuthManager] Login failed: {e.Code} — {e.Message}");
                return new LoginResult
                {
                    Success      = false,
                    ErrorCode    = e.Code,
                    ErrorMessage = e.Message
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthManager] Login unexpected error: {e.Message}");
                return new LoginResult
                {
                    Success      = false,
                    ErrorCode    = "N001",
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
                GuestRestrictedFeature.HandHistory  => true,
                GuestRestrictedFeature.ProfileEdit  => true,
                _                                   => false
            };
        }
    }
}