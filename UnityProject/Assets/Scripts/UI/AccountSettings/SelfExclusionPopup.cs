using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    /// <summary>
    /// Self-exclusion = delete the account for good. Confirm stays faded and dead
    /// until the player types their own User ID exactly; confirming deletes the
    /// account, shows a success toast, closes the popup, wipes what is saved on
    /// this device and lands on Login.
    ///
    /// An account the server hasn't given a User ID yet shows only its name, and
    /// confirms by typing that name instead, so it can still be deleted.
    /// </summary>
    public class SelfExclusionPopup : AccountPopup
    {
        private const float LOCKED_ALPHA = 0.5f;

        [Header("Account being deleted")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private AvtarSO avatars;
        [SerializeField] private TextMeshProUGUI userNameText;
        [SerializeField] private TextMeshProUGUI userIdText;
        [SerializeField] private TextMeshProUGUI diamondsText;

        [Header("Confirmation")]
        [Tooltip("The 'Enter User ID' label — reads 'Enter Name' for an account without a User ID.")]
        [SerializeField] private LocalizedText userIdLabel;
        [SerializeField] private TMP_InputField userIdInput;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private CanvasGroup confirmGroup;

        private int _designLimit;   // the field's character limit as set in the prefab
        private string _expected;   // what the player has to type: User ID, else the name shown

        protected override void Awake()
        {
            base.Awake();
            _designLimit = userIdInput.characterLimit;
            cancelButton.onClick.AddListener(Hide);
            confirmButton.onClick.AddListener(() => ConfirmAsync().Forget());
            userIdInput.onValueChanged.AddListener(_ => RefreshConfirm());
        }

        protected override void OnShown()
        {
            UserSession session = AuthManager.Instance.Session;

            if (diamondsText != null) diamondsText.text = "-";
            SetAvatar(session.Avatar);
            LoadDiamondsAsync().Forget();

            userIdInput.text = string.Empty;
            cancelButton.interactable = true;
            ApplyIdentity();

            // User ID and nickname come with the full profile, which may not be
            // loaded yet.
            if (!session.HasFullProfile || string.IsNullOrEmpty(session.PlayerCode))
                LoadProfileAsync().Forget();
        }

        private void ApplyIdentity()
        {
            UserSession session = AuthManager.Instance.Session;
            bool hasId = !string.IsNullOrEmpty(session.PlayerCode);

            // Nickname once set, username until then — the name the main screen shows.
            string displayName = session.DisplayName;
            if (userNameText != null) userNameText.text = displayName;

            _expected = (hasId ? session.PlayerCode : displayName)?.Trim();

            // No ID: the ID line is hidden and the name above is what gets typed.
            if (userIdText != null)
            {
                userIdText.gameObject.SetActive(hasId);
                userIdText.text = hasId ? session.PlayerCode : string.Empty;
            }

            // The field is digits-only in the prefab, which suits a numeric ID but
            // would block typing a name.
            bool digitsOnly = hasId && long.TryParse(_expected, out _);
            var contentType = digitsOnly
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.Standard;

            if (userIdInput.contentType != contentType)
            {
                userIdInput.contentType = contentType;
                userIdInput.ForceLabelUpdate();
            }

            // Same for the prefab's character limit (0 = none): never shorter than
            // what has to be typed.
            int needed = _expected?.Length ?? 0;
            userIdInput.characterLimit = _designLimit > 0 && _designLimit < needed ? needed : _designLimit;

            if (userIdLabel != null) userIdLabel.Key = hasId ? "account.enter_user_id" : "account.enter_name";

            RefreshConfirm();
        }

        private async UniTaskVoid LoadProfileAsync()
        {
            await AuthManager.Instance.GetPlayerProfileAsync();   // fills the session

            if (this != null && gameObject.activeSelf && !Busy)
                ApplyIdentity();
        }

        private void SetAvatar(string avatarName)
        {
            if (avatarImage == null || avatars == null) return;

            AvtarData match = avatars.AvtarBadges.Find(a => a.AvtarName == avatarName);
            if (match != null) avatarImage.sprite = match.AvtarImage;
        }

        private async UniTaskVoid LoadDiamondsAsync()
        {
            DiamondData diamonds = await AuthManager.Instance.GetDiamondsAsync();

            if (this != null && diamonds != null && diamondsText != null)
                diamondsText.text = diamonds.Balance.ToString("N0");
        }

        private bool IdMatches() =>
            !string.IsNullOrEmpty(_expected) && userIdInput.text.Trim() == _expected;

        private void RefreshConfirm()
        {
            bool ready = !Busy && IdMatches();

            confirmButton.interactable = ready;
            if (confirmGroup != null) confirmGroup.alpha = ready ? 1f : LOCKED_ALPHA;
        }

        private async UniTaskVoid ConfirmAsync()
        {
            if (Busy || !IdMatches()) return;

            Busy = true;
            cancelButton.interactable = false;
            RefreshConfirm();

            AuthResult result = await AuthManager.Instance.SelfExcludeAsync();

            if (result.Success)
            {
                SucceedAndExitAsync(Loc.Get("msg.account_deleted"),
                    AuthManager.Instance.CompleteSelfExclusion).Forget();
                return;
            }

            if (this == null) return;

            Busy = false;
            cancelButton.interactable = true;
            RefreshConfirm();
            ToastEvents.Show(Loc.Error(result.ErrorCode, result.ErrorMessage));
        }
    }
}
