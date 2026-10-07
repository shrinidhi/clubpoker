using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        public ProfileView ProfileView;

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
        }

        // Edits the nickname, not the username — rules in NicknameRules.
        private async void ConfirmButtonOnTap()
        {
            if (usernameInput == null)
                return;

            string nickname = usernameInput.text.Trim();

            string error = NicknameRules.Validate(nickname);
            if (error != null)
            {
                InformationPrefabScript.Instance.ShowMessage(error);
                return;
            }

            if (ProfileView != null)
            {
                ProfileView.SetPreviewNickname(nickname);
                await ProfileView.SaveNicknameAsync(nickname);
            }

            CloseButtonOnTap();
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