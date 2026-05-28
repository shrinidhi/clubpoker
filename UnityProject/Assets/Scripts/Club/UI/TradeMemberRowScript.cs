using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TradeMemberRowScript : MonoBehaviour
{
    [Header("Member Info")]
    public Image Avatar_Image;
    public TextMeshProUGUI Name_Text;
    public TextMeshProUGUI Id_Text;
    public Image RoleBadge_Image;
    public TextMeshProUGUI RoleBadge_Text;
    public TextMeshProUGUI Nickname_Text;
    public TextMeshProUGUI Chips_Text;

    [Header("Selection")]
    public Button Row_Button;
    public Toggle Select_Toggle;

    public string MemberId => _memberId;
    public ClubMember Member { get; private set; }

    private string _memberId;
    private bool _isSelected;
    private Action<string, bool> _onSelectionChanged;

    public void Setup(ClubMember member, Action<string, bool> onSelectionChanged)
    {
        _memberId = member.Id;
        Member = member;
        _onSelectionChanged = onSelectionChanged;

        Name_Text.text     = member.Username;
        Id_Text.text       = "ID: " + member.Id.Split('-')[0];
        Nickname_Text.text = "Nickname: " + (string.IsNullOrEmpty(member.Nickname) ? member.Username : member.Nickname);
        Chips_Text.text    = member.Chips.ToString("N0");

        SetRoleBadge(member.Role);

        _isSelected = false;

        if (Select_Toggle != null)
        {
            Select_Toggle.onValueChanged.RemoveAllListeners();
            Select_Toggle.isOn = false;
        }

        if (Row_Button != null)
        {
            Row_Button.onClick.RemoveAllListeners();
            if (onSelectionChanged != null)
                Row_Button.onClick.AddListener(OnRowTapped);
        }
    }

    private void OnRowTapped()
    {
        _isSelected = !_isSelected;
        Select_Toggle.isOn = _isSelected;
        _onSelectionChanged?.Invoke(_memberId, _isSelected);
    }

    private void SetRoleBadge(string role)
    {
        switch (role?.ToUpper())
        {
            case "CREATOR":
                RoleBadge_Text.text   = "C";
                RoleBadge_Image.color = new Color(1f, 0.75f, 0f);
                break;
            case "MANAGER":
                RoleBadge_Text.text   = "M";
                RoleBadge_Image.color = new Color(0.2f, 0.6f, 1f);
                break;
            case "AGENT":
                RoleBadge_Text.text   = "A";
                RoleBadge_Image.color = new Color(0.6f, 0.2f, 1f);
                break;
            default:
                RoleBadge_Text.text   = "M";
                RoleBadge_Image.color = new Color(0.4f, 0.4f, 0.4f);
                break;
        }
    }

    public void Deselect()
    {
        _isSelected = false;
        if (Select_Toggle != null) Select_Toggle.isOn = false;
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (Select_Toggle != null) Select_Toggle.isOn = selected;
    }
}
