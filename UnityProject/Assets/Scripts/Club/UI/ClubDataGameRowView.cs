using System;
using System.Globalization;
using UnityEngine;
using TMPro;

/// <summary>
/// One row in the Data screen's game list. Field mapping mirrors ClubGameData —
/// adjust once the populated games[] schema is confirmed.
/// </summary>
public class ClubDataGameRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dateTimeText;   // "06/19\n20:18"
    [SerializeField] private TextMeshProUGUI tableNameText;
    [SerializeField] private TextMeshProUGUI idText;
    [SerializeField] private TextMeshProUGUI variantText;
    [SerializeField] private TextMeshProUGUI rakeText;
    [SerializeField] private TextMeshProUGUI blindsText;
    [SerializeField] private TextMeshProUGUI feeText;

    public void Setup(ClubGameData data)
    {
        if (dateTimeText != null)  dateTimeText.text = FormatDateTime(data.CreatedAt);
        if (tableNameText != null) tableNameText.text = data.TableName;
        if (idText != null)        idText.text = $"ID: {data.CreatorId}";
        if (variantText != null)   variantText.text = data.Variant;
        if (rakeText != null)      rakeText.text = $"{data.Rake}%";
        if (blindsText != null)    blindsText.text = $"{data.SmallBlind}/{data.BigBlind}";
        if (feeText != null)       feeText.text = data.Fee.ToString();
    }

    // "06/19\n20:18" — date on the first line, time on the second.
    private static string FormatDateTime(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out DateTime dt))
            return $"{dt:MM/dd}\n{dt:HH:mm}";

        return raw;   // fallback if the format is unexpected
    }
}
