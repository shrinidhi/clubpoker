using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class LobbyTableItemUI : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI blindText;
        public TextMeshProUGUI playersText;
        public Button joinButton;

        private string tableId;
        private BuyInView BuyInView;
        public void Setup(TableData data , BuyInView buyInView)
        {
            tableId = data.TableId;
            BuyInView = buyInView;
            Debug.Log("🟢 Setup UI for: " + data.Name);

            nameText.text = data.Name;
            blindText.text = "BB: " + data.BigBlind;
            playersText.text = $"{data.CurrentPlayers}/{data.MaxPlayers}";

            bool isFull = data.CurrentPlayers >= data.MaxPlayers;
            bool isClosed = data.Status != "open";

            joinButton.interactable = !isFull && !isClosed;

            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() =>
            {
                Debug.Log("🎮 Join Table Click: " + tableId);
                ClubPoker.Game.TableJoinHandler.Instance.JoinTable(tableId);
                // BuyInView.gameObject.SetActive(true);
                //  BuyInView.Init(tableId,1000,5000);
            });
        }
    }
}