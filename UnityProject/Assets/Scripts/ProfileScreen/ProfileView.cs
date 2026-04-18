using System;
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
        #region Serialized Fields

        [Header("Profile UI")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI AvtarnameText;

        [SerializeField] private TextMeshProUGUI WalletchipsCountText;
        [SerializeField] private TextMeshProUGUI TotalWinningCountText;
        [SerializeField] private TextMeshProUGUI GamePlayedCountText;
        [SerializeField] private TextMeshProUGUI WinningRateCountText;
        [SerializeField] private TextMeshProUGUI VariantNameText;

        [Header("Buttons")]
        [SerializeField] private Button BackButton;
        [SerializeField] private Button EditButton;


        [Header("Screen")]
        [SerializeField] private GameObject ProfileEditScrreen;
       
        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            LoadProfile();
        }

        #endregion

        #region Setup

        private void BindButtons()
        {
            BackButton.onClick.AddListener(OnBackButtonClicked);
            EditButton.onClick.AddListener(OnEditClicked);
        }

        #endregion

        #region Button Handlers

        private void OnBackButtonClicked()
        {
            GameSceneManager.Instance.LoadScene("Scene_Lobby");
        }

        private void OnEditClicked()
        {
            ProfileEditScrreen.SetActive(true);
            Debug.Log("Open Edit Profile Screen");
        }

        #endregion

        #region Profile Load

        private async void LoadProfile()
        {
            SetLoading(true);

            try
            {
                PlayerData profile = await AuthManager.Instance.GetProfileAsync();
                SetProfileUI(profile);
            }
            catch (Exception e)
            {
                Debug.LogError("[ProfileView] Error: " + e.Message);
            }

            SetLoading(false);
        }

        private void SetProfileUI(PlayerData profile)
        {
            AvtarnameText.text = profile.Username;
            WalletchipsCountText.text = profile.WalletChips.ToString();
            TotalWinningCountText.text = profile.TotalWinnings.ToString();
            GamePlayedCountText.text = profile.GamesPlayed.ToString();
            int winRate = 0;
            if (profile.GamesPlayed > 0)
                winRate = (profile.GamesWon * 100) / profile.GamesPlayed;

            WinningRateCountText.text = winRate + "%";
            VariantNameText.text = profile.Role;

            LoadAvatar(profile.Avatar);
        }

        #endregion

        #region Avatar Load

        private async void LoadAvatar(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                Texture2D texture = await DownloadTexture(url);
                if (texture != null)
                {
                    avatarImage.sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                }
            }
            catch
            {
                Debug.Log("Avatar load failed");
            }
        }

        private async UniTask<Texture2D> DownloadTexture(string url)
        {
            using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                await request.SendWebRequest();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    return UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                }
            }
            return null;
        }

        #endregion

        #region UI Helpers

        private void SetLoading(bool isLoading)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(isLoading);
        }

        #endregion
    }
}