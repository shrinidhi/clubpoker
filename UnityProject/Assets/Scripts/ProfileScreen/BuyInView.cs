using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using ClubPoker.Auth;
using ClubPoker.Networking;

namespace ClubPoker.UI
{
    public class BuyInView : MonoBehaviour
    {
        #region Serialized Fields

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

        [SerializeField] private ChipsHUDView ChipsHUDView; 

        #endregion

        #region Constants

        private const float SHAKE_DURATION = 0.4f;
        private const float SHAKE_STRENGTH = 12f;

        #endregion

        #region Private

        private string tableId  = "550e8400-e29b-41d4-a716-446655440000";

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

        public void Init(string tableId, int min, int max)
        {
            this.tableId = tableId;
            this.minBuyIn = min;
            this.maxBuyIn = max;
            MinBuyInText.text ="min :" +min.ToString();
            MaxBuyInText.text = "max :" + max.ToString();
        }

      

     

        private async void OnJoinButtonClicked()
        {
            if (!Validate(out int amount)) return;

            SetLoading(true);

            try
            {
                var result = await AuthManager.Instance.BuyInAsync(tableId, amount);

                OnSuccess(result);
            }
            catch (GameException e)
            {
                HandleGameError(e.Code, e.Message);
            }
            catch (Exception e)
            {
                ShowError("Something went wrong");
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
                ShowError("Enter valid number");
                Shake();
                return false;
            }

            if (amount < minBuyIn)
            {
                ShowError($"Minimum buy-in is {minBuyIn}");
                Shake();
                return false;
            }

            if (amount > maxBuyIn)
            {
                ShowError($"Maximum buy-in is {maxBuyIn}");
                Shake();
                return false;
            }

            if (amount > AuthManager.Instance.Session.WalletChips)
            {
                ShowError("Not enough balance");
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
            ChipsHUDView.RefreshChips();
            gameObject.SetActive(false);
        }

        #endregion

        #region Error Handling

        private void HandleGameError(string code, string message)
        {
            switch (code)
            {
                case "G003":
                    ShowError("Insufficient balance");
                    break;

                case "G013":
                    ShowError("Buy-in exceeds table limit");
                    break;

                default:
                    ShowError(message);
                    break;
            }

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