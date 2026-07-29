using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CareerScreenScript : MonoBehaviour
{
    public Button Days_7Button;
    public Button Days_30Button;
    public Button Days_TotalButton;
    public TextMeshProUGUI WinningCountText;

    public Transform Days30Content;
    public GameObject Days30Prefab;

    [Header("Selected Button")]
    public GameObject Days7Selected;
    public GameObject Days30Selected;
    public GameObject DaysTotalSelected;

    [Header("Optional")]
    public TextMeshProUGUI ErrorText;

    private string currentPeriod = "30d";
    private bool isLoading;

    private void Start()
    {
        if (Days_7Button != null)
        {
            Days_7Button.onClick.RemoveListener(Days7ButtonOnTap);
            Days_7Button.onClick.AddListener(Days7ButtonOnTap);
        }

        if (Days_30Button != null)
        {
            Days_30Button.onClick.RemoveListener(Days30ButtonOnTap);
            Days_30Button.onClick.AddListener(Days30ButtonOnTap);
        }

        if (Days_TotalButton != null)
        {
            Days_TotalButton.onClick.RemoveListener(DaysTotalButtonOnTap);
            Days_TotalButton.onClick.AddListener(DaysTotalButtonOnTap);
        }

        SelectPeriod("30d");
    }

    private void OnEnable()
    {
        SelectPeriod("30d");
    }

    private void OnDestroy()
    {
        if (Days_7Button != null) Days_7Button.onClick.RemoveListener(Days7ButtonOnTap);
        if (Days_30Button != null) Days_30Button.onClick.RemoveListener(Days30ButtonOnTap);
        if (Days_TotalButton != null) Days_TotalButton.onClick.RemoveListener(DaysTotalButtonOnTap);
    }

    private void Days7ButtonOnTap()
    {
        SelectPeriod("7d");
    }

    private void Days30ButtonOnTap()
    {
        SelectPeriod("30d");
    }

    private void DaysTotalButtonOnTap()
    {
        SelectPeriod("ALL");
    }

    private void SelectPeriod(string period)
    {
        if (isLoading && currentPeriod == period) return;

        currentPeriod = period;

        if (Days7Selected != null) Days7Selected.SetActive(period == "7d");
        if (Days30Selected != null) Days30Selected.SetActive(period == "30d");
        if (DaysTotalSelected != null) DaysTotalSelected.SetActive(period == "ALL");

        LoadCareerData().Forget();
    }

    private async UniTaskVoid LoadCareerData()
    {
        if (isLoading) return;

        if (AuthManager.Instance == null)
        {
            ShowError("AuthManager not available");
            return;
        }

        isLoading = true;
        SetButtonsInteractable(false);
        ClearError();

        try
        {
            CareerOverviewData data = await AuthManager.Instance.GetCareerOverviewAsync(currentPeriod, "ALL");

            if (this == null) return;

            ClearSessionItems();

            if (data == null)
            {
                if (WinningCountText != null) WinningCountText.text = "0";
                ShowError("Career data could not be loaded");
                return;
            }

            if (WinningCountText != null) WinningCountText.text = FormatWinnings(data.Winnings);

            List<CareerSessionData> sessions = data.Sessions;

            if (sessions != null)
            {
                foreach (CareerSessionData session in sessions)
                {
                    GameObject obj = Instantiate(Days30Prefab, Days30Content);
                    Days_30SessionPrefab prefab = obj.GetComponent<Days_30SessionPrefab>();

                    if (prefab != null) prefab.SetData(session);
                    else Destroy(obj);
                }
            }

            Debug.Log($"Career UI loaded | Period: {currentPeriod} | Winnings: {data.Winnings} | Sessions: {sessions?.Count ?? 0}");
        }
        finally
        {
            isLoading = false;
            SetButtonsInteractable(true);
        }
    }

    private void ClearSessionItems()
    {
        if (Days30Content == null) return;

        for (int i = Days30Content.childCount - 1; i >= 0; i--)
            Destroy(Days30Content.GetChild(i).gameObject);
    }

    private string FormatWinnings(int winnings)
    {
        return winnings > 0 ? "+" + winnings : winnings.ToString();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (Days_7Button != null) Days_7Button.interactable = interactable;
        if (Days_30Button != null) Days_30Button.interactable = interactable;
        if (Days_TotalButton != null) Days_TotalButton.interactable = interactable;
    }

    private void ShowError(string message)
    {
        if (ErrorText != null) ErrorText.text = message;
        Debug.LogWarning(message);
    }

    private void ClearError()
    {
        if (ErrorText != null) ErrorText.text = "";
    }
}