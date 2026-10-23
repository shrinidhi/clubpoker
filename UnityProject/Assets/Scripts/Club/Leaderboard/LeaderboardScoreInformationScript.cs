using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;
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
            Msg.text = Loc.Get("lb.info_hands");
        }
        else if (LeaderboardScoreScreen.type == "WINNINGS")
        {
            Msg.text = Loc.Get("lb.info_winnings");
        }
        else
        {
            Msg.text = Loc.Get("lb.info_regular");
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

