using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class ChipsHUDView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("HUD Text")]
        [SerializeField] private TextMeshProUGUI walletChipsCountText;
        [SerializeField] private TextMeshProUGUI lockedInTableChipsCountText;
        [SerializeField] private TextMeshProUGUI availableChipsCountText;


        #endregion

        #region Constants

        private const float POLLING_INTERVAL = 30f;

        #endregion

        #region Private Variables

        private Coroutine pollingCoroutine;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            LoadChips();
            StartPolling();
        }

        private void OnDisable()
        {
            StopPolling();
        }

        #endregion

        #region Load Chips

        private async void LoadChips()
        {
          
            try
            {
                ChipsData data = await AuthManager.Instance.GetChipsAsync();
                SetUI(data);
            }
            catch (Exception e)
            {
                Debug.LogError("[ChipsHUD] Error: " + e.Message);
            }

        }

        #endregion

        #region UI Update

        private void SetUI(ChipsData data)
        {
            if (data == null) return;

            walletChipsCountText.text = data.WalletChips.ToString();
            lockedInTableChipsCountText.text = data.LockedInTables.ToString();
            availableChipsCountText.text = data.AvailableChips.ToString();
        }

        #endregion

        #region Polling
        private void StartPolling()
        {
            pollingCoroutine = StartCoroutine(Polling());
        }

        private void StopPolling()
        {
            if (pollingCoroutine != null)
                StopCoroutine(pollingCoroutine);
        }

        private IEnumerator Polling()
        {
            while (true)
            {
                yield return new WaitForSeconds(POLLING_INTERVAL);
                LoadChips();
            }
        }

        #endregion

        #region Public Refresh (IMPORTANT)

        public void RefreshChips()
        {
            LoadChips();
        }

        #endregion

    }
}