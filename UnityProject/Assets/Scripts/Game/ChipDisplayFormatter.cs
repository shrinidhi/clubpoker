using System;
using System.Globalization;
using UnityEngine;

namespace ClubPoker.Game
{
    // Presentation only. Server values, balances and bets remain in chips.
    public static class ChipDisplayFormatter
    {
        public static bool ShowInBB { get; private set; }
        public static decimal BigBlind { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ShowInBB = false;
            BigBlind = 0m;
            Changed = null;
        }

        public static void SetShowInBB(bool enabled)
        {
            if (ShowInBB == enabled) return;
            ShowInBB = enabled;
            Changed?.Invoke();
        }

        public static void SetBigBlind(decimal amount)
        {
            amount = Math.Max(0m, amount);
            if (BigBlind == amount) return;
            BigBlind = amount;
            Changed?.Invoke();
        }

        public static string Format(int chips)
        {
            // Until table metadata is available, retain chip display (no division by zero).
            if (!ShowInBB || BigBlind <= 0m)
                return chips.ToString(CultureInfo.InvariantCulture);
            decimal bb = Math.Round(chips / BigBlind, 2, MidpointRounding.AwayFromZero);
            return bb.ToString("0.##", CultureInfo.InvariantCulture) + " BB";
        }
    }
}
