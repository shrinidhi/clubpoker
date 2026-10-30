using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;

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

    [Header("7 Days Range")]
    public DateRangePopupView DateRangePopup;   // opened by tapping the 7-day tab while it's selected

    // Picker can't go further back than this, however old the account.
    private const int MaxMonthsBack = 12;
    private const int RangeDays = 7;

    // The 7-day tab's range, both days inclusive. Also its button label ("09.05-09.11").
    private DateTime rangeStart;
    private DateTime rangeEnd;
    private bool isOpeningPicker;

    // "7d" (custom from/to range) | "30d" | "total"
    private string currentPeriod = "30d";
    private string selectedVariant = "ALL";
    private enum TableFilterScope { All, Club, Friend }
    private TableFilterScope selectedTableScope = TableFilterScope.All;
    private bool isLoading;
    private bool isInitialized;
    public GameDataScript GameDataScreen;

    public Button VariantCloseButton;

    public Sprite SelectBG;
    public Sprite UnSelectBG;

    public Button FavoriteButton;
    public GameObject FavoriteScreen;

    [Serializable]
    public class VariantItem
    {
        private readonly string _displayName;
        public string Value;
        public bool Enabled;

        // "ALL" is translated on read — the lists are field initializers, and
        // Loc can't load its file (Resources.Load) during construction.
        public string DisplayName => Value == "ALL" ? Loc.Get("all_caps") : _displayName;

        public VariantItem(string displayName, string value, bool enabled)
        {
            _displayName = displayName;
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
        new VariantItem("ALL", "ALL", true),
        new VariantItem("NLH", "texas_holdem", true),
        new VariantItem("PLO4", "plo4", true),
        new VariantItem("PLO6", "plo6", true),
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
        selectedTableScope = TableFilterScope.All;

        if (VariantText != null)
            VariantText.text = Loc.Get("all_caps");

        if (VariantPanel != null)
            VariantPanel.SetActive(false);

        ResetRange();

        isInitialized = true;
        Days30ButtonOnTap();

        FavoriteButton.onClick.AddListener(FavoriteButtonOnTap);
    }

    void FavoriteButtonOnTap()
    {
        FavoriteScreen.SetActive(true);
    }


    private void OnEnable()
    {
        if (!isInitialized)
            return;

        selectedVariant = "ALL";
        selectedTableScope = TableFilterScope.All;

        if (VariantText != null)
            VariantText.text = Loc.Get("all_caps");

        if (VariantPanel != null)
            VariantPanel.SetActive(false);

        ResetRange();
        Days30ButtonOnTap();
    }

    // Last 7 days including today, e.g. 09.05-09.11 on the 11th.
    private void ResetRange()
    {
        rangeEnd = DateTime.Today;
        rangeStart = rangeEnd.AddDays(-(RangeDays - 1));
        UpdateRangeLabel();
    }

    private void UpdateRangeLabel()
    {
        if (Days_7Button == null)
            return;

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string label = rangeStart.ToString("MM.dd", inv) + "-" + rangeEnd.ToString("MM.dd", inv);

        // The tab's label may be legacy Text or TMP depending on the prefab.
        Text text = Days_7Button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
            return;
        }

        TMP_Text tmp = Days_7Button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = label;
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
        GenerateVariantButtons(VariantContent, AllVariants, OnAllVariantSelected);
    }

    private void GenerateClubVariantButtons()
    {
        GenerateVariantButtons(ClubVariantContent, ClubVariants, OnClubVariantSelected);
    }

    private void GenerateFriendTableVariantButtons()
    {
        GenerateVariantButtons(FriendTableVariantContent, FriendTableVariants, OnFriendTableVariantSelected);
    }

    private void GenerateVariantButtons(Transform content, List<VariantItem> variants)
    {
        GenerateVariantButtons(content, variants, OnVariantSelected);
    }

    private void GenerateVariantButtons(
        Transform content, List<VariantItem> variants,
        Action<string, string> selectionCallback)
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

            prefab.SetData(item.DisplayName, item.Value, selectionCallback);

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

    private void OnAllVariantSelected(string displayName, string variantValue)
    {
        SelectVariant(displayName, variantValue, TableFilterScope.All);
    }

    private void OnClubVariantSelected(string displayName, string variantValue)
    {
        SelectVariant(displayName, variantValue, TableFilterScope.Club);
    }

    private void OnFriendTableVariantSelected(string displayName, string variantValue)
    {
        SelectVariant(displayName, variantValue, TableFilterScope.Friend);
    }

    private void OnVariantSelected(string displayName, string variantValue)
    {
        SelectVariant(displayName, variantValue, TableFilterScope.All);
    }

    private void SelectVariant(
     string displayName,
     string variantValue,
     TableFilterScope scope)
    {
        if (currentPeriod != "7d" || isLoading)
            return;

        selectedTableScope = scope;
        selectedVariant = variantValue;

        if (VariantText != null)
        {
            if (variantValue == "ALL")
            {
                switch (scope)
                {
                    case TableFilterScope.Club:
                        VariantText.text = "All(Club)";
                        break;

                    case TableFilterScope.Friend:
                        VariantText.text = "All(Friend Table)";
                        break;

                    default:
                        VariantText.text = "ALL";
                        break;
                }
            }
            else
            {
                VariantText.text = displayName;
            }
        }

        if (VariantPanel != null)
            VariantPanel.SetActive(false);

        LoadCareerData().Forget();
    }

    // Tabs switch normally; only a tap on the already-selected 7-day tab opens the
    // date picker to change its range.
    private void Days7ButtonOnTap()
    {
        if (currentPeriod == "7d")
        {
            if (DateRangePopup != null)
                OpenRangePicker().Forget();
            return;
        }

        Show7DaysTab();
    }

    private void Show7DaysTab()
    {
        if (Days_7Button != null)
            Days_7Button.image.sprite = SelectBG;

        if (Days_30Button != null)
            Days_30Button.image.sprite = UnSelectBG;

        if (Days_TotalButton != null)
            Days_TotalButton.image.sprite = UnSelectBG;

        SelectPeriod("7d");
    }

    private async UniTaskVoid OpenRangePicker()
    {
        if (isLoading || isOpeningPicker)
            return;

        isOpeningPicker = true;

        try
        {
            DateTime? minDate = await GetMinSelectableDate();

            // User may have switched tabs while the profile was loading.
            if (this == null || currentPeriod != "7d")
                return;

            // X reports back the range we opened with, which OnRangePicked ignores —
            // so only Confirm changes the label.
            DateRangePopup.Open(rangeStart, rangeEnd, OnRangePicked, minDate);
        }
        finally
        {
            isOpeningPicker = false;
        }
    }

    // Account creation day, floored to MaxMonthsBack. Null (creation date unknown)
    // lets the popup fall back to previous + current month.
    private async UniTask<DateTime?> GetMinSelectableDate()
    {
        var auth = AuthManager.Instance;
        if (auth == null)
            return null;

        // Only /profile/full carries the creation date; fetched once per session
        // (usually already done by the main menu HUD for the player code).
        if (auth.Session != null && !auth.Session.HasFullProfile)
            await auth.GetPlayerProfileAsync();

        DateTime? registeredAt = auth.Session?.RegisteredAt;
        if (!registeredAt.HasValue)
            return null;

        DateTime today = DateTime.Today;
        DateTime floor = new DateTime(today.Year, today.Month, 1).AddMonths(-MaxMonthsBack);
        DateTime created = registeredAt.Value.ToLocalTime().Date;

        return created < floor ? floor : created;
    }

    // Confirm (new range) or X (the opening range, so it returns early below).
    private void OnRangePicked(DateTime start, DateTime end)
    {
        if (start == rangeStart && end == rangeEnd)
            return;

        rangeStart = start;
        rangeEnd = end;
        UpdateRangeLabel();

        if (currentPeriod == "7d")
            LoadCareerData().Forget();
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

        SelectPeriod("total");
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
            DaysTotalSelected.SetActive(period == "total");

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
            selectedTableScope = TableFilterScope.All;

            if (VariantText != null)
                VariantText.text = Loc.Get("all_caps");
        }

        LoadCareerData().Forget();
    }

    private async UniTaskVoid LoadCareerData()
    {
        if (isLoading)
            return;

        if (AuthManager.Instance == null)
        {
            ShowError(Loc.Get("something_went_wrong"));
            return;
        }

        isLoading = true;
        SetButtonsInteractable(false);
        ClearError();

        string requestedPeriod = currentPeriod;
        string requestedVariant = requestedPeriod == "7d" ? selectedVariant : "ALL";
        TableFilterScope requestedScope = requestedPeriod == "7d"
            ? selectedTableScope : TableFilterScope.All;
        DateTime requestedStart = rangeStart;
        DateTime requestedEnd = rangeEnd;

        try
        {
            CareerOverviewData data = requestedPeriod == "7d"
                ? await AuthManager.Instance.GetCareerOverviewAsync(requestedStart, requestedEnd, "ALL")
                : await AuthManager.Instance.GetCareerOverviewAsync(requestedPeriod, requestedVariant);

            if (this == null)
                return;

            if (requestedPeriod != currentPeriod)
                return;

            if (requestedPeriod == "7d" &&
                (requestedVariant != selectedVariant || requestedScope != selectedTableScope ||
                 requestedStart != rangeStart || requestedEnd != rangeEnd))
                return;

            ClearAllSessionItems();

            if (data == null)
            {
                if (WinningCountText != null)
                    WinningCountText.text = "0";

                ShowError(Loc.Get("career_load_failed"));
                return;
            }

            // Fetch all variants for the date range, then filter by table source
            // and variant together. Each group's ALL retains its source filter.
            List<CareerSessionData> sessions = FilterSessions(
                data.Sessions, requestedScope, requestedVariant);

            long visibleWinnings = data.Winnings;
            if (requestedScope != TableFilterScope.All ||
                !string.Equals(requestedVariant, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                visibleWinnings = 0;
                foreach (CareerSessionData session in sessions)
                    visibleWinnings += session.Winnings;
            }

            if (WinningCountText != null)
            {
                WinningCountText.text = visibleWinnings > 0
                    ? "+" + visibleWinnings : visibleWinnings.ToString();
                WinningCountText.color = visibleWinnings > 0 ? Color.green
                    : visibleWinnings < 0 ? Color.red : Color.white;
            }

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

            ShowError(string.IsNullOrEmpty(e.Message) ? Loc.Get("career_load_failed") : e.Message);
        }
        finally
        {
            isLoading = false;
            SetButtonsInteractable(true);
        }
    }

    private static List<CareerSessionData> FilterSessions(
        List<CareerSessionData> sessions, TableFilterScope scope, string variant)
    {
        var result = new List<CareerSessionData>();
        if (sessions == null)
            return result;

        bool allVariants = string.Equals(variant, "ALL", StringComparison.OrdinalIgnoreCase);
        string normalizedVariant = NormalizeVariant(variant);

        foreach (CareerSessionData session in sessions)
        {
            if (session == null)
                continue;

            bool isClubTable = !string.IsNullOrWhiteSpace(session.ClubId);
            if (scope == TableFilterScope.Club && !isClubTable)
                continue;
            if (scope == TableFilterScope.Friend && isClubTable)
                continue;
            if (!allVariants && NormalizeVariant(session.Variant) != normalizedVariant)
                continue;

            result.Add(session);
        }

        return result;
    }

    private static string NormalizeVariant(string variant)
    {
        string value = (variant ?? "").Trim().ToUpperInvariant();
        switch (value)
        {
            case "TEXAS_HOLDEM":
            case "NLH":
                return "NLH";
            case "OMAHA":
            case "PLO":
            case "PLO4":
                return "PLO4";
            case "OMAHA_FIVE":
            case "PLO5":
                return "PLO5";
            case "OMAHA_SIX":
            case "PLO6":
                return "PLO6";
            default:
                return value;
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