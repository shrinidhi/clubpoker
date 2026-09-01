using UnityEngine;

namespace ClubPoker.Game
{
    /// <summary>
    /// Auto Rebuy / Auto Withdraw preferences.
    ///
    /// Stored in PlayerPrefs for now. The agreed destination is the player profile
    /// on the backend, which has no field for it yet — when that lands, only Load()
    /// and Save() change and nothing else has to move.
    ///
    /// Deliberately not per-table: the reference client treats these as a player
    /// preference that follows you between tables.
    /// </summary>
    public static class AutoRebuySettings
    {
        private const string KEY_REBUY_ON     = "autoRebuy.enabled";
        private const string KEY_REBUY_PCT    = "autoRebuy.thresholdPercent";
        private const string KEY_WITHDRAW_ON  = "autoWithdraw.enabled";
        private const string KEY_WITHDRAW_MUL = "autoWithdraw.multiple";
        private const string KEY_INITIAL_BUY  = "autoRebuy.initialBuyIn";
        private const string KEY_WITHDRAW_SET = "autoWithdraw.configured";

        /// Auto rebuy is on by default at <see cref="DefaultRebuyPercent"/> — a player
        /// who never opens the section still gets topped back up rather than being
        /// blinded out, which is how the reference client behaves.
        public const int DefaultRebuyPercent = 10;

        public static bool AutoRebuyEnabled      { get; set; } = true;
        public static int  RebuyThresholdPercent { get; set; } = DefaultRebuyPercent;

        public static bool AutoWithdrawEnabled   { get; set; }
        public static int  WithdrawMultiple      { get; set; } = 1;

        /// <summary>
        /// The player has set the withdraw rule at least once (or the server acked
        /// one). Until then the autoWithdraw block is omitted from
        /// player:auto_config so a rebuy edit can't overwrite a rule that was never
        /// chosen.
        /// </summary>
        public static bool WithdrawConfigured    { get; set; }

        /// <summary>
        /// What the player bought in with. Both features are relative to it, and it
        /// is not in game:state_update — so it is captured at buy-in time and kept
        /// here, where it survives a reconnect.
        /// </summary>
        public static int InitialBuyIn { get; set; }

        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded)
                return;

            // Defaults, not zeroes: nothing saved yet means a first-time player, and
            // they get auto rebuy on at 10%.
            AutoRebuyEnabled      = PlayerPrefs.GetInt(KEY_REBUY_ON, 1) == 1;
            RebuyThresholdPercent = PlayerPrefs.GetInt(KEY_REBUY_PCT, DefaultRebuyPercent);
            AutoWithdrawEnabled   = PlayerPrefs.GetInt(KEY_WITHDRAW_ON, 0) == 1;
            WithdrawMultiple      = Mathf.Max(1, PlayerPrefs.GetInt(KEY_WITHDRAW_MUL, 1));
            InitialBuyIn          = PlayerPrefs.GetInt(KEY_INITIAL_BUY, 0);
            WithdrawConfigured    = PlayerPrefs.GetInt(KEY_WITHDRAW_SET, 0) == 1;

            _loaded = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(KEY_REBUY_ON,     AutoRebuyEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KEY_REBUY_PCT,    RebuyThresholdPercent);
            PlayerPrefs.SetInt(KEY_WITHDRAW_ON,  AutoWithdrawEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KEY_WITHDRAW_MUL, WithdrawMultiple);
            PlayerPrefs.SetInt(KEY_INITIAL_BUY,  InitialBuyIn);
            PlayerPrefs.SetInt(KEY_WITHDRAW_SET, WithdrawConfigured ? 1 : 0);
            PlayerPrefs.Save();

            _loaded = true;
        }
    }
}
