using ClubPoker.Networking.Models;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class TransactionPrefabScript : MonoBehaviour
{
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI typeText;
    public TextMeshProUGUI dateText;
    public Image icon;

    public Color creditColor = Color.green;
    public Color debitColor = Color.red;

    public Sprite buyInIcon;
    public Sprite winIcon;
    public Sprite lossIcon;
    public Sprite bonusIcon;

    public void Setup(TransactionData data)
    {
        typeText.text = data.Type;
        dateText.text = FormatDate(data.Timestamp);

        bool isCredit = IsCredit(data.Type);

        amountText.text = (isCredit ? "+" : "-") + data.Amount.ToString();
        amountText.color = isCredit ? creditColor : debitColor;

        icon.sprite = GetIcon(data.Type);
    }

    bool IsCredit(string type)
    {
        return type == "win" ||
               type == "daily_bonus" ||
               type == "signup_bonus";
    }

    Sprite GetIcon(string type)
    {
        switch (type)
        {
            case "buy_in": return buyInIcon;
            case "win": return winIcon;
            case "loss": return lossIcon;
            case "daily_bonus": return bonusIcon;
            default: return buyInIcon;
        }
    }

    string FormatDate(string iso)
    {
        System.DateTime dt = System.DateTime.Parse(iso);
        return dt.ToString("dd MMM yyyy HH:mm");
    }
}

