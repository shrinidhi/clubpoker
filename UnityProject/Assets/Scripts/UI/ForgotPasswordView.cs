//
// Forgot password popup on the Login scene — sits on the popup root, which
// starts inactive; Open/Close show and hide it. Three steps:
//   1. Email  — email + captcha → POST forgot-password/request (code emailed)
//   2. Code   — code → POST forgot-password/verify → resetToken
//   3. Password — new password + confirm → POST forgot-password/reset
// Then the panel closes and Login is pre-filled with the email.
//
// "Resend code" goes back to step 1: the request API needs a fresh captcha.

using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;

namespace ClubPoker.UI
{
    public class ForgotPasswordView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Panel")]
        [SerializeField] private Button     closeButton;

        [Header("Step 1 — Email")]
        [SerializeField] private GameObject       emailStep;
        [SerializeField] private TMP_InputField   emailInput;
        [SerializeField] private TextMeshProUGUI  emailErrorText;
        [SerializeField] private CaptchaImageView captchaImage;
        [SerializeField] private TMP_InputField   captchaInput;
        [Tooltip("Optional — captcha errors fall back to the email error text.")]
        [SerializeField] private TextMeshProUGUI  captchaErrorText;
        [Tooltip("Optional — e.g. a Button on the captcha image for tap-to-refresh.")]
        [SerializeField] private Button           captchaRefreshButton;
        [SerializeField] private Button           sendCodeButton;

        [Header("Step 2 — Code")]
        [SerializeField] private GameObject      codeStep;
        [Tooltip("Optional — \"Code sent to …\".")]
        [SerializeField] private TextMeshProUGUI codeSentText;
        [SerializeField] private TMP_InputField  codeInput;
        [SerializeField] private TextMeshProUGUI codeErrorText;
        [SerializeField] private Button          verifyButton;
        [SerializeField] private Button          resendButton;
        [Tooltip("Optional — label on the resend button, shows the cooldown.")]
        [SerializeField] private TextMeshProUGUI resendText;

        [Header("Step 3 — New Password")]
        [SerializeField] private GameObject      passwordStep;
        [SerializeField] private TMP_InputField  newPasswordInput;
        [SerializeField] private TextMeshProUGUI newPasswordErrorText;
        [SerializeField] private TMP_InputField  confirmPasswordInput;
        [SerializeField] private TextMeshProUGUI confirmPasswordErrorText;
        [Tooltip("Shows \"Password reset…\" on success before the popup closes.")]
        [SerializeField] private TextMeshProUGUI resetStatusText;
        [SerializeField] private Button          resetButton;

        [Header("Password Toggles (optional)")]
        [SerializeField] private Button showHideButton;
        [SerializeField] private Image  showHideIcon;
        [SerializeField] private Button confirmShowHideButton;
        [SerializeField] private Image  confirmShowHideIcon;
        [SerializeField] private Sprite showIcon;
        [SerializeField] private Sprite hideIcon;

        #endregion

        #region Constants

        private const string EMAIL_PATTERN         = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        private const int    RESEND_COOLDOWN_SECS  = 30;
        private const float  RESET_DONE_CLOSE_SECS = 2f;
        private const string RESET_DONE_MESSAGE    = "Password reset. Please log in with your new password.";

        private const float ERROR_SHAKE_DURATION = 0.4f;
        private const float ERROR_SHAKE_STRENGTH = 12f;
        private const int   ERROR_SHAKE_VIBRATO  = 20;

        #endregion

        #region Private Fields

        private enum Step { Email, Code, Password }

        private string _captchaId;
        private string _email;       // the address the code was sent to
        private string _resetToken;
        private bool   _isPasswordVisible;
        private bool   _isConfirmVisible;
        private Coroutine _resendCoroutine;
        private Action<string> _onReset;
        private bool _isBound;

        #endregion

        /// onReset is called with the email once the password is reset (Login pre-fills it).
        public void Open(string prefillEmail, Action<string> onReset)
        {
            EnsureBound();
            _onReset = onReset;

            emailInput.text = prefillEmail ?? string.Empty;
            captchaInput.text = string.Empty;
            codeInput.text = string.Empty;
            newPasswordInput.text = string.Empty;
            confirmPasswordInput.text = string.Empty;
            _resetToken = null;

            _isPasswordVisible = false;
            _isConfirmVisible  = false;
            ApplyPasswordVisibility(newPasswordInput, showHideIcon, false);
            ApplyPasswordVisibility(confirmPasswordInput, confirmShowHideIcon, false);

            gameObject.SetActive(true);
            ShowStep(Step.Email);
            LoadCaptchaAsync().Forget();
        }

