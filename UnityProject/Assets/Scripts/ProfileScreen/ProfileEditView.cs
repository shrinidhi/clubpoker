using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class ProfileEditView : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput;

        [Header("Buttons")]
        [SerializeField] private Button ConfirmButton;
        [SerializeField] private Button closeButton;

        [SerializeField] private GameObject ProfileEditScreen;

        [SerializeField] private TextMeshProUGUI costText;

        public ProfileView ProfileView;

        private string _warnedNickname;

        private void Start()
        {
            if (ConfirmButton != null)
                ConfirmButton.onClick.AddListener(ConfirmButtonOnTap);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseButtonOnTap);
        }

        public void SetData(string username)
        {
            if (usernameInput != null)
                usernameInput.text = username;

            _warnedNickname = null;
            RefreshCostText();
        }

        private int Cost => ProfileView != null ? ProfileView.NicknameChangeCost : -1;

        public void RefreshCostText()
        {
            if (costText == null)
                return;

            costText.text = Cost > 0
                ? Loc.Get("nickname_next_change_cost", ("cost", Cost))
                : Cost == 0 ? Loc.Get("nickname_first_change_free") : string.Empty;
        }

        // Edits the nickname, not the username — rules in NicknameRules.
        private async void ConfirmButtonOnTap()
        {
            if (usernameInput == null || ProfileView == null)
                return;

            string nickname = usernameInput.text.Trim();

            string error = NicknameRules.Validate(nickname);
            if (error != null)
            {
                InformationPrefabScript.Instance.ShowMessage(error);
                return;
            }

            if (nickname == ProfileView.SavedNickname)
            {
                CloseButtonOnTap();
                return;
            }

            if (Cost < 0)
            {
                await ProfileView.LoadProfile();
                RefreshCostText();
            }

            if (Cost > 0)
            {
                if (ProfileView.DiamondBalance < Cost)
                {
                    InformationPrefabScript.Instance.ShowMessage(
                        Loc.Get("nickname_not_enough_diamonds", ("cost", Cost)));
                    return;
                }

                if (_warnedNickname != nickname)
                {
                    _warnedNickname = nickname;
                    InformationPrefabScript.Instance.ShowMessage(
                        Loc.Get("nickname_change_cost_confirm", ("cost", Cost)) + " " +
                        Loc.Get("nickname_tap_confirm_again"));
                    return;
                }
            }

            _warnedNickname = null;

            if (await ProfileView.SaveNicknameAsync(nickname))
                CloseButtonOnTap();
            else
                RefreshCostText();
        }

        private void CloseButtonOnTap()
        {
            if (ProfileEditScreen != null)
                ProfileEditScreen.SetActive(false);
            else
                gameObject.SetActive(false);
        }

    }
}
