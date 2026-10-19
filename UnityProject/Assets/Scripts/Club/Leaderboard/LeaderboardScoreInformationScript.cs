using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class LeaderboardScoreInformationScript : MonoBehaviour
{
    public Button CloseButton;
    public Button ConfirmButton;
    public Text Msg;
    public LeaderboardScoreScreen LeaderboardScoreScreen;
    // Start is called before the first frame update
    void Start()
    {
        CloseButton.onClick.AddListener(CloseScreen);
        ConfirmButton.onClick.AddListener(CloseScreen);
    }
    private void OnEnable()
    {
        if (LeaderboardScoreScreen.type == "HANDS")
        {
            Msg.text = "Hands Leaderboard is ranked based on the number of hands. During Leaderboard, the hands played at the eligible tables will be included in the accumulated hands of the Leaderboard.";
        }
        else if (LeaderboardScoreScreen.type == "WINNINGS")
        {
            Msg.text = "Winnings Leaderboard is ranked based on the accumulated winnings. If winnings <= 0, the player will not be included in Leaderboard.";
        }
        else
        {
            Msg.text = "The Regular Scores Ranking is ranked based on the accumulated scores of Regular games.During the Leaderboard, play at eligible tables for a chance to earn scores. During Double Scores, the scores earned are doubled.";
        }
    }



    void CloseScreen()
    {
        gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {

    }
}