        public void Close()
        {
            StopResendCooldown();
            captchaImage.Clear();
            SetLoading(false);
            gameObject.SetActive(false);
        }

        #region Setup

        // Bound on first Open, not Awake: the popup starts inactive, so Awake
        // wouldn't run before Open.
        private void EnsureBound()
        {
            if (_isBound) return;
            _isBound = true;

            BindButtons();
            BindInputs();
        }

        private void BindButtons()
        {
            closeButton.onClick.AddListener(Close);

            sendCodeButton.onClick.AddListener(OnSendCodeClicked);
            verifyButton.onClick.AddListener(OnVerifyClicked);
            resendButton.onClick.AddListener(OnResendClicked);
            resetButton.onClick.AddListener(OnResetClicked);

            if (captchaRefreshButton != null)
                captchaRefreshButton.onClick.AddListener(() => LoadCaptchaAsync().Forget());

            if (showHideButton != null)
                showHideButton.onClick.AddListener(() =>
                {
                    _isPasswordVisible = !_isPasswordVisible;
                    ApplyPasswordVisibility(newPasswordInput, showHideIcon, _isPasswordVisible);
                });

            if (confirmShowHideButton != null)
                confirmShowHideButton.onClick.AddListener(() =>
                {
                    _isConfirmVisible = !_isConfirmVisible;
                    ApplyPasswordVisibility(confirmPasswordInput, confirmShowHideIcon, _isConfirmVisible);
                });
        }

        private void BindInputs()
        {
            // Typing clears that field's error.
            emailInput.onValueChanged.AddListener(_ => ClearFieldError(emailErrorText));
            captchaInput.onValueChanged.AddListener(_ => ClearCaptchaError());
            codeInput.onValueChanged.AddListener(_ => ClearFieldError(codeErrorText));
            newPasswordInput.onValueChanged.AddListener(_ => ClearFieldError(newPasswordErrorText));
            confirmPasswordInput.onValueChanged.AddListener(_ => ClearFieldError(confirmPasswordErrorText));

            // Leaving a field checks it (same as Register).
            emailInput.onEndEdit.AddListener(text =>
                CheckOnLeave(emailErrorText, text.Trim(), ValidateEmail));
            newPasswordInput.onEndEdit.AddListener(_ => OnPasswordsEndEdit());
            confirmPasswordInput.onEndEdit.AddListener(_ => OnPasswordsEndEdit());

            // Enter / keyboard Done moves on, or submits the step.
            emailInput.onSubmit.AddListener(_ => FocusField(captchaInput));
            captchaInput.onSubmit.AddListener(_ => { if (sendCodeButton.interactable) OnSendCodeClicked(); });
            codeInput.onSubmit.AddListener(_ => { if (verifyButton.interactable) OnVerifyClicked(); });
            newPasswordInput.onSubmit.AddListener(_ => FocusField(confirmPasswordInput));
            confirmPasswordInput.onSubmit.AddListener(_ => { if (resetButton.interactable) OnResetClicked(); });
        }

        #endregion

        #region Steps

        private void ShowStep(Step step)
        {
            emailStep.SetActive(step == Step.Email);
            codeStep.SetActive(step == Step.Code);
            passwordStep.SetActive(step == Step.Password);

            ClearAllErrors();
        }

        // Back to step 1 with the email kept and a fresh captcha.
        private void GoToEmailStep()
        {
            StopResendCooldown();
            ShowStep(Step.Email);
            LoadCaptchaAsync().Forget();
        }

        #endregion

        #region Step 1 — Email

