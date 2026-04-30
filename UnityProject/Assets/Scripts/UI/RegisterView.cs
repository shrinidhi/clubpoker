//
// Responsibilities:
//   - Real-time per-field validation as user types
//   - Register button disabled until all fields pass validation
//   - Password show/hide toggle
//   - Registration API call with U001/U002 field error handling
//   - Signup bonus chip animation on success
//   - Navigation to Lobby after success

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
    public class RegisterView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Error Display")]
        [SerializeField] private TextMeshProUGUI usernameErrorText;
        [SerializeField] private TextMeshProUGUI emailErrorText;
        [SerializeField] private TextMeshProUGUI passwordErrorText;

        [Header("Password Toggle")]
        [SerializeField] private Button showHideButton;
        [SerializeField] private Image  showHideIcon;
        [SerializeField] private Sprite showIcon;
        [SerializeField] private Sprite hideIcon;

        [Header("Buttons")]
        [SerializeField] private Button      registerButton;
        [SerializeField] private Button      loginButton;
        [SerializeField] private CanvasGroup registerButtonGroup;

        [Header("Bonus")]
        [SerializeField] private TextMeshProUGUI bonusText;

        [Header("Background")]
        [SerializeField] private RectTransform backgroundImage;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;


        [Header("Keyboard")]
        [SerializeField] private RectTransform formPanel;

        #endregion

        #region Constants

        // Validation rules (CLUB-525)
        private const int    USERNAME_MIN_LENGTH    = 3;
        private const int    USERNAME_MAX_LENGTH    = 20;
        private const int    PASSWORD_MIN_LENGTH    = 8;
        private const string USERNAME_PATTERN       = @"^[a-zA-Z0-9_]+$";
        private const string EMAIL_PATTERN          = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        private const string PASSWORD_UPPER_PATTERN = @"[A-Z]";
        private const string PASSWORD_NUMBER_PATTERN = @"[0-9]";

        // Animation
        private const float ERROR_SHAKE_DURATION   = 0.4f;
        private const float ERROR_SHAKE_STRENGTH   = 12f;
        private const int   ERROR_SHAKE_VIBRATO    = 20;
        private const float BONUS_ANIMATION_DELAY  = 0.5f;
        private const float LOBBY_NAVIGATE_DELAY   = 1.5f;
        private const float BUTTON_DISABLED_ALPHA  = 0.5f;
        private const float BUTTON_ENABLED_ALPHA   = 1.0f;

        #endregion

        #region Private Fields

        private bool _isPasswordVisible;

        // Track per-field validation state
        private bool _usernameValid;
        private bool _emailValid;
        private bool _passwordValid;
        private Vector2 _formDefaultPos;
        private bool    _keyboardVisible;
        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _formDefaultPos = formPanel.anchoredPosition;

            ResetView();
            BindInputs();
            BindButtons();
            AnimateBackground();
        }

        #endregion

        #region Setup

        private void BindInputs()
        {
            // Real-time validation as user types
            usernameInput.onValueChanged.AddListener(_ => OnUsernameChanged());
            emailInput.onValueChanged.AddListener(_ => OnEmailChanged());
            passwordInput.onValueChanged.AddListener(_ => OnPasswordChanged());
        }

        private void BindButtons()
        {
            registerButton.onClick.AddListener(OnRegisterClicked);
            loginButton.onClick.AddListener(OnLoginClicked);
            showHideButton.onClick.AddListener(OnShowHideClicked);
        }

        private void ResetView()
        {
            ClearAllErrors();
            SetLoading(false);
            SetRegisterButtonEnabled(false);

            bonusText.gameObject.SetActive(false);

            usernameInput.text = string.Empty;
            emailInput.text    = string.Empty;
            passwordInput.text = string.Empty;

            // Password hidden by default — crossed eye shown
            _isPasswordVisible            = false;
            showHideIcon.sprite           = hideIcon;
            passwordInput.contentType     = TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();

            _usernameValid = false;
            _emailValid    = false;
            _passwordValid = false;
        }

        #endregion


        private void Update()
        {
            HandleKeyboard();
        }

        private void HandleKeyboard()
        {
            bool keyboardOpen = TouchScreenKeyboard.visible;
            if (keyboardOpen == _keyboardVisible) return;
            _keyboardVisible = keyboardOpen;

            if (keyboardOpen)
            {
                float pushAmount = GetPushAmount();
                 formPanel.DOAnchorPosY(_formDefaultPos.y + pushAmount, 0.3f)
                           .SetEase(Ease.OutCubic);
            }
            else
            {
                formPanel.DOAnchorPosY(_formDefaultPos.y, 0.3f)
                         .SetEase(Ease.OutCubic);
            }
        }

        private float GetPushAmount()
        {
            float formHeight = formPanel.rect.height;

            if (passwordInput.isFocused)
                return formHeight * 0.62f;  // password is lowest — needs most push

            if (emailInput.isFocused)
                return formHeight * 0.2f;  // email is middle — moderate push

            if (usernameInput.isFocused)
                return 0f;                      // username is top — no push needed

            return 0f;
        }

        #region Real-time Validation

        private void OnUsernameChanged()
        {
            string username = usernameInput.text.Trim();
            ClearFieldError(usernameErrorText);

            if (string.IsNullOrEmpty(username))
            {
                _usernameValid = false;
            }
            else if (username.Length < USERNAME_MIN_LENGTH)
            {
                ShowFieldError(usernameErrorText,
                    $"Username must be at least {USERNAME_MIN_LENGTH} characters.");
                _usernameValid = false;
            }
            else if (username.Length > USERNAME_MAX_LENGTH)
            {
                ShowFieldError(usernameErrorText,
                    $"Username must be under {USERNAME_MAX_LENGTH} characters.");
                _usernameValid = false;
            }
            else if (!Regex.IsMatch(username, USERNAME_PATTERN))
            {
                ShowFieldError(usernameErrorText,
                    "Username can only contain letters, numbers and underscores.");
                _usernameValid = false;
            }
            else
            {
                _usernameValid = true;
            }

            UpdateRegisterButton();
        }

        private void OnEmailChanged()
        {
            string email = emailInput.text.Trim();
            ClearFieldError(emailErrorText);

            if (string.IsNullOrEmpty(email))
            {
                _emailValid = false;
            }
            else if (!Regex.IsMatch(email, EMAIL_PATTERN))
            {
                ShowFieldError(emailErrorText, "Please enter a valid email address.");
                _emailValid = false;
            }
            else
            {
                _emailValid = true;
            }

            UpdateRegisterButton();
        }

        private void OnPasswordChanged()
        {
            string password = passwordInput.text;
            ClearFieldError(passwordErrorText);

            if (string.IsNullOrEmpty(password))
            {
                _passwordValid = false;
            }
            else if (password.Length < PASSWORD_MIN_LENGTH)
            {
                ShowFieldError(passwordErrorText,
                    $"Password must be at least {PASSWORD_MIN_LENGTH} characters.");
                _passwordValid = false;
            }
            else if (!Regex.IsMatch(password, PASSWORD_UPPER_PATTERN))
            {
                ShowFieldError(passwordErrorText,
                    "Password must contain at least 1 uppercase letter.");
                _passwordValid = false;
            }
            else if (!Regex.IsMatch(password, PASSWORD_NUMBER_PATTERN))
            {
                ShowFieldError(passwordErrorText,
                    "Password must contain at least 1 number.");
                _passwordValid = false;
            }
            else
            {
                _passwordValid = true;
            }

            UpdateRegisterButton();
        }

        private void UpdateRegisterButton()
        {
            bool allValid = _usernameValid && _emailValid && _passwordValid;
            SetRegisterButtonEnabled(allValid);
        }

        #endregion

        #region Button Handlers

        private async void OnRegisterClicked()
        {
            // Final validation check before API call
            if (!_usernameValid || !_emailValid || !_passwordValid) return;

            SetLoading(true);

            RegisterResult result = await AuthManager.Instance.RegisterAsync(
                usernameInput.text.Trim(),
                emailInput.text.Trim(),
                passwordInput.text);

            SetLoading(false);

            if (result.Success)
            {
                OnRegisterSuccess();
                return;
            }

            HandleRegisterError(result);
        }

        private void OnLoginClicked()
        {
            GameSceneManager.Instance.LoadScene("Scene_Login");
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

        #region Success

        private void OnRegisterSuccess()
        {
            SetButtonsInteractable(false);
            StartCoroutine(ShowBonusAndNavigate());
        }

        private IEnumerator ShowBonusAndNavigate()
        {
            yield return new WaitForSeconds(BONUS_ANIMATION_DELAY);

            int chips = AuthManager.Instance.Session.WalletChips;
            bonusText.text = $"+{chips} chips bonus!";
            bonusText.gameObject.SetActive(true);
            bonusText.transform
                .DOPunchScale(Vector3.one * 0.3f, 0.5f, 5, 0.5f)
                .SetEase(Ease.OutBack);

            yield return new WaitForSeconds(LOBBY_NAVIGATE_DELAY);

            GameSceneManager.Instance.LoadScene("Scene_MainMenu");
        }

        #endregion

        #region Error Handling

        private void HandleRegisterError(RegisterResult result)
        {
            switch (result.ErrorCode)
            {
                case "U001":
                    ShowFieldError(usernameErrorText, result.ErrorMessage);
                    ShakeField(usernameInput.GetComponent<RectTransform>());
                    break;

                case "U002":
                    ShowFieldError(emailErrorText, result.ErrorMessage);
                    ShakeField(emailInput.GetComponent<RectTransform>());
                    break;

                case "V001":
                    ShowFieldError(passwordErrorText, result.ErrorMessage);
                    ShakeField(passwordInput.GetComponent<RectTransform>());
                    break;

                case "A007":
                    ShowFieldError(passwordErrorText, result.ErrorMessage);
                    break;

                default:
                    ShowFieldError(passwordErrorText, result.ErrorMessage);
                    break;
            }
        }

        private void ShowFieldError(TextMeshProUGUI errorText, string message)
        {
            errorText.text = message;
            errorText.gameObject.SetActive(true);
        }

        private void ClearFieldError(TextMeshProUGUI errorText)
        {
            errorText.gameObject.SetActive(false);
            errorText.text = string.Empty;
        }

        private void ClearAllErrors()
        {
            ClearFieldError(usernameErrorText);
            ClearFieldError(emailErrorText);
            ClearFieldError(passwordErrorText);
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
            loginButton.interactable = interactable;

            // Register button only interactable if all fields valid
            if (interactable)
                UpdateRegisterButton();
            else
                SetRegisterButtonEnabled(false);
        }

        private void SetRegisterButtonEnabled(bool enabled)
        {
            registerButton.interactable      = enabled;
            registerButtonGroup.alpha        = enabled
                ? BUTTON_ENABLED_ALPHA
                : BUTTON_DISABLED_ALPHA;
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

        private void AnimateBackground()
        {
            if (backgroundImage == null) return;

            backgroundImage.localScale = Vector3.one * 1.05f;

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
    }
}