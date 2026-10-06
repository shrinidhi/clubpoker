using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;

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

        private async void ConfirmButtonOnTap()
        {
            if (usernameInput == null)
                return;

            string username = usernameInput.text.Trim();

            if (string.IsNullOrEmpty(username))
                return;

            if (ProfileView != null)
                ProfileView.SetPreviewUserName(username);

            await UpdateProfileFromEdit(username);

            CloseButtonOnTap();
        }

        private void CloseButtonOnTap()
        {
            if (ProfileEditScreen != null)
                ProfileEditScreen.SetActive(false);
            else
                gameObject.SetActive(false);
        }


        public async UniTask UpdateProfileFromEdit(string username)
        {
            try
            {

                UpdateProfileData result =await AuthManager.Instance.UpdatePlayerProfileAsync(username, ProfileView.currentAvatar);


                if (result == null)
                    return;

                ProfileView.currentUserName = result.Username;
                ProfileView.selectedAvatar = result.Avatar;

                ProfileView.AvtarnameText.text = ProfileView.currentUserName;

                InformationPrefabScript.Instance.ShowMessage(
                    "Profile updated successfully."
                );
            }
            catch (System.Exception ex)
            {
                

                InformationPrefabScript.Instance.ShowMessage(
                    ex.Message
                );
            }
        }

    }
}