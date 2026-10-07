//
// Responsibilities:
//   - Real-time per-field validation as user types
//   - Register button disabled until all fields pass validation
//   - Password show/hide toggle
//   - Registration API call with U001/U002 field error handling
//   - Signup bonus chip animation on success
//   - Navigation to Lobby after success

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

        [Tooltip("How far the form lifts while the keyboard is open, as a fraction " +
                 "of the form's height. Kept small — the form should only nudge up.")]
        [SerializeField, Range(0f, 0.5f)] private float keyboardPushFraction = 0.15f;

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
        private const float BUTTON_DISABLED_ALPHA  = 0.5f;
        private const float BUTTON_ENABLED_ALPHA   = 1.0f;

        #endregion

        #region Private Fields

        private bool _isPasswordVisible;
        private Vector2 _formDefaultPos;
        private float   _currentPush;
        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _formDefaultPos = formPanel.anchoredPosition;

            ResetView();
            BindInputs();
            BindButtons();
            // AnimateBackground();
        }

        #endregion

        #region Setup

        private void BindInputs()
        {
            // Validation runs on Register, not while typing — typing only clears
            // that field's error so a fixed field stops showing red.
            usernameInput.onValueChanged.AddListener(_ => ClearFieldError(usernameErrorText));
            emailInput.onValueChanged.AddListener(_ => ClearFieldError(emailErrorText));
            passwordInput.onValueChanged.AddListener(_ => ClearFieldError(passwordErrorText));

            // Enter / keyboard Done: username → email → password → register.
            usernameInput.onSubmit.AddListener(_ => FocusField(emailInput));
            emailInput.onSubmit.AddListener(_ => FocusField(passwordInput));
            passwordInput.onSubmit.AddListener(_ =>
            {
                // Same gate as tapping it — off only while a request is running.
                if (registerButton.interactable) OnRegisterClicked();
            });
        }

        private static void FocusField(TMP_InputField field)
        {
            field.Select();
            field.ActivateInputField();
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

            bonusText.gameObject.SetActive(false);

            usernameInput.text = string.Empty;
            emailInput.text    = string.Empty;
            passwordInput.text = string.Empty;

            // Password hidden by default — crossed eye shown
            _isPasswordVisible            = false;
            showHideIcon.sprite           = hideIcon;
            passwordInput.contentType     = TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();
        }

        #endregion


        private void Update()
        {
            HandleKeyboard();
        }

        // Re-checked every frame, not just when the keyboard opens: Enter moves
        // focus between fields with the keyboard still up, and the lift has to
        // follow (username → none, email → lifted).
        private void HandleKeyboard()
        {
            float push = TouchScreenKeyboard.visible ? GetPushAmount() : 0f;
            if (Mathf.Approximately(push, _currentPush)) return;
            _currentPush = push;

            formPanel.DOKill();
            formPanel.DOAnchorPosY(_formDefaultPos.y + push, 0.3f)
                     .SetEase(Ease.OutCubic);
        }

        // Username sits at the top, clear of the keyboard — no lift. Email and
        // password get one small lift (the form used to fly up by most of its
        // height for the password).
        private float GetPushAmount()
        {
            if (usernameInput.isFocused)
                return 0f;

            if (emailInput.isFocused || passwordInput.isFocused)
                return formPanel.rect.height * keyboardPushFraction;

            // Nothing focused for a frame while focus hops between fields —
            // hold the current lift so the form doesn't dip and come back.
            return _currentPush;
        }

        #region Validation

        // Each returns the error to show, or null when the field is fine.

        private static string ValidateUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
                return "Please enter a username.";

            if (username.Length < USERNAME_MIN_LENGTH)
                return $"Username must be at least {USERNAME_MIN_LENGTH} characters.";

            if (username.Length > USERNAME_MAX_LENGTH)
                return $"Username must be under {USERNAME_MAX_LENGTH} characters.";

            if (!Regex.IsMatch(username, USERNAME_PATTERN))
                return "Username can only contain letters, numbers and underscores.";

            return null;
        }

        private static string ValidateEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return "Please enter your email.";

            if (!Regex.IsMatch(email, EMAIL_PATTERN))
                return "Please enter a valid email address.";

            return null;
        }

        private static string ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return "Please enter a password.";

            if (password.Length < PASSWORD_MIN_LENGTH)
                return $"Password must be at least {PASSWORD_MIN_LENGTH} characters.";

            if (!Regex.IsMatch(password, PASSWORD_UPPER_PATTERN))
                return "Password must contain at least 1 uppercase letter.";

            if (!Regex.IsMatch(password, PASSWORD_NUMBER_PATTERN))
                return "Password must contain at least 1 number.";

            return null;
        }

        /// <summary>Check every field, show each one's error and shake it.
        /// All three are checked, so the player sees every problem at once.</summary>
        private bool ValidateAll()
        {
            bool ok = true;

            ok &= CheckField(usernameInput, usernameErrorText,
                             ValidateUsername(usernameInput.text.Trim()));
            ok &= CheckField(emailInput, emailErrorText,
                             ValidateEmail(emailInput.text.Trim()));
            ok &= CheckField(passwordInput, passwordErrorText,
                             ValidatePassword(passwordInput.text));

            return ok;
        }

        private bool CheckField(TMP_InputField input, TextMeshProUGUI errorText, string error)
        {
            if (error == null)
            {
                ClearFieldError(errorText);
                return true;
            }

            ShowFieldError(errorText, error);
            ShakeInput(input);
            return false;
        }

        #endregion

        #region Button Handlers

        private async void OnRegisterClicked()
        {
            if (!ValidateAll()) return;

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
            // Sign-up bonus popup removed at client request — go straight to main menu,
            // which asks for a nickname once (pre-filled with the username).
            SetButtonsInteractable(false);
            NicknamePromptPanel.MarkPending(AuthManager.Instance.Session?.Id);
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
                    ShakeInput(usernameInput);
                    break;

                case "U002":
                    ShowFieldError(emailErrorText, result.ErrorMessage);
                    ShakeInput(emailInput);
                    break;

                case "V001":
                    ShowFieldError(passwordErrorText, result.ErrorMessage);
                    ShakeInput(passwordInput);
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

        // Register is always tappable except while a request is running or we're
        // leaving — bad input is reported on tap, not by greying the button out.
        private void SetButtonsInteractable(bool interactable)
        {
            loginButton.interactable = interactable;
            SetRegisterButtonEnabled(interactable);
        }

        private void SetRegisterButtonEnabled(bool enabled)
        {
            registerButton.interactable      = enabled;
            registerButtonGroup.alpha        = enabled
                ? BUTTON_ENABLED_ALPHA
                : BUTTON_DISABLED_ALPHA;
        }

        // Shake the field's whole row (…Container: input + error text, plus the
        // show/hide button for password), not just the input. A field placed
        // straight in the form's layout has no row, so it shakes by itself —
        // never the whole form.
        private void ShakeInput(TMP_InputField input)
        {
            var parent = input.transform.parent as RectTransform;

            bool hasRow = parent != null && parent.GetComponent<LayoutGroup>() == null;

            ShakeField(hasRow ? parent : (RectTransform)input.transform);
        }

        private void ShakeField(RectTransform rectTransform)
        {
            // Finish any shake still running first. Starting one mid-shake captures
            // the offset position as "home", so repeated taps walk the field away.
            rectTransform.DOComplete();

            rectTransform.DOShakeAnchorPos(
                ERROR_SHAKE_DURATION,
                ERROR_SHAKE_STRENGTH,
                ERROR_SHAKE_VIBRATO,
                randomness: 0f,
                snapping: false,
                fadeOut: true)
                // Fields inside a layout group: the tween ends on the position it
                // captured at start, which is stale if the layout moved meanwhile
                // (error text, keyboard) — hand placement back to the layout group.
                .OnComplete(() => RestoreLayout(rectTransform));
        }

        private static void RestoreLayout(RectTransform rectTransform)
        {
            if (rectTransform != null && rectTransform.parent is RectTransform parent)
                LayoutRebuilder.MarkLayoutForRebuild(parent);
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