using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class SelectLeaderboardOption : MonoBehaviour
{
    public Button BackButton;
    public Button HandsButton;
    public Button ScoreButton;
    public Button WinningButton;
    public Button LeaderBoardTemplateButton;

    public GameObject CreateLeaderBoardScreen;
    public GameObject LeaderBoardTempleteScreen;
    
    // Start is called before the first frame update
    void Start()
    {
        BackButton.onClick.AddListener(BackButtonOnTap);
        HandsButton.onClick.AddListener(HandsButtonOnTap);
        ScoreButton.onClick.AddListener(ScoreButtonOnTap);
        WinningButton.onClick.AddListener(WinningButtonOnTap);
        LeaderBoardTemplateButton.onClick.AddListener(LeaderBoardTemplateButtonOnTap);
    }

    void BackButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    void HandsButtonOnTap()
    {
        LeaderboardManager.Instance.Type = "HANDS";
        CreateLeaderBoard();
    }

    void ScoreButtonOnTap()
    {
        LeaderboardManager.Instance.Type = "SCORE";
        CreateLeaderBoard();
    }

    void WinningButtonOnTap()
    {
        LeaderboardManager.Instance.Type = "WINNINGS";
        CreateLeaderBoard();
    }

    void CreateLeaderBoard()
    {
        CreateLeaderBoardScreen.SetActive(true);
    }

    void LeaderBoardTemplateButtonOnTap()
    {
        LeaderBoardTempleteScreen.SetActive(true);
    }



}
