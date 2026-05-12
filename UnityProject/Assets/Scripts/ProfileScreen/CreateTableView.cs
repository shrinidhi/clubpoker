using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;
using ClubPoker.Networking;
using ClubPoker.Core;
using ClubPoker.Game;
using TMPro;

namespace ClubPoker.UI
{
    // TODO (CLUB-536 - future):
    // - Add variant selector dropdown (NLH / PLO4 / PLO6)
    // - Auto-cap max players at 7 when PLO6 selected
    // - Add blind level picker instead of free text input
    // - Show rake % auto-calculated based on blind level
    // - Add timer setting option
    public class CreateTableView : MonoBehaviour
    {
        [Header("Create Table Popup")]
        public GameObject createTablePanel;

        [Header("Table Settings")]
        public TMP_InputField minBuyInInput;
        public TMP_InputField maxBuyInInput;
        public TMP_InputField smallBlindInput;
        public TMP_InputField bigBlindInput;
        public TMP_InputField Max_PlayerInput;

        [Header("UI")]
        public TextMeshProUGUI errorText;
        public Button createButton;
        public Button Close_Button;

        [Header("Share Code Popup")]
        public GameObject shareCodePanel;
        public TextMeshProUGUI shareCodeText;
        public Button joinTableButton;
        public Button copyCodeButton;
        public Button shareCodeCloseButton;

        private string _pendingTableId;
        private string _pendingShareCode;
        private int _pendingMinBuyIn;
        private int _pendingMaxPlayers;

        private void OnEnable()
        {
            Close_Button.onClick.AddListener(Close_ButtonOnTap);
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(OnCreateClicked);

            if (shareCodePanel != null)
                shareCodePanel.SetActive(false);
        }

        void Close_ButtonOnTap()
        {
            gameObject.SetActive(false);
        }

        private async void OnCreateClicked()
        {
            errorText.text = "";

            if (!ValidateInputs(out CreateTableRequest request, out int minBuyIn))
                return;

            createButton.interactable = false;

            try
            {
                var response = await AuthManager.Instance.CreateTableAsync(request);
                _pendingMinBuyIn = minBuyIn;
                await OnCreateSuccess(response);
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

        private bool ValidateInputs(out CreateTableRequest request, out int minBuyIn)
        {
            request = null;
            minBuyIn = 0;

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

            if (!int.TryParse(Max_PlayerInput.text, out int maxPlayer))
            {
                errorText.text = "Enter valid Max Player";
                return false;
            }

            if (minBuy > maxBuy)
            {
                errorText.text = "Min Buy-In cannot be greater than Max Buy-In";
                return false;
            }

            minBuyIn = minBuy;
            _pendingMaxPlayers = maxPlayer;
            request = new CreateTableRequest
            {
                Variant = "texas_holdem",
                MaxPlayers = maxPlayer,
                SmallBlind = smallBlind,
                BigBlind = bigBlind,
                MinBuyIn = minBuy,
                MaxBuyIn = maxBuy
            };

            return true;
        }

        private async UniTask OnCreateSuccess(CreateTableResponse response)
        {
            Debug.Log("Table Created: " + response.TableId);
           
            var joinResponse = await AuthManager.Instance.JoinTableAsync(response.TableId, _pendingMinBuyIn);

            _pendingTableId = response.TableId;
            _pendingShareCode = joinResponse.shareCode;

            ShowShareCodePopup(_pendingShareCode);
            InformationPrefabScript.Instance.ShowMessage("Table created successfully!");
        }

        private void ShowShareCodePopup(string shareCode)
        {
            if (shareCodePanel == null)
            {
                JoinAndEnterTable(_pendingTableId, _pendingMinBuyIn).Forget();
                return;
            }

            createTablePanel.SetActive(false);
            shareCodePanel.SetActive(true);

            if (shareCodeText != null)
                shareCodeText.text = shareCode;

            if (joinTableButton != null)
            {
                joinTableButton.onClick.RemoveAllListeners();
                joinTableButton.onClick.AddListener(() => JoinAndEnterTable(_pendingTableId, _pendingMinBuyIn).Forget());
            }

            // AutoJoinAfterDelay(_pendingTableId, _pendingMinBuyIn).Forget();

            if (copyCodeButton != null)
            {
                copyCodeButton.onClick.RemoveAllListeners();
                copyCodeButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = shareCode);
            }

            if (shareCodeCloseButton != null)
            {
                shareCodeCloseButton.onClick.RemoveAllListeners();
                shareCodeCloseButton.onClick.AddListener(() => shareCodePanel.SetActive(false));
            }
        }

        private async UniTaskVoid AutoJoinAfterDelay(string tableId, int buyIn)
        {
            await UniTask.Delay(4000);
            if (shareCodePanel != null && shareCodePanel.activeSelf)
                JoinAndEnterTable(tableId, buyIn).Forget();
        }

        private async UniTaskVoid JoinAndEnterTable(string tableId, int buyIn)
        {
            if (joinTableButton != null)
                joinTableButton.interactable = false;

            try
            {
                if (UnityBotRunner.Instance != null)
                    UnityBotRunner.Instance.StopBots();

                TableJoinHandler.Instance.JoinTable(tableId);

                await UniTask.Delay(1500);

                if (UnityBotRunner.Instance != null)
                    await UnityBotRunner.Instance.StartBots(tableId, _pendingMaxPlayers);

                await UniTask.Delay(1500);

                await AuthManager.Instance.StartTableAsync(tableId, 3);
            }
            catch (Exception e)
            {
                Debug.LogError("Join failed: " + e.Message);
                if (joinTableButton != null)
                    joinTableButton.interactable = true;
            }
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
    }
}
