using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class TransactionView : MonoBehaviour
    {
        public Transform contentParent;
        public GameObject itemPrefab;
        public ScrollRect scrollRect;

        private int currentPage = 1;
        private int limit = 20;
        public string Type = "All";
        private bool isLoading;
        private bool hasMore = true;

        public GameObject Data_Not_Found;
        public Button Back_Button;
        public GameObject Transaction_Screen;
        private void Start()
        {
            LoadTransactions().Forget();
            scrollRect.onValueChanged.AddListener(OnScroll);
            Back_Button.onClick.AddListener(Back_ButtonOnTap);
        }


        void Back_ButtonOnTap()
        {
            Transaction_Screen.SetActive(false);
        }
       async UniTaskVoid LoadTransactions()
{
    if (isLoading || !hasMore) return;

    isLoading = true;

    try
    {
        var data = await AuthManager.Instance.GetTransactions(currentPage, limit, Type);

        if (data == null || data.Items == null || data.Items.Count == 0)
        {
            if (currentPage == 1)
                Data_Not_Found.SetActive(true);

            hasMore = false;
            isLoading = false;
            return;
        }

        Data_Not_Found.SetActive(false);

        foreach (var tx in data.Items)
        {
            var go = Instantiate(itemPrefab, contentParent);
            var ui = go.GetComponent<TransactionPrefabScript>();

            if (ui != null)
                ui.Setup(tx);
        }

        if (!data.Pagination.HasMore)
            hasMore = false;
        else
            currentPage++;
    }
    catch (System.Exception e)
    {
        Debug.LogError("Transaction Load Error: " + e.Message);
        Data_Not_Found.SetActive(true);
    }

    isLoading = false;
}

        void OnScroll(Vector2 pos)
        {
           
            if (pos.y <= 0.1f)
            {
                LoadTransactions().Forget();
            }
        }
    }
}