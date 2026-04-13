
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ClubPoker.Auth;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class LoginView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Error Display")]
        [SerializeField] private TextMeshProUGUI passwordErrorText;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private Button registerButton;

        [Header("Remember Me")]
        [SerializeField] private Toggle rememberMeToggle;

        [Header("Lockout")]
        [SerializeField] private GameObject lockoutPanel;
        [SerializeField] private TextMeshProUGUI lockoutText;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;

        #endregion

        #region Constants

        private const float ERROR_SHAKE_DURATION = 0.4f;
        private const float ERROR_SHAKE_STRENGTH  = 12f;
        private const int   ERROR_SHAKE_VIBRATO   = 20;

        #endregion

        #region Private Fields

        private Coroutine _lockoutCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            ResetView();
            BindButtons();
        }

        private void OnDestroy()
        {
            if (_lockoutCoroutine != null)
                StopCoroutine(_lockoutCoroutine);
        }

        #endregion

        #region Setup

        private void BindButtons()
        {
            loginButton.onClick.AddListener(OnLoginClicked);
            guestButton.onClick.AddListener(OnGuestClicked);
            registerButton.onClick.AddListener(OnRegisterClicked);

            // Clear errors as the user types
            emailInput.onValueChanged.AddListener(_ => ClearErrors());
            passwordInput.onValueChanged.AddListener(_ => ClearErrors());
        }

        private void ResetView()
        {
            ClearErrors();
            SetLoading(false);
            lockoutPanel.SetActive(false);
            emailInput.text    = string.Empty;
            passwordInput.text = string.Empty;
        }

        #endregion

        #region Button Handlers

        private async void OnLoginClicked()
        {
            if (!ValidateInputs()) return;

            SetLoading(true);
            ClearErrors();

            LoginResult result = await AuthManager.Instance.LoginAsync(
                emailInput.text.Trim(),
                passwordInput.text,
                rememberMeToggle.isOn);

            SetLoading(false);

            if (result.Success)
            {
                GameSceneManager.Instance.LoadScene("Scene_Lobby");
                return;
            }

            HandleLoginError(result);
        }

        private async void OnGuestClicked()
        {
            SetLoading(true);

            AuthResult result = await AuthManager.Instance.LoginAsGuestAsync();

            SetLoading(false);

            if (result.Success)
            {
                GameSceneManager.Instance.LoadScene("Scene_Lobby");
                return;
            }

            ShowGeneralError(result.ErrorMessage);
        }

        private void OnRegisterClicked()
        {
            GameSceneManager.Instance.LoadScene("Scene_Register");
        }

        #endregion

        #region Error Handling

        private void HandleLoginError(LoginResult result)
        {
            switch (result.ErrorCode)
            {
                case "A006":
                    // Wrong password — shake field, show inline error
                    ShowPasswordError(result.ErrorMessage);
                    ShakeField(passwordInput.GetComponent<RectTransform>());
                    break;

                case "A007":
                    // Account locked — show countdown timer
                    int seconds = result.LockoutRemainingSeconds ?? 60;
                    ShowLockout(seconds);
                    break;

                default:
                    ShowGeneralError(result.ErrorMessage);
                    break;
            }
        }

        private void ShowPasswordError(string message)
        {
            passwordErrorText.text = message;
            passwordErrorText.gameObject.SetActive(true);
        }

        private void ShowLockout(int remainingSeconds)
        {
            lockoutPanel.SetActive(true);
            SetButtonsInteractable(false);

            if (_lockoutCoroutine != null)
                StopCoroutine(_lockoutCoroutine);

            _lockoutCoroutine = StartCoroutine(LockoutCountdown(remainingSeconds));
        }

        private void ShowGeneralError(string message)
        {
            passwordErrorText.text = message;
            passwordErrorText.gameObject.SetActive(true);
        }

        private void ClearErrors()
        {
            passwordErrorText.gameObject.SetActive(false);
            passwordErrorText.text = string.Empty;
        }

        #endregion

        #region Lockout Countdown

        private IEnumerator LockoutCountdown(int seconds)
        {
            int remaining = seconds;

            while (remaining > 0)
            {
                int minutes = remaining / 60;
                int secs    = remaining % 60;
                lockoutText.text = $"Too many attempts. Try again in {minutes:00}:{secs:00}";
                yield return new WaitForSeconds(1f);
                remaining--;
            }

            lockoutPanel.SetActive(false);
            SetButtonsInteractable(true);
            ClearErrors();
            lockoutText.text  = string.Empty;
            _lockoutCoroutine = null;
        }

        #endregion

        #region Validation

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(emailInput.text))
            {
                ShakeField(emailInput.GetComponent<RectTransform>());
                return false;
            }

            if (string.IsNullOrWhiteSpace(passwordInput.text))
            {
                ShakeField(passwordInput.GetComponent<RectTransform>());
                return false;
            }

            return true;
        }

        #endregion

        #region UI Helpers

        private void SetLoading(bool isLoading)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(isLoading);

            SetButtonsInteractable(!isLoading);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            loginButton.interactable    = interactable;
            guestButton.interactable    = interactable;
            registerButton.interactable = interactable;
        }

        private void ShakeField(RectTransform rectTransform)
        {
            rectTransform.DOShakeAnchorPos(
                ERROR_SHAKE_DURATION,
                ERROR_SHAKE_STRENGTH,
                ERROR_SHAKE_VIBRATO,
                randomness: 0f,
                snapping: false,
                fadeOut: true);
        }

        #endregion
    }
}