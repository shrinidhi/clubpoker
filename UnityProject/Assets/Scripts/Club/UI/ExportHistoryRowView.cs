using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ClubPoker.Core;

/// <summary>
/// One row of the Export Record tab: date range, data types, diamond cost,
/// who exported it and when. Every text is optional — wire what the design has.
/// </summary>
public class ExportHistoryRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rangeText;     // 2026.11.01 - 2026.11.13
    [SerializeField] private TextMeshProUGUI typesText;     // Club Data, Trade Record
    [SerializeField] private TextMeshProUGUI costText;      // 258
    [SerializeField] private TextMeshProUGUI byText;        // By alice · 2026.11.13 14:05
    [SerializeField] private TextMeshProUGUI fileNameText;

    public void Setup(ExportHistoryItem item, Func<string, string> typeName)
    {
        if (item == null) return;

        if (rangeText != null)
            rangeText.text = $"{FormatDay(item.FromDate)} - {FormatDay(item.ToDate)}";

        if (typesText != null)
        {
            var names = new List<string>();
            if (item.Types != null)
                foreach (string key in item.Types)
                    names.Add(typeName != null ? typeName(key) : key);
            typesText.text = string.Join(", ", names);
        }

        if (costText != null)
            costText.text = item.DiamondCost.ToString("N0");

        if (byText != null)
            byText.text = Loc.Get("export.history_by",
                ("user", item.ActorUsername ?? "-"),
                ("time", item.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm")));

        if (fileNameText != null)
            fileNameText.text = item.FileName ?? string.Empty;
    }

    // 2026-11-17T23:59:59.999Z → "2026.11.17", same style as the date picker
    // label. The export range is in UTC days, so format in UTC — local time
    // would shift the "to" day forward east of UTC (India: 11-18 05:29).
    private static string FormatDay(DateTime date)
    {
        if (date == default)
            return "-";

        return date.ToUniversalTime().ToString("yyyy.MM.dd");
    }
}
