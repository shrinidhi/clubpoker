using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RealTimeResultPlayerPrefab : MonoBehaviour
{
    public TextMeshProUGUI Nickname;
    public TextMeshProUGUI BuyIn;
    public TextMeshProUGUI Stack;
    public TextMeshProUGUI Winning;
    public Button KickPlayerButton;

    private string playerId;
    private int buyInAmount;
    public Color ProfitColor = Color.green;
    public Color LossColor = Color.red;
    public Action<string> OnKickAction;

    public void SetData(
        string id,
        string username,
        int buyIn,
        int currentStack,
        bool showKickButton)
    {
        playerId = id;
        buyInAmount = buyIn;

        Nickname.text = username;
        BuyIn.text = buyIn.ToString();
        Stack.text = currentStack.ToString();

        KickPlayerButton.gameObject.SetActive(showKickButton);

        KickPlayerButton.onClick.RemoveAllListeners();
        KickPlayerButton.onClick.AddListener(OnKickClicked);

        UpdateWinning(currentStack);
    }

    private void OnKickClicked()
    {
        OnKickAction?.Invoke(playerId);
    }

    public void UpdateStack(int currentStack)
    {
        Stack.text = currentStack.ToString();
        UpdateWinning(currentStack);
    }

    private void UpdateWinning(int currentStack)
    {
        int winning = currentStack - buyInAmount;

        if (winning > 0)
        {
            Winning.text = "+" + winning;
            Winning.color = ProfitColor;
        }
        else if (winning < 0)
        {
            Winning.text = winning.ToString();
            Winning.color = LossColor;
        }
        else
        {
            Winning.text = "0";
            Winning.color = Color.white; 
        }
    }
}