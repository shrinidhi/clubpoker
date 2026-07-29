using ClubPoker.Game;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CareerCardPrefab : MonoBehaviour
{
    public Image CardImage;
    public List<CardSpriteData> CardSprites =
        new List<CardSpriteData>();

    public Sprite BlackCoverCard;

    public void SetCard(string serverCard)
    {
        if (CardImage == null)
            return;

        if (string.IsNullOrEmpty(serverCard))
        {
            CardImage.sprite = BlackCoverCard;
            CardImage.enabled = true;
            CardImage.color = Color.white;
            return;
        }

        string key = ConvertCardKey(serverCard);

        CardSpriteData data =
            CardSprites.Find(
                x => x != null &&
                     !string.IsNullOrEmpty(x.CardName) &&
                     ConvertCardKey(x.CardName) == key
            );

        CardImage.sprite =
            data != null && data.CardSprite != null
                ? data.CardSprite
                : BlackCoverCard;

        CardImage.enabled = true;
        CardImage.color = Color.white;
    }

    public void SetGray(bool isGray)
    {
        if (CardImage == null)
            return;

        CardImage.color =
            isGray
                ? new Color(0.4f, 0.4f, 0.4f, 1f)
                : Color.white;
    }

    private string ConvertCardKey(string serverCard)
    {
        if (string.IsNullOrEmpty(serverCard))
            return "";

        return serverCard
            .Replace("♥", "H")
            .Replace("♦", "D")
            .Replace("♣", "C")
            .Replace("♠", "S")
            .Replace("10", "T")
            .Trim()
            .ToUpper();
    }
}