using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Game;

public class CardPrefab : MonoBehaviour
{
    public Image CardImage;
    public List<CardSpriteData> CardSprites = new List<CardSpriteData>();
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

        Sprite sprite = null;

        CardSpriteData data =
            CardSprites.Find(x => x.CardName == key);

        if (data != null)
            sprite = data.CardSprite;

        if (sprite != null)
        {
            CardImage.sprite = sprite;
        }
        else
        {
            CardImage.sprite = BlackCoverCard;
        }

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
            .ToUpper();
    }
}