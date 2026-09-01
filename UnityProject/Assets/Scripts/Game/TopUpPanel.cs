using System;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    /// <summary>
    /// Top Up — add chips to the stack while seated. Offered on both lobby and club
    /// tables, so there's no collapsible section: slider, amount read-out, balance,
    /// confirm.
    ///
    /// The server applies it at the start of the next hand. Same endpoint as the
    /// seat buy-in (POST /api/economy/buyin) — a top-up is a buy-in on a seat that's
    /// already occupied.
    /// </summary>
    public class TopUpPanel : MonoBehaviour
    {
        [Header("Amount")]
        [SerializeField] private TextMeshProUGUI amountText;   // read-out beside the chip icon
        [SerializeField] private Slider amountSlider;          // the only control

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI balanceText;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        private int _min;
        private int _max;
        private int _amount;
        private bool _busy;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (closeButton != null)   closeButton.onClick.AddListener(Close);

            if (amountSlider != null)
            {
                amountSlider.wholeNumbers = true;
                amountSlider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        private void OnEnable()
        {
            Refresh();

            // Club chips can move while we sit here (other tables, transfers), so the
            // cached figure is only good enough to draw with until the refetch lands.
            if (TableContext.IsClub)
                RefreshClubChipsAsync().Forget();
        }

        private async UniTaskVoid RefreshClubChipsAsync()
        {
            await ClubWallet.RefreshAsync(TableContext.ClubId);

            if (this != null && gameObject.activeInHierarchy)
                Refresh();
        }

        /// <summary>Recompute the range from the live stack and wallet, then reseed.</summary>
        public void Refresh()
        {
            var table = TableContext.Info;

            int minBuyIn = table?.BuyInMin ?? 0;
            int maxBuyIn = table?.BuyInMax ?? 0;
            int wallet   = Wallet;

            // The table caps what a seat may hold, so a big stack can top up less
            // than a short one — and never more than the wallet holds.
            int headroom = maxBuyIn > 0 ? maxBuyIn - MyTableChips : wallet;

            _max = Mathf.Min(headroom, wallet);
            // Fall back to 1 when table metadata never arrived (join by code).
            _min = minBuyIn > 0 ? Mathf.Min(minBuyIn, _max) : 1;
            _min = Mathf.Max(1, _min);

            bool usable = _max >= _min;

            if (confirmButton != null) confirmButton.interactable = usable;
            if (amountSlider != null)  amountSlider.interactable = usable;

            if (!usable)
            {
                ShowError(wallet <= 0
                    ? GameMessages.NotEnoughBalance
                    : GameMessages.StackAtTableMaximum);

                SetAmount(0);
                RefreshBalance();
                return;
            }

            if (amountSlider != null)
            {
                amountSlider.minValue = _min;
                amountSlider.maxValue = _max;
            }

            SetAmount(_min);   // opens at the smallest legal top-up, player slides up
            RefreshBalance();
        }

        private void OnSliderChanged(float value)
        {
            SetAmount(Mathf.RoundToInt(value));
        }

        private void SetAmount(int value)
        {
            _amount = _max >= _min ? Mathf.Clamp(value, _min, _max) : 0;

            if (amountSlider != null) amountSlider.SetValueWithoutNotify(_amount);
            if (amountText != null)   amountText.text = _amount.ToString("N0");
        }

        private void OnConfirm()
        {
            if (_busy || _amount < _min) return;

            TopUpAsync(_amount).Forget();
        }

        private async UniTaskVoid TopUpAsync(int amount)
        {
            string tableId = GameStateManager.Instance != null
                ? GameStateManager.Instance.TableId
                : null;

            if (string.IsNullOrEmpty(tableId))
            {
                ShowError(GameMessages.NotSeatedAtTable);
                return;
            }

            _busy = true;
            // Dead while the call is in flight, so a second tap can't send it twice.
            if (confirmButton != null) confirmButton.interactable = false;

            try
            {
                // Club tables are funded from the club balance, not the wallet —
                // sending the club id is what selects that source server-side.
                BuyInResponse response =
                    await AuthManager.Instance.BuyInAsync(tableId, amount, TableContext.ClubId);

                if (response?.Data != null)
                {
                    // Keep the cached wallet in step so the next popup opens with the
                    // right balance without another profile fetch. (BuyInAsync does the
                    // same for the club balance.)
                    if (response.Data.Source != "club")
                        AuthManager.Instance.Session.WalletChips = response.Data.WalletChips;

                    Debug.Log($"[TopUp] +{amount} → table {response.Data.TableChips}, " +
                              $"wallet {response.Data.WalletChips}");
                }

                Close();
            }
            catch (ApiException e)
            {
                ShowError(string.IsNullOrEmpty(e.Message) ? GameMessages.TopUpFailed : e.Message);
                Debug.LogError($"[TopUp] {e.Code}: {e.Message}");
            }
            catch (Exception e)
            {
                ShowError(GameMessages.SomethingWentWrong);
                Debug.LogError($"[TopUp] {e}");
            }
            finally
            {
                _busy = false;
                if (confirmButton != null) confirmButton.interactable = true;
            }
        }

        private void RefreshBalance()
        {
            if (balanceText != null)
                balanceText.text = $"{Wallet:N0}";
        }

        // Shared bottom toast, same one the club screens use.
        private static void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (InformationPrefabScript.Instance != null)
                InformationPrefabScript.Instance.ShowMessage(message);
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>Spendable balance: club chips at a club table, wallet otherwise.</summary>
        private static int Wallet
        {
            get
            {
                if (TableContext.IsClub)
                    return ClubWallet.Chips;

                return AuthManager.Instance != null && AuthManager.Instance.Session != null
                    ? AuthManager.Instance.Session.WalletChips
                    : 0;
            }
        }

        /// <summary>Chips this player currently has in front of them, 0 if not seated.</summary>
        private static int MyTableChips
        {
            get
            {
                if (GameStateManager.Instance == null ||
                    AuthManager.Instance == null ||
                    AuthManager.Instance.Session == null)
                    return 0;

                var me = GameStateManager.Instance
                    .GetPlayerById(AuthManager.Instance.Session.Id);

                return me?.Chips ?? 0;
            }
        }
    }
}
