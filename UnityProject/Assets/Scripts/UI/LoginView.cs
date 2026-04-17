
using System.Collections;
using System.Text.RegularExpressions;
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

        [Header("Background")]
        [SerializeField] private RectTransform backgroundImage;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Password Toggle")]
        [SerializeField] private Button showHideButton;
        [SerializeField] private Image  showHideIcon;
        [SerializeField] private Sprite showIcon;
        [SerializeField] private Sprite hideIcon;

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

        [Header("Version Info")]
        [SerializeField] private TextMeshProUGUI versionText;

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
        private bool _isPasswordVisible = false;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            ResetView();
            BindButtons();
            UpdateVersionText();

            AnimateBackground();
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
            showHideButton.onClick.AddListener(OnShowHideClicked);

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

            _isPasswordVisible  = false;
            showHideIcon.sprite = hideIcon;
            passwordInput.contentType = TMP_InputField.ContentType.Password;
        }

        private void AnimateBackground()
        {
            // Start slightly zoomed in
            backgroundImage.localScale = Vector3.one * 1.05f;

            // Slowly zoom in further while panning slightly
            backgroundImage
                .DOScale(1.12f, 8f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

            backgroundImage
                .DOAnchorPos(new Vector2(20f, 15f), 8f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        #endregion

        private void UpdateVersionText()
        {
            versionText.text = $"Version : {Application.version}";
        }
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

        private void OnShowHideClicked()
        {
            _isPasswordVisible = !_isPasswordVisible;

            passwordInput.contentType = _isPasswordVisible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            passwordInput.ForceLabelUpdate();

            showHideIcon.sprite = _isPasswordVisible ? showIcon : hideIcon;
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
                case "N001":
                    ShakeField(loginButton.GetComponent<RectTransform>());
                    ShowPasswordError("No internet connection. Please try again.");
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
            bool valid = true;

            if (string.IsNullOrWhiteSpace(emailInput.text))
            {
                ShakeField(emailInput.GetComponent<RectTransform>());
                valid = false;
            }
            else if (!Regex.IsMatch(emailInput.text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ShowPasswordError("Please enter a valid email address.");
                ShakeField(emailInput.GetComponent<RectTransform>());
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(passwordInput.text))
            {
                ShakeField(passwordInput.GetComponent<RectTransform>());
                valid = false;
            }

            return valid;
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