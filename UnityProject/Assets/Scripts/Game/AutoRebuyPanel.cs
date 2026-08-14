using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Auth;

namespace ClubPoker.Game
{
    /// <summary>
    /// Auto Rebuy / Auto Withdraw settings popup, shown from the table menu.
    ///
    /// Only edits and stores settings — it never buys in or withdraws itself.
    /// AutoRebuyHandler watches the table stack and acts on what's saved here, so
    /// the popup can be closed (or never opened) without affecting behaviour.
    /// </summary>
    public class AutoRebuyPanel : MonoBehaviour
    {
        [Header("Header")]
        public Button CloseButton;
        public TextMeshProUGUI InitialBuyInText;   // chip amount, top-left
        public TextMeshProUGUI ChipsBalanceText;   // "Chips Balance: 1,326.06"

        [Header("Auto Rebuy")]
        // The section owns the toggle and collapses its own body when switched off.
        public CollapsibleSection AutoRebuySection;
        public Slider AutoRebuyThresholdSlider;    // whole percent, 0–100
        public TextMeshProUGUI AutoRebuyInfoText;

        [Header("Auto Withdraw")]
        public CollapsibleSection AutoWithdrawSection;
        public Slider AutoWithdrawMultipleSlider;  // whole multiples, 1–10
        public TextMeshProUGUI AutoWithdrawInfoText;

        [Header("Confirm")]
        public Button ConfirmButton;

        [Header("Stacking (no layout groups)")]
        [Tooltip("The popup body to resize as sections open and close.")]
        public RectTransform PanelRect;

        [Tooltip("Everything below the top info, top to bottom: the two sections " +
                 "then the Confirm button. Each must be anchored top with pivot Y = 1.")]
        public RectTransform[] StackItems;

        [Tooltip("Leave 0 to start from wherever the first item is placed in the " +
                 "scene, which keeps your hand-built layout. Set a value to override.")]
        public float StackStartY = 0f;

        [Tooltip("Gap between stack items.")]
        public float Spacing = 12f;

        [Tooltip("Tick to use a different gap before the LAST stack item (normally " +
                 "Confirm). Separate because a section's measured height includes its " +
                 "own padding, which makes the gap above Confirm read larger.")]
        public bool OverrideLastItemSpacing;

        [Tooltip("Gap before the last item when the override is ticked. Negative " +
                 "values are allowed and pull it upward.")]
        public float LastItemSpacing = 12f;

        [Tooltip("Space left below the last item.")]
        public float BottomPadding = 24f;

        [Header("Highlight Colour")]
        [Tooltip("Applied to the numbers inside the description text.")]
        public Color HighlightColor = new Color(1f, 0.82f, 0.29f);

        // The amount the player bought in with. Both features are expressed relative
        // to it — rebuy tops back up to it, withdraw skims anything above a multiple
        // of it.
        private int _initialBuyIn;

        // Captured once from the scene so the popup keeps the position it was built
        // at. Without this the panel imposed its own Y and pushed Confirm off-screen
        // whenever StackStartY didn't happen to match the design.
        private float _stackStartY;
        private bool  _stackStartCaptured;

        private void Awake()
        {
            if (CloseButton != null)
                CloseButton.onClick.AddListener(Close);

            if (ConfirmButton != null)
                ConfirmButton.onClick.AddListener(Confirm);

            // Live preview: the description text is the only place the slider value
            // is readable, so it has to track while dragging.
            if (AutoRebuyThresholdSlider != null)
            {
                AutoRebuyThresholdSlider.wholeNumbers = true;
                AutoRebuyThresholdSlider.minValue = 0;
                AutoRebuyThresholdSlider.maxValue = 100;
                AutoRebuyThresholdSlider.onValueChanged.AddListener(_ => RefreshRebuyText());
            }

            if (AutoWithdrawMultipleSlider != null)
            {
                AutoWithdrawMultipleSlider.wholeNumbers = true;
                AutoWithdrawMultipleSlider.minValue = 1;
                AutoWithdrawMultipleSlider.maxValue = 10;
                AutoWithdrawMultipleSlider.onValueChanged.AddListener(_ => RefreshWithdrawText());
            }

            // Sections show/hide their own bodies; the panel only needs to refresh
            // the description when a section is switched on.
            // Sections hide their own bodies; the panel re-stacks what's below them
            // and resizes. Nothing here relies on Unity layout groups.
            if (AutoRebuySection != null)
            {
                AutoRebuySection.OnChanged = _ =>
                {
                    RefreshRebuyText();
                    Relayout();
                };
            }

            if (AutoWithdrawSection != null)
            {
                AutoWithdrawSection.OnChanged = _ =>
                {
                    RefreshWithdrawText();
                    Relayout();
                };
            }
        }

        /// <summary>
        /// Sync from saved settings whenever the popup appears, however it was shown.
        ///
        /// Without this, activating the object directly (rather than via Open) left
        /// each section's stored state at OFF while its body was still visible from
        /// the scene. Relayout then measured header-only heights and the sections
        /// overlapped — which read as one section collapsing when you touched
        /// another.
        /// </summary>
        private void OnEnable()
        {
            AutoRebuySettings.EnsureLoaded();
            ApplySettingsToUI();
            Relayout();
        }

        /// <summary>
        /// Open the popup, seeded from the saved settings.
        /// </summary>
        public void Open(int initialBuyIn)
        {
            _initialBuyIn = initialBuyIn > 0
                ? initialBuyIn
                : AutoRebuySettings.InitialBuyIn;

            // Remember it so the handler can rebuy the same amount later, and so the
            // popup shows the right figure if reopened after a reconnect.
            AutoRebuySettings.InitialBuyIn = _initialBuyIn;

            gameObject.SetActive(true);   // OnEnable does the rest
            ApplySettingsToUI();
            Relayout();
        }

