using System;
using ClubPoker.Core;

namespace ClubPoker.Networking
{
    /// <summary>
    /// The text to show a player for a caught exception. Use this instead of
    /// e.Message anywhere a message reaches the UI.
    ///
    /// An ApiException's message is already player-facing: the server's error
    /// text, or one of our localized strings (ApiClient never puts raw
    /// exception text or 5xx crash text in one). Anything else —
    /// NullReferenceException, a JSON parse error, an IOException — carries
    /// technical text that must never be shown, so the caller's fallback (or
    /// "Something went wrong") is used and the real reason stays in the log.
    /// </summary>
    public static class PlayerError
    {
        /// <param name="e">The caught exception (may be null).</param>
        /// <param name="fallback">What to show when e isn't an API error or has no
        /// message — usually the screen's own "… failed" string. Null → "Something
        /// went wrong".</param>
        public static string Text(Exception e, string fallback = null)
        {
            if (e is ApiException && !string.IsNullOrEmpty(e.Message))
                return e.Message;

            return string.IsNullOrEmpty(fallback) ? Loc.Get("something_went_wrong") : fallback;
        }
    }
}
