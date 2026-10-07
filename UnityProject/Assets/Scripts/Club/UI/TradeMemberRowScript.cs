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

    [Header("Role Sprites")]
    public Sprite CreatorBadge_Sprite;
    public Sprite OtherBadge_Sprite;
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

        // Name line = username, Nickname line = the player's nickname (username
        // until they set one), ID = public player code. member.Id (userId) stays
        // internal — it's what gets sent to the chips API, never displayed.
        Name_Text.text     = member.Username;
        Id_Text.text       = "ID: " + (string.IsNullOrEmpty(member.PlayerCode) ? "-" : member.PlayerCode);
        Nickname_Text.text = "Nickname: " + member.DisplayName;
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
        string letter = role?.ToUpper() switch
        {
            "CREATOR" => "C",
            "MANAGER" => "M",
            "AGENT"   => "A",
            _         => ""
        };

        // A plain MEMBER has no letter, so the badge would render as an empty
        // blob — hide it instead of drawing OtherBadge_Sprite with no text.
        bool hasBadge = letter.Length > 0;

        if (RoleBadge_Image != null)
        {
            RoleBadge_Image.gameObject.SetActive(hasBadge);
            if (hasBadge)
                RoleBadge_Image.sprite = letter == "C" ? CreatorBadge_Sprite : OtherBadge_Sprite;
        }

        if (RoleBadge_Text != null)
        {
            RoleBadge_Text.gameObject.SetActive(hasBadge);
            RoleBadge_Text.text = letter;
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
