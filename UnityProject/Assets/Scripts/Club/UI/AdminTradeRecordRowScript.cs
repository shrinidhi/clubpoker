using UnityEngine;
using TMPro;

/// <summary>
/// One row in Admin ▸ Personal Trade Record. Simpler than the Cashier row: just the member
/// name, what happened, when, and the chip amount — green for Send Out, red for Claimed Back.
/// </summary>
public class AdminTradeRecordRowScript : MonoBehaviour
{
    public TextMeshProUGUI Name_Text;
    public TextMeshProUGUI Action_Text;      // "Send Out" / "Claimed Back"
    public TextMeshProUGUI DateTime_Text;
    public TextMeshProUGUI Amount_Text;

    private static readonly Color ColorSend  = new Color(0.2f, 0.85f, 0.2f);  // green
    private static readonly Color ColorClaim = new Color(1f, 0.3f, 0.3f);      // red

    public void Setup(ChipRecord record)
    {
        if (Name_Text != null)
            Name_Text.text = record.MemberName;

        if (DateTime_Text != null)
            DateTime_Text.text = record.Timestamp.ToLocalTime()
                .ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        long amount = System.Math.Abs(record.Amount);

        switch (record.Type?.ToUpper())
        {
            case "SEND":
                SetAction("Send Out", "+" + amount.ToString("N0"), ColorSend);
                break;

            case "CLAIM_BACK":
                SetAction("Claimed Back", "-" + amount.ToString("N0"), ColorClaim);
                break;

            case "REQUEST":
                SetAction("Request", "+" + amount.ToString("N0"), ColorSend);
                break;

            default:
                SetAction(record.Type, amount.ToString("N0"), ColorClaim);
                break;
        }
    }

    private void SetAction(string action, string amount, Color color)
    {
        if (Action_Text != null) Action_Text.text = action;
        if (Amount_Text != null)
        {
            Amount_Text.text  = amount;
            Amount_Text.color = color;
        }
    }
}
