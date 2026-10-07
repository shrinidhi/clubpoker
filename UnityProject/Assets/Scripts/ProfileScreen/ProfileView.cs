using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class ProfileView : MonoBehaviour
    {
        [Header("Profile UI")]
        public Image avatarImage;
        public TextMeshProUGUI AvtarnameText;

        [Header("Buttons")]
        [SerializeField] private Button BackButton;
        [SerializeField] private Button EditButton;
        [SerializeField] private Button SaveButton;
        [Header("Screen")]
        [SerializeField] private GameObject ProfileEditScrreen;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;

        public Transform AvtarImageGrid;
        public GameObject AvtarPrefab;
        public AvtarSO AvtarSO;

        private readonly List<AvtarprefabScript> avatarItems = new List<AvtarprefabScript>();

        public string currentUserName = "";
        public string currentNickname = "";
        public string selectedAvatar = "";
        public string currentAvatar = "";
        public ProfileEditView editView;

        private void Start()
        {
            if (BackButton != null)
                BackButton.onClick.AddListener(OnBackButtonClicked);

            if (EditButton != null)
                EditButton.onClick.AddListener(OnEditClicked);

            if (SaveButton != null)
                SaveButton.onClick.AddListener(SaveButtonOnTap);
            var session = AuthManager.Instance.Session;
            AvtarnameText.text = session.DisplayName;
        }

        private async void OnEnable()
        {
            GenerateAvatarGrid();
            await LoadProfile();
        }

        public async UniTask LoadProfile()
        {
            SetLoading(true);

            PlayerFullProfileData profile =
                await AuthManager.Instance.GetPlayerProfileAsync();

            SetLoading(false);

            if (profile == null)
            {
                Debug.Log("It's Null");
                return;
            }
            else
            {
                Debug.Log("Name : " + profile.Username);
            }
                

            currentUserName = profile.Username;
            currentNickname = profile.Nickname;
            selectedAvatar = profile.Avatar;
            currentAvatar = profile.Avatar;
            RefreshNameText();

            SetAvatarImage(selectedAvatar);
            RefreshAvatarSelection();
        }

        private void GenerateAvatarGrid()
        {
            avatarItems.Clear();

            if (AvtarImageGrid == null || AvtarPrefab == null || AvtarSO == null)
                return;

            for (int i = AvtarImageGrid.childCount - 1; i >= 0; i--)
            {
                Destroy(AvtarImageGrid.GetChild(i).gameObject);
            }

            foreach (AvtarData data in AvtarSO.AvtarBadges)
            {
                GameObject obj = Instantiate(AvtarPrefab, AvtarImageGrid);
                AvtarprefabScript item = obj.GetComponent<AvtarprefabScript>();

                if (item == null)
                    continue;

                item.Setup(data, OnAvatarSelected);
                avatarItems.Add(item);
            }
        }

        private void OnAvatarSelected(string avatarName)
        {
            selectedAvatar = avatarName;

            SetAvatarImage(selectedAvatar);
            RefreshAvatarSelection();
        }

        private void RefreshAvatarSelection()
        {
            foreach (AvtarprefabScript item in avatarItems)
            {
                item.SetSelected(item.GetAvatarName() == selectedAvatar);
            }
        }

        private void SetAvatarImage(string avatarName)
        {
            if (avatarImage == null || AvtarSO == null)
                return;

            foreach (AvtarData data in AvtarSO.AvtarBadges)
            {
                if (data.AvtarName == avatarName)
                {
                    avatarImage.sprite = data.AvtarImage;
                    return;
                }
            }
        }
        /// <summary>The profile shows the nickname (spaces allowed); username is the
        /// login handle and only stands in until a nickname is set.</summary>
        public string DisplayName =>
            string.IsNullOrEmpty(currentNickname) ? currentUserName : currentNickname;

        private void RefreshNameText()
        {
            if (AvtarnameText != null)
                AvtarnameText.text = DisplayName;
        }

        public void SetPreviewNickname(string nickname)
        {
            currentNickname = nickname;
            RefreshNameText();
        }

        // Save button commits the avatar picked in the grid; the nickname is
        // committed by the edit popup, so it isn't resent here.
        private async void SaveButtonOnTap()
        {
            await SaveAvatarAsync(selectedAvatar);
        }
        private void OnBackButtonClicked()
        {
            GameSceneManager.Instance.LoadScene("Scene_MainMenu");
        }

        private void OnEditClicked()
        {
            if (ProfileEditScrreen == null)
                return;

            ProfileEditScrreen.SetActive(true);
            editView.SetData(DisplayName);

        }

        /// <summary>Save the avatar picked in the grid — avatar only, and only if it
        /// actually changed.</summary>
        private async UniTask SaveAvatarAsync(string avatar)
        {
            if (string.IsNullOrEmpty(avatar) || avatar == currentAvatar)
                return;

            try
            {
                SetLoading(true);

                UpdateProfileData result =
                    await AuthManager.Instance.UpdateAvatarAsync(avatar);

                SetLoading(false);

                if (result == null)
                    return;

                selectedAvatar = result.Avatar;
                currentAvatar = result.Avatar;

                RefreshNameText();

                InformationPrefabScript.Instance.ShowMessage("Profile updated successfully.");
            }
            catch (System.Exception ex)
            {
                SetLoading(false);
                InformationPrefabScript.Instance.ShowMessage(ex.Message);
            }
        }

        /// <summary>Save the nickname (edit popup). Shows what the server stored.</summary>
        public async UniTask SaveNicknameAsync(string nickname)
        {
            try
            {
                SetLoading(true);

                UpdateProfileData result =
                    await AuthManager.Instance.UpdateNicknameAsync(nickname);

                SetLoading(false);

                if (result == null)
                    return;

                currentNickname = result.Nickname;
                RefreshNameText();

                InformationPrefabScript.Instance.ShowMessage("Nickname updated successfully.");
            }
            catch (System.Exception ex)
            {
                SetLoading(false);
                InformationPrefabScript.Instance.ShowMessage(ex.Message);
            }
        }

        private void SetLoading(bool isLoading)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(isLoading);
        }
    }
}