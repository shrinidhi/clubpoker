using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class Days_30SessionPrefab : MonoBehaviour
{
    public Text VariantName;
    public Text GameCount;
    public Text HandCount;
    public Text ChipsCount;

    [Header("Optional")]
    public Text TableName;
    public Text BlindText;
    public Text DateText;
    public Text BuyInText;

    public void SetData(CareerSessionData data)
    {
        if (data == null) return;

        if (VariantName != null) VariantName.text = GetVariantName(data.Variant);
        if (GameCount != null) GameCount.text = data.Games.ToString();
        if (HandCount != null) HandCount.text = data.Hands.ToString();
        if (ChipsCount != null) ChipsCount.text = data.Winnings > 0 ? "+" + data.Winnings : data.Winnings.ToString();
        if (TableName != null) TableName.text = data.TableName ?? "";
        if (BlindText != null) BlindText.text = data.BlindsLabel ?? "";
        if (DateText != null) DateText.text = data.Date ?? "";
        if (BuyInText != null) BuyInText.text = data.BuyIn.ToString();
    }

    private string GetVariantName(string variant)
    {
        switch (variant)
        {
            case "texas_holdem": return "NLH";
            case "omaha":
            case "plo4": return "PLO4";
            case "omaha_six":
            case "plo6": return "PLO6";
            default: return variant;
        }
    }
}