using System;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    /// <summary>
    /// Club seat buy-in popup — its own screen, separate from the lobby's BuyInView.
    ///
    /// The difference that earns the separate screen is the Auto Rebuy section: a
    /// collapsible whose switch and threshold are saved with the buy-in, so the
    /// player sets "rebuy back to this amount at N%" in the same breath as sitting
    /// down. Top Up and Withdraw have no collapsible — they're single actions.
    /// </summary>
    public class ClubBuyInPanel : MonoBehaviour
    {
        [Header("Amount")]
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Slider amountSlider;

        [Header("Labels")]
        [Tooltip("Spendable balance — club chips on a club table, wallet otherwise.")]
        [SerializeField] private TextMeshProUGUI balanceText;

        [Tooltip("Table minimum buy-in, shown in the coin pill at the top.")]
        [SerializeField] private TextMeshProUGUI minBuyInText;

        [Header("Auto Rebuy")]
        [Tooltip("Collapsible section: switch in the header, threshold slider in the body.")]
        [SerializeField] private CollapsibleSection autoRebuySection;
        [SerializeField] private Slider autoRebuyThresholdSlider;   // whole percent, 0–100
        [SerializeField] private TextMeshProUGUI autoRebuyInfoText;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        [Header("Resize (no layout groups)")]
        [Tooltip("Popup body to shrink and grow. Anchor it top with pivot Y = 1 so " +
                 "it grows downward. Leave empty to keep the designed size and only " +
                 "move the items.")]
        [SerializeField] private RectTransform panelRect;

        [Tooltip("Items that stack top to bottom — normally the Auto Rebuy section " +
                 "then the Confirm button. Each anchored top, pivot Y = 1.")]
        [SerializeField] private RectTransform[] stackItems;

        [Tooltip("Gap BETWEEN items. Not added after the last one.")]
        [SerializeField] private float spacing = 12f;

        [Tooltip("Space below the last item. Independent of Spacing.")]
        [SerializeField] private float bottomPadding = 24f;

        [Header("Slider stepping")]
        [Tooltip("Roughly how many stops the amount slider should have. The step is " +
                 "rounded to a nice number (1, 2, 5, 10, 25, 50, 100…) from the " +
                 "buy-in range divided by this.")]
        [SerializeField] private int targetSteps = 10;

        private string _tableId;
        private string _clubId;
        private int _step = 1;

        // False only between opening a club buy-in and its chip fetch returning.
        // Wallet buy-ins know the balance from the session and start true.
        private bool _balanceKnown = true;

        // Opened as the seat handshake rather than as an optional popup: there is no
        // "+ seat" control behind it to re-open it with, so dismissing it must leave
        // the table, not drop the player onto a table they never joined.
        private bool _mustBuyInOrLeave;

        // Set by a successful confirm, so the Close that follows it doesn't read as
        // a dismissal and bounce the player back out of the table they just joined.
        private bool _boughtIn;

        private int _min;
        private int _max;
        private int _amount;
        private bool _busy;

        // Top of the stack, captured once from the scene so the popup keeps the
        // position it was built at rather than imposing a Y of its own.
        private float _stackStartY;
        private bool _stackStartCaptured;

        // Open() has run and the range is real. Until then Refresh must not judge
        // the popup unusable — _min/_max are still 0 and it would toast on open.
        private bool _configured;


        /// <summary>What the caller does with the confirmed amount — normally
        /// buy-in + seat. Left to the caller so this panel stays out of the join
        /// sequence.</summary>
        private Func<int, UniTask> _onConfirm;

        private void Awake()
        {
            AutoRebuySettings.EnsureLoaded();

            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (closeButton != null)   closeButton.onClick.AddListener(Close);

            if (amountSlider != null)
            {
                amountSlider.wholeNumbers = true;
                amountSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (autoRebuyThresholdSlider != null)
            {
                autoRebuyThresholdSlider.wholeNumbers = true;
                autoRebuyThresholdSlider.minValue = 0;
                autoRebuyThresholdSlider.maxValue = 100;
                autoRebuyThresholdSlider.onValueChanged.AddListener(_ => RefreshRebuyInfo());
            }

            // The section hides its own body; the popup re-stacks what sits below it
            // and resizes. No Unity layout groups involved.
            if (autoRebuySection != null)
            {
                autoRebuySection.OnChanged = _ =>
                {
                    RefreshRebuyInfo();
                    Relayout();
                };
            }
        }

        /// <summary>Open for a table. <paramref name="onConfirm"/> runs on a valid
        /// amount; throwing from it keeps the popup open with the error shown.
        ///
        /// <paramref name="clubId"/> makes this a club buy-in: the balance shown and
        /// spent is the member's club chips, not the wallet. <paramref name="tableId"/>
        /// may be null when the real table is only created on confirm — the callback
        /// owns the join in that case.</summary>
        /// <param name="mustBuyInOrLeave">This popup IS the way into the table (the
        /// club entry), so closing it goes back to where the player came from
        /// instead of leaving them stranded at a table with no seat and no way to
        /// ask for one.</param>
        public void Open(string tableId, int min, int max, Func<int, UniTask> onConfirm,
                         string clubId = null, bool mustBuyInOrLeave = false)
        {
            _mustBuyInOrLeave = mustBuyInOrLeave;
            _boughtIn         = false;

            _tableId    = tableId;
            _clubId     = string.IsNullOrEmpty(clubId) ? TableContext.ClubId : clubId;
            _min        = min;
            _max        = max;
            _onConfirm  = onConfirm;
            _configured = true;

            // Club chips move outside this client (other tables, transfers), so the
            // number on screen is only trustworthy if re-read on open. Nothing may
            // judge the balance until that lands.
            bool isClub = !string.IsNullOrEmpty(_clubId);
            _balanceKnown = !isClub;

            gameObject.SetActive(true);
            Refresh();

            if (isClub)
                RefreshClubChipsAsync().Forget();
        }

        private void OnEnable()
        {
            ClubWallet.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            ClubWallet.OnChanged -= Refresh;
        }

        private async UniTaskVoid RefreshClubChipsAsync()
        {
            await ClubWallet.RefreshAsync(_clubId);

            // Even a failed fetch counts as known: it falls back to the cached figure,
            // and staying silent forever would hide a genuinely short balance.
            _balanceKnown = true;

            if (this != null && gameObject.activeInHierarchy)
                Refresh();
        }

        /// <summary>
        /// Buy-in range. Open() supplies it; when the popup is shown directly
        /// (activated in the scene, no caller) fall back to the table we're at, so
        /// the slider still has a real range to move in instead of [0,0].
        /// </summary>
        private void ResolveLimits(out int min, out int max)
        {
            if (_configured)
            {
                min = _min;
                max = _max;
                return;
            }

            var table = TableContext.Info;
            min = table?.BuyInMin ?? 0;
            max = table?.BuyInMax ?? 0;
        }

        private void Refresh()
        {
            // Never offer more than the wallet holds — the server would reject it
            // and the player would only find out after confirming.
            ResolveLimits(out _min, out _max);

            int max = Mathf.Min(_max, Wallet);
            bool usable = _min > 0 && max >= _min;

            if (confirmButton != null) confirmButton.interactable = usable;
            if (amountSlider != null)  amountSlider.interactable = usable;

            if (!usable)
            {
                // Club chips arrive from a fetch that starts with the popup, so an
                // unusable range means nothing until it lands — complaining first
                // would toast "Not enough balance" at every player on every open.
                if (_balanceKnown)
                    ShowError(Wallet < _min ? GameMessages.NotEnoughBalance : GameMessages.BuyInUnavailable);

                SetAmount(0);
            }
            else
            {
                // Stepping comes from the TABLE's range, not the wallet-capped one:
                // the stops a player sees must be the same at this table however
                // many chips they happen to hold. A short balance only moves where
                // the slider ends, not where it clicks.
                _step = NiceStep(_max - _min, targetSteps);

                // Range first — SetAmount below writes into the slider, and writing
                // before the range is set would clamp against the old bounds.
                if (amountSlider != null)
                {
                    amountSlider.minValue = _min;
                    amountSlider.maxValue = max;
                }

                SetAmount(_min);
            }

            if (balanceText != null)
                balanceText.text = $"{Wallet:N0}";

            if (minBuyInText != null)
                minBuyInText.text = _min.ToString("N0");

            if (autoRebuySection != null)
                autoRebuySection.SetOn(AutoRebuySettings.AutoRebuyEnabled);

            if (autoRebuyThresholdSlider != null)
                autoRebuyThresholdSlider.SetValueWithoutNotify(
                    AutoRebuySettings.RebuyThresholdPercent);

            RefreshRebuyInfo();
            Relayout();
        }

        /// <summary>
        /// Re-stack the items and resize the popup, in code — no layout groups.
        ///
        /// Called whenever the Auto Rebuy section opens or closes: collapsed, its
        /// body is inactive, so the section measures shorter and everything below it
        /// moves up.
        ///
        /// The two knobs are independent by construction:
        ///   Spacing       — only ever inserted BETWEEN two items, never after the
        ///                   last one, so it can't leak into the popup's height.
        ///   BottomPadding — the only thing added below the last item.
        /// Negative values on either are fine and mean exactly what they say: pull
        /// the next item up, or end the popup above the last item's bottom edge.
        /// </summary>
        private void Relayout()
        {
            if (stackItems == null || stackItems.Length == 0)
                return;

            if (!_stackStartCaptured)
            {
                // Design-time Y of the first item is the top of the stack, so the
                // popup keeps the position it was built at.
                _stackStartY = stackItems[0] != null
                    ? stackItems[0].anchoredPosition.y
                    : 0f;

                _stackStartCaptured = true;
            }

            float y = _stackStartY;
            bool first = true;

            foreach (RectTransform item in stackItems)
            {
                if (item == null || !item.gameObject.activeSelf)
                    continue;

                // Gap goes before each item except the first — that way a trailing
                // gap never exists and BottomPadding stands alone.
                if (!first) y -= spacing;
                first = false;

                item.anchoredPosition = new Vector2(item.anchoredPosition.x, y);

                y -= ItemHeight(item);
            }

            // y is now the bottom edge of the last item, measured down from the
            // panel's top (items are anchored top, so their Y is negative).
            if (panelRect != null)
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, -y + bottomPadding);
        }

        // A section's height depends on whether it's expanded, so ask it rather than
        // reading the RectTransform — the body is inactive and wouldn't be counted.
        private static float ItemHeight(RectTransform item)
        {
            CollapsibleSection section = item.GetComponent<CollapsibleSection>();

            return section != null ? section.Height : item.rect.height;
        }

        private void OnSliderChanged(float value)
        {
            SetAmount(Mathf.RoundToInt(value));
        }

        /// <summary>
        /// A round increment for the buy-in range: the range split into roughly
        /// <paramref name="steps"/> parts, then rounded up to the nearest 1 / 2 / 5 ×
        /// a power of ten. A 100–1000 table steps in 100s, a 10–100 one in 10s, and
        /// nothing ever lands on a number like 63.
        /// </summary>
        private static int NiceStep(int range, int steps)
        {
            if (range <= 0 || steps <= 0)
                return 1;

            float raw = (float)range / steps;

            if (raw <= 1f)
                return 1;

            // Split into mantissa × 10^exponent, round the mantissa up to 1/2/5, and
            // put it back together.
            int exponent = Mathf.FloorToInt(Mathf.Log10(raw));
            float pow = Mathf.Pow(10f, exponent);
            float mantissa = raw / pow;

            float nice = mantissa <= 1f ? 1f
                       : mantissa <= 2f ? 2f
                       : mantissa <= 5f ? 5f
                       : 10f;

            return Mathf.Max(1, Mathf.RoundToInt(nice * pow));
        }

        /// <summary>Snap to the nearest step above the minimum, keeping the two ends
        /// of the range exactly reachable — a step that doesn't divide the range
        /// evenly must not cost the player the table maximum.</summary>
        private int Snap(int value, int min, int max)
        {
            if (_step <= 1)
                return Mathf.Clamp(value, min, max);

            int snapped = min + Mathf.RoundToInt((float)(value - min) / _step) * _step;

            // Within half a step of the top → the top itself, so the max is always
            // selectable however the range divides.
            if (max - value < _step * 0.5f)
                snapped = max;

            return Mathf.Clamp(snapped, min, max);
        }

        private void SetAmount(int value)
        {
            int max = Mathf.Min(_max, Wallet);
            bool usable = _min > 0 && max >= _min;

            _amount = usable ? Snap(value, _min, max) : 0;

            // With no usable range, leave the slider alone — writing 0 back into it
            // on every drag is what makes the handle look stuck.
            if (amountSlider != null && usable)
                amountSlider.SetValueWithoutNotify(_amount);

            if (amountText != null)
                amountText.text = _amount.ToString("N0");
        }

        private void OnConfirm()
        {
            if (_busy || _amount < _min) return;

            if (_amount > Wallet)
            {
                ShowError(GameMessages.NotEnoughBalance);
                return;
            }

            BuyInAsync(_amount).Forget();
        }

        private async UniTaskVoid BuyInAsync(int amount)
        {
            _busy = true;
            if (confirmButton != null) confirmButton.interactable = false;

            try
            {
                if (_onConfirm != null)
                    await _onConfirm(amount);
                else
                    await AuthManager.Instance.BuyInAsync(_tableId, amount, _clubId);

                SaveRebuySettings(amount);

                _boughtIn = true;
                Close();
            }
            catch (ApiException e)
            {
                ShowError(string.IsNullOrEmpty(e.Message) ? GameMessages.BuyInFailed : e.Message);
                Debug.LogError($"[ClubBuyIn] {e.Code}: {e.Message}");
            }
            catch (Exception e)
            {
                ShowError(GameMessages.SomethingWentWrong);
                Debug.LogError($"[ClubBuyIn] {e}");
            }
            finally
            {
                _busy = false;
                if (confirmButton != null) confirmButton.interactable = true;
            }
        }

        private void SaveRebuySettings(int amount)
        {
            // Auto rebuy is relative to what was actually bought in with, and that
            // number appears in no server payload — capture it here or it's lost.
            AutoRebuySettings.InitialBuyIn = amount;

            if (autoRebuySection != null)
                AutoRebuySettings.AutoRebuyEnabled = autoRebuySection.IsOn;

            if (autoRebuyThresholdSlider != null)
                AutoRebuySettings.RebuyThresholdPercent = (int)autoRebuyThresholdSlider.value;

            AutoRebuySettings.Save();

            // The server runs the rule, but there's no seat to attach it to yet —
            // TableJoinHandler emits it once the join is confirmed.
            AutoConfigClient.MarkPending();

            Debug.Log($"[ClubBuyIn] {amount} | autoRebuy {AutoRebuySettings.AutoRebuyEnabled} " +
                      $"at {AutoRebuySettings.RebuyThresholdPercent}%");
        }

        private void RefreshRebuyInfo()
        {
            if (autoRebuyInfoText == null) return;

            int percent = autoRebuyThresholdSlider != null
                ? (int)autoRebuyThresholdSlider.value
                : AutoRebuySettings.RebuyThresholdPercent;

            autoRebuyInfoText.text =
                $"When your stack drops to <color=#ECBF8D>{percent}%</color>,\n" +
                "it will auto rebuy the initial buy-in.";
        }

        // Shared bottom toast, same one the club screens use.
        private static void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (InformationPrefabScript.Instance != null)
                InformationPrefabScript.Instance.ShowMessage(message);
        }

        public void Close()
        {
            // Dismissed or done — either way the buy-in is no longer owed on arrival.
            TableContext.EndClubBuyIn();

            gameObject.SetActive(false);

            // Nothing to stay for: no seat was taken and this popup was the only way
            // to take one, so leave the table rather than showing an empty felt.
            if (_mustBuyInOrLeave && !_boughtIn)
                TableExitRouter.GoBackAndClear();

            _mustBuyInOrLeave = false;
        }

        /// <summary>Spendable balance for this buy-in. Club tables are played with
        /// club chips — the wallet figure is irrelevant there and showing it would
        /// offer amounts the server refuses.</summary>
        private int Wallet
        {
            get
            {
                if (!string.IsNullOrEmpty(_clubId))
                    return ClubWallet.Chips;

                return AuthManager.Instance != null && AuthManager.Instance.Session != null
                    ? AuthManager.Instance.Session.WalletChips
                    : 0;
            }
        }
    }
}
