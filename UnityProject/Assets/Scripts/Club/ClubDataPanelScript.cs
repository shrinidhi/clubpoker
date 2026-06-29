using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Club Data screen. Top date range (with prev/next arrows) + three tabs:
/// Yesterday, Last 7 days, and Select (custom range via the shared date picker).
/// </summary>
public class ClubDataPanelScript : MonoBehaviour
{
    private enum DataTab { Yesterday, Last7, Select }

    [Header("Navigation")]
    public Button Back_Button;
    public Button Export_Button;            // top-right export icon
    public ExportDataModalScript ExportModal;

    [Header("Top Range")]
    public TextMeshProUGUI TopRange_Text;   // center "yyyy.MM.dd - yyyy.MM.dd"
    public Button TopRange_Button;          // tap the top date → open picker
    public Button Prev_Button;              // <  shift one period back
    public Button Next_Button;              // >  shift one period forward

    [Header("Tabs")]
    public Button Yesterday_Button;
    public Button Last7Days_Button;
    public Button Select_Button;
    public TextMeshProUGUI Select_Label;    // shows the range when Select is active, else "Select"

    [Header("Tab Highlight (optional)")]
    public Sprite SelectedTabSprite;
    public Sprite UnselectedTabSprite;

    [Header("Date Picker")]
    public DateRangePopupView DateRangePopupView;

    private DateTime _rangeStart;
    private DateTime _rangeEnd;
    private DataTab _activeTab;

    // Earliest selectable day: first of last month (the data/picker window).
    private static DateTime MinDate =>
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);

    private void Start()
    {
        if (Back_Button != null)        Back_Button.onClick.AddListener(BackOnTap);
        if (Export_Button != null)      Export_Button.onClick.AddListener(OpenExportModal);
        if (Yesterday_Button != null)   Yesterday_Button.onClick.AddListener(SelectYesterday);
        if (Last7Days_Button != null)   Last7Days_Button.onClick.AddListener(SelectLast7Days);
        if (Select_Button != null)      Select_Button.onClick.AddListener(OpenSelectPopup);
        if (TopRange_Button != null)    TopRange_Button.onClick.AddListener(OpenSelectPopup);
        if (Prev_Button != null)        Prev_Button.onClick.AddListener(() => Shift(-1));
        if (Next_Button != null)        Next_Button.onClick.AddListener(() => Shift(+1));

        // Default to Last 7 days.
        SelectLast7Days();
    }

    /// <summary>The active range, exposed so the Export modal can seed from it.</summary>
    public DateTime RangeStart => _rangeStart;
    public DateTime RangeEnd => _rangeEnd;

    // ── Tab handlers ────────────────────────────────────────────────────────

    private void SelectYesterday()
    {
        DateTime yesterday = DateTime.Today.AddDays(-1);
        ApplyRange(yesterday, yesterday, DataTab.Yesterday);
    }

    private void SelectLast7Days()
    {
        // 7 days ending yesterday (today excluded).
        DateTime yesterday = DateTime.Today.AddDays(-1);
        ApplyRange(yesterday.AddDays(-6), yesterday, DataTab.Last7);
    }

    private void OpenSelectPopup()
    {
        // Preset the picker to the current range; result routes back here and
        // becomes the Select tab's custom range. Closing also applies (per spec).
        if (DateRangePopupView != null)
            DateRangePopupView.Open(_rangeStart, _rangeEnd, OnPopupRangePicked);
    }

    private void OnPopupRangePicked(DateTime start, DateTime end)
    {
        ApplyRange(start, end, DataTab.Select);
    }

    // ── Arrows ────────────────────────────────────────────────────────────────

    private void Shift(int direction)
    {
        // Slide the whole window by one day, keeping its length.
        DateTime start = _rangeStart.AddDays(direction);
        DateTime end = _rangeEnd.AddDays(direction);

        // Stay within [last month start, today].
        if (end > DateTime.Today || start < MinDate)
            return;

        // Manual navigation always lands on the Select tab.
        ApplyRange(start, end, DataTab.Select);
    }

    // ── Core ────────────────────────────────────────────────────────────────

    private void ApplyRange(DateTime start, DateTime end, DataTab tab)
    {
        _rangeStart = start;
        _rangeEnd = end;
        _activeTab = tab;

        UpdateUI();
        LoadData(start, end).Forget();
    }

    private void UpdateUI()
    {
        if (TopRange_Text != null)
            TopRange_Text.text = $"{_rangeStart:yyyy.MM.dd} - {_rangeEnd:yyyy.MM.dd}";

        // Select tab shows a short (no-year) range only while it's the active tab.
        if (Select_Label != null)
            Select_Label.text = _activeTab == DataTab.Select
                ? $"{_rangeStart:MM.dd} - {_rangeEnd:MM.dd}"
                : "Select";

        UpdateTabHighlights();
        UpdateArrows();
    }

    private void UpdateTabHighlights()
    {
        SetTabSprite(Yesterday_Button, _activeTab == DataTab.Yesterday);
        SetTabSprite(Last7Days_Button, _activeTab == DataTab.Last7);
        SetTabSprite(Select_Button, _activeTab == DataTab.Select);
    }

    private void SetTabSprite(Button button, bool selected)
    {
        if (button == null || button.image == null || SelectedTabSprite == null || UnselectedTabSprite == null)
            return;

        button.image.sprite = selected ? SelectedTabSprite : UnselectedTabSprite;
    }

    private void UpdateArrows()
    {
        // > enabled while there's room before today; < while there's room before last month's start.
        if (Next_Button != null)
            Next_Button.interactable = _rangeEnd < DateTime.Today;

        if (Prev_Button != null)
            Prev_Button.interactable = _rangeStart > MinDate;
    }

    // ── Data ──────────────────────────────────────────────────────────────

    private async UniTaskVoid LoadData(DateTime start, DateTime end)
    {
        // TODO: fetch club data for [start, end], e.g.
        // var rows = await AuthManager.Instance.GetClubDataAsync(ClubContext.ClubId, start, end);
        // Populate(rows);
        await UniTask.CompletedTask;
    }

    private void OpenExportModal()
    {
        if (ExportModal == null)
            return;

        // Seed the modal with the current range, then open it.
        ExportModal.SetRange(_rangeStart, _rangeEnd);
        ExportModal.gameObject.SetActive(true);
    }

    private void BackOnTap()
    {
        gameObject.SetActive(false);
    }
}
