using System.Text.RegularExpressions;
using ClubPoker.Auth;
using ClubPoker.Core;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    /// <summary>
    /// Bind / change email. Send OTP emails a code and unlocks the code row (faded
    /// until then); Verify binds the email, toasts and closes. Changing the email
    /// after a code was sent starts over, so a typo isn't a dead end.
    /// </summary>
    public class BindEmailPopup : AccountPopup
    {
        private const string EMAIL_PATTERN = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        private const float  LOCKED_ALPHA  = 0.4f;

        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private Button sendOtpButton;

        [Header("Code row — locked until a code is sent")]
        [SerializeField] private CanvasGroup codeGroup;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button verifyButton;

        [SerializeField] private TextMeshProUGUI errorText;

        private string _sentTo;   // email the code went to; null = none sent yet

        protected override void Awake()
        {
            base.Awake();
            sendOtpButton.onClick.AddListener(() => SendCodeAsync().Forget());
            verifyButton.onClick.AddListener(() => VerifyAsync().Forget());
            emailInput.onValueChanged.AddListener(OnEmailChanged);
        }

        protected override void OnShown()
        {
            emailInput.SetTextWithoutNotify(string.Empty);
            codeInput.text = string.Empty;
            SetError(errorText, null);
            SetCodeSent(null);
        }

        private void SetCodeSent(string email)
        {
            _sentTo = email;
            bool sent = email != null;

            sendOtpButton.interactable = !sent;
            codeGroup.alpha = sent ? 1f : LOCKED_ALPHA;
            codeGroup.interactable = sent;
            codeGroup.blocksRaycasts = sent;
        }

        private void OnEmailChanged(string value)
        {
            if (_sentTo == null || Busy || value.Trim() == _sentTo) return;

            codeInput.text = string.Empty;
            SetCodeSent(null);
        }

        private async UniTaskVoid SendCodeAsync()
        {
            if (Busy) return;

            string email = emailInput.text.Trim();
            string problem = string.IsNullOrEmpty(email) ? Loc.Get("enter_email")
                           : !Regex.IsMatch(email, EMAIL_PATTERN) ? Loc.Get("invalid_email")
                           : null;
            SetError(errorText, problem);
            if (problem != null) return;

            Busy = true;
            sendOtpButton.interactable = false;

            BindEmailCodeResult result = await AuthManager.Instance.RequestBindEmailCodeAsync(email);

            if (this == null) return;
            Busy = false;

            if (!result.Success)
            {
                sendOtpButton.interactable = true;
                SetError(errorText, Loc.Error(result.ErrorCode, result.ErrorMessage));
                return;
            }

            SetCodeSent(email);
            ToastEvents.Show(result.ExpiresInMinutes.HasValue
                ? Loc.Get("account.code_sent_valid", ("minutes", result.ExpiresInMinutes.Value))
                : Loc.Get("account.code_sent"));
        }

        private async UniTaskVoid VerifyAsync()
        {
            if (Busy || _sentTo == null) return;

            string code = codeInput.text.Trim();
            SetError(errorText, string.IsNullOrEmpty(code) ? Loc.Get("enter_code_prompt") : null);
            if (string.IsNullOrEmpty(code)) return;

            Busy = true;
            verifyButton.interactable = false;

            AuthResult result = await AuthManager.Instance.ConfirmBindEmailAsync(_sentTo, code);

            if (this == null) return;
            Busy = false;
            verifyButton.interactable = true;

            if (!result.Success)
            {
                SetError(errorText, Loc.Error(result.ErrorCode, result.ErrorMessage));
                return;
            }

            ToastEvents.Show(Loc.Get("account.email_bound"));
            Hide();
        }
    }
}
