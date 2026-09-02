using ClubPoker.Game;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CareerHandHistoryCard : MonoBehaviour
{
    public Image CardImage;
    public List<CardSpriteData> CardSprites = new List<CardSpriteData>();

    public void SetCard(string serverCard)
    {
        if (CardImage == null)
            return;

        string cardKey = ConvertCardKey(serverCard);

        CardSpriteData cardData = CardSprites.Find(x =>
            x != null &&
            !string.IsNullOrEmpty(x.CardName) &&
            x.CardName.ToUpper() == cardKey
        );

        if (cardData != null && cardData.CardSprite != null)
        {
            CardImage.sprite = cardData.CardSprite;
            CardImage.enabled = true;
        }
        else
        {
            Debug.LogWarning("Card sprite not found: " + serverCard + " | Converted: " + cardKey);
            CardImage.enabled = false;
        }
    }

    private string ConvertCardKey(string serverCard)
    {
        if (string.IsNullOrEmpty(serverCard))
            return "";

        string value = serverCard
            .Replace("♥", "H")
            .Replace("♦", "D")
            .Replace("♣", "C")
            .Replace("♠", "S")
            .ToUpper()
            .Trim();


        return value;
    }
}