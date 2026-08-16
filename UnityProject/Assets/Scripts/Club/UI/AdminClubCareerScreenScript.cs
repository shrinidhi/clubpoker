using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ClubPoker.Networking.Models;

/// <summary>
/// Admin ▸ Club Career. A trimmed Club Data screen: same date range + tabs + variant filter
/// and the same GET /api/clubs/{clubId}/data endpoint, but only Winnings/Games in the summary
/// and no export. Reuses ClubManager, DateRangePopupView, the variant chip prefab and
/// ClubDataGameRowView.
/// Backend is LIVE.
/// </summary>
public class AdminClubCareerScreenScript : MonoBehaviour
{
    private enum CareerTab { Yesterday, Last7, Select }

    [Header("Navigation")]
    public Button Back_Button;

    [Header("Top Range")]
    public TextMeshProUGUI TopRange_Text;   // "yyyy.MM.dd — yyyy.MM.dd"
    public Button TopRange_Button;          // tap the top date → open picker
    public Button Prev_Button;              // ◀ shift one day back
    public Button Next_Button;              // ▶ shift one day forward

    [Header("Tabs")]
    public Button Yesterday_Button;
    public Button Last7Days_Button;
    public Button Select_Button;
    public TextMeshProUGUI Select_Label;    // range while Select is active, else "Select"

    [Header("Tab Highlight (optional)")]
    public Sprite SelectedTabSprite;
    public Sprite UnselectedTabSprite;

    [Header("Date Picker")]
    public DateRangePopupView DateRangePopupView;   // own instance, nested under this screen

    [Header("Summary")]
    public TextMeshProUGUI Winnings_Text;
    public TextMeshProUGUI Games_Text;

    [Header("Variant Filter")]
    public Transform Variant_Content;
    public GameObject FilterVariantPrefab;
    public TextAsset ClubDataVariantJson;

    [Header("Games List")]
    public ScrollRect ScrollView;
    public Transform Games_Content;
    public GameObject GameRowPrefab;        // prefab with ClubDataGameRowView
    public GameObject EmptyState;           // "No Game Data"

    private DateTime _rangeStart;
    private DateTime _rangeEnd;
    private CareerTab _activeTab;
    private bool _started;

    private string _selectedVariantKey = "ALL";
    private readonly List<FilterTableByVariantPrefabScrtipt> _variantItems = new();
    private FilterTableByVariantPrefabScrtipt _selectedVariantItem;

    // Earliest selectable day — same rule the date picker renders (club creation month).
    private static DateTime MinDate => ClubContext.MinSelectableDate;

    private void Start()
    {
        if (Back_Button != null)      Back_Button.onClick.AddListener(BackOnTap);
        if (Yesterday_Button != null) Yesterday_Button.onClick.AddListener(SelectYesterday);
        if (Last7Days_Button != null) Last7Days_Button.onClick.AddListener(SelectLast7Days);
        if (Select_Button != null)    Select_Button.onClick.AddListener(OpenSelectPopup);
        if (TopRange_Button != null)  TopRange_Button.onClick.AddListener(OpenSelectPopup);
        if (Prev_Button != null)      Prev_Button.onClick.AddListener(() => Shift(-1));
        if (Next_Button != null)      Next_Button.onClick.AddListener(() => Shift(+1));

        GenerateVariantFilters();

        _started = true;
        SelectYesterday();   // default tab + first load
    }

    // Reopening the screen refreshes from the default tab.
    private void OnEnable()
    {
        if (_started) SelectYesterday();
    }

    // ── Variant filter ──────────────────────────────────────────────────────

    private void GenerateVariantFilters()
    {
        if (Variant_Content == null || FilterVariantPrefab == null)
            return;

        for (int i = Variant_Content.childCount - 1; i >= 0; i--)
            Destroy(Variant_Content.GetChild(i).gameObject);
        _variantItems.Clear();

        CreateVariantFilter("ALL", "All",false);

        ClubTableVariantResponse parsed = null;
        if (ClubDataVariantJson != null)
            parsed = JsonConvert.DeserializeObject<ClubTableVariantResponse>(ClubDataVariantJson.text);

        if (parsed?.ClubTableVariants != null)
            foreach (ClubTableVariantData v in parsed.ClubTableVariants)
                CreateVariantFilter(v.VariantKey, v.VariantName,v.IsLocked);

        if (_variantItems.Count > 0)
        {
            _selectedVariantKey = "ALL";
            _selectedVariantItem = _variantItems[0];
            foreach (var item in _variantItems)
                item.SetSelected(item == _selectedVariantItem);
        }
    }

    private void CreateVariantFilter(string key, string displayName,bool islocked)
    {
        GameObject obj = Instantiate(FilterVariantPrefab, Variant_Content);
        var prefab = obj.GetComponent<FilterTableByVariantPrefabScrtipt>();
        prefab.SetData(key, displayName, islocked, OnVariantSelected);
        _variantItems.Add(prefab);
    }

