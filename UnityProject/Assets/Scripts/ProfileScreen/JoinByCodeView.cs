using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;
using ClubPoker.Game;
using ClubPoker.Auth;
using TMPro;
using System;
using ClubPoker.Core;

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
            SetError(Loc.Get("msg.table_unreachable"));
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
                SetError(Loc.Get("enter_valid_code"));
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
                    SetError(Loc.Get("error.V010"));
                    return;
                }

                TableContext.EnterFromLobby(response.tableId);
                TableJoinHandler.Instance.JoinTable(response.tableId);

                await UniTask.Delay(1500);

                // Auto-starting bots on join is disabled. StopBots stays so any
                // bots left running from a previous table are still cleared.
                if (UnityBotRunner.Instance != null)
                {
                    UnityBotRunner.Instance.StopBots();
                    // await UnityBotRunner.Instance.StartBots(response.tableId, response.maxPlayers, response.minBuyIn);
                }

                await UniTask.Delay(1500);

                await AuthManager.Instance.StartTableAsync(response.tableId, 3);
            }
            catch (LobbyException e)
            {
                SetError(e.Code == "L002" ? Loc.Get("table_full") : Loc.Get("error.V010"));
            }
            catch (Exception e)
            {
                Debug.LogError("JoinByCode failed: " + e.Message);
                SetError(Loc.Get("something_went_wrong"));
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
