using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;
using ClubPoker.Game;
using ClubPoker.Auth;
using TMPro;
using System;

namespace ClubPoker.UI
{
    public class JoinByCodeView : MonoBehaviour
    {
        [Header("UI")]
        public TMP_InputField shareCodeInput;
        public Button joinButton;
        public Button closeButton;
        public TextMeshProUGUI errorText;
        public GameObject loadingPanel;

        private void OnEnable()
        {
            TableJoinHandler.OnJoinFailed += OnJoinFailed;
        }

        private void OnDisable()
        {
            TableJoinHandler.OnJoinFailed -= OnJoinFailed;
        }

        private void OnJoinFailed(string message)
        {
            SetError("Could not connect to table. Please try again.");
            joinButton.interactable = true;
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        private void Start()
        {
            joinButton.onClick.AddListener(() => OnJoinClicked().Forget());
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (shareCodeInput != null)
            {
                shareCodeInput.characterLimit = 6;
                shareCodeInput.onValueChanged.AddListener(v =>
                {
                    shareCodeInput.SetTextWithoutNotify(v.ToUpper());
                    SetError("");
                });
            }
        }

        private async UniTaskVoid OnJoinClicked()
        {
            if (shareCodeInput == null) return;

            string code = shareCodeInput.text.Trim().ToUpper();

            if (code.Length != 6)
            {
                SetError("Enter a valid 6-character code");
                return;
            }

            SetError("");
            joinButton.interactable = false;

            if (loadingPanel != null)
                loadingPanel.SetActive(true);

            try
            {
                var response = await AuthManager.Instance.JoinByCodeAsync(code, 1);

                if (string.IsNullOrEmpty(response.tableId))
                {
                    Debug.LogError("JoinByCode failed: No table ID returned");
                    SetError("Invalid or expired code");
                    return;
                }

                TableJoinHandler.Instance.JoinTable(response.tableId);

                await UniTask.Delay(1500);

                if (UnityBotRunner.Instance != null)
                    await UnityBotRunner.Instance.StartBots(response.tableId, response.maxPlayers, response.minBuyIn);

                await UniTask.Delay(1500);

                await AuthManager.Instance.StartTableAsync(response.tableId, 3);
            }
            catch (LobbyException e)
            {
                SetError(e.Code == "L002" ? "Table is full" : "Invalid or expired code");
            }
            catch (Exception e)
            {
                Debug.LogError("JoinByCode failed: " + e.Message);
                SetError("Something went wrong");
            }
            finally
            {
                joinButton.interactable = true;
                if (loadingPanel != null)
                    loadingPanel.SetActive(false);
            }
        }

        private void SetError(string message)
        {
            if (errorText != null)
            {
                errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
                errorText.text = message;
            }
        }
    }
}