    private void OnVariantSelected(string variantKey, FilterTableByVariantPrefabScrtipt item)
    {
        _selectedVariantKey = variantKey;
        _selectedVariantItem = item;

        foreach (var v in _variantItems)
            v.SetSelected(v == item);

        LoadData(_rangeStart, _rangeEnd).Forget();
    }

    // ── Tabs ────────────────────────────────────────────────────────────────

    private void SelectYesterday()
    {
        DateTime yesterday = DateTime.Today.AddDays(-1);
        ApplyRange(yesterday, yesterday, CareerTab.Yesterday);
    }

    private void SelectLast7Days()
    {
        DateTime yesterday = DateTime.Today.AddDays(-1);
        ApplyRange(yesterday.AddDays(-6), yesterday, CareerTab.Last7);
    }

    private void OpenSelectPopup()
    {
        if (DateRangePopupView != null)
            DateRangePopupView.Open(_rangeStart, _rangeEnd, OnPopupRangePicked);
    }

    private void OnPopupRangePicked(DateTime start, DateTime end)
    {
        ApplyRange(start, end, CareerTab.Select);
    }

    // ── Arrows ──────────────────────────────────────────────────────────────

    private void Shift(int direction)
    {
        DateTime start = _rangeStart.AddDays(direction);
        DateTime end   = _rangeEnd.AddDays(direction);

        if (end > DateTime.Today || start < MinDate)
            return;

        ApplyRange(start, end, CareerTab.Select);
    }

    // ── Core ────────────────────────────────────────────────────────────────

    private void ApplyRange(DateTime start, DateTime end, CareerTab tab)
    {
        _rangeStart = start;
        _rangeEnd   = end;
        _activeTab  = tab;

        UpdateUI();
        LoadData(start, end).Forget();
    }

    private void UpdateUI()
    {
        if (TopRange_Text != null)
            TopRange_Text.text = $"{_rangeStart:yyyy.MM.dd} - {_rangeEnd:yyyy.MM.dd}";

        if (Select_Label != null)
            Select_Label.text = _activeTab == CareerTab.Select
                ? $"{_rangeStart:MM.dd} - {_rangeEnd:MM.dd}"
                : "Select";

        SetTabSprite(Yesterday_Button, _activeTab == CareerTab.Yesterday);
        SetTabSprite(Last7Days_Button, _activeTab == CareerTab.Last7);
        SetTabSprite(Select_Button,    _activeTab == CareerTab.Select);

        if (Next_Button != null) Next_Button.interactable = _rangeEnd   < DateTime.Today;
        if (Prev_Button != null) Prev_Button.interactable = _rangeStart > MinDate;
    }

    private void SetTabSprite(Button button, bool selected)
    {
        if (button == null || button.image == null ||
            SelectedTabSprite == null || UnselectedTabSprite == null)
            return;

        button.image.sprite = selected ? SelectedTabSprite : UnselectedTabSprite;
    }

    // ── Data ────────────────────────────────────────────────────────────────

    private async UniTaskVoid LoadData(DateTime start, DateTime end)
    {
        string clubId = ClubContext.ClubId;
        if (string.IsNullOrEmpty(clubId) || ClubManager.Instance == null)
            return;

        try
        {
            ClubDataResponse res = await ClubManager.Instance.GetClubDataAsync(
                clubId, start, end, _selectedVariantKey);

            PopulateSummary(res?.Summary);
            PopulateGames(res?.Games);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminClubCareerScreenScript] LoadData failed: {e.Message}");
        }
    }

    private void PopulateSummary(ClubDataSummary summary)
    {
        if (Winnings_Text != null) Winnings_Text.text = (summary?.PlayerWinnings ?? 0).ToString("N0");
        if (Games_Text    != null) Games_Text.text    = (summary?.TotalGames     ?? 0).ToString();
    }

    private void PopulateGames(List<ClubGameData> games)
    {
        if (Games_Content == null) return;

        for (int i = Games_Content.childCount - 1; i >= 0; i--)
            Destroy(Games_Content.GetChild(i).gameObject);

        int count = games?.Count ?? 0;

        if (EmptyState != null) EmptyState.SetActive(count == 0);
        if (count == 0 || GameRowPrefab == null) return;

        foreach (ClubGameData game in games)
        {
            GameObject obj = Instantiate(GameRowPrefab, Games_Content);
            var row = obj.GetComponent<ClubDataGameRowView>();
            if (row != null) row.Setup(game);
        }

        ResetScroll();
    }

    // Rebuild the layout first so content height is known, then snap to top.
    private void ResetScroll()
    {
        if (ScrollView == null) return;
        Canvas.ForceUpdateCanvases();
        ScrollView.verticalNormalizedPosition = 1f;
    }

    private void BackOnTap()
    {
        gameObject.SetActive(false);
    }
}
