using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using ClubPoker.Auth;
namespace ClubPoker.UI
{
    public class DailyBonusView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI bonusText;
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private GameObject banner;

        private const string BONUS_TIME_KEY = "DailyBonusTime";

        private void Start()
        {
            claimButton.onClick.AddListener(OnClaimClicked);
            RestoreCountdown();
        }

        private async void OnClaimClicked()
        {
            claimButton.interactable = false;

            var result = await AuthManager.Instance.ClaimDailyBonusAsync();

            claimButton.interactable = true;

            if (result.Success)
            {
                ShowBonusAnimation(result.ChipsGranted);
                //UpdateBalance(result.NewBalance);

                banner.SetActive(false);
            }
            else if (result.ErrorCode == "E001")
            {
                StartCountdown(result.NextBonusTime);
            }
            else
            {
                Debug.LogError(result.ErrorMessage);
            }
        }

    
        private void ShowBonusAnimation(int chips)
        {
            bonusText.text = "+" + chips + " Chips!";
            bonusText.gameObject.SetActive(true);

            bonusText.transform.localScale = Vector3.zero;
            bonusText.transform
                .DOScale(1f, 0.5f)
                .SetEase(Ease.OutBack);

            bonusText.DOFade(0, 1f).SetDelay(1f);
        }

 
        private void UpdateBalance(int balance)
        {
        //    balanceText.text = balance.ToString();
        }

       
        private void StartCountdown(DateTime nextTime)
        {
            PlayerPrefs.SetString(BONUS_TIME_KEY, nextTime.ToString());
            PlayerPrefs.Save();

            banner.SetActive(true);
            StartCoroutine(CountdownRoutine(nextTime));
        }

      
        private IEnumerator CountdownRoutine(DateTime nextTime)
        {
            while (true)
            {
                TimeSpan remaining = nextTime - DateTime.UtcNow;

                if (remaining.TotalSeconds <= 0)
                {
                    timerText.text = "Claim Now!";
                    claimButton.interactable = true;
                    yield break;
                }

                timerText.text =
                    $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";

                claimButton.interactable = false;

                yield return new WaitForSeconds(1);
            }
        }

       
        private void RestoreCountdown()
        {
            if (!PlayerPrefs.HasKey(BONUS_TIME_KEY))
                return;

            DateTime savedTime = DateTime.Parse(PlayerPrefs.GetString(BONUS_TIME_KEY));

            if (savedTime > DateTime.UtcNow)
            {
                StartCountdown(savedTime);
            }
            else
            {
                banner.SetActive(true);
                claimButton.interactable = true;
            }
        }
    }
}
