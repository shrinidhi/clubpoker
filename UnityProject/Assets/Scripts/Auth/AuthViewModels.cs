using ClubPoker.Networking.Models;
//
// Contains ONLY view-facing types and runtime state.
// All fields are derived from actual server responses.
// No hardcoded values live here.

namespace ClubPoker.Auth
{
    // ── Result types — returned by AuthManager to Views ───────────────────────
    // Views call AuthManager methods and switch on these results.
    // They never parse API responses or touch TokenStore directly.

    public class AuthResult
    {
        public bool   Success      { get; set; }
        public string ErrorCode    { get; set; }
        public string ErrorMessage { get; set; }

        public static AuthResult Ok() => new AuthResult { Success = true };

        public static AuthResult Fail(string code, string message) =>
            new AuthResult { Success = false, ErrorCode = code, ErrorMessage = message };
    }

    /// <summary>
    /// Returned by AuthManager.RegisterAsync().
    /// On success, view reads AuthManager.Instance.Session.WalletChips
    /// to get the chip balance and drive any bonus animation.
    /// </summary>
    public class RegisterResult : AuthResult { }

    /// <summary>
    /// Returned by AuthManager.LoginAsync().
    /// LockoutRemainingSeconds is populated on A007 (account locked)
    /// and comes directly from the server error response.
    /// </summary>
    public class LoginResult : AuthResult
    {
        /// <summary>
        /// Seconds until the account lockout lifts.
        /// Sourced from ApiError.LockoutRemainingSeconds in the server response.
        /// The view uses this to drive the countdown timer display.
        /// </summary>
        public int? LockoutRemainingSeconds { get; set; }
    }

    public class UpdateProfileResult : AuthResult { }
   

    // ── UserSession — runtime player identity ─────────────────────────────────
    // Populated by AuthManager after any successful auth flow.
    // All fields map directly to PlayerData or GuestPlayerData from the server.
    // Any system that needs the current player reads AuthManager.Instance.Session.

    public class UserSession
    {
        // From PlayerData / GuestPlayerData
        public string Id          { get; set; }
        public string Username    { get; set; }
        public string Email       { get; set; }
        public string Avatar      { get; set; }
        public int    WalletChips { get; set; }
        public string Role        { get; set; }
        public bool   IsGuest     { get; set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(Id);

        /// <summary>
        /// Build a session from a full PlayerData (register or login response).
        /// </summary>
        public static UserSession From(PlayerData player) => new UserSession
        {
            Id          = player.Id,
            Username    = player.Username,
            Email       = player.Email,
            Avatar      = player.Avatar,
            WalletChips = player.WalletChips,
            Role        = player.Role,
            IsGuest     = false
        };

        /// <summary>
        /// Build a session from a GuestPlayerData (guest response).
        /// Email is not returned by the guest endpoint so it stays null.
        /// </summary>
        public static UserSession FromGuest(GuestPlayerData player) => new UserSession
        {
            Id          = player.Id,
            Username    = player.Username,
            Avatar      = player.Avatar,
            WalletChips = player.WalletChips,
            Role        = "guest",
            IsGuest     = player.IsGuest
        };
    }

    // ── Guest feature gate ────────────────────────────────────────────────────
    // Views call AuthManager.IsFeatureRestrictedForGuest(feature) and show
    // the upgrade prompt if true.
    // These map to the restrictions defined in the guest endpoint docs.

    public enum GuestRestrictedFeature
    {
        Leaderboard,
        HandHistory,
        ProfileEdit
    }
}