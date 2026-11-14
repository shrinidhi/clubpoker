using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    /// <summary>
    /// Account setting, opened from the side menu on the home and lobby screens:
    /// a list of three options, each opening its own popup. Only one of the list
    /// and the popups shows at a time; closing a popup returns to the list.
    /// </summary>
    public class AccountSettingsPanel : MonoBehaviour
    {
        [Header("Option list")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button bindEmailButton;
        [SerializeField] private Button changePasswordButton;
        [SerializeField] private Button selfExclusionButton;

        [Header("Popups")]
        [SerializeField] private BindEmailPopup bindEmailPopup;
        [SerializeField] private ChangePasswordPopup changePasswordPopup;
        [SerializeField] private SelfExclusionPopup selfExclusionPopup;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            Bind(bindEmailButton, bindEmailPopup);
            Bind(changePasswordButton, changePasswordPopup);
            Bind(selfExclusionButton, selfExclusionPopup);
        }

        private void Bind(Button button, AccountPopup popup)
        {
            if (button == null || popup == null) return;

            button.onClick.AddListener(() => Open(popup));
            popup.Closed += ShowOptions;
        }

        public void Show()
        {
            transform.SetAsLastSibling();   // above whatever opened it
            gameObject.SetActive(true);

            // A popup left open last time closes without bringing the list back twice.
            foreach (AccountPopup popup in new AccountPopup[] { bindEmailPopup, changePasswordPopup, selfExclusionPopup })
                if (popup != null) popup.gameObject.SetActive(false);

            ShowOptions();
        }

        public void Hide() => gameObject.SetActive(false);

        private void ShowOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }

        private void Open(AccountPopup popup)
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
            popup.Show();
        }
    }
}
