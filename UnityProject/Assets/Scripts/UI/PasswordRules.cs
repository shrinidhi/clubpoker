using System.Text.RegularExpressions;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    /// <summary>
    /// Password rules (CLUB-525) — 8+ characters, 1 uppercase letter, 1 number.
    /// Shared by register and forgot-password reset so they can't drift. Stricter
    /// than the server (letter + number), on purpose.
    /// </summary>
    public static class PasswordRules
    {
        public const int    MinLength      = 8;
        public const string UpperPattern   = @"[A-Z]";
        public const string NumberPattern  = @"[0-9]";

        /// <summary>Error message, or null when valid. Don't trim passwords.</summary>
        public static string Validate(string password)
        {
            if (string.IsNullOrEmpty(password))
                return Loc.Get("enter_password");

            if (password.Length < MinLength)
                return Loc.Get("password_too_short", ("min", MinLength));

            if (!Regex.IsMatch(password, UpperPattern))
                return Loc.Get("password_needs_uppercase");

            if (!Regex.IsMatch(password, NumberPattern))
                return Loc.Get("password_needs_number");

            return null;
        }

        /// <summary>Error for the re-enter / confirm field, or null when it matches.</summary>
        public static string ValidateMatch(string password, string reEntered)
        {
            if (string.IsNullOrEmpty(reEntered))
                return Loc.Get("reenter_password_prompt");

            if (reEntered != password)
                return Loc.Get("passwords_do_not_match");

            return null;
        }
    }
}
