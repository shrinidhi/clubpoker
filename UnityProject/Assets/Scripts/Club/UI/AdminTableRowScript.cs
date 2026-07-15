using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Networking.Models;

/// <summary>
/// One row in the scroll-message "Choose a table" list: table name + variant + blinds.
/// </summary>
public class AdminTableRowScript : MonoBehaviour
{
    public TextMeshProUGUI Name_Text;
    public TextMeshProUGUI Variant_Text;
    public TextMeshProUGUI Blinds_Text;      // "10/20"
    public Button Row_Button;

    private ClubTableData _table;
    private Action<ClubTableData> _onClick;

    public void Setup(ClubTableData table, Action<ClubTableData> onClick)
    {
        _table = table;
        _onClick = onClick;

        if (Name_Text    != null) Name_Text.text    = table.Name;
        if (Variant_Text != null) Variant_Text.text = PrettyVariant(table.Variant);
        if (Blinds_Text  != null) Blinds_Text.text  = $"{table.SmallBlind}/{table.BigBlind}";

        if (Row_Button != null)
        {
            Row_Button.onClick.RemoveAllListeners();
            Row_Button.onClick.AddListener(() => _onClick?.Invoke(_table));
        }
    }

    // "texas_holdem" → "Texas Holdem", "omaha" → "Omaha"
    private static string PrettyVariant(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string[] parts = raw.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }
}
