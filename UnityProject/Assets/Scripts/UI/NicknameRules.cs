using System.Text.RegularExpressions;

namespace ClubPoker.UI
{
    /// <summary>
    /// Nickname = display name, separate from username (the login handle, no
    /// spaces — see RegisterView). Spaces allowed, but only single spaces between
    /// words; callers trim first, so none at the ends. Shared by the profile edit
    /// popup and the first-time prompt on the main menu so they can't drift.
    /// </summary>
    public static class NicknameRules
    {
        public const int    MinLength = 3;
        public const int    MaxLength = 20;
        public const string Pattern   = @"^[a-zA-Z0-9_]+( [a-zA-Z0-9_]+)*$";

        /// <summary>Error message, or null when valid. Pass the trimmed text.</summary>
        public static string Validate(string nickname)
        {
            if (string.IsNullOrEmpty(nickname))
                return "Nickname cannot be empty.";

            if (nickname.Length < MinLength)
                return $"Nickname must be at least {MinLength} characters.";

            if (nickname.Length > MaxLength)
                return $"Nickname must be under {MaxLength} characters.";

            if (!Regex.IsMatch(nickname, Pattern))
                return "Nickname can only contain letters, numbers, underscores and single spaces.";

            return null;
        }
    }
}
