using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;
public class LeaderboardInformationScript : MonoBehaviour
{
    public Button CloseButton;
    public Button ConfirmButton;
    public Text Msg;
    // Start is called before the first frame update
    void Start()
    {
        CloseButton.onClick.AddListener(CloseScreen);
        ConfirmButton.onClick.AddListener(CloseScreen);
    }
    private void OnEnable()
    {
        if(LeaderboardManager.Instance.Type == "HANDS")
        {
            Msg.text = Loc.Get("lb.info_hands");
        }
        else if (LeaderboardManager.Instance.Type == "WINNINGS")
        {
            Msg.text = Loc.Get("lb.info_winnings");
        }
        else
        {
            Msg.text = Loc.Get("lb.info_mtt");
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
