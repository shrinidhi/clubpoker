namespace ClubPoker.Core
{
    /// <summary>
    /// Every fixed string the player is shown — toasts, popup errors, status lines.
    ///
    /// Kept in one place because the same idea kept getting three wordings: joining a
    /// table failed as "Failed to join", "Could not join table" and "Failed to take
    /// seat", depending on which screen you were on. One list is also what makes
    /// localisation a rename rather than a hunt: these became the "msg.*" keys in
    /// Resources/Localization/*.json, and each member returns the player's language.
    /// (Properties, not consts — a const can't change with the language.)
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

        public static string StandUpAfterHand => Loc.Get("msg.stand_up_after_hand");
        public static string StandUpCancelled => Loc.Get("msg.stand_up_cancelled");
        public static string NotSeated        => Loc.Get("msg.not_seated");
        public static string SeatedNextHand   => Loc.Get("msg.seated_next_hand");
        public static string NotSeatedAtTable => Loc.Get("msg.not_seated_at_table");

        // ── Exit ────────────────────────────────────────────────────────────

        public static string ConfirmLeaveSeatAndExit => Loc.Get("msg.confirm_leave_seat_and_exit");
        public static string ConfirmExitTable        => Loc.Get("msg.confirm_exit_table");
        public static string ExitBlockedInHand       => Loc.Get("msg.exit_blocked_in_hand");

        // Server moved us out of the seat. Fallbacks only — the payload's own
        // message is shown when there is one, since it knows the exact cause.
        public static string MovedToSpectator     => Loc.Get("msg.moved_to_spectator");
        public static string StoodUpToSpectator   => Loc.Get("msg.stood_up_to_spectator");
        public static string SitOutExpired        => Loc.Get("msg.sit_out_expired");
        public static string BustedToSpectator    => Loc.Get("msg.busted_to_spectator");

        /// Sit Out now has a deadline (3 hands), so leaving the table screen on it
        /// is a choice with a cost. Said before it happens, not after.
        public static string SitOutHandLimitWarning => Loc.Get("msg.sit_out_hand_limit_warning");

        // ── Buy-in authorization (player side) ──────────────────────────────
        // Auth-Buyin on: the buy-in becomes a request the host approves.
        // TODO: not wired yet — needs the pending buy-in response and the
        // accepted / rejected socket events from the backend.

        public static string BuyInAuthRequesting => Loc.Get("msg.buy_in_auth_requesting");
        public static string BuyInAuthAccepted   => Loc.Get("msg.buy_in_auth_accepted");
        public static string BuyInAuthRejected   => Loc.Get("msg.buy_in_auth_rejected");

        /// Taking a seat from "+". The server seats no one mid-hand, and it stops
        /// there rather than queueing — say so, so the player knows to tap again.
        public static string TakeSeatHandInProgress => Loc.Get("msg.take_seat_hand_in_progress");

        /// Cause and consequence together: without the second half, being dropped
        /// back at the lobby reads as a crash or a kick.
        public static string TableEmptied     => Loc.Get("msg.table_emptied");

        // ── Connection ──────────────────────────────────────────────────────

        public static string Reconnecting      => Loc.Get("msg.reconnecting");
        public static string StillReconnecting => Loc.Get("msg.still_reconnecting");
        public static string LoginRateLimited  => Loc.Get("msg.login_rate_limited");
        public static string TableUnreachable  => Loc.Get("msg.table_unreachable");

        // ── Money ───────────────────────────────────────────────────────────

        public static string NotEnoughBalance    => Loc.Get("not_enough_balance");
        public static string BuyInUnavailable    => Loc.Get("msg.buy_in_unavailable");
        public static string BuyInFailed         => Loc.Get("msg.buy_in_failed");
        public static string TopUpFailed         => Loc.Get("msg.top_up_failed");
        public static string WithdrawFailed      => Loc.Get("msg.withdraw_failed");
        public static string StackAtTableMaximum => Loc.Get("msg.stack_at_table_maximum");

        // ── Catch-all ───────────────────────────────────────────────────────

        /// Last resort, when an exception carries nothing worth showing.
        public static string SomethingWentWrong => Loc.Get("something_went_wrong");

        // ── With a value in them ────────────────────────────────────────────

        public static string JoinFailed(string reason)     => Loc.Get("msg.join_failed", ("reason", reason));
        public static string WatchFailed(string reason)    => Loc.Get("msg.watch_failed", ("reason", reason));
        public static string TakeSeatFailed(string reason) => Loc.Get("msg.take_seat_failed", ("reason", reason));

        public static string PlayerLostConnection(string who) => Loc.Get("msg.player_lost_connection", ("name", who));
        public static string PlayerReconnected(string who)    => Loc.Get("msg.player_reconnected", ("name", who));
        public static string RemovedForInactivity(string who) => Loc.Get("msg.removed_for_inactivity", ("name", who));

        public static string AutoRebuy(int amount) => Loc.Get("msg.auto_rebuy", ("amount", amount.ToString("N0")));

        public static string AutoWithdraw(int amount) =>
            Loc.Get("msg.auto_withdraw", ("amount", amount.ToString("N0")));

        /// <summary>
        /// Game error codes → player-facing text: "error.&lt;code&gt;" in the language
        /// files (G001 Not your turn, G002 Invalid action, G009 Raise too low,
        /// G010 Already folded, G011 Already all-in, G015 Rule violation, …).
        /// Unknown codes show the server's own message.
        /// </summary>
        public static string ForGameError(string code, string serverMessage)
        {
            return Loc.Error(code, serverMessage ?? Loc.Get("msg.game_error"));
        }
    }
}
