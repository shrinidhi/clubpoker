using System;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ClubPoker.Auth
{
    /// <summary>
    /// The player's chip balance *inside a club*.
    ///
    /// Club games are played with club chips, not the global wallet: the club owns
    /// a pool and every member holds their own balance out of it. Buy-in on a club
    /// table debits that balance (POST /api/economy/buyin with clubId returns
    /// "source": "club"), so every screen that shows a spendable balance while a
    /// club table is in play must read this rather than
    /// <see cref="UserSession.WalletChips"/>.
    ///
    /// Source of truth is GET /api/clubs/{clubId}/members/{userId} → member.chips.
    /// Responses that carry a fresh figure (buy-in, top-up, withdraw) feed it back
    /// through <see cref="Set"/> so the UI doesn't need a second round trip.
    /// </summary>
    public static class ClubWallet
    {
        /// <summary>Last known club chip balance for <see cref="ClubId"/>. 0 until
        /// the first refresh lands.</summary>
        public static int Chips { get; private set; }

        /// <summary>Club the balance belongs to. Switching clubs zeroes the figure
        /// rather than showing another club's number.</summary>
        public static string ClubId { get; private set; }

        /// <summary>Raised whenever the balance changes, so open popups can redraw
        /// without polling.</summary>
        public static event Action OnChanged;

        /// <summary>Fetch the member record and store its chip balance. Returns the
        /// balance, or the previous one when the call fails — never throws, so
        /// callers can fire it without guarding every UI path.</summary>
        public static async UniTask<int> RefreshAsync(string clubId = null)
        {
            clubId = string.IsNullOrEmpty(clubId) ? ClubId : clubId;

            string userId = AuthManager.Instance != null && AuthManager.Instance.Session != null
                ? AuthManager.Instance.Session.Id
                : null;

            if (string.IsNullOrEmpty(clubId) || string.IsNullOrEmpty(userId))
                return Chips;

            try
            {
                ClubMemberData member =
                    await AuthManager.Instance.GetMemberDetailAsync(clubId, userId);

                if (member != null)
                    Set(clubId, member.Chips);
            }
            catch (Exception e)
            {
                // Stale balance beats a blank one — the popup still opens and the
                // server rejects an over-spend anyway.
                Debug.LogWarning($"[ClubWallet] Refresh failed: {e.Message}");
            }

            return Chips;
        }

        /// <summary>Write a balance known from another response (buy-in, top-up,
        /// withdraw), skipping the member fetch.</summary>
        public static void Set(string clubId, int chips)
        {
            if (!string.IsNullOrEmpty(clubId))
                ClubId = clubId;

            if (Chips == chips)
                return;

            Chips = chips;
            OnChanged?.Invoke();
        }

        /// <summary>Leaving the club context — drop the figure so a stale balance
        /// can't be shown against a different club.</summary>
        public static void Clear()
        {
            ClubId = null;
            Chips = 0;
            OnChanged?.Invoke();
        }
    }
}
