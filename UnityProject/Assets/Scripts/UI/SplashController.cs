
// Responsibilities:
//   - Eye blink animation on character sprite
//   - Loading bar fill tied to actual session check progress
//   - Check TokenStore for persisted token
//   - Fetch player profile to rebuild full session on cold start
//   - Route to Lobby (session found) or Login (no session)

using System;
using System.Collections;
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

        // Eye blink timings — natural human blink rhythm
        private const float BLINK_INTERVAL_MIN       = 2.5f;  // min seconds between blinks
        private const float BLINK_INTERVAL_MAX       = 4.5f;  // max seconds between blinks
        private const float BLINK_CLOSE_DURATION     = 0.08f; // how long eyes stay closed
        private const float BLINK_OPEN_DURATION      = 0.06f; // how long reopening takes

        // Loading bar progress stages
        private const float PROGRESS_INIT            = 0.05f; // started
        private const float PROGRESS_TOKEN_CHECK     = 0.30f; // token checked
        private const float PROGRESS_FETCHING        = 0.60f; // fetching profile
        private const float PROGRESS_COMPLETE        = 1.00f; // done

        private const float BAR_ANIMATE_DURATION     = 0.4f;

        #endregion

        #region Private Fields

        private Coroutine _blinkCoroutine;
        private bool      _isRunning = true;

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
            if (_blinkCoroutine != null)
                StopCoroutine(_blinkCoroutine);
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

            // Start eye blink loop
            _blinkCoroutine = StartCoroutine(BlinkCoroutine());

            // Run minimum wait and session check in parallel
            SetLoadingText("Loading...");
            SetBarProgress(PROGRESS_INIT, animate: true);

            var minimumWait   = UniTask.Delay(TimeSpan.FromSeconds(MINIMUM_SPLASH_SECONDS));
            var sessionResult = CheckSessionAsync();

            await UniTask.WhenAll(minimumWait, sessionResult);

            bool hasSession = await sessionResult;

            // Complete the bar before navigating
            SetBarProgress(PROGRESS_COMPLETE, animate: true);
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

            // Stop blink and navigate
            _isRunning = false;
            await FadeOutAndNavigate(hasSession ? "Scene_Lobby" : "Scene_Login");
        }

        #endregion

        #region Eye Blink Animation

        private IEnumerator BlinkCoroutine()
        {
            while (_isRunning)
            {
                // Wait random interval between blinks
                float interval = UnityEngine.Random.Range(BLINK_INTERVAL_MIN, BLINK_INTERVAL_MAX);
                yield return new WaitForSeconds(interval);

                if (!_isRunning) yield break;

                // Close eyes
                eyesClosedImage.SetActive(true);
                yield return new WaitForSeconds(BLINK_CLOSE_DURATION);

                // Open eyes
                eyesClosedImage.SetActive(false);
                yield return new WaitForSeconds(BLINK_OPEN_DURATION);
            }
        }

        #endregion

        #region Session Check

        /// <summary>
        /// Checks TokenStore for a persisted access token.
        /// If found, calls GET /api/player/profile to rebuild the full session.
        /// Returns true if a valid session was restored, false otherwise.
        /// </summary>
        private async UniTask<bool> CheckSessionAsync()
        {
            SetBarProgress(PROGRESS_TOKEN_CHECK, animate: true);

            // Check for regular session
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

            // Check for guest session
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

            // No token — fresh launch
            Debug.Log("[SplashController] No token — routing to Login.");
            SetLoadingText("Loading...");
            return false;
        }

        /// <summary>
        /// Fetches player profile and rebuilds UserSession.
        /// The 401 interceptor in ApiClient handles silent token refresh automatically.
        /// Returns true on success, false on any failure.
        /// </summary>
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

        /// <summary>
        /// Restores a guest session from the stored guest token without a network call.
        /// </summary>
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

        #region Navigation

        private async UniTask FadeOutAndNavigate(string sceneName)
        {
            if (canvasGroup != null)
                await canvasGroup.DOFade(0f, FADE_OUT_DURATION).AsyncWaitForCompletion();

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