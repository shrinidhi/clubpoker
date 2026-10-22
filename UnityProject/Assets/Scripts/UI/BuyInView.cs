using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;

namespace ClubPoker.UI
{
    public class BuyInView : MonoBehaviour
    {
        #region Serialized Fields


        [SerializeField] private TextMeshProUGUI blindsText;
        [Header("Input")]
        [SerializeField] private TMP_InputField amountInput;

        [Header("Table Limits")]
        [SerializeField] private int minBuyIn = 100;
        [SerializeField] private int maxBuyIn = 5000;

        [Header("Min_Max")]
        [SerializeField] private TextMeshProUGUI MinBuyInText;
        [SerializeField] private TextMeshProUGUI MaxBuyInText;

        [Header("Error")]
        [SerializeField] private TextMeshProUGUI errorText;

        [Header("Buttons")]
        [SerializeField] private Button JoinButton;
        [SerializeField] private Button closeButton;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;

        // [SerializeField] private PlayerHUDView playerHUDView; --- IGNORE ---

        #endregion

        #region Constants

        private const float SHAKE_DURATION = 0.4f;
        private const float SHAKE_STRENGTH = 12f;

        #endregion

        #region Private

        private string tableId  = "550e8400-e29b-41d4-a716-446655440000";

        // Optional caller-supplied confirm action. When set, OnJoinButtonClicked
        // awaits this instead of the default economy BuyInAsync — lets the lobby
        // drive JoinTableAsync (seat) while reusing this popup's validation + UI.
        private Func<int, UniTask> _onConfirm;

        #endregion

        #region Unity

        private void Start()
        {
            JoinButton.onClick.AddListener(OnJoinButtonClicked);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            amountInput.onValueChanged.AddListener(_ => ClearError());
        }

        #endregion

        #region Setup

        public void Init(string tableId, int min, int max, int smallBlind, int bigBlind, Func<int, UniTask> onConfirm = null)
        {
            this.tableId = tableId;
            this.minBuyIn = min;
            this.maxBuyIn = max;
            _onConfirm = onConfirm;
            MinBuyInText.text = Loc.Get("buyin_min", ("amount", min));
            MaxBuyInText.text = Loc.Get("buyin_max", ("amount", max));

            // The color stays in code, only "Blinds" is translated — translators
            // never see (or break) the tag.
            if (blindsText != null)
                blindsText.text = Loc.Get("blinds_value", ("blinds", $"<color=#ECBF8D>{smallBlind}/{bigBlind}</color>"));

            ClearError();
            gameObject.SetActive(true);
        }

      

     

        private async void OnJoinButtonClicked()
        {
            if (!Validate(out int amount)) return;

            SetLoading(true);

            try
            {
                if (_onConfirm != null)
                    await _onConfirm(amount);
                else
                    await AuthManager.Instance.BuyInAsync(tableId, amount);

                OnSuccess(null);
            }
            catch (GameException e)
            {
                HandleGameError(e.Code, e.Message);
            }
            catch (Exception e)
            {
                ShowError(Loc.Get("something_went_wrong"));
                Debug.LogError(e.Message);
            }

            SetLoading(false);
        }

        #endregion

        #region Validation

        private bool Validate(out int amount)
        {
            amount = 0;

            if (!int.TryParse(amountInput.text, out amount))
            {
                ShowError(Loc.Get("enter_valid_number"));
                Shake();
                return false;
            }

            if (amount < minBuyIn)
            {
                ShowError(Loc.Get("min_buyin_is", ("amount", minBuyIn)));
                Shake();
                return false;
            }

            if (amount > maxBuyIn)
            {
                ShowError(Loc.Get("max_buyin_is", ("amount", maxBuyIn)));
                Shake();
                return false;
            }

            if (amount > AuthManager.Instance.Session.WalletChips)
            {
                ShowError(Loc.Get("not_enough_balance"));
                Shake();
                return false;
            }

            return true;
        }

        #endregion

        #region Success

        private void OnSuccess(dynamic result)
        {
            Debug.Log("Buy-in success");
            // playerHUDView.RefreshChips(); --- IGNORE ---
            gameObject.SetActive(false);
        }

        #endregion

        #region Error Handling

        private void HandleGameError(string code, string message)
        {
            // G003 insufficient balance, G013 over the table limit — error.<code> in
            // the language files; any other code shows the server's own text.
            ShowError(Loc.Error(code, message));

            Shake();
        }

        private void ShowError(string msg)
        {
            errorText.text = msg;
            errorText.gameObject.SetActive(true);
        }

        private void ClearError()
        {
            errorText.text = "";
            errorText.gameObject.SetActive(false);
        }

        #endregion

        #region UI

        private void SetLoading(bool value)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(value);

            JoinButton.interactable = !value;
        }

        private void Shake()
        {
            amountInput.GetComponent<RectTransform>()
                .DOShakeAnchorPos(SHAKE_DURATION, SHAKE_STRENGTH);
        }

        #endregion
    }
}