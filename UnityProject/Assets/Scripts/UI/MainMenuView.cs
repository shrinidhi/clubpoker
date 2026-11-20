using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class MainMenuView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Buttons")]
        [SerializeField] private Button dailyBonusBtn;
        [SerializeField] private Button lobbyBtn;
        [SerializeField] private Button DaimondPlusButton;
        [Tooltip("The whole top-bar diamond balance panel. Opens the shop on the Diamond tab, same as its +.")]
        [SerializeField] private Button DiamondPanelButton;
        [Header("Club Info Buttons")]
        [SerializeField] private Button Club_FriendButton;
        [SerializeField] private Button Club_FriendCloseButton;
        [SerializeField] private Button FriendTableButton;
        [SerializeField] private Button CreateClubButton;
        [SerializeField] private Button SearchClubButton;

        [Header("Club Search Join Panel Button")]
        [SerializeField] private Button Center_CreateClubButton;


        [Header("Panels")]
        [SerializeField] private GameObject dailyBonusPanel;
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private GameObject transactionPanel;
        [SerializeField] private GameObject CreateClubPanel;
        [SerializeField] private GameObject SearchClubScreen;
        [SerializeField] private GameObject ClubfriendScreen;
        [SerializeField] private GameObject friendTableScreen;
        [SerializeField] private GameObject CarrerScreen;
        [SerializeField] private GameObject ShopScreen;
        [SerializeField] private GameObject MessageScreen;

        [Header("Bottom Buttons")]
        [SerializeField] private Button ShopButton;
        [SerializeField] private Button MessageButton;
        [SerializeField] private Button CareerButton;
        
        
        [Header("Feature-gated extras")]
        [Tooltip("Anything else that belongs to tournaments — the Tournament_Button " +
                 "row, banners, badges. Hidden together with the MTT button when the " +
                 "tournaments flag is off.")]
        [SerializeField] private GameObject[] tournamentObjects;

        [Tooltip("Anything else that belongs to the daily bonus. The bonus button " +
                 "and its auto-popup are handled without this.")]
        [SerializeField] private GameObject[] dailyBonusObjects;


        [SerializeField] private Text DaimondText;
        public ShopScreenScript shopScreenScript;

        [Header("First-time nickname")]
        [Tooltip("\"Enter a nickname\" popup, shown once after sign-up.")]
        [SerializeField] private NicknamePromptPanel nicknamePrompt;



        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            ApplyFeatureFlags();

            AutoShowDailyBonusAsync().Forget();
            if (nicknamePrompt != null) nicknamePrompt.TryShow();
            ShopButton.image.color = new Color32(255, 255, 255, 0);
            MessageButton.image.color = new Color32(255, 255, 255, 0);
            FriendTableButton.image.color = new Color32(255, 255, 255, 0);
            CareerButton.image.color = new Color32(255, 255, 255, 0);
            RefreshChips().Forget();

        }

        private void OnEnable()
        {
            dailyBonusBtn.onClick.AddListener(OnDailyBonusTapped);
            lobbyBtn.onClick.AddListener(OnLobbyTapped);
            CreateClubButton.onClick.AddListener(CreateClubButtonOnTap);
            Center_CreateClubButton.onClick.AddListener(CreateClubButtonOnTap);
            ShopButton.onClick.AddListener(ShopButtonOnTap);
            MessageButton.onClick.AddListener(MessageButtonOnTap);
            CareerButton.onClick.AddListener(CareerButtonOnTap);
            SearchClubButton.onClick.AddListener(SearchClubButtonOnTap);
            Club_FriendButton.onClick.AddListener(Club_FriendButtonOnTap);
            FriendTableButton.onClick.AddListener(FriendTableButtonOnTap);
            Club_FriendCloseButton.onClick.AddListener(Club_FriendCloseButtonOnTap);
            DaimondPlusButton.onClick.AddListener(DiamondShopOnTap);
            if (DiamondPanelButton != null)
                DiamondPanelButton.onClick.AddListener(DiamondShopOnTap);
        }

        private void OnDisable()
        {
            dailyBonusBtn.onClick.RemoveListener(OnDailyBonusTapped);
            lobbyBtn.onClick.RemoveListener(OnLobbyTapped);
            CreateClubButton.onClick.RemoveListener(CreateClubButtonOnTap);
            Center_CreateClubButton.onClick.RemoveListener(CreateClubButtonOnTap);
            SearchClubButton.onClick.RemoveListener(SearchClubButtonOnTap);
            DaimondPlusButton.onClick.RemoveListener(DiamondShopOnTap);
            if (DiamondPanelButton != null)
                DiamondPanelButton.onClick.RemoveListener(DiamondShopOnTap);
        }

        #endregion


        public async UniTaskVoid RefreshChips()
        {
            ChipsData chipsData = await AuthManager.Instance.GetChipsAsync();

            if (this == null) return;

            if (shopScreenScript.CoinCountText != null) shopScreenScript.CoinCountText.text = chipsData != null ? FormatChipCount(chipsData.AvailableChips) : "0";

            try
            {
                
                DiamondData diamondData = await AuthManager.Instance.GetDiamondsAsync();
                DaimondText.text = FormatChipCount(diamondData.Available);
                shopScreenScript.DaimondCountText.text = FormatChipCount(diamondData.Available);


            }
            catch (Exception e)
            {
                Debug.LogWarning("[LobbyController] Chips fetch failed: " + e.Message);
            }
        }
        private static string FormatChipCount(long chips)
        {
            if (chips >= 1_000_000) return $"{chips / 1_000_000f:0.#}M";
            if (chips >= 1_000) return $"{chips / 1_000f:0.#}K";
            return chips.ToString();
        }

        void Club_FriendCloseButtonOnTap()
        {
            ClubfriendScreen.SetActive(false);
        }
        void Club_FriendButtonOnTap()
        {
            CreateClubPanel.SetActive(true);
            //ClubfriendScreen.SetActive(true);
        }

        void FriendTableButtonOnTap()
        {
            friendTableScreen.SetActive(true);
        }

        void SearchClubButtonOnTap()
        {
            SearchClubScreen.SetActive(true);
        }
        void ShopButtonOnTap()
        {
            //ShopButton.image.color = new Color32(255, 255, 255, 255);
            MessageButton.image.color = new Color32(255, 255, 255, 0);
            FriendTableButton.image.color = new Color32(255, 255, 255, 0);
            CareerButton.image.color = new Color32(255, 255, 255, 0);
            ShopScreen.SetActive(true);
        }

        // Top-bar diamond balance (panel or its +) → shop on the Diamond tab.
        // The bottom-bar Shop button still opens on Gold.
        void DiamondShopOnTap()
        {
            ShopNavigation.Request(ShopTab.Diamond);

            // Already open → re-enable so the shop's OnEnable switches tab.
            if (ShopScreen.activeSelf)
                ShopScreen.SetActive(false);

            ShopButtonOnTap();
        }

        void MessageButtonOnTap()
        {
            ShopButton.image.color = new Color32(255, 255, 255, 0);
            // MessageButton.image.color = new Color32(255, 255, 255, 255);
            FriendTableButton.image.color = new Color32(255, 255, 255, 0);
            CareerButton.image.color = new Color32(255, 255, 255, 0);
            MessageScreen.SetActive(true);
        }

        

        void CareerButtonOnTap()
        {
            ShopButton.image.color = new Color32(255, 255, 255, 0);
            MessageButton.image.color = new Color32(255, 255, 255, 0);
            FriendTableButton.image.color = new Color32(255, 255, 255, 0);
           // CareerButton.image.color = new Color32(255, 255, 255, 255);
            CarrerScreen.SetActive(true);
        }


        void CreateClubButtonOnTap()
        {
            CreateClubPanel.SetActive(true);
        }

        #region Feature flags

        /// <summary>
        /// Daily bonus and tournaments are both off for this client. Gated rather
        /// than deleted: the screens work, so turning either back on is a flag in
        /// AppConfig, not a code change.
        ///
        /// Both default to OFF when the flag is missing — an environment whose config
        /// predates them must not show a half-built feature.
        /// </summary>
        private void ApplyFeatureFlags()
        {
            bool dailyBonus  = FeatureFlagManager.IsOn(FeatureFlagManager.FlagDailyBonus);
            bool tournaments = FeatureFlagManager.IsOn(FeatureFlagManager.FlagTournaments);

            if (dailyBonusBtn != null)
                dailyBonusBtn.gameObject.SetActive(dailyBonus);

            // Off means never opened, by tap or by itself — leaving the panel
            // reachable would show a bonus the client asked to remove.
            if (!dailyBonus && dailyBonusPanel != null)
                dailyBonusPanel.SetActive(false);

            SetAllActive(dailyBonusObjects, dailyBonus);

            SetAllActive(tournamentObjects, tournaments);
        }

        private static void SetAllActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;

            foreach (GameObject go in objects)
                if (go != null) go.SetActive(active);
        }

        #endregion

        #region Daily Bonus Auto Prompt
        private static bool _bonusAutoShownThisSession = false;

        private async UniTaskVoid AutoShowDailyBonusAsync()
        {
            if (!FeatureFlagManager.IsOn(FeatureFlagManager.FlagDailyBonus)) return;
            if (_bonusAutoShownThisSession) return;
            if (AuthManager.Instance == null) return;

            var session = AuthManager.Instance.Session;
            if (session == null || session.IsGuest) return;

            bool alreadyClaimed = session.LastDailyBonus.HasValue
                                  && session.LastDailyBonus.Value.ToUniversalTime().AddDays(1) > DateTime.UtcNow;
            if (alreadyClaimed) return;

            _bonusAutoShownThisSession = true;

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

        private void OnDailyBonusTapped()
        {
            // The button is hidden while the flag is off; this is the belt to that
            // braces, for a tap routed from somewhere else in the scene.
            if (!FeatureFlagManager.IsOn(FeatureFlagManager.FlagDailyBonus)) return;

            dailyBonusPanel.SetActive(true);
        }

        private void OnLobbyTapped()
        {
            if (GameSceneManager.Instance == null) return;
            GameSceneManager.Instance.LoadScene("Scene_Lobby");
        }


        #endregion
    }
}
