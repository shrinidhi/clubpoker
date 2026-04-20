using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using DG.Tweening;
using ClubPoker.Auth;
using System.Collections.Generic;

[Serializable]
public class DailyReward
{
    public int Day;
    public int Coins;
}
namespace ClubPoker.UI
{
    public class DailyBonusView : MonoBehaviour
    {
        public Button CollectBtn;
        public Button CloseBtn;

        public TextMeshProUGUI TimerText;
        public TextMeshProUGUI RewardText;

        private DateTime nextTime;
        private bool isRunning;
        public Transform Days_Content;
        public GameObject Days_Prefab;
        private int currentDay = 1;
        private List<DailyReward> rewards = new List<DailyReward>()
        {
            new DailyReward{ Day = 1, Coins = 100 },
            new DailyReward{ Day = 2, Coins = 200 },
            new DailyReward{ Day = 3, Coins = 300 },
            new DailyReward{ Day = 4, Coins = 400 },
            new DailyReward{ Day = 5, Coins = 500 },
            new DailyReward{ Day = 6, Coins = 700 },
            new DailyReward{ Day = 7, Coins = 1000 },
        };

        void Start()
        {
            CollectBtn.onClick.AddListener(OnCollect);
            CloseBtn.onClick.AddListener(() => gameObject.SetActive(false));

            InitFromServerData();
        }


        void InitFromServerData()
        {
            var last = AuthManager.Instance.Session.LastDailyBonus;

            if (last == null)
            {

                TimerText.text = "Collect Now!";
                SetUI(true);
                return;
            }

            nextTime = last.Value.AddDays(1);

            if (DateTime.UtcNow >= nextTime)
            {
                TimerText.text = "Collect Now!";
                SetUI(true);
            }
            else
            {
                SetUI(false);
                StartTimer();
            }
        }

        void SetUI(bool canClaim)
        {
            CollectBtn.gameObject.SetActive(canClaim);
            CloseBtn.gameObject.SetActive(!canClaim);
        }


        async void OnCollect()
        {
            CollectBtn.interactable = false;

            var res = await AuthManager.Instance.ClaimDailyBonusAsync();

            if (res.Success)
            {

                PlayAnimation(res.ChipsGranted);
                nextTime = res.NextBonusTime;
                SetUI(false);
                StartTimer();

            }
            else if (res.ErrorCode == "E001")
            {
                nextTime = res.NextBonusTime;
                SetUI(false);
                StartTimer();
            }

            CollectBtn.interactable = true;
        }


        void StartTimer()
        {
            if (isRunning) return;

            isRunning = true;
            RunTimer().Forget();
        }

        async UniTaskVoid RunTimer()
        {
            while (true)
            {
                var remain = nextTime - DateTime.UtcNow;

                if (remain.TotalSeconds <= 0)
                {
                    TimerText.text = "Collect Now!";
                    SetUI(true);
                    isRunning = false;
                    break;
                }

                TimerText.text =
                    $"{(int)remain.TotalHours:D2}:{remain.Minutes:D2}:{remain.Seconds:D2}";

                await UniTask.Delay(1000);
            }
        }

        void PlayAnimation(int amount)
        {
            RewardText.gameObject.SetActive(true);
            RewardText.text = "+" + amount;

            RewardText.transform.localScale = Vector3.zero;

            RewardText.transform.DOScale(1.5f, 0.5f)
                .OnComplete(() =>
                {
                    RewardText.transform.DOScale(1f, 0.3f)
                        .OnComplete(() => RewardText.gameObject.SetActive(false));
                });
        }



        void GenerateDaysUI()
        {
            foreach (Transform child in Days_Content)
                Destroy(child.gameObject);

            for (int i = 0; i < rewards.Count; i++)
            {
                var go = Instantiate(Days_Prefab, Days_Content);
                var ui = go.GetComponent<DailyBonusDayPrefab>();

                bool isClaimed = (i + 1) < currentDay;
                bool isToday = (i + 1) == currentDay;

                ui.Setup(
                    rewards[i].Day,
                    rewards[i].Coins,
                    isClaimed,
                    isToday
                );
            }
        }
    }
}
