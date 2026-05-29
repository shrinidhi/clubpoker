using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Game;
using ClubPoker.Core;
using System;

namespace ClubPoker.Lobby
{
    public class LobbyTableItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI blindText;
        [SerializeField] private TextMeshProUGUI playersText;
        [SerializeField] private Button joinButton;

        private string _tableId;
        private int maxPlayer;
        public void Setup(TableData data)
        {
            _tableId = data.TableId;
            maxPlayer = data.MaxPlayers;
            nameText.text = data.Name;
            blindText.text = $"BB: {data.BigBlind}";
            playersText.text = $"{data.CurrentPlayers}/{data.MaxPlayers}";

            bool isFull = data.CurrentPlayers >= data.MaxPlayers;
            bool isClosed = data.Status != "open";

            joinButton.interactable = !isFull && !isClosed;

            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() =>
            {
                OnJoinClicked().Forget();
            });


        
    }


        private async UniTaskVoid OnJoinClicked()
        {
            try
            {
                joinButton.interactable = false;

                int buyIn = 1000;

                if (UnityBotRunner.Instance != null)
                {
                    UnityBotRunner.Instance.StopBots(); 
                }

                try
                {
                    await AuthManager.Instance.JoinTableAsync(_tableId, buyIn);
                }
                catch (Exception e)
                {
                    if (!e.Message.Contains("Already seated"))
                        throw;
                }

                TableJoinHandler.Instance.JoinTable(_tableId);

                await UniTask.Delay(1500);

                if (UnityBotRunner.Instance != null)
                {
                    await UnityBotRunner.Instance.StartBots(_tableId, maxPlayer); 
                }

                await UniTask.Delay(1500);

                await AuthManager.Instance.StartTableAsync(_tableId, 3);
            }
            catch (Exception e)
            {
                Debug.LogError("Game start failed: " + e.Message);
                joinButton.interactable = true;
                ToastEvents.Show("Failed to join: " + e.Message);
            }
        }
    }
}
