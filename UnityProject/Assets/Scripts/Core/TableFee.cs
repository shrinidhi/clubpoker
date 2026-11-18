using System;
using System.Globalization;

namespace ClubPoker.Core
{
    /// <summary>
    /// Club table fee. The player types a percent (0–10, decimals allowed); the
    /// server takes and returns a fraction (feePercent 0.05 = 5%) and rejects
    /// anything over 0.10 with V002.
    ///
    /// Tables and templates saved before the switch still hold the raw percent
    /// (5 for 5%). No fraction can reach 1 under the 10% cap, so a value of 1 or
    /// more is read as one of those.
    /// </summary>
    public static class TableFee
    {
        public const double MinPercent = 0;
        public const double MaxPercent = 10;

        /// Typed text → percent. Accepts "2.5" and "2,5" whatever the device
        /// language. False for empty or non-numeric text.
        public static bool TryParsePercent(string text, out double percent)
        {
            percent = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return double.TryParse(text.Trim().Replace(',', '.'),
                                   NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
        }

        public static bool InRange(double percent) =>
            percent >= MinPercent && percent <= MaxPercent;

        /// 5 → 0.05, for the request. Rounded so 2.3 doesn't go out as 0.022999….
        public static double ToFraction(double percent) =>
            (double)Math.Round((decimal)percent / 100m, 6);

        /// Server value → percent for the input. 0.05 → 5; legacy 5 → 5.
        public static double ToPercent(double serverValue) =>
            serverValue >= 1 ? serverValue : (double)Math.Round((decimal)serverValue * 100m, 4);

        /// Server value → fraction, fixing legacy raw-percent rows (5 → 0.05).
        public static double NormalizeFraction(double serverValue) =>
            serverValue >= 1 ? ToFraction(serverValue) : serverValue;

        /// Percent as the input shows it: 5 → "5", 2.5 → "2.5".
        public static string Format(double percent) =>
            percent.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
