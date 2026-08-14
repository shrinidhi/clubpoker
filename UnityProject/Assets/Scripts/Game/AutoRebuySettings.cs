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

        public static bool AutoRebuyEnabled      { get; set; }
        public static int  RebuyThresholdPercent { get; set; } = 0;

        public static bool AutoWithdrawEnabled   { get; set; }
        public static int  WithdrawMultiple      { get; set; } = 1;

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

            AutoRebuyEnabled      = PlayerPrefs.GetInt(KEY_REBUY_ON, 0) == 1;
            RebuyThresholdPercent = PlayerPrefs.GetInt(KEY_REBUY_PCT, 0);
            AutoWithdrawEnabled   = PlayerPrefs.GetInt(KEY_WITHDRAW_ON, 0) == 1;
            WithdrawMultiple      = Mathf.Max(1, PlayerPrefs.GetInt(KEY_WITHDRAW_MUL, 1));
            InitialBuyIn          = PlayerPrefs.GetInt(KEY_INITIAL_BUY, 0);

            _loaded = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(KEY_REBUY_ON,     AutoRebuyEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KEY_REBUY_PCT,    RebuyThresholdPercent);
            PlayerPrefs.SetInt(KEY_WITHDRAW_ON,  AutoWithdrawEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KEY_WITHDRAW_MUL, WithdrawMultiple);
            PlayerPrefs.SetInt(KEY_INITIAL_BUY,  InitialBuyIn);
            PlayerPrefs.Save();

            _loaded = true;
        }
    }
}
