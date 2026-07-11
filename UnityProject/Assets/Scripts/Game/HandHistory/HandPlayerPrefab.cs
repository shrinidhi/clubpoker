using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandPlayerPrefab : MonoBehaviour
{
    public Text PlayerName;
    public Text PairText;
    public Text Chips;
    public Transform HoleCardContent;
    public GameObject CardPrefab;

    public Transform CommunityCardContent;

    public RectTransform HandPlayer;

    public Text SeatName;

    public void SetData(
    string playerName,
    string handName,
    int chipDifference,
    bool isWinner,
    List<string> holeCards,
    List<string> communityCards,
    List<string> bestHandCards,
    string seatName,
    bool showChips = true,
    int fallbackHoleCardCount = 0)
    {
        PlayerName.text = playerName;
        PairText.text = string.IsNullOrEmpty(handName) ? "fold" : handName;
        SeatName.text = seatName;
        SeatName.gameObject.SetActive(!string.IsNullOrEmpty(seatName));
        if (showChips)
        {
            Chips.gameObject.SetActive(true);
            Chips.text = (chipDifference >= 0 ? "+" : "") + chipDifference;
            
        }
        else
        {
            Chips.gameObject.SetActive(false);
        }

        if (isWinner)
        {
            PlayerName.color = new Color32(255,255,255,255);
            PairText.color = new Color32(255, 255, 255, 255);
            Chips.color = new Color32(36, 157, 178, 255);
        }
        else
        {
            PlayerName.color = new Color32(250, 205, 133, 255);
            PairText.color = new Color32(250, 205, 133, 255);
            Chips.color = new Color32(250, 205, 133, 255);
        }


        foreach (Transform child in HoleCardContent)
            Destroy(child.gameObject);

        foreach (Transform child in CommunityCardContent)
            Destroy(child.gameObject);

        int holeCount =
            holeCards != null && holeCards.Count > 0
                ? holeCards.Count
                : fallbackHoleCardCount;

        for (int i = 0; i < holeCount; i++)
        {
            string card =
                holeCards != null && i < holeCards.Count
                    ? holeCards[i]
                    : "";

            GameObject obj =
                Instantiate(CardPrefab, HoleCardContent);

            CardPrefab cardPrefab =
                obj.GetComponent<CardPrefab>();

            cardPrefab.SetCard(card);

            bool isBest =
                IsBestHandCard(card, bestHandCards);

            cardPrefab.SetGray(!isBest);
        }

        if (communityCards != null)
        {
            foreach (string card in communityCards)
            {
                GameObject obj =
                    Instantiate(CardPrefab, CommunityCardContent);

                CardPrefab cardPrefab =
                    obj.GetComponent<CardPrefab>();

                cardPrefab.SetCard(card);

                if (string.IsNullOrEmpty(card))
                {
                    cardPrefab.SetGray(false);
                }
                else
                {
                    bool isBest =
                        IsBestHandCard(card, bestHandCards);

                    cardPrefab.SetGray(!isBest);
                }
            }
        }
    }

    bool IsBestHandCard(
        string card,
        List<string> bestHandCards)
    {
        if (string.IsNullOrEmpty(card))
            return false;

        if (bestHandCards == null ||
            bestHandCards.Count == 0)
            return false;

        string cardKey =
            ConvertCardKey(card);

        foreach (string bestCard in bestHandCards)
        {
            if (ConvertCardKey(bestCard) == cardKey)
                return true;
        }

        return false;
    }

    string ConvertCardKey(string serverCard)
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