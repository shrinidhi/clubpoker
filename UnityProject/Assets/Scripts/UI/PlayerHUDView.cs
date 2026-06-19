using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class PlayerHUDView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Profile")]
        [SerializeField] private Image           avatarImage;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private GameObject      guestBadge;

        [Header("Chips")]
        [SerializeField] private TextMeshProUGUI walletChipsText;
        [SerializeField] private TextMeshProUGUI lockedChipsText;
        [SerializeField] private TextMeshProUGUI availableChipsText;

        [SerializeField] private Button ProfileButton;
        public AvtarSO AvtarSO;
        #endregion

        #region Constants

        private const float ChipsPollIntervalSeconds = 30f;

        #endregion

        #region Private Fields

        private CancellationTokenSource _pollCts;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            ProfileButton.onClick.AddListener(ProfileButtonOnTap);
        }

        private void OnEnable()
        {
            RefreshProfile();
            RefreshChips();
            StartChipsPolling();
        }

        private void OnDisable()
        {
            StopChipsPolling();
        }

        #endregion

        #region Public API


        void ProfileButtonOnTap()
        {
            GameSceneManager.Instance.LoadScene("Scene_Profile");
        }

        // Call after returning from Scene_Profile to sync username and avatar.
        public void RefreshProfile()
        {
            if (AuthManager.Instance == null) return;
            var session = AuthManager.Instance.Session;
            if (session == null) return;

            usernameText.text = session.Username ?? "Player";
            SetAvatarImage(session.Avatar);
            guestBadge.SetActive(session.IsGuest);

            // Wire when AvatarLoader is ready:
            // AvatarLoader.Instance.Load(session.Avatar, avatarImage);
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

        // Call after any transaction or table join to sync chip counts.
        public void RefreshChips()
        {
            FetchAndDisplayChipsAsync().Forget();
        }

        #endregion

        #region Chips

        private async UniTaskVoid FetchAndDisplayChipsAsync()
        {
            try
            {
                var data = await AuthManager.Instance.GetChipsAsync()
                    .AttachExternalCancellation(destroyCancellationToken);

                DisplayChips(data);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerHUDView] Chips fetch failed: {e.Message}");
            }
        }

        private void DisplayChips(ChipsData data)
        {
            if (data == null) return;

            walletChipsText.text    = FormatChipCount(data.WalletChips);
            lockedChipsText.text    = FormatChipCount(data.LockedInTables);
            availableChipsText.text = FormatChipCount(data.AvailableChips);
        }

        #endregion

        #region Chips Polling

        private void StartChipsPolling()
        {
            _pollCts = new CancellationTokenSource();
            RunChipsPollLoopAsync(_pollCts.Token).Forget();
        }

        private void StopChipsPolling()
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _pollCts = null;
        }

        private async UniTaskVoid RunChipsPollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                bool cancelled = await UniTask
                    .Delay(TimeSpan.FromSeconds(ChipsPollIntervalSeconds), cancellationToken: ct)
                    .SuppressCancellationThrow();

                if (cancelled) break;

                FetchAndDisplayChipsAsync().Forget();
            }
        }

        #endregion

        #region Helpers

        private static string FormatChipCount(long chips)
        {
            if (chips >= 1_000_000) return $"{chips / 1_000_000f:0.#}M";
            if (chips >= 1_000)     return $"{chips / 1_000f:0.#}K";
            return chips.ToString();
        }

        #endregion
    }
}
