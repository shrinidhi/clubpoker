using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

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
        JackPotID.text = "Jackpot Id : " + data.JackpotId;
        TableID.text = "Table Id : " + data.TableId;
        WinnerName.text = "Winner : " + data.WinnerName;
        WinnerHand.text = data.WinnerHand;
        WinnerPayout.text = data.WinnerPayout.ToString();
        LoserName.text = "Loser Name : " + data.LoserName;
        LoserHand.text = data.LoserHand;
        LoserPayout.text = data.LoserPayout.ToString();
    }

}