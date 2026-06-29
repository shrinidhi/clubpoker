using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using TMPro;

/// <summary>
/// Renders one calendar month: a "MMMM yyyy" label and a 7-column grid of day
/// cells. Row count is derived from BOTH the leading offset of day 1 and the
/// number of days in the month — never hardcoded.
/// </summary>
public class MonthBlockView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI monthLabel;
    [SerializeField] private Transform daysGrid;     // GridLayoutGroup, 7 fixed columns
    [SerializeField] private DayCellView dayCellPrefab;

    private readonly List<DayCellView> _dayCells = new();

    /// <summary>Real (tappable) day cells in this month — used to refresh highlights.</summary>
    public IReadOnlyList<DayCellView> DayCells => _dayCells;

    /// <summary>
    /// Build the month containing <paramref name="anyDayInMonth"/>. Each real day's
    /// button reports its DateTime back through <paramref name="onDayClick"/>.
    /// </summary>
    public void Build(DateTime anyDayInMonth, Action<DateTime> onDayClick)
    {
        Clear();

        int year = anyDayInMonth.Year;
        int month = anyDayInMonth.Month;

        monthLabel.text = anyDayInMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        // ── Row math (Sunday = 0 ... Saturday = 6) ──────────────────────────
        int leadingEmpty = (int)new DateTime(year, month, 1).DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int totalCells = leadingEmpty + daysInMonth;
        int rows = Mathf.CeilToInt(totalCells / 7f);
        int totalCellsToInstantiate = rows * 7;
        int trailingEmpty = totalCellsToInstantiate - leadingEmpty - daysInMonth;

        // ── Leading spacers ─────────────────────────────────────────────────
        for (int i = 0; i < leadingEmpty; i++)
            CreateCell().SetBlank();

        // ── Real days (future dates are shown but not selectable) ────────────
        DateTime today = DateTime.Today;
        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime date = new DateTime(year, month, day);
            DayCellView cell = CreateCell();
            cell.SetDay(date, onDayClick, selectable: date <= today);
            _dayCells.Add(cell);
        }

        // ── Trailing spacers (complete the final row to 7 columns) ──────────
        for (int i = 0; i < trailingEmpty; i++)
            CreateCell().SetBlank();
    }

    private DayCellView CreateCell()
    {
        return Instantiate(dayCellPrefab, daysGrid);
    }

    private void Clear()
    {
        _dayCells.Clear();

        for (int i = daysGrid.childCount - 1; i >= 0; i--)
            Destroy(daysGrid.GetChild(i).gameObject);
    }
}
