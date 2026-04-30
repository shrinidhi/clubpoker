using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;

namespace ClubPoker.Lobby
{
    public class GuestFlowController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("TOP HUD")]
        [SerializeField] private GameObject guestBadge;
        [SerializeField] private GameObject temporaryChipsIndicator;

        [Header("Restricted Buttons")]
        [SerializeField] private Button createTableButton;
        [SerializeField] private Button transactionButton;
        [SerializeField] private Button leaderBoardButton;
        [SerializeField] private Button profileButton;

        [Header("Guest Restricted Popup")]
        [SerializeField] private GameObject      guestRestrictedPopup;
        [SerializeField] private TextMeshProUGUI popupHeaderText;
        [SerializeField] private Button          registerButton;
        [SerializeField] private Button          popupCloseButton;

        #endregion

        #region Private Fields

        private bool _isRunning = true;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            var session = AuthManager.Instance.Session;
            if (session == null) return;

            guestBadge.SetActive(session.IsGuest);

            if (temporaryChipsIndicator != null)
                temporaryChipsIndicator.SetActive(session.IsGuest);

            if (!session.IsGuest) return;

            // Yield one frame so all other Start() calls (e.g. LobbyController)
            // finish adding their button listeners before we remove and replace them.
            UniTask.Void(async () =>
            {
                await UniTask.NextFrame();
                OverrideRestrictedButtons();
                StartTokenExpiryAsync().Forget();
            });

            BindButtons();
        }

        private void BindButtons()
        {
            if (registerButton != null)
                registerButton.onClick.AddListener(OnRegisterTapped);
            if (popupCloseButton != null)
                popupCloseButton.onClick.AddListener(ClosePopup);

        }

        private void OnEnable()
        {
           
        }

        private void OnDisable()
        {
            if (registerButton != null)
                registerButton.onClick.RemoveListener(OnRegisterTapped);
            if (popupCloseButton != null)
                popupCloseButton.onClick.RemoveListener(ClosePopup);
        }

        private void OnDestroy()
        {
            _isRunning = false;
        }

        #endregion

        #region Guest Restrictions

        private void OverrideRestrictedButtons()
        {
            OverrideButton(createTableButton, GuestRestrictedFeature.CreateTable);
            OverrideButton(transactionButton,  GuestRestrictedFeature.Transaction);
            OverrideButton(leaderBoardButton,  GuestRestrictedFeature.Leaderboard);
            OverrideButton(profileButton,      GuestRestrictedFeature.ProfileEdit);
        }

        private void OverrideButton(Button button, GuestRestrictedFeature feature)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowGuestRestrictedPopup(feature));
        }

        private void ShowGuestRestrictedPopup(GuestRestrictedFeature feature)
        {
            Debug.Log($"[GuestFlowController] Guest attempted restricted feature: {feature}");

            if (popupHeaderText != null)
                popupHeaderText.text = GetFeatureHeader(feature);

            if (guestRestrictedPopup != null)
                guestRestrictedPopup.SetActive(true);
        }

        private void ClosePopup()
        {
            if (guestRestrictedPopup != null)
                guestRestrictedPopup.SetActive(false);
        }

        private void OnRegisterTapped()
        {
            ClosePopup();
            GameSceneManager.Instance.LoadScene("Scene_Register");
        }

        private static string GetFeatureHeader(GuestRestrictedFeature feature) => feature switch
        {
            GuestRestrictedFeature.Leaderboard => "Leaderboard requires an account",
            GuestRestrictedFeature.ProfileEdit => "Profile editing requires an account",
            GuestRestrictedFeature.HandHistory => "Hand history requires an account",
            GuestRestrictedFeature.CreateTable => "Creating tables requires an account",
            GuestRestrictedFeature.Transaction => "Transactions require an account",
            _                                  => "This feature requires an account",
        };

        #endregion

        #region Token Expiry

        private async UniTaskVoid StartTokenExpiryAsync()
        {
            TimeSpan timeLeft = TokenStore.GuestTimeRemaining();

            if (timeLeft.TotalSeconds <= 0)
            {
                ForceGuestLogout();
                return;
            }

            Debug.Log($"[GuestFlowController] Guest token expires in " +
                      $"{timeLeft.Hours:00}:{timeLeft.Minutes:00}:{timeLeft.Seconds:00}");

            // Sleep in 60-second chunks, re-reading real wall-clock time each cycle.
            // This ensures background suspension is handled: when the app resumes,
            // GuestTimeRemaining() compares against DateTime.UtcNow, not Unity time.
            while (_isRunning)
            {
                TimeSpan remaining = TokenStore.GuestTimeRemaining();
                if (remaining <= TimeSpan.Zero) break;

                float sleepSeconds = (float)Math.Min(remaining.TotalSeconds, 60.0);
                await UniTask.Delay(TimeSpan.FromSeconds(sleepSeconds));
            }

            if (_isRunning)
                ForceGuestLogout();
        }

        private void ForceGuestLogout()
        {
            Debug.Log("[GuestFlowController] Guest token expired — forcing logout.");
            AuthManager.Instance.LogoutAsync(callServer: false).Forget();
        }

        #endregion
    }
}
