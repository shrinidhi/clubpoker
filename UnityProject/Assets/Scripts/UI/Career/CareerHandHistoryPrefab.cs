using System.Collections.Generic;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class CareerHandHistoryPrefab : MonoBehaviour
{
    public Transform CardContent;
    public GameObject CardPrefab;
    public Text NetResult;
    public Button PrefabButton;

    private CareerHandHistoryItem handData;
    private CareerHandHistoryPanel historyPanel;
    private List<CareerHandHistoryItem> allHands;

    public void SetData(
        CareerHandHistoryItem data,
        List<CareerHandHistoryItem> hands,
        CareerHandHistoryPanel panel)
    {
        handData = data;
        allHands = hands;
        historyPanel = panel;

        ClearCards();

        if (data == null)
            return;

        if (data.YourCards != null)
        {
            foreach (string card in data.YourCards)
            {
                GameObject obj =
                    Instantiate(CardPrefab, CardContent);

                CareerHandHistoryCard cardPrefab =
                    obj.GetComponent<CareerHandHistoryCard>();

                if (cardPrefab != null)
                    cardPrefab.SetCard(card);
                else
                    Destroy(obj);
            }
        }

        if (NetResult != null)
        {
            NetResult.text =
                (data.NetResult > 0 ? "+" : "") +
                data.NetResult;
        }

        if (PrefabButton != null)
        {
            PrefabButton.onClick.RemoveAllListeners();
            PrefabButton.onClick.AddListener(OpenHandDetail);
        }
    }

    private void OpenHandDetail()
    {
        if (handData == null ||
            historyPanel == null)
            return;

        historyPanel.Open(allHands, handData.HandId);
    }

    private void ClearCards()
    {
        if (CardContent == null)
            return;

        for (int i = CardContent.childCount - 1; i >= 0; i--)
            Destroy(CardContent.GetChild(i).gameObject);
    }
}