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
    /// Withdraw Chips — move part of the stack back to the wallet without leaving
    /// the seat. Club tables only; a lobby stack settles on leave instead.
    ///
    /// Two table rules, both enforced here:
    ///   • at least one minimum buy-in has to stay on the table, and
    ///   • the amount withdrawn is a whole multiple of the minimum buy-in.
    /// The slider therefore steps in min-buy-in units.
    /// </summary>
    public class WithdrawChipsPanel : MonoBehaviour
    {
        [Header("Amount")]
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Slider amountSlider;   // steps of one min buy-in

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI balanceText;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        private int _minBuyIn;   // also the step
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

        private void OnEnable() => Refresh();

        /// <summary>Recompute what may be withdrawn from the live stack.</summary>
        public void Refresh()
        {
            _minBuyIn = TableContext.Info?.BuyInMin ?? 0;

            // Whole multiples of the min buy-in, keeping one behind.
            int multiples = _minBuyIn > 0 ? (MyTableChips - _minBuyIn) / _minBuyIn : 0;
            _max = multiples * _minBuyIn;

            bool usable = _minBuyIn > 0 && multiples >= 1;

            if (confirmButton != null) confirmButton.interactable = usable;
            if (amountSlider != null)  amountSlider.interactable = usable;

            if (!usable)
            {
                ShowError(_minBuyIn > 0
                    ? $"Need more than {_minBuyIn:N0} on the table to withdraw"
                    : "Table limits unavailable");

                SetAmount(0);
                RefreshBalance();
                return;
            }

            // Slider counts multiples, so every position is already a legal amount —
            // no snapping needed, and the handle can't land between steps.
            if (amountSlider != null)
            {
                amountSlider.minValue = 1;
                amountSlider.maxValue = multiples;
            }

            SetMultiple(1);
            RefreshBalance();
        }

        private void OnSliderChanged(float value)
        {
            SetMultiple(Mathf.RoundToInt(value));
        }

        private void SetMultiple(int multiple)
        {
            SetAmount(multiple * _minBuyIn);

            if (amountSlider != null)
                amountSlider.SetValueWithoutNotify(multiple);
        }

        private void SetAmount(int value)
        {
            _amount = _max > 0 ? Mathf.Clamp(value, _minBuyIn, _max) : 0;

            if (amountText != null)
                amountText.text = _amount.ToString("N0");
        }

        private void OnConfirm()
        {
            if (_busy || _amount <= 0) return;

            // Re-checked against the live stack, not the value read when the popup
            // opened — a hand can finish while it sits there.
            if (MyTableChips - _amount < _minBuyIn)
            {
                ShowError($"Must keep at least {_minBuyIn:N0} on the table");
                Refresh();
                return;
            }

            WithdrawAsync(_amount).Forget();
        }

        private async UniTaskVoid WithdrawAsync(int amount)
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
            if (confirmButton != null) confirmButton.interactable = false;

            try
            {
                BuyInResponse response =
                    await AuthManager.Instance.WithdrawChipsAsync(tableId, amount);

                if (response?.Data != null)
                {
                    // Club stacks settle back into the club balance, not the wallet.
                    if (response.Data.Source == "club")
                        ClubWallet.Set(TableContext.ClubId, response.Data.ClubChips);
                    else
                        AuthManager.Instance.Session.WalletChips = response.Data.WalletChips;

                    Debug.Log($"[Withdraw] -{amount} → table {response.Data.TableChips}, " +
                              $"balance {(response.Data.Source == "club" ? response.Data.ClubChips : response.Data.WalletChips)}" +
                              $" ({response.Data.Source ?? "wallet"})");
                }

                Close();
            }
            catch (ApiException e)
            {
                ShowError(string.IsNullOrEmpty(e.Message) ? GameMessages.WithdrawFailed : e.Message);
                Debug.LogError($"[Withdraw] {e.Code}: {e.Message}");
            }
            catch (Exception e)
            {
                ShowError(GameMessages.SomethingWentWrong);
                Debug.LogError($"[Withdraw] {e}");
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
                balanceText.text = $"Chips Balance: {Wallet:N0}";
        }

        // Shared bottom toast, same one the club screens use.
        private static void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (InformationPrefabScript.Instance != null)
                InformationPrefabScript.Instance.ShowMessage(message);
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>Balance the withdrawn chips land in — club chips at a club
        /// table, wallet otherwise.</summary>
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
