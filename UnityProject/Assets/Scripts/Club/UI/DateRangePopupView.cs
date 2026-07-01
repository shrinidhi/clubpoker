using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Date-range picker popup. Renders the previous + current month as one
/// continuous vertical scroll, lets the user tap a start then an end date
/// (max 7 days inclusive), shows the live range in the subtitle, and fires
/// OnRangeSelected when a complete valid range is chosen.
/// </summary>
public class DateRangePopupView : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Navigation")]
    [SerializeField] private Button confirmButton;   // saves the range and closes
    [SerializeField] private Button closeButton;     // optional plain dismiss (no save)

    [Header("Month List")]
    [SerializeField] private ScrollRect monthsScrollRect;  // the months ScrollView
    [SerializeField] private Transform monthsContent;      // ScrollView Content
    [SerializeField] private MonthBlockView monthBlockPrefab;

    [Header("Highlight Colors")]
    [SerializeField] private Color endpointColor = new Color(0.55f, 0.35f, 0.18f, 1f);   // brown
    [SerializeField] private Color betweenColor  = new Color(0.55f, 0.35f, 0.18f, 0.4f); // brown, low alpha
    [SerializeField] private Color normalColor   = Color.clear;

    private const int MaxRangeDays = 7;

    /// <summary>
    /// Fired on Confirm. Prefer Open(start, end, onConfirm) for per-caller routing;
    /// this event is a fallback for a single static owner.
    /// </summary>
    public event Action<DateTime, DateTime> OnRangeSelected;

    private readonly List<MonthBlockView> _months = new();

    // ── Selection state ─────────────────────────────────────────────────────
    private DateTime _rangeStart;
    private DateTime? _rangeEnd;
    // True while we're waiting for the second (end) tap. False = range complete,
    // so the next tap starts a brand-new range.
    private bool _awaitingEnd;

    // Per-open context (set by Open, consumed by Build / Confirm).
    private Action<DateTime, DateTime> _onConfirm;
    private DateTime? _presetStart;
    private DateTime? _presetEnd;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        Build();
    }

    /// <summary>
    /// Open the shared picker preset to [start, end] and route its result to
    /// <paramref name="onConfirm"/>. Use this from each trigger (Data "Select",
    /// Export calendar, etc.) so the right caller receives the range.
    /// </summary>
    public void Open(DateTime start, DateTime end, Action<DateTime, DateTime> onConfirm)
    {
        _presetStart = start;
        _presetEnd = end;
        _onConfirm = onConfirm;
        gameObject.SetActive(true);   // triggers OnEnable → Build (consumes the preset)
    }

    /// <summary>Confirm button — apply the current range and close.</summary>
    public void Confirm() => Dismiss();

    /// <summary>Close/X — also applies the current range (per Data screen spec).</summary>
    public void Close() => Dismiss();

    private void Dismiss()
    {
        DateTime end = _rangeEnd ?? _rangeStart;

        // Capture + clear the callback before hiding so it can't double-fire.
        Action<DateTime, DateTime> cb = _onConfirm;
        _onConfirm = null;

        gameObject.SetActive(false);

        cb?.Invoke(_rangeStart, end);
        OnRangeSelected?.Invoke(_rangeStart, end);
    }

    // ── Build ────────────────────────────────────────────────────────────────

    private void Build()
    {
        if (titleText != null)
            titleText.text = "Select";

        ClearMonths();

        DateTime today = DateTime.Today;

        if (_presetStart.HasValue)
        {
            // Opened via Open(start, end, ...) — show that range as a completed selection.
            _rangeStart = _presetStart.Value;
            _rangeEnd = _presetEnd;
        }
        else
        {
            // Plain open: today as a single day (no end).
            _rangeStart = today;
            _rangeEnd = null;
        }

        // Either way the range is complete, so the first tap starts a fresh range.
        _awaitingEnd = false;

        // Preset consumed for this open.
        _presetStart = null;
        _presetEnd = null;

        // Only the previous and current month are ever rendered, in that order.
        DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
        AddMonth(currentMonth.AddMonths(-1));
        AddMonth(currentMonth);

        UpdateSubtitle();
        RefreshHighlights();

        // Start scrolled to the bottom (current month). Wait a frame so the month
        // blocks' layout/ContentSizeFitter has computed the real content height.
        if (monthsScrollRect != null && isActiveAndEnabled)
            StartCoroutine(ScrollToBottomNextFrame());
    }

    private System.Collections.IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;   // let the layout system rebuild with the new rows

        if (monthsContent is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        Canvas.ForceUpdateCanvases();
        monthsScrollRect.verticalNormalizedPosition = 0f;   // 0 = bottom
    }

    private void AddMonth(DateTime anyDayInMonth)
    {
        MonthBlockView block = Instantiate(monthBlockPrefab, monthsContent);
        block.Build(anyDayInMonth, OnDayTapped);
        _months.Add(block);
    }

    private void ClearMonths()
    {
        _months.Clear();

        for (int i = monthsContent.childCount - 1; i >= 0; i--)
            Destroy(monthsContent.GetChild(i).gameObject);
    }

    // ── Selection logic ───────────────────────────────────────────────────────

    private void OnDayTapped(DateTime date)
    {
        if (!_awaitingEnd)
        {
            // Start a new range. End stays null until the second tap, but the
            // subtitle will mirror start for both until then.
            _rangeStart = date;
            _rangeEnd = null;
            _awaitingEnd = true;
        }
        else
        {
            // Complete the range; order the two taps so start <= end.
            DateTime start = _rangeStart;
            DateTime end = date;
            if (end < start)
                (start, end) = (end, start);

            // Reject ranges longer than 7 days (inclusive) — don't clamp or rearrange.
            if ((end - start).Days + 1 > MaxRangeDays)
            {
                ShowToast("The range Should be within 7 Days");
                return;   // keep waiting for a valid end tap; start unchanged
            }

            _rangeStart = start;
            _rangeEnd = end;
            _awaitingEnd = false;
            // No save here — the range is committed only when Confirm is tapped.
        }

        UpdateSubtitle();
        RefreshHighlights();
    }

    // ── Visuals ────────────────────────────────────────────────────────────────

    private void UpdateSubtitle()
    {
        if (subtitleText == null)
            return;

        // Single day picked (no end yet) → show only the start date.
        subtitleText.text = _rangeEnd == null
            ? $"Start: {_rangeStart:yyyy.MM.dd}"
            : $"Start: {_rangeStart:yyyy.MM.dd} - End: {_rangeEnd.Value:yyyy.MM.dd}";
    }

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }

    private void RefreshHighlights()
    {
        DateTime today = DateTime.Today;
        DateTime end = _rangeEnd ?? _rangeStart;

        foreach (MonthBlockView month in _months)
        {
            foreach (DayCellView cell in month.DayCells)
            {
                DateTime d = cell.Date;

                bool isEndpoint = d == _rangeStart || d == end;
                bool isBetween = d > _rangeStart && d < end;

                Color bg = isEndpoint ? endpointColor
                         : isBetween  ? betweenColor
                         : normalColor;

                cell.ApplyVisual(bg, d == today);
            }
        }
    }
}
