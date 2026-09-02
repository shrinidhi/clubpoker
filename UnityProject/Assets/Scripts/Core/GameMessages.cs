namespace ClubPoker.Core
{
    /// <summary>
    /// Every fixed string the player is shown — toasts, popup errors, status lines.
    ///
    /// Kept in one place because the same idea kept getting three wordings: joining a
    /// table failed as "Failed to join", "Could not join table" and "Failed to take
    /// seat", depending on which screen you were on. One list is also what makes
    /// localisation a rename rather than a hunt: these become the keys.
    ///
    /// This lives in ClubPoker.Core, which every other assembly references, so it is
    /// reachable from Game, UI, Lobby, Auth, Networking and the club screens alike.
    ///
    /// Not here: text the SERVER supplies (ApiException.Message, payload.Message).
    /// That's already a message for this player about this failure — pass it through
    /// rather than replacing it with something vaguer.
    /// </summary>
    public static class GameMessages
    {
        // ── Seat ────────────────────────────────────────────────────────────

        public const string StandUpAfterHand = "You will stand up after this hand.";
        public const string NotSeated        = "You're not seated at the table.";
        public const string NotSeatedAtTable = "Not seated at a table";

        /// Cause and consequence together: without the second half, being dropped
        /// back at the lobby reads as a crash or a kick.
        public const string TableEmptied     = "All players left — leaving table";

        // ── Connection ──────────────────────────────────────────────────────

        public const string Reconnecting      = "Reconnecting...";
        public const string StillReconnecting = "Still reconnecting — try again in a moment";
        public const string TableUnreachable  = "Could not connect to table. Please try again.";

        // ── Money ───────────────────────────────────────────────────────────

        public const string NotEnoughBalance  = "Not enough balance";
        public const string BuyInUnavailable  = "Buy-in unavailable";
        public const string BuyInFailed       = "Buy-in failed";
        public const string TopUpFailed       = "Top up failed";
        public const string WithdrawFailed    = "Withdraw failed";
        public const string StackAtTableMaximum = "Your stack is already at the table maximum";

        // ── Catch-all ───────────────────────────────────────────────────────

        /// Last resort, when an exception carries nothing worth showing.
        public const string SomethingWentWrong = "Something went wrong";

        // ── With a value in them ────────────────────────────────────────────

        public static string JoinFailed(string reason)     => $"Failed to join: {reason}";
        public static string WatchFailed(string reason)    => $"Failed to watch: {reason}";
        public static string TakeSeatFailed(string reason) => $"Failed to take seat: {reason}";

        public static string PlayerLostConnection(string who) => $"{who} lost connection";
        public static string PlayerReconnected(string who)    => $"{who} reconnected";
        public static string RemovedForInactivity(string who) => $"{who} removed for inactivity";

        public static string AutoRebuy(int amount) => $"Auto rebuy: +{amount:N0}";

        public static string AutoWithdraw(int amount) =>
            $"Auto withdraw: {amount:N0} moved to your balance";

        /// <summary>
        /// Game error codes → player-facing text. A lookup rather than loose consts:
        /// the codes are the API's vocabulary (see ApiException), and keeping them
        /// paired with their wording is what stops the two drifting.
        /// </summary>
        public static string ForGameError(string code, string serverMessage)
        {
            return code switch
            {
                "G001" => "Not your turn",
                "G002" => "Invalid action",
                "G009" => "Raise amount too low",
                "G010" => "Already folded",
                "G011" => "Already all-in",
                "G015" => "Rule violation",
                _      => serverMessage ?? "Game error"
            };
        }
    }
}
