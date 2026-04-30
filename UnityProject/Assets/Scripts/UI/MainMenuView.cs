using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class MainMenuView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Buttons")]
        [SerializeField] private Button leaderboardBtn;
        [SerializeField] private Button transactionBtn;
        [SerializeField] private Button dailyBonusBtn;
        [SerializeField] private Button createTableBtn;
        [SerializeField] private Button quickJoinBtn;
        [SerializeField] private Button lobbyBtn;

        [Header("Panels")]
        [SerializeField] private GameObject dailyBonusPanel;
        [SerializeField] private GameObject createTablePanel;
        [SerializeField] private GameObject quickJoinPanel;
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private GameObject transactionPanel;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            AutoShowDailyBonusAsync().Forget();
        }

        private void OnEnable()
        {
            leaderboardBtn.onClick.AddListener(OnLeaderboardTapped);
            transactionBtn.onClick.AddListener(OnTransactionTapped);
            dailyBonusBtn.onClick.AddListener(OnDailyBonusTapped);
            createTableBtn.onClick.AddListener(OnCreateTableTapped);
            quickJoinBtn.onClick.AddListener(OnQuickJoinTapped);
            lobbyBtn.onClick.AddListener(OnLobbyTapped);

        }

        private void OnDisable()
        {
            leaderboardBtn.onClick.RemoveListener(OnLeaderboardTapped);
            transactionBtn.onClick.RemoveListener(OnTransactionTapped);
            dailyBonusBtn.onClick.RemoveListener(OnDailyBonusTapped);
            createTableBtn.onClick.RemoveListener(OnCreateTableTapped);
            quickJoinBtn.onClick.RemoveListener(OnQuickJoinTapped);
            lobbyBtn.onClick.RemoveListener(OnLobbyTapped);
        }

        #endregion

        #region Daily Bonus Auto Prompt

        private async UniTaskVoid AutoShowDailyBonusAsync()
        {
            if (AuthManager.Instance == null) return;

            var session = AuthManager.Instance.Session;
            if (session == null || session.IsGuest) return;

            bool bonusAvailable = session.LastDailyBonus == null
                                  || session.LastDailyBonus.Value.Date < DateTime.UtcNow.Date;
            if (!bonusAvailable) return;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: destroyCancellationToken);
                dailyBonusPanel.SetActive(true);
                Debug.Log("[MainMenuView] Daily bonus auto-shown.");
            }
            catch (OperationCanceledException) { }
        }

        #endregion

        #region Button Handlers

        private void OnLeaderboardTapped() => leaderboardPanel.SetActive(true);
        private void OnTransactionTapped() => transactionPanel.SetActive(true);
        private void OnDailyBonusTapped()  => dailyBonusPanel.SetActive(true);
        private void OnCreateTableTapped() => createTablePanel.SetActive(true);
        private void OnQuickJoinTapped()   => quickJoinPanel.SetActive(true);

        private void OnLobbyTapped()
        {
            if (GameSceneManager.Instance == null) return;
            GameSceneManager.Instance.LoadScene("Scene_Lobby");
        }


        #endregion
    }
}
