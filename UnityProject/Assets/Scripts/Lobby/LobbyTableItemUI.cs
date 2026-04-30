using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

namespace ClubPoker.Lobby
{
    public class LobbyTableItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI blindText;
        [SerializeField] private TextMeshProUGUI playersText;
        [SerializeField] private Button joinButton;

        private string _tableId;

        public void Setup(TableData data)
        {
            _tableId = data.TableId;

            nameText.text = data.Name;
            blindText.text = $"BB: {data.BigBlind}";
            playersText.text = $"{data.CurrentPlayers}/{data.MaxPlayers}";

            bool isFull = data.CurrentPlayers >= data.MaxPlayers;
            bool isClosed = data.Status != "open";

            joinButton.interactable = !isFull && !isClosed;

            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() =>
                ClubPoker.Game.TableJoinHandler.Instance.JoinTable(_tableId));
        }
    }
}