        private async void OnSendCodeClicked()
        {
            string email = emailInput.text.Trim();
            string captcha = captchaInput.text.Trim();

            bool ok = true;
            ok &= CheckField(emailInput, emailErrorText, ValidateEmail(email));

            string captchaError = ValidateCaptcha(captcha);
            if (captchaError != null)
            {
                ShowFieldError(CaptchaErrorText, captchaError);
                ShakeInput(captchaInput);
                ok = false;
            }

            if (!ok) return;

            SetLoading(true);
            PasswordResetRequestResult result = await AuthManager.Instance.RequestPasswordResetAsync(
                email, _captchaId, captcha);
            if (this == null) return;
            SetLoading(false);

            if (result.Success)
            {
                _email = email;
                codeInput.text = string.Empty;
                if (codeSentText != null)
                {
                    codeSentText.text = $"A 6-digit code was sent to {email}.";

                    // Dev server returns the code — show it in dev builds only
                    // (testers on a phone have no Console).
                    if (Debug.isDebugBuild && !string.IsNullOrEmpty(result.DevCode))
                        codeSentText.text += $" (dev: {result.DevCode})";
                }

                ShowStep(Step.Code);
                StartResendCooldown();
                FocusField(codeInput);
                return;
            }

            if (result.ErrorCode == "V009")
            {
                ShowFieldError(CaptchaErrorText, result.ErrorMessage);
                ShakeInput(captchaInput);
            }
            else
            {
                ShowFieldError(emailErrorText, result.ErrorMessage);
            }

            // A captcha is single-use — every failed attempt needs a new one.
            LoadCaptchaAsync().Forget();
        }

        private async UniTaskVoid LoadCaptchaAsync()
        {
            _captchaId = null;
            // Without notify: clearing must not wipe an error just shown.
            captchaInput.SetTextWithoutNotify(string.Empty);
            captchaImage.Clear();

            var captcha = await AuthManager.Instance.GetCaptchaAsync();
            if (this == null) return;

            if (captcha == null || !captchaImage.Show(captcha.Image))
            {
                ShowFieldError(CaptchaErrorText, "Couldn't load captcha. Tap the captcha to retry.");
                return;
            }

            _captchaId = captcha.CaptchaId;
        }

        #endregion

        #region Step 2 — Code

        private async void OnVerifyClicked()
        {
            string code = codeInput.text.Trim();

            if (!CheckField(codeInput, codeErrorText,
                            string.IsNullOrEmpty(code) ? "Please enter the code." : null))
                return;

            SetLoading(true);
            PasswordResetCodeResult result =
                await AuthManager.Instance.VerifyPasswordResetCodeAsync(_email, code);
            if (this == null) return;
            SetLoading(false);

            if (result.Success)
            {
                _resetToken = result.ResetToken;
                newPasswordInput.text = string.Empty;
                confirmPasswordInput.text = string.Empty;
                ShowStep(Step.Password);
                FocusField(newPasswordInput);
                return;
            }

            // V010 incorrect / expired code — and anything else — under the code.
            ShowFieldError(codeErrorText, result.ErrorMessage);
            ShakeInput(codeInput);
        }

        private void OnResendClicked() => GoToEmailStep();

        private void StartResendCooldown()
        {
            StopResendCooldown();
            _resendCoroutine = StartCoroutine(ResendCooldown(RESEND_COOLDOWN_SECS));
        }

        private void StopResendCooldown()
        {
            if (_resendCoroutine != null)
                StopCoroutine(_resendCoroutine);
            _resendCoroutine = null;

            resendButton.interactable = true;
            if (resendText != null) resendText.text = "Resend code";
        }

        private IEnumerator ResendCooldown(int seconds)
        {
            resendButton.interactable = false;

            for (int remaining = seconds; remaining > 0; remaining--)
            {
                if (resendText != null) resendText.text = $"Resend code in {remaining}s";
                yield return new WaitForSeconds(1f);
            }

            _resendCoroutine = null;
            resendButton.interactable = true;
            if (resendText != null) resendText.text = "Resend code";
        }

        #endregion

        #region Step 3 — New Password

        private async void OnResetClicked()
        {
            string password = newPasswordInput.text;

            bool ok = true;
            ok &= CheckField(newPasswordInput, newPasswordErrorText,
                             PasswordRules.Validate(password));
            ok &= CheckField(confirmPasswordInput, confirmPasswordErrorText,
                             PasswordRules.ValidateMatch(password, confirmPasswordInput.text));
            if (!ok) return;

            SetLoading(true);
            AuthResult result = await AuthManager.Instance.ResetPasswordAsync(_resetToken, password);
            if (this == null) return;
            SetLoading(false);

            if (result.Success)
            {
                OnResetDone().Forget();
                return;
            }

            if (result.ErrorCode == "A009")
            {
                // Reset token expired — start over with a new code.
                GoToEmailStep();
                ShowFieldError(emailErrorText, result.ErrorMessage);
                return;
            }

            // V001 weak password — and anything else — under the new password.
            ShowFieldError(newPasswordErrorText, result.ErrorMessage);
            ShakeInput(newPasswordInput);
        }

