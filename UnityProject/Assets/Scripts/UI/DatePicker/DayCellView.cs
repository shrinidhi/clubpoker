using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single cell in a month's day grid. Can be a real, tappable day or a blank
/// spacer slot (used for leading/trailing cells so the 7-column grid stays aligned).
/// Blank cells stay active so they still occupy a grid slot — they're just empty.
/// </summary>
public class DayCellView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image background;

    [Header("Label Colors")]
    [SerializeField] private Color dayLabelColor = Color.white;
    [SerializeField] private Color disabledLabelColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color todayLabelColor = new Color(1f, 0.84f, 0f, 1f);   // distinct (gold)

    private bool _selectable;

    /// <summary>The date this cell represents (only meaningful when IsDay is true).</summary>
    public DateTime Date { get; private set; }

    /// <summary>True for real day cells, false for blank leading/trailing spacers.</summary>
    public bool IsDay { get; private set; }

    private Action<DateTime> _onClick;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (IsDay)
            _onClick?.Invoke(Date);
    }

    /// <summary>
    /// Configure this cell as a real day. When <paramref name="selectable"/> is
    /// false (e.g. a future date) the cell shows the number but can't be tapped
    /// and is dimmed.
    /// </summary>
    public void SetDay(DateTime date, Action<DateTime> onClick, bool selectable = true)
    {
        Date = date;
        IsDay = true;
        _onClick = onClick;
        _selectable = selectable;

        label.text = date.Day.ToString();
        button.interactable = selectable;
        label.color = selectable ? dayLabelColor : disabledLabelColor;
    }

    /// <summary>Configure this cell as a blank spacer (no day, not tappable).</summary>
    public void SetBlank()
    {
        IsDay = false;
        _onClick = null;

        label.text = string.Empty;
        button.interactable = false;
        background.color = Color.clear;
    }

    /// <summary>
    /// Apply selection visuals. Only called for real day cells.
    /// <paramref name="bg"/> is the background highlight; <paramref name="isToday"/>
    /// bolds/underlines the label regardless of selection state.
    /// </summary>
    public void ApplyVisual(Color bg, bool isToday)
    {
        background.color = bg;

        // Today gets its own color; otherwise keep the normal/dimmed day color.
        label.color = isToday
            ? todayLabelColor
            : (_selectable ? dayLabelColor : disabledLabelColor);

        label.fontStyle = isToday
            ? (FontStyles.Bold | FontStyles.Underline)
            : FontStyles.Normal;
    }
}
