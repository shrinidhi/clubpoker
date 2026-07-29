using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CareerHandPlayerPrefab : MonoBehaviour
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
        if (PlayerName != null)
            PlayerName.text = playerName;

        if (PairText != null)
            PairText.text =
                string.IsNullOrEmpty(handName)
                    ? "fold"
                    : handName;

        if (SeatName != null)
        {
            SeatName.text = seatName;
            SeatName.gameObject.SetActive(
                !string.IsNullOrEmpty(seatName)
            );
        }

        if (Chips != null)
        {
            Chips.gameObject.SetActive(showChips);

            if (showChips)
            {
                Chips.text =
                    (chipDifference >= 0 ? "+" : "") +
                    chipDifference;
            }
        }

        if (isWinner)
        {
            if (PlayerName != null)
                PlayerName.color =
                    new Color32(255, 255, 255, 255);

            if (PairText != null)
                PairText.color =
                    new Color32(255, 255, 255, 255);

            if (Chips != null)
                Chips.color =
                    new Color32(36, 157, 178, 255);
        }
        else
        {
            if (PlayerName != null)
                PlayerName.color =
                    new Color32(250, 205, 133, 255);

            if (PairText != null)
                PairText.color =
                    new Color32(250, 205, 133, 255);

            if (Chips != null)
                Chips.color =
                    new Color32(250, 205, 133, 255);
        }

        Clear(HoleCardContent);
        Clear(CommunityCardContent);

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

            CareerCardPrefab cardPrefab =
                obj.GetComponent<CareerCardPrefab>();

            if (cardPrefab == null)
            {
                Destroy(obj);
                continue;
            }

            cardPrefab.SetCard(card);

            bool isBest =
                IsBestHandCard(card, bestHandCards);

            cardPrefab.SetGray(
                !string.IsNullOrEmpty(card) && !isBest
            );
        }

        if (communityCards != null)
        {
            foreach (string card in communityCards)
            {
                GameObject obj =
                    Instantiate(CardPrefab, CommunityCardContent);

                CareerCardPrefab cardPrefab =
                    obj.GetComponent<CareerCardPrefab>();

                if (cardPrefab == null)
                {
                    Destroy(obj);
                    continue;
                }

                cardPrefab.SetCard(card);

                bool isBest =
                    IsBestHandCard(card, bestHandCards);

                cardPrefab.SetGray(!isBest);
            }
        }
    }

    private bool IsBestHandCard(
        string card,
        List<string> bestHandCards)
    {
        if (string.IsNullOrEmpty(card) ||
            bestHandCards == null ||
            bestHandCards.Count == 0)
            return false;

        string cardKey = ConvertCardKey(card);

        foreach (string bestCard in bestHandCards)
        {
            if (ConvertCardKey(bestCard) == cardKey)
                return true;
        }

        return false;
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

    private void Clear(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}