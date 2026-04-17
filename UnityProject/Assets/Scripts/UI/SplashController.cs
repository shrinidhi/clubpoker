
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class SplashController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Character")]
        [SerializeField] private GameObject eyesClosedImage;

        [Header("Loading")]
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Image           barFill;

        [Header("Animation")]
        [SerializeField] private CanvasGroup canvasGroup;

        #endregion

        #region Constants

        private const float MINIMUM_SPLASH_SECONDS  = 2f;
        private const float FADE_IN_DURATION         = 0.5f;
        private const float FADE_OUT_DURATION        = 0.3f;

        private const float BLINK_INTERVAL_MIN       = 0.8f;
        private const float BLINK_INTERVAL_MAX       = 1.0f;
        private const float BLINK_CLOSE_DURATION     = 0.3f;
        private const float BLINK_OPEN_DURATION      = 0.06f;

        private const float PROGRESS_INIT            = 0.05f;
        private const float PROGRESS_CONFIG_CHECK    = 0.25f;
        private const float PROGRESS_TOKEN_CHECK     = 0.50f;
        private const float PROGRESS_FETCHING        = 0.75f;
        private const float PROGRESS_COMPLETE        = 1.00f;
        private const float BAR_ANIMATE_DURATION     = 0.4f;

        #endregion

        #region Private Fields

        private bool _isRunning = true;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            eyesClosedImage.SetActive(false);
            SetBarProgress(0f, animate: false);
            InitialiseAsync().Forget();
        }

        private void OnDestroy()
        {
            _isRunning = false;
        }

        #endregion

        #region Initialisation

        private async UniTaskVoid InitialiseAsync()
        {
            // Fade in
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                await canvasGroup.DOFade(1f, FADE_IN_DURATION).AsyncWaitForCompletion();
            }

            // Start eye blink
            StartCoroutine(BlinkCoroutine());

            SetLoadingText("Loading...");
            SetBarProgress(PROGRESS_INIT, animate: true);
            
            await WaitForInternetAsync();
            // Step 1 — config validation + version check
            bool canProceed = await ValidateConfigAsync();
            if (!canProceed)
            {
                // ForceUpgradeScreen is now showing — stop here
                // Player must update before continuing
                Debug.Log("[SplashController] Version outdated — blocked at upgrade screen.");
                return;
            }

            // Step 2 — session check (parallel with minimum display time)
            SetBarProgress(PROGRESS_TOKEN_CHECK, animate: true);

            var minimumWait = UniTask.Delay(TimeSpan.FromSeconds(MINIMUM_SPLASH_SECONDS));

            bool hasSession = false;
            await UniTask.WhenAll(
                minimumWait,
                CheckSessionAsync().ContinueWith(result => hasSession = result)
            );

            // Complete bar
            SetBarProgress(PROGRESS_COMPLETE, animate: true);
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

            // Navigate
            _isRunning = false;
            await FadeOutAndNavigate(hasSession ? "Scene_Lobby" : "Scene_Login");
        }

        #endregion

        #region Step 1 — Config Validation

        /// <summary>
        /// Fetches server config and checks app version.
        /// Returns false if version is outdated — ForceUpgradeScreen will be showing.
        /// Returns true if version is ok and initialisation can continue.
        /// </summary>
        private async UniTask<bool> ValidateConfigAsync()
        {
            SetLoadingText("Checking for updates...");
            SetBarProgress(PROGRESS_CONFIG_CHECK, animate: true);

            bool canProceed   = true;
            bool callbackFired = false;

            void OnSuccess()
            {
                canProceed    = true;
                callbackFired = true;
            }

            void OnOutdated()
            {
                canProceed    = false;
                callbackFired = true;
            }

            ConfigValidator.Instance.OnValidationSuccess += OnSuccess;
            ConfigValidator.Instance.OnVersionOutdated   += OnOutdated;

            try
            {
                await ConfigValidator.Instance.ValidateAsync();

                // Wait for callback to fire if not already
                float timeout = 0f;
                while (!callbackFired && timeout < 5f)
                {
                    await UniTask.Delay(TimeSpan.FromMilliseconds(100));
                    timeout += 0.1f;
                }
            }
            finally
            {
                // Always unsubscribe to prevent memory leaks
                ConfigValidator.Instance.OnValidationSuccess -= OnSuccess;
                ConfigValidator.Instance.OnVersionOutdated   -= OnOutdated;
            }

            return canProceed;
        }

        private async UniTask WaitForInternetAsync()
        {
            if (NetworkMonitor.Instance == null || NetworkMonitor.Instance.IsOnline)
                return;

            Debug.LogWarning("[SplashController] No internet — waiting for connection.");

            while (!NetworkMonitor.Instance.IsOnline)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1f));
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
            Debug.Log("[SplashController] Internet restored — proceeding.");
        }        
        #endregion

        #region Step 2 — Session Check

        /// <summary>
        /// Checks TokenStore for a persisted access token.
        /// If found, fetches player profile to rebuild full session.
        /// Returns true if a valid session was restored, false otherwise.
        /// </summary>
        private async UniTask<bool> CheckSessionAsync()
        {
            // Check regular session
            string accessToken = TokenStore.LoadAccessToken();
            if (!string.IsNullOrEmpty(accessToken))
            {
                Debug.Log("[SplashController] Token found — fetching profile.");
                SetLoadingText("Restoring session...");
                SetBarProgress(PROGRESS_FETCHING, animate: true);

                bool restored = await FetchAndRestoreSessionAsync(accessToken);
                if (restored)
                {
                    SetLoadingText("Welcome back!");
                    return true;
                }

                Debug.LogWarning("[SplashController] Profile fetch failed — routing to Login.");
                SetLoadingText("Session expired.");
                return false;
            }

            // Check guest session
            string guestToken = TokenStore.LoadGuestToken();
            if (!string.IsNullOrEmpty(guestToken))
            {
                Debug.Log("[SplashController] Guest token found — restoring guest session.");
                SetLoadingText("Restoring guest session...");
                SetBarProgress(PROGRESS_FETCHING, animate: true);
                RestoreGuestSession(guestToken);
                SetLoadingText("Welcome back!");
                return true;
            }

            Debug.Log("[SplashController] No token — routing to Login.");
            SetLoadingText("Loading...");
            return false;
        }

        private async UniTask<bool> FetchAndRestoreSessionAsync(string accessToken)
        {
            try
            {
                string refreshToken = TokenStore.LoadRefreshToken();
                ApiClient.Instance.SetTokens(accessToken, refreshToken);

                PlayerData profile = await ApiClient.Instance.Get<PlayerData>(
                    "/api/player/profile");

                AuthManager.Instance.Session = UserSession.From(profile);

                Debug.Log($"[SplashController] Session restored. User: {profile.Username}");
                return true;
            }
            catch (ApiException e)
            {
                Debug.LogWarning($"[SplashController] Profile fetch failed: {e.Code} — {e.Message}");

                if (e.Code == "A001" || e.Code == "A002")
                    TokenStore.ClearAll();

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SplashController] Profile fetch error: {e.Message}");
                return false;
            }
        }

        private void RestoreGuestSession(string guestToken)
        {
            ApiClient.Instance.SetTokens(guestToken, null);

            AuthManager.Instance.Session = new UserSession
            {
                Username = "Guest",
                Role     = "guest",
                IsGuest  = true
            };

            TimeSpan remaining = TokenStore.GuestTimeRemaining();
            Debug.Log($"[SplashController] Guest session restored. " +
                      $"Remaining: {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}");
        }

        #endregion

        #region Eye Blink Animation

        private System.Collections.IEnumerator BlinkCoroutine()
        {
            while (_isRunning)
            {
                float interval = UnityEngine.Random.Range(BLINK_INTERVAL_MIN, BLINK_INTERVAL_MAX);
                yield return new WaitForSeconds(interval);

                if (!_isRunning) yield break;

                eyesClosedImage.SetActive(true);
                yield return new WaitForSeconds(BLINK_CLOSE_DURATION);

                eyesClosedImage.SetActive(false);
                yield return new WaitForSeconds(BLINK_OPEN_DURATION);
            }
        }

        #endregion

        #region Navigation

        private async UniTask FadeOutAndNavigate(string sceneName)
        {
            if (canvasGroup != null)
                await canvasGroup.DOFade(0f, FADE_OUT_DURATION).AsyncWaitForCompletion();

            if (GameSceneManager.Instance == null)
            {
                Debug.LogError("[SplashController] GameSceneManager not found.");
                return;
            }

            Debug.Log($"[SplashController] Navigating to {sceneName}.");
            GameSceneManager.Instance.LoadScene(sceneName);
        }

        #endregion

        #region UI Helpers

        private void SetLoadingText(string message)
        {
            if (loadingText != null)
                loadingText.text = message;
        }

        private void SetBarProgress(float progress, bool animate)
        {
            if (barFill == null) return;
            progress = Mathf.Clamp01(progress);

            if (animate)
                barFill.DOFillAmount(progress, BAR_ANIMATE_DURATION).SetEase(Ease.OutCubic);
            else
                barFill.fillAmount = progress;
        }

        #endregion
    }
}