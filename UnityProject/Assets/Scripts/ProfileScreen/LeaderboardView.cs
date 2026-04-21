using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;

namespace ClubPoker.UI
{
    public class LeaderboardView : MonoBehaviour
    {
        [Header("Tabs")]
        public Button globalTab;
        public Button weeklyTab;

        [Header("UI")]
        public Transform contentParent;
        public GameObject itemPrefab;
        public GameObject loadingPanel;
        public TMP_Text weeklyTimerText;

        private int page = 1;
        private int limit = 20;
        private bool isWeekly = false;
        public Button Back_Button;
            

        private void Start()
        {
            globalTab.onClick.AddListener(() => LoadGlobal());
            weeklyTab.onClick.AddListener(() => LoadWeekly());
            Back_Button.onClick.AddListener(Back_ButtonOnTap);

            LoadGlobal();
        }

        void Back_ButtonOnTap()
        {
            gameObject.SetActive(false);
        }

        async void LoadGlobal()
        {
            weeklyTimerText.text = "";
            globalTab.image.color = Color.white;
            weeklyTab.image.color = Color.gray;

               isWeekly = false;
            page = 1;
            await LoadData();
        }

        async void LoadWeekly()
        {
            isWeekly = true;
            globalTab.image.color = Color.gray;
            weeklyTab.image.color = Color.white;
            page = 1;
            await LoadData();
        }

        async UniTask LoadData()
        {
            ShowLoading(true);
            ClearList();

            try
            {
                if (isWeekly)
                {
                    var data = await AuthManager.Instance.GetWeeklyLeaderboard(page, limit);

                    weeklyTimerText.text = FormatReset(data.ResetsIn);

                    foreach (var item in data.Items)
                    {
                        CreateItem(item.Rank, item.Username, item.WeeklyWinnings, item.IsCurrentPlayer);
                    }
                }
                else
                {
                    var data = await AuthManager.Instance.GetGlobalLeaderboard(page, limit);

                    foreach (var item in data.Items)
                    {
                        CreateItem(item.Rank, item.Username, item.TotalWinnings, item.IsCurrentPlayer);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(e.Message);
            }

            ShowLoading(false);
        }

        void CreateItem(int rank, string username, int win, bool isMe)
        {
            GameObject obj = Instantiate(itemPrefab, contentParent);

            var ui = obj.GetComponent<LeaderBoardItemPrefab>();
            ui.SetData(rank, username, win, isMe);

            if (rank == 1) ui.SetTopColor(Color.yellow);
            else if (rank == 2) ui.SetTopColor(Color.gray);
            else if (rank == 3) ui.SetTopColor(new Color(1f, 0.5f, 0f));
        }

        void ClearList()
        {
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
        }

        void ShowLoading(bool show)
        {
            loadingPanel.SetActive(show);
        }

        string FormatReset(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            return raw
                .Replace("days", "d")
                .Replace("day", "d")
                .Replace("hours", "h")
                .Replace("hour", "h")
                .Replace("minutes", "m")
                .Replace("minute", "m");
        }
    }
}