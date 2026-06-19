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

        private string currentUserName = "";
        private string selectedAvatar = "";
        public ProfileEditView editView;

        private void Start()
        {
            if (BackButton != null)
                BackButton.onClick.AddListener(OnBackButtonClicked);

            if (EditButton != null)
                EditButton.onClick.AddListener(OnEditClicked);

            if (SaveButton != null)
                SaveButton.onClick.AddListener(SaveButtonOnTap);
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
            selectedAvatar = profile.Avatar;

            if (AvtarnameText != null)
                AvtarnameText.text = currentUserName;

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
        public void SetPreviewUserName(string username)
        {
            currentUserName = username;

            if (AvtarnameText != null)
                AvtarnameText.text = currentUserName;
        }

        private async void SaveButtonOnTap()
        {
            await UpdateProfileFromEdit(currentUserName);
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
            editView.SetData(currentUserName);

        }

        public async UniTask UpdateProfileFromEdit(string username)
        {
            try
            {
                SetLoading(true);

                UpdateProfileData result =
                    await AuthManager.Instance.UpdatePlayerProfileAsync(
                        username,
                        selectedAvatar);

                SetLoading(false);

                if (result == null)
                    return;

                currentUserName = result.Username;
                selectedAvatar = result.Avatar;

                AvtarnameText.text = currentUserName;

                InformationPrefabScript.Instance.ShowMessage(
                    "Profile updated successfully."
                );
            }
            catch (System.Exception ex)
            {
                SetLoading(false);

                InformationPrefabScript.Instance.ShowMessage(
                    ex.Message
                );
            }
        }

        private void SetLoading(bool isLoading)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(isLoading);
        }
    }
}