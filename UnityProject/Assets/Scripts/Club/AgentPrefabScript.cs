using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class AgentPrefabScript : MonoBehaviour
{
    public Text PlayerName;
    public Text Playerid;
    public Text PlayerNickName;
    public Text PlayerType;

    public Text DownLineMember_Count;
    public Text DownLineChips_Count;
    public Text CreditBalance_Count;

    public Text Winning_ThisWeak_Count;
    public Text Winning_LastWeak_Count;
    public Text Winning_Total_Count;

    public Text Fee_ThisWeak_Count;
    public Text Fee_LastWeak_Count;
    public Text Fee_Total_Count;

    public Text I_EVChop_ThisWeak_Count;
    public Text I_EVChop_LastWeak_Count;
    public Text I_EVChop_Total_Count;

    public void Setup(ClubMemberData member, AgentDataApiResponse agentData)
    {
        PlayerName.text = member.Username;
        Playerid.text = member.UserId.Substring(0, 6);
        PlayerNickName.text = "Nickname : " + member.Username;
           

        PlayerType.text = string.IsNullOrEmpty(member.Role)
            ? ""
            : member.Role.Substring(0, 1);

        DownLineMember_Count.text = agentData != null
            ? agentData.DownlineCount.ToString()
            : "0";

        DownLineChips_Count.text = agentData != null
            ? agentData.DownlineChips.ToString()
            : "0";

        CreditBalance_Count.text = agentData != null
            ? agentData.AgentCredit.ToString()
            : "0";

        SetStats(agentData);
    }

    private void SetStats(AgentDataApiResponse agentData)
    {
        if (agentData == null || agentData.Stats == null)
        {
            SetAllStatsZero();
            return;
        }

        Winning_ThisWeak_Count.text = agentData.Stats.ThisWeek.Winnings.ToString();
        Winning_LastWeak_Count.text = agentData.Stats.LastWeek.Winnings.ToString();
        Winning_Total_Count.text = agentData.Stats.Total.Winnings.ToString();

        Fee_ThisWeak_Count.text = agentData.Stats.ThisWeek.Fee.ToString();
        Fee_LastWeak_Count.text = agentData.Stats.LastWeek.Fee.ToString();
        Fee_Total_Count.text = agentData.Stats.Total.Fee.ToString();

        I_EVChop_ThisWeak_Count.text = agentData.Stats.ThisWeek.Hands.ToString();
        I_EVChop_LastWeak_Count.text = agentData.Stats.LastWeek.Hands.ToString();
        I_EVChop_Total_Count.text = agentData.Stats.Total.Hands.ToString();
    }

    private void SetAllStatsZero()
    {
        Winning_ThisWeak_Count.text = "0";
        Winning_LastWeak_Count.text = "0";
        Winning_Total_Count.text = "0";

        Fee_ThisWeak_Count.text = "0";
        Fee_LastWeak_Count.text = "0";
        Fee_Total_Count.text = "0";

        I_EVChop_ThisWeak_Count.text = "0";
        I_EVChop_LastWeak_Count.text = "0";
        I_EVChop_Total_Count.text = "0";
    }
}