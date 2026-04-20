using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class ProfileEditView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput;

        [Header("Avatar")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private Transform avatarContent;
        [SerializeField] private GameObject avatarPrefab;

        [Header("Error Display")]
        [SerializeField] private TextMeshProUGUI usernameErrorText;

        [Header("Buttons")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;


        [SerializeField] private GameObject ProfileEditScreen;

        public ProfileView ProfileView;

        #endregion

        #region Constants

        private const float SHAKE_DURATION = 0.4f;
        private const float SHAKE_STRENGTH = 12f;
        private const int SHAKE_VIBRATO = 20;

        #endregion

        #region Private

        private string selectedAvatarKey = "";
        private List<AvtarprefabScript> avatarItems = new();

        #endregion

        #region Unity

        private void Start()
        {
            BindButtons();
        }

        private async void OnEnable()
        {
            ResetView();
            SetLoading(true);

            await LoadProfileAsync();
            await LoadAvatarsAsync();

            SetLoading(false);
        }

        #endregion

        #region Setup

        private void BindButtons()
        {
            saveButton.onClick.AddListener(OnSaveClicked);
            closeButton.onClick.AddListener(() => ProfileEditScreen.SetActive(false));

            usernameInput.onValueChanged.AddListener(_ => ClearError());
        }

        private void ResetView()
        {
            ClearError();
        }

        #endregion

        #region Load Profile

        private async UniTask LoadProfileAsync()
        {
            try
            {
                var profile = await AuthManager.Instance.GetProfileAsync();

                usernameInput.text = profile.Username;
                selectedAvatarKey = profile.Avatar;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProfileEdit] Profile Load Error: " + e.Message);
            }
        }

        #endregion

        #region Load Avatars

        private async UniTask LoadAvatarsAsync()
        {
            try
            {
                var avatars = await AuthManager.Instance.GetAvatarsAsync();

                foreach (Transform child in avatarContent)
                    Destroy(child.gameObject);

                avatarItems.Clear();

                foreach (var avatar in avatars)
                {
                    GameObject obj = Instantiate(avatarPrefab, avatarContent);

                    var item = obj.GetComponent<AvtarprefabScript>();
                    item.Setup(avatar, OnAvatarSelected);

                    avatarItems.Add(item);

                    // highlight selected
                    if (avatar.Key == selectedAvatarKey)
                        item.SetSelected(true);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ProfileEdit] Avatar Load Error: " + e.Message);
            }
        }

        private void OnAvatarSelected(AvatarData avatar)
        {
            if (!avatar.Unlocked)
            {
                Debug.Log("Locked Avatar");
                return;
            }

            selectedAvatarKey = avatar.Key;

            foreach (var item in avatarItems)
                item.SetSelected(false);

            avatarItems.Find(x => x.Data.Key == avatar.Key)?.SetSelected(true);

            Debug.Log("Selected Avatar: " + avatar.Key);
        }

        #endregion

        #region Save

        private async void OnSaveClicked()
        {
            if (!Validate()) return;

            SetLoading(true);

            var result = await AuthManager.Instance.UpdateProfileAsync(
                usernameInput.text.Trim(),
                selectedAvatarKey
            );

            SetLoading(false);

            if (result.Success)
            {
                OnSaveSuccess();
            }
            else
            {
                HandleError(result.ErrorMessage);
            }
        }

        private void OnSaveSuccess()
        {
            Debug.Log("Profile Updated");
            ProfileView.AvtarnameText.text = AuthManager.Instance.Session.Username;
            transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
            ProfileEditScreen.SetActive(false);
        }

        #endregion

        #region Validation

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(usernameInput.text))
            {
                Shake(usernameInput.GetComponent<RectTransform>());
                return false;
            }

          /*  if (string.IsNullOrEmpty(selectedAvatarKey))
            {
                Debug.Log("Select Avatar");
                return false;
            }*/

            return true;
        }

        #endregion

        #region Error

        private void HandleError(string msg)
        {
            usernameErrorText.text = msg;
            usernameErrorText.gameObject.SetActive(true);

            Shake(usernameInput.GetComponent<RectTransform>());
        }

        private void ClearError()
        {
            usernameErrorText.text = "";
            usernameErrorText.gameObject.SetActive(false);
        }

        #endregion

        #region UI

        private void SetLoading(bool value)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(value);

            saveButton.interactable = !value;
        }

        private void Shake(RectTransform rect)
        {
            rect.DOShakeAnchorPos(SHAKE_DURATION, SHAKE_STRENGTH, SHAKE_VIBRATO);
        }

        #endregion
    }
}