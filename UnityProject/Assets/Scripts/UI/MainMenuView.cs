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
        [SerializeField] private Button joinByCodeBtn;
        [SerializeField] private Button lobbyBtn;
        [SerializeField] private Button LogOutButton;
        [SerializeField] private Button CreateClubButton;
        [SerializeField] private Button Center_CreateClubButton;
        [SerializeField] private Button SearchClubButton;



        [Header("Panels")]
        [SerializeField] private GameObject dailyBonusPanel;
        [SerializeField] private GameObject createTablePanel;
        [SerializeField] private GameObject quickJoinPanel;
        [SerializeField] private GameObject joinByCodePanel;
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private GameObject transactionPanel;
        [SerializeField] private GameObject CreateClubPanel;
        [SerializeField] private GameObject SearchClubScreen;


        [Header("Bottom Buttons")]
        [SerializeField] private Button ShopButton;
        [SerializeField] private Button MessageButton;
        [SerializeField] private Button MTTButton;
        [SerializeField] private Button CareerButton;



        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            AutoShowDailyBonusAsync().Forget();
            ShopButton.image.color = new Color32(255, 255, 255, 0);
            MessageButton.image.color = new Color32(255, 255, 255, 0);
            MTTButton.image.color = new Color32(255, 255, 255, 0);
            CareerButton.image.color = new Color32(255, 255, 255, 0);
        }

        private void OnEnable()
        {
            leaderboardBtn.onClick.AddListener(OnLeaderboardTapped);
            transactionBtn.onClick.AddListener(OnTransactionTapped);
            dailyBonusBtn.onClick.AddListener(OnDailyBonusTapped);
            createTableBtn.onClick.AddListener(OnCreateTableTapped);
            quickJoinBtn.onClick.AddListener(OnQuickJoinTapped);
            if (joinByCodeBtn != null)
                joinByCodeBtn.onClick.AddListener(OnJoinByCodeTapped);
            lobbyBtn.onClick.AddListener(OnLobbyTapped);
            LogOutButton.onClick.AddListener(LogOutButtonOnTap);
            CreateClubButton.onClick.AddListener(CreateClubButtonOnTap);
            Center_CreateClubButton.onClick.AddListener(CreateClubButtonOnTap);
            ShopButton.onClick.AddListener(ShopButtonOnTap);
            MessageButton.onClick.AddListener(MessageButtonOnTap);
            MTTButton.onClick.AddListener(MTTButtonOnTap);
            CareerButton.onClick.AddListener(CareerButtonOnTap);
            SearchClubButton.onClick.AddListener(SearchClubButtonOnTap);
        }

        private void OnDisable()
        {
            leaderboardBtn.onClick.RemoveListener(OnLeaderboardTapped);
            transactionBtn.onClick.RemoveListener(OnTransactionTapped);
            dailyBonusBtn.onClick.RemoveListener(OnDailyBonusTapped);
            createTableBtn.onClick.RemoveListener(OnCreateTableTapped);
            quickJoinBtn.onClick.RemoveListener(OnQuickJoinTapped);
            if (joinByCodeBtn != null)
                joinByCodeBtn.onClick.RemoveListener(OnJoinByCodeTapped);
            lobbyBtn.onClick.RemoveListener(OnLobbyTapped);
            CreateClubButton.onClick.RemoveListener(CreateClubButtonOnTap);
            Center_CreateClubButton.onClick.RemoveListener(CreateClubButtonOnTap);
            SearchClubButton.onClick.RemoveListener(SearchClubButtonOnTap);
        }

        #endregion




        void SearchClubButtonOnTap()
        {
            SearchClubScreen.SetActive(true);
        }
        void ShopButtonOnTap()
        {
            ShopButton.image.color = new Color32(255, 255, 255, 255);
            MessageButton.image.color = new Color32(255, 255, 255, 0);
            MTTButton.image.color = new Color32(255, 255, 255, 0);
            CareerButton.image.color = new Color32(255, 255, 255, 0);
        }
        void MessageButtonOnTap()
        {
            ShopButton.image.color = new Color32(255, 255, 255, 0);
            MessageButton.image.color = new Color32(255, 255, 255, 255);
            MTTButton.image.color = new Color32(255, 255, 255, 0);
            CareerButton.image.color = new Color32(255, 255, 255, 0);
        }

        void MTTButtonOnTap()
        {
            ShopButton.image.color = new Color32(255, 255, 255, 0);
            MessageButton.image.color = new Color32(255, 255, 255, 0);
            MTTButton.image.color = new Color32(255, 255, 255, 255);
            CareerButton.image.color = new Color32(255, 255, 255, 0);
        }

        void CareerButtonOnTap()
        {
            ShopButton.image.color = new Color32(255, 255, 255, 0);
            MessageButton.image.color = new Color32(255, 255, 255, 0);
            MTTButton.image.color = new Color32(255, 255, 255, 0);
            CareerButton.image.color = new Color32(255, 255, 255, 255);
        }


       void LogOutButtonOnTap()
        {
            AuthManager.Instance.LogoutAsync();
        }



        void CreateClubButtonOnTap()
        {
            CreateClubPanel.SetActive(true);
        }

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
        private void OnQuickJoinTapped()    => quickJoinPanel.SetActive(true);
        private void OnJoinByCodeTapped()   => joinByCodePanel?.SetActive(true);

        private void OnLobbyTapped()
        {
            if (GameSceneManager.Instance == null) return;
            GameSceneManager.Instance.LoadScene("Scene_Lobby");
        }


        #endregion
    }
}
