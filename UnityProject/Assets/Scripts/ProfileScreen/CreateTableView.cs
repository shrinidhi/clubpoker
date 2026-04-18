using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;
using ClubPoker.Networking;
using TMPro;

namespace ClubPoker.UI
{
    public class CreateTableView : MonoBehaviour
    {
        [Header("Inputs")]
        public TMP_InputField minBuyInInput;
        public TMP_InputField maxBuyInInput;
        public TMP_InputField smallBlindInput;
        public TMP_InputField bigBlindInput;

        [Header("UI")]
        public TextMeshProUGUI errorText;
        public Button createButton;
        public Button Close_Button;

        private void OnEnable()
        {
            Close_Button.onClick.AddListener(Close_ButtonOnTap);
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(OnCreateClicked);
        }

        void Close_ButtonOnTap()
        {
            gameObject.SetActive(false);
        }

        private async void OnCreateClicked()
        {
            errorText.text = "";

            if (!ValidateInputs(out CreateTableRequest request))
                return;

            createButton.interactable = false;

            try
            {
                var response = await AuthManager.Instance.CreateTableAsync(request);

                OnCreateSuccess(response);
            }
            catch (ValidationException e)
            {
                HandleValidationError(e);
            }
            catch (AuthException)
            {
                errorText.text = "Session expired. Please login again.";
            }
            catch (Exception)
            {
                errorText.text = "Something went wrong";
            }

            createButton.interactable = true;
        }

        private bool ValidateInputs(out CreateTableRequest request)
        {
            request = null;

            if (!int.TryParse(minBuyInInput.text, out int minBuy))
            {
                errorText.text = "Enter valid Min Buy-In";
                return false;
            }

            if (!int.TryParse(maxBuyInInput.text, out int maxBuy))
            {
                errorText.text = "Enter valid Max Buy-In";
                return false;
            }

            if (!int.TryParse(smallBlindInput.text, out int smallBlind))
            {
                errorText.text = "Enter valid Small Blind";
                return false;
            }

            if (!int.TryParse(bigBlindInput.text, out int bigBlind))
            {
                errorText.text = "Enter valid Big Blind";
                return false;
            }

            if (minBuy > maxBuy)
            {
                errorText.text = "Min Buy-In cannot be greater than Max Buy-In";
                return false;
            }

            request = new CreateTableRequest
            {
                Variant = "texas_holdem",
                MaxPlayers = 4,
                SmallBlind = smallBlind,
                BigBlind = bigBlind,
                MinBuyIn = minBuy,
                MaxBuyIn = maxBuy
            };

            return true;
        }



        private void HandleValidationError(ValidationException e)
        {
            switch (e.Code)
            {
                case "V001":
                    errorText.text = "Invalid game variant";
                    break;

                case "V002":
                    errorText.text = "Max players not allowed";
                    break;

                case "V003":
                    errorText.text = "Min Buy-In must be less than Max Buy-In";
                    break;

                case "V004":
                    errorText.text = "Invalid blind values";
                    break;

                default:
                    errorText.text = e.Message;
                    break;
            }
        }


        private void OnCreateSuccess(CreateTableResponse response)
        {
            Debug.Log("🎉 Table Created Successfully");
            gameObject.SetActive(false);
            NavigateToTable(response.TableId);
        }

        private void NavigateToTable(string tableId)
        {
            Debug.Log("➡️ Entering Table: " + tableId);

           // GameSceneManager.Instance.LoadScene("GameScene");
        }
    }
}
 