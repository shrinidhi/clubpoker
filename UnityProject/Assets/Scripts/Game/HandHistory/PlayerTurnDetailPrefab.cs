using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerTurnDetailPrefab : MonoBehaviour
{
    public Text PlayerName;
    public Text ActionText;
    public Text Chips;

    public Image Action_BG;
    public List<Sprite> ActionSprite;
    public Text seatname;
    public void SetData(
     string playerName,
     string action,
     float amount,
     int chipsAfter,
     string seatNameText)
    {
        PlayerName.text = playerName;
        string displayAction = action.Replace("_", " ");

        ActionText.text = displayAction + (amount > 0 ? " " + amount : "");
       
        Chips.text = chipsAfter.ToString();

        seatname.text = seatNameText;
        seatname.gameObject.SetActive(!string.IsNullOrEmpty(seatNameText));

        SetActionBackground(action);
    }

    void SetActionBackground(string action)
    {
        action = action.ToUpper().Trim();

        Sprite sprite = ActionSprite[5]; 

        switch (action)
        {
            case "FOLD":
                sprite = ActionSprite[0];
                break;

            case "CHECK":
                sprite = ActionSprite[1];
                break;

            case "CALL":
                sprite = ActionSprite[2];
                break;

            case "RAISE":
                sprite = ActionSprite[3];
                break;

            case "ALL_IN":
            case "ALL IN":
                sprite = ActionSprite[4];
                break;
        }

        Action_BG.sprite = sprite;
    }
    public void SetRowColor(int index)
    {
        Color whiteColor = new Color32(255, 255, 255, 255);
        Color goldColor = new Color32(250, 205, 133, 255);

        Chips.color = (index % 2 == 0) ? whiteColor : goldColor;
    }

}