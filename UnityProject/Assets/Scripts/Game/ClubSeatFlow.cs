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
            TableContext.EndClubBuyIn();
        }

        /// <summary>Drop the row without touching TableContext. Called from
        /// TableContext.Clear, which is already clearing the flag itself.</summary>
        public static void Forget() => Row = null;

        /// <summary>
        /// Resolve the row to a live table id, creating and linking the engine table
        /// the first time anyone buys in. Returns null if it couldn't be created.
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

            try
            {
                await AuthManager.Instance.LinkClubTableAsync(tableId, row.ClubId, row.Id);
                row.TableId = tableId;
            }
            catch (System.Exception e)
            {
                // The table exists — seat this player anyway; the club list re-links
                // on its next refresh.
                Debug.LogError($"[ClubSeatFlow] Link club table failed: {e.Message}");
            }

            // Re-enter with the final id so Back, the club-only menu rows and the
            // in-game popups all point at the right table.
            TableContext.EnterFromClub(row, tableId, TableContext.BackScene);

            return tableId;
        }
    }
}
