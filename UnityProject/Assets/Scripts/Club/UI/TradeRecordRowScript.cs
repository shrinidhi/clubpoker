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

    [Header("Role Sprites")]
    public Sprite CreatorBadge_Sprite;
    public Sprite OtherBadge_Sprite;

    private static readonly Color ColorSend    = new Color(0.2f, 0.85f, 0.2f); // green
    private static readonly Color ColorClaim   = new Color(1f, 0.3f, 0.3f);   // red
    private static readonly Color ColorRequest = new Color(1f, 0.75f, 0.2f);   // yellow

    public void Setup(ChipRecord record)
    {
        // operator
        OperatorName_Text.text = record.OperatorName;
        OperatorId_Text.text   = "ID: " + record.OperatorId?.Split('-')[0];
        SetRoleBadge(OperatorRoleBadge_Image, OperatorRoleBadge_Text, record.OperatorRole);

        // member
        MemberName_Text.text = record.MemberName;
        MemberId_Text.text   = "ID: " + record.MemberId?.Split('-')[0];
        SetRoleBadge(MemberRoleBadge_Image, MemberRoleBadge_Text, record.MemberRole);

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
        string letter = role?.ToUpper().Replace(" ", "_") switch
        {
            "CREATOR"       => "C",
            "MANAGER"       => "M",
            "TABLE_MANAGER" => "TM",
            "SUPER_AGENT"   => "SA",
            "AGENT"         => "A",
            _               => ""      // MEMBER / unknown → no badge
        };

        bool hasBadge = letter.Length > 0;

        if (badgeImage != null)
        {
            badgeImage.gameObject.SetActive(hasBadge);
            // sprites are optional — keep whatever the prefab ships if unassigned
            var sprite = letter == "C" ? CreatorBadge_Sprite : OtherBadge_Sprite;
            if (hasBadge && sprite != null)
                badgeImage.sprite = sprite;
        }

        if (badgeText != null)
        {
            badgeText.gameObject.SetActive(hasBadge);
            badgeText.text = letter;
        }
    }
}
