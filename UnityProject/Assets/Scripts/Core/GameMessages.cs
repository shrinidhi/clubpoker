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
        public const string StandUpCancelled = "Stand up cancelled — you keep your seat.";
        public const string NotSeated        = "You're not seated at the table.";
        public const string SeatedNextHand   = "Hand in progress — you'll be dealt in from the next hand.";
        public const string NotSeatedAtTable = "Not seated at a table";

        // Server moved us out of the seat. Fallbacks only — the payload's own
        // message is shown when there is one, since it knows the exact cause.
        public const string MovedToSpectator     = "You've been moved to spectator.";
        public const string StoodUpToSpectator   = "You stood up — watching. Buy in again to take a seat.";
        public const string SitOutExpired        = "Sat out too long — your seat was released and your chips returned.";
        public const string BustedToSpectator    = "Out of chips — buy in again to take a seat.";

        /// Sit Out now has a deadline (3 hands), so leaving the table screen on it
        /// is a choice with a cost. Said before it happens, not after.
        public const string SitOutHandLimitWarning =
            "You'll sit out while you're away. If you don't come back within 3 hands, " +
            "your seat is released and your chips are returned.";

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
