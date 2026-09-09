using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ClubPoker.Game
{
    /// <summary>
    /// The club table a player is currently buying into, and the deferred creation
    /// of the engine table behind it.
    ///
    /// A club table row is a template. The real (lobby) table it points at is only
    /// created when somebody actually sits down — tapping a row, looking at it and
    /// backing out must leave nothing behind, or the club fills with empty tables
    /// nobody opened on purpose.
    ///
    /// So the row travels here from the club screen, and <see cref="EnsureTableAsync"/>
    /// runs from the buy-in popup's confirm inside GameTable: create, link, and only
    /// then seat.
    /// </summary>
    public static class ClubSeatFlow
    {
        /// <summary>Club table row being bought into. Null outside the flow.</summary>
        public static ClubTableData Row { get; private set; }

        /// Engine table this flow created and has not linked to the club row yet.
        /// The link is deliberately the LAST call of the join — see LinkPendingAsync.
        private static string _pendingLinkTableId;

        /// <summary>Remember the row and mark a buy-in owed, so GameTable opens the
        /// popup on arrival.</summary>
        public static void Begin(ClubTableData row)
        {
            Row = row;
            TableContext.BeginClubBuyIn();
        }

        /// <summary>Seat taken (or the flow abandoned) — stop offering the buy-in.</summary>
        public static void End()
        {
            Row = null;
            _pendingLinkTableId = null;
            TableContext.EndClubBuyIn();
        }

        /// <summary>Drop the row without touching TableContext. Called from
        /// TableContext.Clear, which is already clearing the flag itself.</summary>
        public static void Forget()
        {
            Row = null;
            _pendingLinkTableId = null;
        }

        /// <summary>
        /// Resolve the row to a live table id, creating the engine table the first
        /// time anyone buys in. Returns null if it couldn't be created.
        ///
        /// Creates only — the club row is linked by <see cref="LinkPendingAsync"/>
        /// once the seat is actually taken.
        /// </summary>
        public static async UniTask<string> EnsureTableAsync()
        {
            // Already live — either the row was linked before we got here, or another
            // member created it while this popup was open.
            if (!string.IsNullOrEmpty(TableContext.TableId))
                return TableContext.TableId;

            ClubTableData row = Row;

            if (row == null)
                return null;

            if (!string.IsNullOrEmpty(row.TableId))
            {
                // Second and later players: the row is already linked, so no create
                // and no link-club-table — buy-in + join only.
                //
                // Keep the back destination the club screen set — this runs inside
                // GameTable, where the scene-derived default would be wrong.
                TableContext.EnterFromClub(row, row.TableId, TableContext.BackScene);
                return row.TableId;
            }

            var req = new CreateTableRequest
            {
                Variant    = row.Variant,
                MaxPlayers = row.MaxSeats,
                SmallBlind = row.SmallBlind,
                BigBlind   = row.BigBlind,
                MinBuyIn   = row.BuyInMin,
                MaxBuyIn   = row.BuyInMax,
                ClubId     = row.ClubId
            };

            var res = await AuthManager.Instance.CreateTableAsync(req);
            string tableId = res?.TableId;

            if (string.IsNullOrEmpty(tableId))
                return null;

            // Link comes after the seat, not here. Buy-in or join can still fail —
            // linking first would publish this table to the whole club as the row's
            // live table while nobody is sitting at it.
            _pendingLinkTableId = tableId;

            // Re-enter with the final id so Back, the club-only menu rows and the
            // in-game popups all point at the right table.
            TableContext.EnterFromClub(row, tableId, TableContext.BackScene);

            return tableId;
        }

        /// <summary>
        /// Point the club row at the table we just created, now that a player is
        /// actually seated at it: POST link-club-table. No-op for anyone joining a
        /// row that was already linked.
        ///
        /// A failure here leaves a live table the row doesn't know about — the
        /// player stays seated (they're already in the hand) and the next arrival
        /// creates a second table, so it's logged loudly rather than swallowed.
        /// </summary>
        public static async UniTask LinkPendingAsync()
        {
            string tableId = _pendingLinkTableId;
            ClubTableData row = Row;

            if (string.IsNullOrEmpty(tableId) || row == null)
                return;

            _pendingLinkTableId = null;

            try
            {
                await AuthManager.Instance.LinkClubTableAsync(tableId, row.ClubId, row.Id);
                row.TableId = tableId;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ClubSeatFlow] Link club table failed: {e.Message}");
            }
        }
    }
}
