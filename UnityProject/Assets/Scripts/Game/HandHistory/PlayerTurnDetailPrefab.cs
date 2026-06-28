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

    public void SetData(
        string playerName,
        string action,
        float amount,
        int chipsAfter)
    {
        PlayerName.text = playerName;

        ActionText.text = action +
            (amount > 0 ? " " + amount : "");

        Chips.text = chipsAfter.ToString();

        SetActionBackground(action);
    }

    void SetActionBackground(string action)
    {
        action = action.ToUpper().Trim();

        Sprite sprite = ActionSprite[3]; 

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
                sprite = ActionSprite[2];
                break;
        }

        Action_BG.sprite = sprite;
    }
}