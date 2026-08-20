using System;
using ClubPoker.Auth;
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
        [SerializeField] private TextMeshProUGUI balanceText;

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

        private string _tableId;
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
        /// amount; throwing from it keeps the popup open with the error shown.</summary>
        public void Open(string tableId, int min, int max, Func<int, UniTask> onConfirm)
        {
            _tableId    = tableId;
            _min        = min;
            _max        = max;
            _onConfirm  = onConfirm;
            _configured = true;

            gameObject.SetActive(true);
            Refresh();
        }

        private void OnEnable() => Refresh();

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
                ShowError(Wallet < _min ? "Not enough balance" : "Buy-in unavailable");
                SetAmount(0);
            }
            else
            {
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

        private void SetAmount(int value)
        {
            int max = Mathf.Min(_max, Wallet);
            bool usable = _min > 0 && max >= _min;

            _amount = usable ? Mathf.Clamp(value, _min, max) : 0;

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
                ShowError("Not enough balance");
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
                    await AuthManager.Instance.BuyInAsync(_tableId, amount);

                SaveRebuySettings(amount);
                Close();
            }
            catch (ApiException e)
            {
                ShowError(string.IsNullOrEmpty(e.Message) ? "Buy-in failed" : e.Message);
                Debug.LogError($"[ClubBuyIn] {e.Code}: {e.Message}");
            }
            catch (Exception e)
            {
                ShowError("Something went wrong");
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

        public void Close() => gameObject.SetActive(false);

        private static int Wallet =>
            AuthManager.Instance != null && AuthManager.Instance.Session != null
                ? AuthManager.Instance.Session.WalletChips
                : 0;
    }
}
