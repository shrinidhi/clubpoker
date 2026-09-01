using System;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ClubPoker.Game
{
    /// <summary>
    /// Auto rebuy / auto withdraw are executed by the server, not the client: it
    /// is told the rule once and applies it every hand. This is the wire for that
    /// rule.
    ///
    ///   → player:auto_config     {"autoRebuy":{"enabled":true,"thresholdPct":5}}
    ///   ← table:auto_config_ack  {"ok":true,"autoRebuy":{…},"autoWithdraw":null}
    ///
    /// The ack is authoritative — whatever it echoes is written back into
    /// <see cref="AutoRebuySettings"/>, so a popup reopened later shows the rule
    /// the server is actually running rather than the last thing typed into the UI.
    ///
    /// Config chosen at buy-in time can't be sent yet (no seat, no table room), so
    /// it is parked with <see cref="MarkPending"/> and flushed by TableJoinHandler
    /// once the join is confirmed.
    /// </summary>
    public static class AutoConfigClient
    {
        private const string EVENT_SEND     = "player:auto_config";
        private const string EVENT_ACK      = "table:auto_config_ack";
        private const string EVENT_REBUY    = "table:auto_rebuy";
        private const string EVENT_WITHDRAW = "table:auto_withdraw";

        /// <summary>Server confirmed a rule and the settings changed — open popups
        /// redraw off this.</summary>
        public static event Action OnAckChanged;

        /// <summary>The server ran an auto rebuy (positive) or auto withdraw
        /// (negative) for us. Amount is 0 when the payload didn't carry one.</summary>
        public static event Action<int> OnAutoAction;

        private static bool _listening;
        private static bool _pending;

        /// <summary>Start listening for acks. Safe to call repeatedly; only the
        /// first call registers.</summary>
        public static void EnsureListening()
        {
            if (_listening || SocketManager.Instance == null)
                return;

            SocketManager.Instance.On(EVENT_ACK, OnAck);

            // The server executes both rules, so these are how we learn they fired.
            SocketManager.Instance.On(EVENT_REBUY,    json => OnAutoAction_Received(json, true));
            SocketManager.Instance.On(EVENT_WITHDRAW, json => OnAutoAction_Received(json, false));

            _listening = true;
        }

        /// <summary>
        /// An auto rebuy or auto withdraw ran. Both move club chips as well as the
        /// table stack, so the cached club balance is refreshed — otherwise the
        /// top-up and buy-in popups keep offering chips that were already spent.
        ///
        /// The payload shape isn't pinned down yet, so the amount is read from
        /// whichever of the usual field names is present and everything else is
        /// tolerated. The raw JSON is logged to make the real shape visible.
        /// </summary>
        private static void OnAutoAction_Received(string json, bool isRebuy)
        {
            string label = isRebuy ? EVENT_REBUY : EVENT_WITHDRAW;

            Debug.Log($"[AutoConfig] ← \"{label}\" {json}");

            try
            {
                var payload = JObject.Parse(json);

                // Only ours. Payloads that don't name a player are assumed to be
                // addressed to this socket.
                string playerId = payload["playerId"]?.ToString()
                                  ?? payload["userId"]?.ToString();

                string myId = AuthManager.Instance != null && AuthManager.Instance.Session != null
                    ? AuthManager.Instance.Session.Id
                    : null;

                if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(myId) &&
                    playerId != myId)
                    return;

                int amount = ReadInt(payload["amount"],
                             ReadInt(payload["chips"],
                             ReadInt(payload["rebuyAmount"],
                             ReadInt(payload["withdrawAmount"], 0))));

                // A club balance may come back with it; use it rather than refetching.
                JToken clubChips = payload["clubChips"];

                if (clubChips is JValue clubValue && clubValue.Value != null)
                    ClubWallet.Set(TableContext.ClubId, Convert.ToInt32(clubValue.Value));
                else if (TableContext.IsClub)
                    ClubWallet.RefreshAsync(TableContext.ClubId).Forget();

                if (amount > 0)
                {
                    ToastEvents.Show(isRebuy
                        ? GameMessages.AutoRebuy(amount)
                        : GameMessages.AutoWithdraw(amount));
                }

                OnAutoAction?.Invoke(isRebuy ? amount : -amount);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoConfig] {label} parse failed: {e.Message}");
            }
        }

        /// <summary>Config was chosen before the seat existed (the buy-in popup).
        /// Send it as soon as we're at the table.</summary>
        public static void MarkPending() => _pending = true;

        /// <summary>Called once the join is confirmed. Sends only if something was
        /// parked, so a plain join emits nothing.</summary>
        public static void FlushPending()
        {
            if (!_pending)
                return;

            _pending = false;
            Send();
        }

        /// <summary>
        /// Emit the current saved settings. Auto-withdraw is left out until the
        /// player has actually configured it — the server treats a missing block as
        /// "unchanged", so an untouched withdraw rule is never overwritten by a
        /// rebuy edit.
        /// </summary>
        public static void Send()
        {
            if (SocketManager.Instance == null)
            {
                Debug.LogWarning("[AutoConfig] No socket — config not sent.");
                return;
            }

            AutoRebuySettings.EnsureLoaded();
            EnsureListening();

            var payload = new AutoConfigPayload
            {
                AutoRebuy = new AutoRebuyBlock
                {
                    Enabled      = AutoRebuySettings.AutoRebuyEnabled,
                    ThresholdPct = AutoRebuySettings.RebuyThresholdPercent
                },
                AutoWithdraw = AutoRebuySettings.WithdrawConfigured
                    ? new AutoWithdrawBlock
                    {
                        Enabled       = AutoRebuySettings.AutoWithdrawEnabled,
                        ThresholdMult = AutoRebuySettings.WithdrawMultiple
                    }
                    : null
            };

            // Log the exact JSON going out, not a summary of it — the emit path has
            // reshaped nested blocks before, and a summary hid it.
            Debug.Log($"[AutoConfig] → \"{EVENT_SEND}\" " +
                      JsonConvert.SerializeObject(payload));

            SocketManager.Instance.Emit(EVENT_SEND, payload);
        }

        private static void OnAck(string json)
        {
            Debug.Log($"[AutoConfig] ← \"{EVENT_ACK}\" {json}");

            try
            {
                var ack = JObject.Parse(json);

                AutoRebuySettings.EnsureLoaded();

                // Echoed blocks win over whatever the UI last set, whichever way the
                // ok flag reads — the server is the one running the rule. Each field
                // is read defensively: a block that comes back malformed leaves that
                // setting alone rather than losing the whole ack.
                var rebuy = ack["autoRebuy"] as JObject;

                if (rebuy != null)
                {
                    AutoRebuySettings.AutoRebuyEnabled =
                        ReadBool(rebuy["enabled"], AutoRebuySettings.AutoRebuyEnabled);

                    AutoRebuySettings.RebuyThresholdPercent =
                        ReadInt(rebuy["thresholdPct"], AutoRebuySettings.RebuyThresholdPercent);
                }

                var withdraw = ack["autoWithdraw"] as JObject;

                if (withdraw != null)
                {
                    AutoRebuySettings.AutoWithdrawEnabled =
                        ReadBool(withdraw["enabled"], AutoRebuySettings.AutoWithdrawEnabled);

                    AutoRebuySettings.WithdrawMultiple =
                        ReadInt(withdraw["thresholdMult"], AutoRebuySettings.WithdrawMultiple);

                    AutoRebuySettings.WithdrawConfigured = true;
                }

                AutoRebuySettings.Save();

                OnAckChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError("[AutoConfig] ack parse failed: " + e.Message);
            }
        }

        /// <summary>A value only when the token really is one — an empty array or a
        /// missing field keeps what we already had.</summary>
        private static bool ReadBool(JToken token, bool fallback) =>
            token is JValue value && value.Value != null
                ? Convert.ToBoolean(value.Value)
                : fallback;

        private static int ReadInt(JToken token, int fallback) =>
            token is JValue value && value.Value != null
                ? Convert.ToInt32(value.Value)
                : fallback;

        // ── Wire shapes ─────────────────────────────────────────────────────

        private class AutoConfigPayload
        {
            [JsonProperty("autoRebuy")]
            public AutoRebuyBlock AutoRebuy { get; set; }

            // Dropped from the JSON entirely when null, which is how "leave the
            // withdraw rule alone" is expressed.
            [JsonProperty("autoWithdraw", NullValueHandling = NullValueHandling.Ignore)]
            public AutoWithdrawBlock AutoWithdraw { get; set; }
        }

        private class AutoRebuyBlock
        {
            [JsonProperty("enabled")]
            public bool Enabled { get; set; }

            [JsonProperty("thresholdPct")]
            public int ThresholdPct { get; set; }
        }

        private class AutoWithdrawBlock
        {
            [JsonProperty("enabled")]
            public bool Enabled { get; set; }

            [JsonProperty("thresholdMult")]
            public int ThresholdMult { get; set; }
        }

    }
}
