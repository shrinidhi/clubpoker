using ClubPoker.Auth;
using ClubPoker.Core;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    /// <summary>
    /// Change password: current + new + confirm, each with its own show/hide eye.
    /// The new and confirm fields are checked as the player types.
    /// The new password follows the same rules as register (PasswordRules). A
    /// change shows a success toast, closes the popup and logs the player out to
    /// Login, where they sign in with the new password.
    /// </summary>
    public class ChangePasswordPopup : AccountPopup
    {
        [System.Serializable]
        private class Field
        {
            public TMP_InputField input;
            public Button showHideButton;
            public Image showHideIcon;
            public TextMeshProUGUI errorText;

            [System.NonSerialized] public bool Visible;
        }

        [SerializeField] private Field current;
        [SerializeField] private Field newPassword;
        [SerializeField] private Field confirm;

        [Tooltip("Shown under the fields for server errors.")]
        [SerializeField] private TextMeshProUGUI generalErrorText;
        [SerializeField] private Button confirmButton;

        [Header("Eye icons")]
        [SerializeField] private Sprite showIcon;
        [SerializeField] private Sprite hideIcon;

        private Field[] Fields => new[] { current, newPassword, confirm };

        protected override void Awake()
        {
            base.Awake();
            confirmButton.onClick.AddListener(() => SubmitAsync().Forget());

            foreach (Field field in Fields)
            {
                Field f = field;
                if (f.showHideButton != null)
                    f.showHideButton.onClick.AddListener(() => SetVisible(f, !f.Visible));
            }

            current.input.onValueChanged.AddListener(_ => ValidateLive());
            newPassword.input.onValueChanged.AddListener(_ => ValidateLive());
            confirm.input.onValueChanged.AddListener(_ => ValidateLive());
        }

        /// <summary>
        /// Checks while typing. A field still empty shows nothing — "please enter"
        /// messages wait for the Confirm button.
        /// </summary>
        private void ValidateLive()
        {
            SetError(current.errorText, null);
            SetError(generalErrorText, null);
            SetError(newPassword.errorText,
                string.IsNullOrEmpty(newPassword.input.text) ? null : NewPasswordProblem());
            SetError(confirm.errorText,
                string.IsNullOrEmpty(confirm.input.text) ? null : ConfirmProblem());
        }

        private string NewPasswordProblem()
        {
            string value = newPassword.input.text;
            string problem = PasswordRules.Validate(value);

            if (problem == null && value == current.input.text)
                problem = Loc.Get("account.new_password_same");

            return problem;
        }

        private string ConfirmProblem() =>
            PasswordRules.ValidateMatch(newPassword.input.text, confirm.input.text);

        protected override void OnShown()
        {
            foreach (Field field in Fields)
            {
                field.input.text = string.Empty;
                SetVisible(field, false);
                SetError(field.errorText, null);
            }

            SetError(generalErrorText, null);
            confirmButton.interactable = true;
        }

        private void SetVisible(Field field, bool visible)
        {
            field.Visible = visible;
            field.input.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            field.input.ForceLabelUpdate();

            if (field.showHideIcon == null) return;

            Sprite sprite = visible ? showIcon : hideIcon;
            if (sprite != null) field.showHideIcon.sprite = sprite;
        }

        private async UniTaskVoid SubmitAsync()
        {
            if (Busy) return;

            // Passwords aren't trimmed — a space can be part of one.
            string currentValue = current.input.text;
            string newValue = newPassword.input.text;

            string currentProblem = string.IsNullOrEmpty(currentValue)
                ? Loc.Get("account.enter_current_password")
                : null;

            string newProblem = NewPasswordProblem();
            string confirmProblem = ConfirmProblem();

            SetError(current.errorText, currentProblem);
            SetError(newPassword.errorText, newProblem);
            SetError(confirm.errorText, confirmProblem);
            SetError(generalErrorText, null);

            if (currentProblem != null || newProblem != null || confirmProblem != null)
                return;

            Busy = true;
            confirmButton.interactable = false;

            AuthResult result = await AuthManager.Instance.ChangePasswordAsync(currentValue, newValue);

            if (result.Success)
            {
                // Same logout as the server's own "password changed" notice;
                // whichever comes second is ignored.
                SucceedAndExitAsync(Loc.Get("account.password_changed"),
                    () => AuthManager.Instance.HandleSessionRevoked("password_changed")).Forget();
                return;
            }

            if (this == null) return;

            Busy = false;
            confirmButton.interactable = true;
            SetError(generalErrorText, Loc.Error(result.ErrorCode, result.ErrorMessage));
        }
    }
}
