using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TradeRecordRowScript : MonoBehaviour
{
    [Header("Operator (Left)")]
    public Image OperatorAvatar_Image;
    public TextMeshProUGUI OperatorName_Text;
    public Image OperatorRoleBadge_Image;
    public TextMeshProUGUI OperatorRoleBadge_Text;
    public TextMeshProUGUI OperatorId_Text;

    [Header("Center")]
    public Image Arrow_Image;
    public Sprite ArrowLeft_Sprite;   // grey ← claim back
    public Sprite ArrowRight_Sprite;  // red  → send out
    public TextMeshProUGUI Amount_Text;
    public TextMeshProUGUI Timestamp_Text;

    [Header("Member (Right)")]
    public Image MemberAvatar_Image;
    public TextMeshProUGUI MemberName_Text;
    public Image MemberRoleBadge_Image;
    public TextMeshProUGUI MemberRoleBadge_Text;
    public TextMeshProUGUI MemberId_Text;

    private static readonly Color ColorSend    = new Color(0.2f, 0.85f, 0.2f); // green
    private static readonly Color ColorClaim   = new Color(1f, 0.3f, 0.3f);   // red
    private static readonly Color ColorRequest = new Color(1f, 0.75f, 0.2f);   // yellow

    public void Setup(ChipRecord record)
    {
        // operator
        OperatorName_Text.text = record.OperatorName;
        OperatorId_Text.text   = "ID: " + record.OperatorId?.Split('-')[0];
        if (OperatorRoleBadge_Image != null) OperatorRoleBadge_Image.gameObject.SetActive(false);

        // member
        MemberName_Text.text = record.MemberName;
        MemberId_Text.text   = "ID: " + record.MemberId?.Split('-')[0];
        if (MemberRoleBadge_Image != null) MemberRoleBadge_Image.gameObject.SetActive(false);

        // timestamp — convert UTC to local
        var localTime = record.Timestamp.ToLocalTime();
        Timestamp_Text.text = localTime.ToString("dd/MM/yyyy HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);

        // amount + arrow + color by type
        switch (record.Type?.ToUpper())
        {
            case "SEND":
                Amount_Text.text   = "+" + System.Math.Abs(record.Amount).ToString("N0");
                Amount_Text.color  = ColorSend;
                Arrow_Image.sprite = ArrowLeft_Sprite;
                Arrow_Image.color  = ColorSend;
                break;

            case "CLAIM_BACK":
                Amount_Text.text   = "-" + System.Math.Abs(record.Amount).ToString("N0");
                Amount_Text.color  = ColorClaim;
                Arrow_Image.sprite = ArrowRight_Sprite;
                Arrow_Image.color  = ColorClaim;
                break;

            case "REQUEST":
                Amount_Text.text   = "+" + System.Math.Abs(record.Amount).ToString("N0");
                Amount_Text.color  = ColorRequest;
                Arrow_Image.sprite = ArrowLeft_Sprite;
                Arrow_Image.color  = ColorRequest;
                break;

            default:
                Amount_Text.text  = System.Math.Abs(record.Amount).ToString("N0");
                Amount_Text.color = ColorClaim;
                Arrow_Image.color = ColorClaim;
                break;
        }
    }

    private void SetRoleBadge(Image badgeImage, TextMeshProUGUI badgeText, string role)
    {
        switch (role?.ToUpper())
        {
            case "CREATOR":
                badgeText.text    = "C";
                badgeImage.color  = new Color(1f, 0.75f, 0f);
                break;
            case "MANAGER":
                badgeText.text    = "M";
                badgeImage.color  = new Color(0.2f, 0.6f, 1f);
                break;
            case "AGENT":
                badgeText.text    = "A";
                badgeImage.color  = new Color(0.6f, 0.2f, 1f);
                break;
            default:
                badgeText.text    = "M";
                badgeImage.color  = new Color(0.4f, 0.4f, 0.4f);
                break;
        }
    }
}
