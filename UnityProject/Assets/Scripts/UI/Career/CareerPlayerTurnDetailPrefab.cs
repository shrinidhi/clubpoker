using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CareerPlayerTurnDetailPrefab : MonoBehaviour
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
        if (PlayerName != null)
            PlayerName.text = playerName;

        string displayAction = GetDisplayAction(action);

        if (ActionText != null)
        {
            ActionText.text =
                displayAction +
                (amount > 0
                    ? " " + FormatAmount(amount)
                    : "");
        }

        if (Chips != null)
            Chips.text = chipsAfter.ToString();

        if (seatname != null)
        {
            seatname.text = seatNameText;
            seatname.gameObject.SetActive(
                !string.IsNullOrEmpty(seatNameText)
            );
        }

        SetActionBackground(action);
    }

    private string GetDisplayAction(string action)
    {
        if (string.IsNullOrEmpty(action))
            return "";

        switch (action.ToUpper().Trim())
        {
            case "ALL_IN":
                return "All In";

            case "SB":
                return "SB";

            case "BB":
                return "BB";

            default:
                string lower = action.ToLower().Trim();

                return char.ToUpper(lower[0]) +
                       lower.Substring(1);
        }
    }

    private string FormatAmount(float amount)
    {
        if (Mathf.Approximately(amount % 1f, 0f))
            return ((int)amount).ToString();

        return amount.ToString("0.##");
    }

    private void SetActionBackground(string action)
    {
        if (Action_BG == null ||
            ActionSprite == null ||
            ActionSprite.Count == 0)
            return;

        string value =
            string.IsNullOrEmpty(action)
                ? ""
                : action.ToUpper().Trim();

        int spriteIndex =
            ActionSprite.Count > 5
                ? 5
                : ActionSprite.Count - 1;

        switch (value)
        {
            case "FOLD":
                spriteIndex = 0;
                break;

            case "CHECK":
                spriteIndex = 1;
                break;

            case "CALL":
                spriteIndex = 2;
                break;

            case "RAISE":
                spriteIndex = 3;
                break;

            case "ALL_IN":
            case "ALL IN":
                spriteIndex = 4;
                break;
        }

        if (spriteIndex >= 0 &&
            spriteIndex < ActionSprite.Count)
        {
            Action_BG.sprite =
                ActionSprite[spriteIndex];
        }
    }

    public void SetRowColor(int index)
    {
        if (Chips == null)
            return;

        Color whiteColor =
            new Color32(255, 255, 255, 255);

        Color goldColor =
            new Color32(250, 205, 133, 255);

        Chips.color =
            index % 2 == 0
                ? whiteColor
                : goldColor;
    }
}
