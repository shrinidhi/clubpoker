using UnityEngine;
using UnityEngine.UI;

public class CardPrefab : MonoBehaviour
{
    public Text CardText;
    public Image CardImage;

    public void SetCard(string cardCode, SmallCardSO cardSO)
    {
        string value;
        string suitKey;

        GetCardData(cardCode, out value, out suitKey);

        CardText.text = value;

        SmallCardData cardData =
            cardSO.SmallCard.Find(x => x.CardName == suitKey);

        if (cardData != null && cardData.CardImage != null)
        {
            CardImage.sprite = cardData.CardImage;
            CardImage.enabled = true;
        }
        else
        {
            CardImage.enabled = false;
        }

        CardText.color = IsRedSuit(suitKey) ? Color.red : Color.black;
    }

    void GetCardData(string cardCode, out string value, out string suit)
    {
        value = cardCode.Substring(0, cardCode.Length - 1);
        string suitSymbol = cardCode.Substring(cardCode.Length - 1);

        switch (suitSymbol)
        {
            case "♥":
                suit = "H";
                break;

            case "♦":
                suit = "D";
                break;

            case "♠":
                suit = "S";
                break;

            case "♣":
                suit = "C";
                break;

            default:
                suit = suitSymbol.ToUpper();
                break;
        }
    }

    bool IsRedSuit(string suit)
    {
        return suit == "H" || suit == "D";
    }
}