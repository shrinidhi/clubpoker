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
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class RegisterView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField reEnterPasswordInput;

        [Header("Error Display")]
        [SerializeField] private TextMeshProUGUI usernameErrorText;
        [SerializeField] private TextMeshProUGUI emailErrorText;
        [SerializeField] private TextMeshProUGUI passwordErrorText;
        [SerializeField] private TextMeshProUGUI reEnterPasswordErrorText;

        [Header("Password Toggle")]
        [SerializeField] private Button showHideButton;
        [SerializeField] private Image  showHideIcon;
        [SerializeField] private Button reEnterShowHideButton;
        [SerializeField] private Image  reEnterShowHideIcon;
        [SerializeField] private Sprite showIcon;
        [SerializeField] private Sprite hideIcon;

        [Header("Captcha")]
        [SerializeField] private CaptchaImageView captchaImage;
        [SerializeField] private TMP_InputField   captchaInput;
        [Tooltip("Optional — captcha errors fall back to the password error text.")]
        [SerializeField] private TextMeshProUGUI  captchaErrorText;
        [Tooltip("Optional — e.g. a Button on the captcha image for tap-to-refresh.")]
        [SerializeField] private Button           captchaRefreshButton;

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

        [Header("Version")]
        [Tooltip("Filled from Application.version in the current language — no LocalizedText on it.")]
        [SerializeField] private TextMeshProUGUI versionText;


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
        private const string USERNAME_PATTERN       = @"^[a-zA-Z0-9]+$";
        private const string EMAIL_PATTERN          = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        // Animation
        private const float ERROR_SHAKE_DURATION   = 0.4f;
        private const float ERROR_SHAKE_STRENGTH   = 12f;
        private const int   ERROR_SHAKE_VIBRATO    = 20;
        private const float BUTTON_DISABLED_ALPHA  = 0.5f;
        private const float BUTTON_ENABLED_ALPHA   = 1.0f;

        #endregion

        #region Private Fields

        private bool _isPasswordVisible;
        private bool _isReEnterVisible;
        private string _captchaId;
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
            LoadCaptchaAsync().Forget();
            UpdateVersionText();
            Loc.OnLanguageChanged += UpdateVersionText;
            // AnimateBackground();
        }

        private void OnDestroy()
        {
            Loc.OnLanguageChanged -= UpdateVersionText;
        }

        private void UpdateVersionText()
        {
            if (versionText != null)
                versionText.text = Loc.Get("version", ("version", Application.version));
        }

        #endregion

        #region Setup

        private void BindInputs()
        {
            // Full validation runs on Register, not while typing — typing only clears
            // that field's error so a fixed field stops showing red. Each field is
            // also checked when the player leaves it (onEndEdit).
            usernameInput.onValueChanged.AddListener(_ => ClearFieldError(usernameErrorText));
            emailInput.onValueChanged.AddListener(_ => ClearFieldError(emailErrorText));
            passwordInput.onValueChanged.AddListener(_ => ClearFieldError(passwordErrorText));
            reEnterPasswordInput.onValueChanged.AddListener(_ => ClearFieldError(reEnterPasswordErrorText));
            captchaInput.onValueChanged.AddListener(_ => ClearCaptchaError());

            usernameInput.onEndEdit.AddListener(text =>
                CheckOnLeave(usernameErrorText, text.Trim(), ValidateUsername));
            emailInput.onEndEdit.AddListener(text =>
                CheckOnLeave(emailErrorText, text.Trim(), ValidateEmail));
            passwordInput.onEndEdit.AddListener(_ => OnPasswordsEndEdit());
            reEnterPasswordInput.onEndEdit.AddListener(_ => OnPasswordsEndEdit());

            // Enter / keyboard Done: username → email → password → re-enter → captcha → register.
            usernameInput.onSubmit.AddListener(_ => FocusField(emailInput));
            emailInput.onSubmit.AddListener(_ => FocusField(passwordInput));
            passwordInput.onSubmit.AddListener(_ => FocusField(reEnterPasswordInput));
            reEnterPasswordInput.onSubmit.AddListener(_ => FocusField(captchaInput));
            captchaInput.onSubmit.AddListener(_ =>
            {
                // Same gate as tapping it — off only while a request is running.
                if (registerButton.interactable) OnRegisterClicked();
            });
        }

        // On leaving a field: show its error, no shake. Empty fields are skipped —
        // tapping through the form shouldn't flash errors.
        private void CheckOnLeave(TextMeshProUGUI errorText, string text,
                                  System.Func<string, string> validate)
        {
            if (string.IsNullOrEmpty(text)) return;

            string error = validate(text);
            if (error == null) ClearFieldError(errorText);
            else               ShowFieldError(errorText, error);
        }

        // Both password fields are re-checked from either one, so editing the
        // password re-flags (or clears) a mismatch.
        private void OnPasswordsEndEdit()
        {
            string password = passwordInput.text;

            CheckOnLeave(passwordErrorText, password, ValidatePassword);
            CheckOnLeave(reEnterPasswordErrorText, reEnterPasswordInput.text,
                         reEntered => ValidateReEnterPassword(password, reEntered));
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
            reEnterShowHideButton.onClick.AddListener(OnReEnterShowHideClicked);

            if (captchaRefreshButton != null)
                captchaRefreshButton.onClick.AddListener(() => LoadCaptchaAsync().Forget());
        }

        private void ResetView()
        {
            ClearAllErrors();
            SetLoading(false);

            bonusText.gameObject.SetActive(false);

            usernameInput.text        = string.Empty;
            emailInput.text           = string.Empty;
            passwordInput.text        = string.Empty;
            reEnterPasswordInput.text = string.Empty;
            captchaInput.text         = string.Empty;

            // Passwords hidden by default — crossed eye shown
            _isPasswordVisible = false;
            _isReEnterVisible  = false;
            ApplyPasswordVisibility(passwordInput, showHideIcon, false);
            ApplyPasswordVisibility(reEnterPasswordInput, reEnterShowHideIcon, false);
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

        // Username sits at the top, clear of the keyboard — no lift. The fields
        // below it get one small lift (the form used to fly up by most of its
        // height for the password).
        private float GetPushAmount()
        {
            if (usernameInput.isFocused)
                return 0f;

            if (emailInput.isFocused || passwordInput.isFocused ||
                reEnterPasswordInput.isFocused || captchaInput.isFocused)
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
                return Loc.Get("enter_username");

            if (username.Length < USERNAME_MIN_LENGTH)
                return Loc.Get("username_too_short", ("min", USERNAME_MIN_LENGTH));

            if (username.Length > USERNAME_MAX_LENGTH)
                return Loc.Get("username_too_long", ("max", USERNAME_MAX_LENGTH));

            if (!Regex.IsMatch(username, USERNAME_PATTERN))
                return Loc.Get("username_letters_numbers");

            return null;
        }

        private static string ValidateEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return Loc.Get("enter_email");

            if (!Regex.IsMatch(email, EMAIL_PATTERN))
                return Loc.Get("invalid_email");

            return null;
        }

        private static string ValidatePassword(string password) =>
            PasswordRules.Validate(password);

        private static string ValidateReEnterPassword(string password, string reEntered) =>
            PasswordRules.ValidateMatch(password, reEntered);

        private string ValidateCaptcha(string text)
        {
            if (string.IsNullOrEmpty(_captchaId))
                return Loc.Get("captcha_not_loaded");

            if (string.IsNullOrEmpty(text))
                return Loc.Get("enter_captcha");

            return null;
        }

        /// <summary>Check every field, show each one's error and shake it.
        /// All fields are checked, so the player sees every problem at once.</summary>
        private bool ValidateAll()
        {
            bool ok = true;

            ok &= CheckField(usernameInput, usernameErrorText,
                             ValidateUsername(usernameInput.text.Trim()));
            ok &= CheckField(emailInput, emailErrorText,
                             ValidateEmail(emailInput.text.Trim()));
            ok &= CheckField(passwordInput, passwordErrorText,
                             ValidatePassword(passwordInput.text));
            ok &= CheckField(reEnterPasswordInput, reEnterPasswordErrorText,
                             ValidateReEnterPassword(passwordInput.text, reEnterPasswordInput.text));

            // Not CheckField: a passing captcha mustn't clear a shared error text.
            string captchaError = ValidateCaptcha(captchaInput.text.Trim());
            if (captchaError != null)
            {
                ShowFieldError(CaptchaErrorText, captchaError);
                ShakeInput(captchaInput);
                ok = false;
            }
            else
            {
                ClearCaptchaError();
            }

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
                passwordInput.text,
                _captchaId,
                captchaInput.text.Trim());

            SetLoading(false);

            if (result.Success)
            {
                OnRegisterSuccess();
                return;
            }

            HandleRegisterError(result);

            // A captcha is single-use — every failed attempt needs a new one.
            LoadCaptchaAsync().Forget();
        }

        private async UniTaskVoid LoadCaptchaAsync()
        {
            _captchaId = null;
            // Without notify: clearing must not wipe the error a failed register just showed.
            captchaInput.SetTextWithoutNotify(string.Empty);
            captchaImage.Clear();

            CaptchaResponse captcha = await AuthManager.Instance.GetCaptchaAsync();
            if (this == null) return; // scene left while loading

            if (captcha == null || !captchaImage.Show(captcha.Image))
            {
                ShowFieldError(CaptchaErrorText, Loc.Get("captcha_load_failed"));
                return;
            }

            _captchaId = captcha.CaptchaId;
        }

        private void OnLoginClicked()
        {
            GameSceneManager.Instance.LoadSceneWithFade("Scene_Login");
        }

        private void OnShowHideClicked()
        {
            _isPasswordVisible = !_isPasswordVisible;
            ApplyPasswordVisibility(passwordInput, showHideIcon, _isPasswordVisible);
        }

        private void OnReEnterShowHideClicked()
        {
            _isReEnterVisible = !_isReEnterVisible;
            ApplyPasswordVisibility(reEnterPasswordInput, reEnterShowHideIcon, _isReEnterVisible);
        }

        private void ApplyPasswordVisibility(TMP_InputField input, Image icon, bool visible)
        {
            input.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            input.ForceLabelUpdate();

            icon.sprite = visible ? showIcon : hideIcon;
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
            // error.<code> in the player's language when we have it, else the server's text.
            string message = Loc.Error(result.ErrorCode, result.ErrorMessage);

            switch (result.ErrorCode)
            {
                case "U001":
                    ShowFieldError(usernameErrorText, message);
                    ShakeInput(usernameInput);
                    break;

                case "U002":
                    ShowFieldError(emailErrorText, message);
                    ShakeInput(emailInput);
                    break;

                case "V001":
                    // Server-side validation with a specific reason — no generic
                    // translation would say the same, so its own text shows.
                    ShowFieldError(passwordErrorText, message);
                    ShakeInput(passwordInput);
                    break;

                case "V009":
                    // Wrong / expired / missing captcha — a fresh one is loaded after this.
                    ShowFieldError(CaptchaErrorText, message);
                    ShakeInput(captchaInput);
                    break;

                default:
                    ShowFieldError(passwordErrorText, message);
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
            ClearFieldError(reEnterPasswordErrorText);
            ClearCaptchaError();
        }

        // No captcha error text in the scene yet — share the password one.
        private TextMeshProUGUI CaptchaErrorText =>
            captchaErrorText != null ? captchaErrorText : passwordErrorText;

        // Only a dedicated captcha error text is cleared here — the shared
        // password one belongs to the password field.
        private void ClearCaptchaError()
        {
            if (captchaErrorText != null)
                ClearFieldError(captchaErrorText);
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