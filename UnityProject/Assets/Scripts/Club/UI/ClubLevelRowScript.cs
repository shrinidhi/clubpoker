using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row in the Club Level list. Shows label + max agents/members + diamond cost.
/// Clickable only when the level is above the club's current level (an upgrade).
/// </summary>
public class ClubLevelRowScript : MonoBehaviour
{
    public TextMeshProUGUI Label_Text;
    public TextMeshProUGUI MaxAgents_Text;
    public TextMeshProUGUI MaxMembers_Text;
    public TextMeshProUGUI DiamondCost_Text;

    public TextMeshProUGUI LevelTag_Text; 

    public Button Row_Button;

    private ClubLevelItem _item;
    private Action<ClubLevelItem> _onClick;

    public void Setup(ClubLevelItem item, bool isCurrent, bool isUpgradeable,
                      Action<ClubLevelItem> onClick)
    {
        _item = item;
        _onClick = onClick;

        if (Label_Text       != null) Label_Text.text       = "Club Level " + item.Level.ToString();
        if (LevelTag_Text    != null) LevelTag_Text.text    = item.Level.ToString();
        if (MaxAgents_Text   != null) MaxAgents_Text.text   = item.MaxAgents.ToString();
        if (MaxMembers_Text  != null) MaxMembers_Text.text  = item.MaxMembers.ToString();
        if (DiamondCost_Text != null) DiamondCost_Text.text = item.DiamondCost.ToString("N0");


        if (Row_Button != null)
        {
            Row_Button.onClick.RemoveAllListeners();
            Row_Button.interactable = isUpgradeable;
            if (isUpgradeable)
                Row_Button.onClick.AddListener(() => _onClick?.Invoke(_item));
        }
    }
}
