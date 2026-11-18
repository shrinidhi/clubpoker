using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Game
{
    /// <summary>
    /// Club tables this player still has a seat at, kept on the device.
    ///
    /// A club table is entered through the buy-in popup, but leaving with Back
    /// keeps the seat (sit out), and buying in at a table you're already seated
    /// at only fails with "already seated". With a record here, opening the
    /// table skips the popup and rejoins the seat instead
    /// (TableJoinHandler.RejoinHeldSeat).
    ///
    /// A hint, not the truth: the server can release the seat while we're away
    /// (3 sat-out hands, a stand up on another device). The rejoin checks the
    /// state it gets back and falls back to the popup when we aren't in it.
    /// Recorded whenever a club seat is confirmed; dropped on a full leave or
    /// when a rejoin finds the seat gone. Per player, so a shared device can't
    /// skip someone else's buy-in.
    /// </summary>
    public static class HeldClubSeat
    {
        private const string KeyPrefix = "held_club_seats_";

        public static bool Has(string tableId) =>
            !string.IsNullOrEmpty(tableId) && Load().Contains(tableId);

        public static void Remember(string tableId)
        {
            if (string.IsNullOrEmpty(tableId)) return;

            HashSet<string> tables = Load();
            if (tables.Add(tableId))
                Save(tables);
        }

        public static void Forget(string tableId)
        {
            if (string.IsNullOrEmpty(tableId)) return;

            HashSet<string> tables = Load();
            if (tables.Remove(tableId))
                Save(tables);
        }

        private static string Key =>
            KeyPrefix + (Auth.AuthManager.Instance?.Session?.Id ?? "none");

        private static HashSet<string> Load()
        {
            string raw = PlayerPrefs.GetString(Key, string.Empty);
            return new HashSet<string>(
                raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void Save(HashSet<string> tables)
        {
            PlayerPrefs.SetString(Key, string.Join(",", tables));
            PlayerPrefs.Save();
        }
    }
}
