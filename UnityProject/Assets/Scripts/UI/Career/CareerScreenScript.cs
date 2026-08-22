using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CareerScreenScript : MonoBehaviour
{
    public Button CloseButton;

    public Button Days_7Button;
    public Button Days_30Button;
    public Button Days_TotalButton;
    public Text WinningCountText;

    [Header("30 Days And Total")]
    public Transform Days30Content;
    public GameObject Days30Prefab;

    [Header("7 Days")]
    public Transform Days7Content;
    public GameObject Days7Prefab;

    [Header("Selected Button")]
    public GameObject Days7Selected;
    public GameObject Days30Selected;
    public GameObject DaysTotalSelected;

    [Header("Optional")]
    public TextMeshProUGUI ErrorText;

    [Header("Variant")]
    public Transform VariantContent;
    public GameObject VariantPrefab;
    public Button VariantButton;
    public Text VariantText;
    public GameObject VariantPanel;

    [Header("Club Variant")]
    public Transform ClubVariantContent;

    [Header("Friend Table Variant")]
    public Transform FriendTableVariantContent;

    [Header("Period Panels")]
    public GameObject Day7_Panel;
    public GameObject Day30Panel;

    private string currentPeriod = "30d";
    private string selectedVariant = "ALL";
    private bool isLoading;
    private bool isInitialized;
    public GameDataScript GameDataScreen;

    public Button VariantCloseButton;

    public Sprite SelectBG;
    public Sprite UnSelectBG;

    [Serializable]
    public class VariantItem
    {
        public string DisplayName;
        public string Value;
        public bool Enabled;

        public VariantItem(string displayName, string value, bool enabled)
        {
            DisplayName = displayName;
            Value = value;
            Enabled = enabled;
        }
    }

    [Header("ALL Variant - Only ALL")]
    private List<VariantItem> AllVariants = new List<VariantItem>()
    {
        new VariantItem("ALL", "ALL", true)
    };

    [Header("Club Variants")]
    private List<VariantItem> ClubVariants = new List<VariantItem>()
    {
        new VariantItem("ALL", "ALL", true),
        new VariantItem("NLH", "texas_holdem", true),
        new VariantItem("PLO4", "plo4", true),
        new VariantItem("PLO6", "plo6", true),
        new VariantItem("AoF-NLH", "aof_nlh", false),
        new VariantItem("PLO", "plo", false),
        new VariantItem("AoF-PLO", "aof_plo", false),
        new VariantItem("PLO(H/L)", "plo_hl", false),
        new VariantItem("Flash/NLH", "flash_nlh", false),
        new VariantItem("Flash/PLO", "flash_plo", false),
        new VariantItem("Mixed", "mixed", false),
        new VariantItem("OFC", "ofc", false),
        new VariantItem("Mau Binh", "mau_binh", false),
        new VariantItem("13 Poker", "13_poker", false),
        new VariantItem("Mata", "mata", false),
        new VariantItem("Pusoy", "pusoy", false),
        new VariantItem("Other", "other", false),
        new VariantItem("MTT-NLH", "mtt_nlh", false),
        new VariantItem("MTT-PLO", "mtt_plo", false),
        new VariantItem("MTT-PLO(H/L)", "mtt_plo_hl", false),
        new VariantItem("MTT-MauBinh", "mtt_mau_binh", false),
        new VariantItem("MTT-Pusoy", "mtt_pusoy", false),
        new VariantItem("MTT-OFC", "mtt_ofc", false),
        new VariantItem("SNG-NLH", "sng_nlh", false),
        new VariantItem("SNG-PLO", "sng_plo", false),
        new VariantItem("SNG-PLO(H/L)", "sng_plo_hl", false)
    };

    [Header("Friend Table Variants")]
    private List<VariantItem> FriendTableVariants = new List<VariantItem>()
    {
        new VariantItem("ALL", "ALL", false),
        new VariantItem("NLH", "texas_holdem", false),
        new VariantItem("PLO4", "plo4", false),
        new VariantItem("PLO6", "plo6", false),
        new VariantItem("AoF-NLH", "aof_nlh", false),
        new VariantItem("PLO", "plo", false),
        new VariantItem("AoF-PLO", "aof_plo", false),
        new VariantItem("PLO(H/L)", "plo_hl", false),
        new VariantItem("Mixed", "mixed", false),
        new VariantItem("OFC", "ofc", false),
        new VariantItem("Mau Binh", "mau_binh", false),
        new VariantItem("13 Poker", "13_poker", false),
        new VariantItem("Pusoy", "pusoy", false),
        new VariantItem("Other", "other", false)
    };

    private void Start()
    {
        SetupButtons();
        GenerateAllVariantButtons();
        GenerateClubVariantButtons();
        GenerateFriendTableVariantButtons();

        selectedVariant = "ALL";

        if (VariantText != null)
            VariantText.text = "ALL";

        if (VariantPanel != null)
            VariantPanel.SetActive(false);

        isInitialized = true;
        Days30ButtonOnTap();
    }

    private void OnEnable()
    {
        if (!isInitialized)
            return;

        selectedVariant = "ALL";

        if (VariantText != null)
            VariantText.text = "ALL";

        if (VariantPanel != null)
            VariantPanel.SetActive(false);

        Days30ButtonOnTap();
    }

    private void SetupButtons()
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

        if (VariantButton != null)
        {
            VariantButton.onClick.RemoveListener(VariantButtonOnTap);
            VariantButton.onClick.AddListener(VariantButtonOnTap);
        }

        if (CloseButton != null)
        {
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
            CloseButton.onClick.AddListener(CloseButtonOnTap);
        }

        VariantCloseButton.onClick.AddListener(VariantCloseButtonOnTap);
    }

    private void OnDestroy()
    {
        if (Days_7Button != null)
            Days_7Button.onClick.RemoveListener(Days7ButtonOnTap);

        if (Days_30Button != null)
            Days_30Button.onClick.RemoveListener(Days30ButtonOnTap);

        if (Days_TotalButton != null)
            Days_TotalButton.onClick.RemoveListener(DaysTotalButtonOnTap);

        if (VariantButton != null)
            VariantButton.onClick.RemoveListener(VariantButtonOnTap);

        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
    }

    private void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }
    void VariantCloseButtonOnTap()
    {
        VariantPanel.SetActive(false);
    }
    private void GenerateAllVariantButtons()
    {
        GenerateVariantButtons(VariantContent, AllVariants);
    }

    private void GenerateClubVariantButtons()
    {
        GenerateVariantButtons(ClubVariantContent, ClubVariants);
    }

    private void GenerateFriendTableVariantButtons()
    {
        GenerateVariantButtons(FriendTableVariantContent, FriendTableVariants);
    }

    private void GenerateVariantButtons(Transform content, List<VariantItem> variants)
    {
        if (content == null || VariantPrefab == null)
        {
            Debug.LogError("Variant Content or VariantPrefab missing");
            return;
        }

        ClearContent(content);

        if (variants == null || variants.Count == 0)
            return;

        foreach (VariantItem item in variants)
        {
            if (item == null)
                continue;

            GameObject obj = Instantiate(VariantPrefab, content);

            VariantPrefabScript prefab = obj.GetComponent<VariantPrefabScript>();

            if (prefab == null)
            {
                Debug.LogError("VariantPrefabScript component missing on VariantPrefab");
                Destroy(obj);
                continue;
            }

            prefab.SetData(item.DisplayName, item.Value, OnVariantSelected);

            bool interactableState = item.Enabled;

            prefab.SetInteractable(interactableState);

            Debug.Log(
                "Generated Variant: " +
                item.DisplayName +
                " | Enabled: " +
                item.Enabled +
                " | Button Interactable: " +
                prefab.VariantButton.interactable
            );
        }
    }

    private void VariantButtonOnTap()
    {
        if (currentPeriod != "7d")
            return;

        if (VariantPanel != null)
            VariantPanel.SetActive(!VariantPanel.activeSelf);
    }

    private void OnVariantSelected(string displayName, string variantValue)
    {
        if (currentPeriod != "7d" || isLoading)
            return;

        selectedVariant = variantValue;

        if (VariantText != null)
            VariantText.text = displayName;

        if (VariantPanel != null)
            VariantPanel.SetActive(false);

        LoadCareerData().Forget();
    }

    private void Days7ButtonOnTap()
    {
        if (Days_7Button != null)
            Days_7Button.image.sprite = SelectBG;

        if (Days_30Button != null)
            Days_30Button.image.sprite = UnSelectBG;

        if (Days_TotalButton != null)
            Days_TotalButton.image.sprite = UnSelectBG;

        SelectPeriod("7d");
    }

    private void Days30ButtonOnTap()
    {
        if (Days_7Button != null)
            Days_7Button.image.sprite = UnSelectBG;

        if (Days_30Button != null)
            Days_30Button.image.sprite = SelectBG;

        if (Days_TotalButton != null)
            Days_TotalButton.image.sprite = UnSelectBG;

        SelectPeriod("30d");
    }

    private void DaysTotalButtonOnTap()
    {
        if (Days_7Button != null)
            Days_7Button.image.sprite = UnSelectBG;

        if (Days_30Button != null)
            Days_30Button.image.sprite = UnSelectBG;

        if (Days_TotalButton != null)
            Days_TotalButton.image.sprite = SelectBG;

        SelectPeriod("ALL");
    }

    private void SelectPeriod(string period)
    {
        if (isLoading)
            return;

        currentPeriod = period;

        if (Days7Selected != null)
            Days7Selected.SetActive(period == "7d");

        if (Days30Selected != null)
            Days30Selected.SetActive(period == "30d");

        if (DaysTotalSelected != null)
            DaysTotalSelected.SetActive(period == "ALL");

        bool isSevenDays = period == "7d";

        if (Day7_Panel != null)
            Day7_Panel.SetActive(isSevenDays);

        if (Day30Panel != null)
            Day30Panel.SetActive(!isSevenDays);

        if (Days7Content != null)
            Days7Content.gameObject.SetActive(isSevenDays);

        if (Days30Content != null)
            Days30Content.gameObject.SetActive(!isSevenDays);

        if (VariantButton != null)
            VariantButton.gameObject.SetActive(isSevenDays);

        if (VariantPanel != null)
            VariantPanel.SetActive(false);

        if (!isSevenDays)
        {
            selectedVariant = "ALL";

            if (VariantText != null)
                VariantText.text = "ALL";
        }

        LoadCareerData().Forget();
    }

    private async UniTaskVoid LoadCareerData()
    {
        if (isLoading)
            return;

        if (AuthManager.Instance == null)
        {
            ShowError("AuthManager not available");
            return;
        }

        isLoading = true;
        SetButtonsInteractable(false);
        ClearError();

        string requestedPeriod = currentPeriod;
        string requestedVariant = requestedPeriod == "7d" ? selectedVariant : "ALL";

        try
        {
            CareerOverviewData data = await AuthManager.Instance.GetCareerOverviewAsync(requestedPeriod, requestedVariant);

            if (this == null)
                return;

            if (requestedPeriod != currentPeriod)
                return;

            if (requestedPeriod == "7d" && requestedVariant != selectedVariant)
                return;

            ClearAllSessionItems();

            if (data == null)
            {
                if (WinningCountText != null)
                    WinningCountText.text = "0";

                ShowError("Career data could not be loaded");
                return;
            }

            if (WinningCountText != null)
            {
                WinningCountText.text = FormatWinnings(data.Winnings);

                if (data.Winnings > 0)
                    WinningCountText.color = Color.green;
                else if (data.Winnings < 0)
                    WinningCountText.color = Color.red;
                else
                    WinningCountText.color = Color.white;
            }

            List<CareerSessionData> sessions = data.Sessions;

            if (sessions == null || sessions.Count == 0)
            {
                Debug.Log("Career sessions empty | Period: " + requestedPeriod + " | Variant: " + requestedVariant);
                return;
            }

            if (requestedPeriod == "7d")
                Generate7DaysSessions(sessions);
            else
                Generate30DaysSessions(sessions);

            Debug.Log("Career UI loaded | Period: " + requestedPeriod + " | Variant: " + requestedVariant + " | Winnings: " + data.Winnings + " | Sessions: " + sessions.Count);
        }
        catch (Exception e)
        {
            Debug.LogError("Career data load failed: " + e.Message);

            if (WinningCountText != null)
                WinningCountText.text = "0";

            ShowError(string.IsNullOrEmpty(e.Message) ? "Career data could not be loaded" : e.Message);
        }
        finally
        {
            isLoading = false;
            SetButtonsInteractable(true);
        }
    }

    private void Generate7DaysSessions(List<CareerSessionData> sessions)
    {
        if (Days7Content == null || Days7Prefab == null)
        {
            Debug.LogError("Days7Content or Days7Prefab missing");
            return;
        }

        foreach (CareerSessionData session in sessions)
        {
            GameObject obj = Instantiate(Days7Prefab, Days7Content);
            Days_7SessionPrefab prefab = obj.GetComponent<Days_7SessionPrefab>();

            if (prefab != null)
            {
                prefab.GameDataScreen = GameDataScreen;
                prefab.SetData(session);
            }
            else
                Destroy(obj);
        }
    }

    private void Generate30DaysSessions(List<CareerSessionData> sessions)
    {
        if (Days30Content == null || Days30Prefab == null)
        {
            Debug.LogError("Days30Content or Days30Prefab missing");
            return;
        }

        foreach (CareerSessionData session in sessions)
        {
            GameObject obj = Instantiate(Days30Prefab, Days30Content);
            Days_30SessionPrefab prefab = obj.GetComponent<Days_30SessionPrefab>();

            if (prefab != null)
                prefab.SetData(session);
            else
                Destroy(obj);
        }
    }

    private void ClearAllSessionItems()
    {
        ClearContent(Days7Content);
        ClearContent(Days30Content);
    }

    private void ClearContent(Transform content)
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    private string FormatWinnings(int winnings)
    {
        return winnings > 0 ? "+" + winnings : winnings.ToString();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (Days_7Button != null)
            Days_7Button.interactable = interactable;

        if (Days_30Button != null)
            Days_30Button.interactable = interactable;

        if (Days_TotalButton != null)
            Days_TotalButton.interactable = interactable;

        if (VariantButton != null)
            VariantButton.interactable = interactable && currentPeriod == "7d";
    }

    private void ShowError(string message)
    {
        if (ErrorText != null)
            ErrorText.text = message;

        Debug.LogWarning(message);
    }

    private void ClearError()
    {
        if (ErrorText != null)
            ErrorText.text = "";
    }

    public void SetAllVariantEnabled(string variantValue, bool enabled)
    {
        SetVariantEnabled(AllVariants, variantValue, enabled);
        GenerateAllVariantButtons();
    }

    public void SetClubVariantEnabled(string variantValue, bool enabled)
    {
        SetVariantEnabled(ClubVariants, variantValue, enabled);
        GenerateClubVariantButtons();
    }

    public void SetFriendTableVariantEnabled(string variantValue, bool enabled)
    {
        SetVariantEnabled(FriendTableVariants, variantValue, enabled);
        GenerateFriendTableVariantButtons();
    }

    private void SetVariantEnabled(List<VariantItem> variants, string variantValue, bool enabled)
    {
        if (variants == null)
            return;

        foreach (VariantItem item in variants)
        {
            if (item == null)
                continue;

            if (string.Equals(item.Value, variantValue, StringComparison.OrdinalIgnoreCase))
            {
                item.Enabled = enabled;
                return;
            }
        }
    }
}