        // Show the success message in the popup, fill Login in now (so closing
        // early still leaves the email there), then close on its own.
        private async UniTaskVoid OnResetDone()
        {
            _onReset?.Invoke(_email);

            resetButton.interactable = false;
            ShowFieldError(resetStatusText, RESET_DONE_MESSAGE);

            await UniTask.Delay(TimeSpan.FromSeconds(RESET_DONE_CLOSE_SECS));
            if (this == null || !gameObject.activeSelf) return; // closed already

            Close();
        }

        // On leaving either password field: show its error, no shake. Empty
        // fields are skipped so tapping through doesn't flash errors.
        private void OnPasswordsEndEdit()
        {
            string password = newPasswordInput.text;

            CheckOnLeave(newPasswordErrorText, password, PasswordRules.Validate);
            CheckOnLeave(confirmPasswordErrorText, confirmPasswordInput.text,
                         reEntered => PasswordRules.ValidateMatch(password, reEntered));
        }

        private void CheckOnLeave(TextMeshProUGUI errorText, string text, Func<string, string> validate)
        {
            if (string.IsNullOrEmpty(text)) return;

            string error = validate(text);
            if (error == null) ClearFieldError(errorText);
            else               ShowFieldError(errorText, error);
        }

        #endregion

        #region Validation

        private static string ValidateEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return "Please enter your email.";

            if (!Regex.IsMatch(email, EMAIL_PATTERN))
                return "Please enter a valid email address.";

            return null;
        }

        private string ValidateCaptcha(string text)
        {
            if (string.IsNullOrEmpty(_captchaId))
                return "Captcha not loaded. Tap the captcha to retry.";

            if (string.IsNullOrEmpty(text))
                return "Please enter the captcha.";

            return null;
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

        #region Error Display

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
            ClearFieldError(emailErrorText);
            ClearCaptchaError();
            ClearFieldError(codeErrorText);
            ClearFieldError(newPasswordErrorText);
            ClearFieldError(confirmPasswordErrorText);
            ClearFieldError(resetStatusText);
        }

        // No captcha error text assigned — share the email one.
        private TextMeshProUGUI CaptchaErrorText =>
            captchaErrorText != null ? captchaErrorText : emailErrorText;

        // Only a dedicated captcha error text is cleared here — the shared
        // email one belongs to the email field.
        private void ClearCaptchaError()
        {
            if (captchaErrorText != null)
                ClearFieldError(captchaErrorText);
        }

        #endregion

        #region UI Helpers

        // No loading overlay — buttons are disabled while a request runs.
        private void SetLoading(bool isLoading)
        {
            sendCodeButton.interactable = !isLoading;
            verifyButton.interactable   = !isLoading;
            resetButton.interactable    = !isLoading;
            closeButton.interactable    = !isLoading;
        }

        private void ApplyPasswordVisibility(TMP_InputField input, Image icon, bool visible)
        {
            input.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            input.ForceLabelUpdate();

            if (icon != null)
                icon.sprite = visible ? showIcon : hideIcon;
        }

        private static void FocusField(TMP_InputField field)
        {
            field.Select();
            field.ActivateInputField();
        }

        // Shake the field's row (input + error text) when it has one, else the input.
        private void ShakeInput(TMP_InputField input)
        {
            var parent = input.transform.parent as RectTransform;
            bool hasRow = parent != null && parent.GetComponent<LayoutGroup>() == null;
            var target = hasRow ? parent : (RectTransform)input.transform;

            // Finish a running shake first so repeated taps don't walk the field away.
            target.DOComplete();
            target.DOShakeAnchorPos(
                    ERROR_SHAKE_DURATION,
                    ERROR_SHAKE_STRENGTH,
                    ERROR_SHAKE_VIBRATO,
                    randomness: 0f,
                    snapping: false,
                    fadeOut: true)
                // Inside a layout group the captured start position goes stale —
                // hand placement back to the layout.
                .OnComplete(() =>
                {
                    if (target != null && target.parent is RectTransform p)
                        LayoutRebuilder.MarkLayoutForRebuild(p);
                });
        }

        #endregion
    }
}
