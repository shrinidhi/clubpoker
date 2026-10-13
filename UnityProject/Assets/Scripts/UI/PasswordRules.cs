using System.Text.RegularExpressions;

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
                return "Please enter a password.";

            if (password.Length < MinLength)
                return $"Password must be at least {MinLength} characters.";

            if (!Regex.IsMatch(password, UpperPattern))
                return "Password must contain at least 1 uppercase letter.";

            if (!Regex.IsMatch(password, NumberPattern))
                return "Password must contain at least 1 number.";

            return null;
        }

        /// <summary>Error for the re-enter / confirm field, or null when it matches.</summary>
        public static string ValidateMatch(string password, string reEntered)
        {
            if (string.IsNullOrEmpty(reEntered))
                return "Please re-enter your password.";

            if (reEntered != password)
                return "Passwords do not match.";

            return null;
        }
    }
}
