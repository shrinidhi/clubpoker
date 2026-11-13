using System;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    /// Sent over the socket (player:manual_top_up → table:manual_top_up_ack); the
    /// server applies it at the start of the next hand.
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

        private const string EVENT_TOP_UP = "player:manual_top_up";
        private const string EVENT_ACK    = "table:manual_top_up_ack";
        private const float  AckTimeoutSeconds = 10f;

        private int _step = 1;
        private int _max;
        private int _amount;
        private bool _busy;
        private UniTaskCompletionSource<JObject> _ack;

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
            _amount = 0;   // every open starts from nothing; the player slides up
            Refresh();

            // Both balances can move while we sit here (other tables, transfers,
            // auto rebuy), so the cached figure is only good enough to draw with
            // until the refetch lands.
            RefreshFromServerAsync().Forget();
        }

        private async UniTaskVoid RefreshFromServerAsync()
        {
            await RefreshBalanceFromServerAsync();

            if (this != null && gameObject.activeInHierarchy && !_busy)
                Refresh();
        }

        /// Slider notches are this many big blinds apart (5k at 500/1000).
        private const int StepBigBlinds = 5;

        /// <summary>
        /// Recompute the range from the live stack and balance. The slider runs in
        /// whole steps from 0 up to the most this seat can take; its value is the
        /// step index, not the chip amount. The last notch is the exact maximum,
        /// so a cap that isn't a whole number of steps is still reachable.
        /// </summary>
        public void Refresh()
        {
            var table = TableContext.Info;

            int maxBuyIn = table?.BuyInMax ?? 0;
            int wallet   = Wallet;

            // The table caps what a seat may hold, so a big stack can top up less
            // than a short one — and never more than the balance holds.
            int headroom = maxBuyIn > 0 ? maxBuyIn - MyTableChips : wallet;

            _max  = Mathf.Max(0, Mathf.Min(headroom, wallet));
            _step = Mathf.Max(1, (table?.BigBlind ?? 0) * StepBigBlinds);

            bool usable = _max > 0;

            if (amountSlider != null)
            {
                amountSlider.wholeNumbers = true;
                amountSlider.minValue     = 0;
                amountSlider.maxValue     = usable ? StepCount : 0;
                amountSlider.interactable = usable;
            }

            if (!usable)
            {
                ShowError(wallet <= 0
                    ? GameMessages.NotEnoughBalance
                    : GameMessages.StackAtTableMaximum);

                SetAmount(0);
                RefreshBalance();
                return;
            }

            // Keep what the player already picked if a balance refetch lands
            // mid-drag; it's only clamped to the new cap.
            SetAmount(_amount);
            RefreshBalance();
        }

        /// Notches above zero: whole steps, plus one more for the exact cap when
        /// it isn't a multiple of the step.
        private int StepCount => (_max + _step - 1) / _step;

        private void OnSliderChanged(float value)
        {
            int index = Mathf.RoundToInt(value);
            SetAmount(index >= StepCount ? _max : index * _step);
        }

        private void SetAmount(int value)
        {
            _amount = Mathf.Clamp(value, 0, _max);

            if (amountSlider != null)
            {
                int index = _amount >= _max ? StepCount : _amount / _step;
                amountSlider.SetValueWithoutNotify(index);
            }

            if (amountText != null) amountText.text = _amount.ToString("N0");

            UpdateConfirm();
        }

        /// Nothing to send at 0, and nothing while a request is in flight.
        private void UpdateConfirm()
        {
            if (confirmButton != null)
                confirmButton.interactable = !_busy && _amount > 0;
        }

        private void OnConfirm()
        {
            if (_busy || _amount <= 0) return;

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

            if (SocketManager.Instance == null || !SocketManager.Instance.IsConnected)
            {
                ShowError(GameMessages.TopUpFailed);
                return;
            }

            _busy = true;
            // Dead while the request is in flight, so a second tap can't send it twice.
            if (confirmButton != null) confirmButton.interactable = false;

            try
            {
                // The server picks the source itself (club chips at a club table,
                // wallet otherwise) and adds the chips at the next hand.
                _ack = new UniTaskCompletionSource<JObject>();
                SocketManager.Instance.On(EVENT_ACK, OnAck);
                SocketManager.Instance.Emit(EVENT_TOP_UP, new { tableId, amount });

                var (timedOut, ack) = await _ack.Task
                    .TimeoutWithoutException(TimeSpan.FromSeconds(AckTimeoutSeconds));

                if (timedOut || ack == null)
                {
                    ShowError(GameMessages.TopUpFailed);
                    Debug.LogWarning("[TopUp] No ack from server");
                    return;
                }

                if (ack["ok"]?.Value<bool>() != true)
                {
                    string reason = ack["message"]?.ToString()
                                    ?? ack["error"]?.ToString();

                    ShowError(string.IsNullOrEmpty(reason) ? GameMessages.TopUpFailed : reason);
                    Debug.LogWarning($"[TopUp] Rejected: {ack.ToString(Formatting.None)}");
                    return;
                }

                // Accepted. The toast and balance refresh come with
                // table:manual_top_up once the chips land (TableJoinHandler).
                Debug.Log($"[TopUp] +{ack["amount"]?.Value<int>() ?? amount} accepted");

                Close();
            }
            catch (Exception e)
            {
                ShowError(GameMessages.SomethingWentWrong);
                Debug.LogError($"[TopUp] {e}");
            }
            finally
            {
                _ack = null;
                _busy = false;
                UpdateConfirm();
            }
        }

        private void OnAck(string json)
        {
            if (_ack == null) return;   // late ack after a timeout

            try
            {
                _ack.TrySetResult(JObject.Parse(json));
            }
            catch (Exception e)
            {
                Debug.LogError($"[TopUp] Ack parse failed: {e.Message}");
                _ack.TrySetResult(null);
            }
        }

        /// <summary>Reload the spendable balance this panel shows: club chips at a
        /// club table, wallet chips otherwise.</summary>
        public static async UniTask RefreshBalanceFromServerAsync()
        {
            if (TableContext.IsClub)
            {
                await ClubWallet.RefreshAsync(TableContext.ClubId);
                return;
            }

            var auth = AuthManager.Instance;
            if (auth == null || auth.Session == null) return;

            ChipsData chips = await auth.GetChipsAsync();
            if (chips != null)
                auth.Session.WalletChips = chips.WalletChips;
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

        private void OnDestroy()
        {
            // SocketManager replays registered handlers on reconnect; don't leave
            // this one pointing at a panel from a scene that's gone.
            if (SocketManager.Instance != null)
                SocketManager.Instance.Off(EVENT_ACK);
        }

        /// <summary>Spendable balance: club chips at a club table, wallet otherwise.</summary>
        private static int Wallet
        {
            get
            {
                // A figure cached for a different club isn't this table's balance.
                if (TableContext.IsClub)
                    return ClubWallet.ClubId == TableContext.ClubId ? ClubWallet.Chips : 0;

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
