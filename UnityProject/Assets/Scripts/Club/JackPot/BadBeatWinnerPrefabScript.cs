using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;

public class BadBeatWinnerPrefabScript : MonoBehaviour
{
    public Text JackPotID;
    public Text TableID;

    public Text WinnerName;
    public Text WinnerHand;
    public Text WinnerPayout;

    public Text LoserName;
    public Text LoserHand;
    public Text LoserPayout;

    public void SetData(ClubJackpotWinnerData data)
    {
        JackPotID.text = $"{Loc.Get("jackpot.id_label")} : {data.JackpotId}";
        TableID.text = $"{Loc.Get("table_id_label")} : {data.TableId}";
        WinnerName.text = $"{Loc.Get("winner_label")} : {data.WinnerName}";
        WinnerHand.text = data.WinnerHand;
        WinnerPayout.text = data.WinnerPayout.ToString();
        LoserName.text = $"{Loc.Get("loser_name_label")} : {data.LoserName}";
        LoserHand.text = data.LoserHand;
        LoserPayout.text = data.LoserPayout.ToString();
    }

}