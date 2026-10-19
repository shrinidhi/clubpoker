using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class MemberPrefabScript : MonoBehaviour
{
    public Text PlayerName;
    public Text PlayerID;
    public Text PlayerNickName;
    public Text Chips_Count;
    public Text FilterText;
    public GameObject ChipsCountBG;
    public Sprite Creator_BG;
    public Sprite Agent_Member_BG;
    public Image Type_BG;
    public Text Type_Text;
    public Button MemberButton;
    public GameObject Online_Dot;

    private ClubMemberData memberData;
    private Action<ClubMemberData> onMemberClick;

    private void Awake()
    {
        if (MemberButton != null)
            MemberButton.onClick.AddListener(OnMemberButtonClick);
    }

    public void Setup(
        ClubMemberData data,
        Action<ClubMemberData> clickCallback,
        bool isOnline,
        string filterKey = "chips")
    {
        memberData = data;
        onMemberClick = clickCallback;

        string role = data != null ? NormalizeRole(data.Role) : "";
        string username = data != null ? data.Username : "";

        SetText(PlayerName, data != null ? username + " (" + role + ")" : "");
        SetText(PlayerID, data != null ? "ID : " + data.PlayerCode : "");
        SetText(PlayerNickName, data != null ? "Nickname: " + username : "");
        SetText(Chips_Count, data != null ? FormatAmount(data.Chips) : "");

        string roleLabel = string.IsNullOrEmpty(role) ? "" : role.Substring(0, 1);
        if (data != null && role == "MANAGER" && data.IsTableManager)
            roleLabel = "TM";

        SetText(Type_Text, roleLabel);

        if (Online_Dot != null)
            Online_Dot.SetActive(data != null && isOnline);

        if (Type_BG != null)
        {
            Type_BG.gameObject.SetActive(data != null && role != "MEMBER");
            Type_BG.sprite = role == "CREATOR" ? Creator_BG : Agent_Member_BG;
        }

        if (MemberButton != null)
            MemberButton.interactable = data != null;

        SetFilter(filterKey);
    }

    public void SetFilter(string filterKey)
    {
        string key = (filterKey ?? "chips").Trim().ToLowerInvariant();
        bool showChips = key == "chips";

        if (ChipsCountBG != null)
            ChipsCountBG.SetActive(showChips);

        if (Chips_Count != null)
            Chips_Count.gameObject.SetActive(showChips);

        if (FilterText != null)
            FilterText.gameObject.SetActive(!showChips);

        if (memberData == null)
        {
            SetText(FilterText, "");
            return;
        }

        switch (key)
        {
            case "hands":
                SetText(FilterText, FormatAmount(memberData.HandsPlayed));
                break;
            case "winnings":
                SetText(FilterText, FormatAmount(memberData.TotalWinnings));
                break;
            case "fees":
                SetText(FilterText, FormatAmount(memberData.TotalFee));
                break;
            case "lastlogin":
                SetText(FilterText, FormatLoginDate(memberData.LastLoginAt));
                break;
            default:
                SetText(FilterText, "");
                break;
        }
    }

    private void OnMemberButtonClick()
    {
        if (memberData != null)
            onMemberClick?.Invoke(memberData);
    }

    private static string FormatLoginDate(string value)
    {
        if (!DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset date))
            return "-";

        return date.ToLocalTime().ToString(
            "HH:mm\nMM/dd", CultureInfo.InvariantCulture);
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("#,0.##", CultureInfo.InvariantCulture);
    }

    private static string NormalizeRole(string role)
    {
        return string.IsNullOrWhiteSpace(role)
            ? "MEMBER"
            : role.Trim().Replace(" ", "_").Replace("-", "_").ToUpperInvariant();
    }

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = value ?? "";
    }

    private void OnDestroy()
    {
        if (MemberButton != null)
            MemberButton.onClick.RemoveListener(OnMemberButtonClick);
    }
}
