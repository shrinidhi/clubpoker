using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandPlayerPrefab : MonoBehaviour
{
    public GameObject WinTrophy;
    public Text PlayerName;
    public Text PairText;
    public Text Chips;
    public Transform CardContent;
    public GameObject CardPrefab;
    public RectTransform HandPlayer;

    public Image Prefab_BG;
    public Sprite WinnerSprite;
    public Sprite LoserSripte;

    public void SetData(
        string playerName,
        string handName,
        int chipDifference,
        bool isWinner,
        List<string> holeCards,
        SmallCardSO smallCardSO,
        bool showChips = true)
    {
        PlayerName.text = playerName;
        PairText.text = handName;

        WinTrophy.SetActive(isWinner);

        if (showChips)
        {
            Chips.gameObject.SetActive(true);

            Chips.text = (chipDifference >= 0 ? "+" : "") + chipDifference;
            Chips.color = isWinner ? Color.green : Color.red;
        }
        else
        {
            Chips.gameObject.SetActive(false);
        }

        Prefab_BG.sprite = isWinner ? WinnerSprite : LoserSripte;

        foreach (Transform child in CardContent)
        {
            Destroy(child.gameObject);
        }

        if (holeCards == null || holeCards.Count == 0)
        {
            HandPlayer.sizeDelta = new Vector2(
                HandPlayer.sizeDelta.x,
                90f);

            return;
        }

        foreach (var card in holeCards)
        {
            var cardObj = Instantiate(CardPrefab, CardContent);

            cardObj.GetComponent<CardPrefab>()
                .SetCard(card, smallCardSO);
        }
    }
}