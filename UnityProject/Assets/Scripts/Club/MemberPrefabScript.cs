using System;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class MemberPrefabScript : MonoBehaviour
{
    public Text PlayerName;
    public Text PlayerID;
    public Text PlayerNickName;
    public Text Chips_Count;

    public Sprite Creator_BG;
    public Sprite Agent_Member_BG;
    public Image Type_BG;
    public Text Type_Text;

    public Button MemberButton;

    private ClubMemberData memberData;
    private Action<ClubMemberData> onMemberClick;

    public void Setup(
        ClubMemberData data,
        Action<ClubMemberData> clickCallback)
    {
        memberData = data;
        onMemberClick = clickCallback;

        PlayerName.text = data.Username;

        PlayerID.text = string.IsNullOrEmpty(data.UserId)
            ? ""
            : data.UserId.Substring(0, 8);

        PlayerNickName.text = "Nickname: " + data.Username;
        Chips_Count.text = data.Chips.ToString();

        Type_Text.text = string.IsNullOrEmpty(data.Role)
            ? ""
            : data.Role.Substring(0, 1).ToUpper();

        if (Type_BG != null)
        {
            if (data.Role == "CREATOR")
                Type_BG.sprite = Creator_BG;
            else
                Type_BG.sprite = Agent_Member_BG;
        }

        MemberButton.onClick.RemoveAllListeners();
        MemberButton.onClick.AddListener(OnMemberButtonClick);
    }

    private void OnMemberButtonClick()
    {
        onMemberClick?.Invoke(memberData);
    }
}