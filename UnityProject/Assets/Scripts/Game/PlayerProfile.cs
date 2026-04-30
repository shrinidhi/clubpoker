using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class PlayerProfile : MonoBehaviour
    {
        public Text Player_Name;
        public Text Player_Chips;
        public Image Player_Avtar;
        public Text BattingAction_Text;

        [Header("Seat Config")]
        public int seatIndex = 0; // Inspector se set karo (0,1,2,3...)

        private void Start()
        {
            LoadPlayerData();
        }

        private void OnEnable()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateUpdated += LoadPlayerData;
        }

        private void OnDisable()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateUpdated -= LoadPlayerData;
        }

        private void LoadPlayerData()
        {
            if (GameStateManager.Instance == null)
            {
                Debug.LogError("[PlayerProfile] GameStateManager not found");
                return;
            }

            List<GamePlayer> players = GameStateManager.Instance.Players;

            if (players == null || players.Count == 0)
            {
                Debug.LogWarning("[PlayerProfile] No players found");
                return;
            }

            GamePlayer targetPlayer = null;

          
            foreach (var player in players)
            {
                if (player.Seat == seatIndex)
                {
                    targetPlayer = player;
                    break;
                }
            }

            if (targetPlayer == null)
            {
                Debug.LogWarning($"[PlayerProfile] No player found on seat {seatIndex}");
                return;
            }

            Player_Name.text = targetPlayer.Username;
            Player_Chips.text = targetPlayer.Chips.ToString();

            if (BattingAction_Text != null)
            {
                BattingAction_Text.text = string.IsNullOrEmpty(targetPlayer.LastAction)
                    ? ""
                    : targetPlayer.LastAction;
            }

            Debug.Log($"[PlayerProfile] UI Updated -> {targetPlayer.Username}");
        }
    }
}