        private void ApplySettingsToUI()
        {
            if (_initialBuyIn <= 0)
                _initialBuyIn = AutoRebuySettings.InitialBuyIn;

            if (AutoRebuySection != null)
                AutoRebuySection.SetOn(AutoRebuySettings.AutoRebuyEnabled);

            if (AutoRebuyThresholdSlider != null)
                AutoRebuyThresholdSlider.SetValueWithoutNotify(AutoRebuySettings.RebuyThresholdPercent);

            if (AutoWithdrawSection != null)
                AutoWithdrawSection.SetOn(AutoRebuySettings.AutoWithdrawEnabled);

            if (AutoWithdrawMultipleSlider != null)
                AutoWithdrawMultipleSlider.SetValueWithoutNotify(AutoRebuySettings.WithdrawMultiple);

            if (InitialBuyInText != null)
                InitialBuyInText.text = _initialBuyIn.ToString("N0");

            RefreshBalance();
            RefreshRebuyText();
            RefreshWithdrawText();
        }

        private void RefreshBalance()
        {
            if (ChipsBalanceText == null)
                return;

            int wallet = AuthManager.Instance != null && AuthManager.Instance.Session != null
                ? AuthManager.Instance.Session.WalletChips
                : 0;

            ChipsBalanceText.text = $"{wallet:N2}";
        }

        private string Hex => ColorUtility.ToHtmlStringRGB(HighlightColor);

        private void RefreshRebuyText()
        {
            if (AutoRebuyInfoText == null)
                return;

            int percent = AutoRebuyThresholdSlider != null
                ? (int)AutoRebuyThresholdSlider.value
                : AutoRebuySettings.RebuyThresholdPercent;

            AutoRebuyInfoText.text =
                $"When your stack drops to <color=#{Hex}>{percent}%</color>,\n" +
                "it will auto rebuy the initial buy-in.";
        }

        private void RefreshWithdrawText()
        {
            if (AutoWithdrawInfoText == null)
                return;

            int multiple = AutoWithdrawMultipleSlider != null
                ? (int)AutoWithdrawMultipleSlider.value
                : AutoRebuySettings.WithdrawMultiple;

            int chips = multiple * _initialBuyIn;

            AutoWithdrawInfoText.text =
                $"Chips over <color=#{Hex}>{multiple} multiple ({chips:N0} Chips)</color> of the initial buy-in\n" +
                "Auto-withdraw to the amount of initial buy-in.";
        }

        /// <summary>
        /// Stack the items top-down and resize the panel to fit.
        ///
        /// Hand-rolled instead of a Vertical Layout Group: the popup is positioned by
        /// hand, and a layout group would take over every child's placement. This
        /// touches only the transforms listed in StackItems.
        /// </summary>
        private void Relayout()
        {
            if (StackItems == null || StackItems.Length == 0)
                return;

            // Heights are read straight off the RectTransforms, and a body activated
            // this frame still reports its old size until the canvas updates.
            Canvas.ForceUpdateCanvases();

            if (!_stackStartCaptured)
            {
                _stackStartY = !Mathf.Approximately(StackStartY, 0f)
                    ? StackStartY
                    : (StackItems[0] != null ? StackItems[0].anchoredPosition.y : 0f);

                _stackStartCaptured = true;
            }

            float y = _stackStartY;

            for (int i = 0; i < StackItems.Length; i++)
            {
                RectTransform item = StackItems[i];

                if (item == null || !item.gameObject.activeSelf)
                    continue;

                Vector2 pos = item.anchoredPosition;
                item.anchoredPosition = new Vector2(pos.x, y);

                bool lastGap = i == StackItems.Length - 2;

                float gap = lastGap && OverrideLastItemSpacing
                    ? LastItemSpacing
                    : Spacing;

                y -= ItemHeight(item) + gap;
            }

            // Only resize when asked to. If PanelRect is left empty the popup keeps
            // its designed size and just the items move, which is usually what you
            // want when the background art is a fixed frame.
            if (PanelRect != null)
            {
                float used = Mathf.Abs(y) + BottomPadding;
                PanelRect.sizeDelta = new Vector2(PanelRect.sizeDelta.x, used);
            }
        }

        // A section's height depends on whether it's expanded, so ask it rather than
        // reading the RectTransform — the body is inactive and wouldn't be counted.
        private float ItemHeight(RectTransform item)
        {
            CollapsibleSection section = item.GetComponent<CollapsibleSection>();

            return section != null ? section.Height : item.rect.height;
        }

        private void Confirm()
        {
            if (AutoRebuySection != null)
                AutoRebuySettings.AutoRebuyEnabled = AutoRebuySection.IsOn;

            if (AutoRebuyThresholdSlider != null)
                AutoRebuySettings.RebuyThresholdPercent = (int)AutoRebuyThresholdSlider.value;

            if (AutoWithdrawSection != null)
                AutoRebuySettings.AutoWithdrawEnabled = AutoWithdrawSection.IsOn;

            if (AutoWithdrawMultipleSlider != null)
                AutoRebuySettings.WithdrawMultiple = (int)AutoWithdrawMultipleSlider.value;

            AutoRebuySettings.Save();

            Debug.Log($"[AutoRebuy] Saved → rebuy {AutoRebuySettings.AutoRebuyEnabled} " +
                      $"at {AutoRebuySettings.RebuyThresholdPercent}% | " +
                      $"withdraw {AutoRebuySettings.AutoWithdrawEnabled} " +
                      $"over {AutoRebuySettings.WithdrawMultiple}x");

            Close();
        }

        private void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
