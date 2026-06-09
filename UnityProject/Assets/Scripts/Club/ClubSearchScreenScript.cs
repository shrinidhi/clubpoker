
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

public class ClubSearchScreenScript : MonoBehaviour
{
    public Button Close_Button;
    public GameObject ClubSearchScreen;

    public TMP_InputField ClubID_InputField;
    public TMP_InputField Referral_ID_InputField;
    public Button Search_Button;

    public ClubSearchCardScript ClubSearchCardPopup;
    public TextMeshProUGUI MsgText;

    public ClubBadgeSO ClubBadgeSO;

    private void Start()
    {
        if (Close_Button != null)
            Close_Button.onClick.AddListener(Close_ButtonOnTap);

        Search_Button.onClick.AddListener(Search_ButtonOnTap);

        if (ClubSearchCardPopup != null)
            ClubSearchCardPopup.gameObject.SetActive(false);

    }
    private void OnEnable()
    {
        ClubID_InputField.text = "";
        MsgText.text = "";
        Referral_ID_InputField.text = "";
    }
    private void Close_ButtonOnTap()
    {
        ClubSearchScreen.SetActive(false);
    }

    private void Search_ButtonOnTap()
    {
        SearchClub().Forget();
    }

    private async UniTaskVoid SearchClub()
    {
        ClearResult();

        string clubCode = ClubID_InputField.text.Trim();

        if (string.IsNullOrEmpty(clubCode))
        {
            ShowError("Enter club ID");
            return;
        }

        Search_Button.interactable = false;

        var result =
            await AuthManager.Instance.SearchClubAsync(clubCode);

        Search_Button.interactable = true;


        if (result.club != null)
        {
            Sprite badgeSprite =
                GetBadgeSprite(result.club.Badge);

            ClubSearchCardPopup.gameObject.SetActive(true);
            if(ClubSearchScreen != null)
            {
                ClubSearchScreen.gameObject.SetActive(false);
            }
           
            ClubSearchCardPopup.Setup(
                result.club,
                badgeSprite,
                this
            );

            return;
        }


        switch (result.errorCode)
        {
            case "A004":
                ShowError("You are not a member of this club");
                break;

            case "404":
            case "NOT_FOUND":
                ShowError("Club not found");
                break;

            default:
                ShowError(result.errorMessage);
                break;
        }
    }

    public async void ApplyToClub(
     string clubId,
     ClubSearchCardScript card)
    {
        if (card == null)
            return;

        string message =
            card.MSG_InputField.text.Trim();

        if (card.Apply_Button != null)
            card.Apply_Button.interactable = false;

        var result =
            await AuthManager.Instance.ApplyToClubAsync(
                clubId,
                message
            );

        if (result.success)
        {
            Debug.Log("✅ Apply Success");

            card.SetPending(true);

            gameObject.SetActive(true);
            card.gameObject.SetActive(false);

            ShowError("Application submitted successfully");
            if (ClubSocketHandler.Instance != null)
                ClubSocketHandler.Instance.JoinClubPage(clubId);
        }
        else
        {
            Debug.LogError("❌ Apply Failed");
            Debug.LogError(result.errorMessage);

            ShowError(result.errorMessage);

            if (card.Apply_Button != null)
                card.Apply_Button.interactable = true;
        }
    }
    private void ClearResult()
    {
        if (MsgText != null)
            MsgText.text = "";

        if (ClubSearchCardPopup != null)
            ClubSearchCardPopup.gameObject.SetActive(false);
    }

    private Sprite GetBadgeSprite(string badgeKey)
    {
        if (string.IsNullOrEmpty(badgeKey))
            return null;

        if (ClubBadgeSO == null || ClubBadgeSO.ClubBadges == null)
            return null;

        foreach (ClubBadgeData badge in ClubBadgeSO.ClubBadges)
        {
            if (badge.BadgeName.ToLower() == badgeKey.ToLower())
                return badge.BadgeImage;
        }

        return null;
    }

    private void ShowError(string msg)
    {
        if (MsgText != null)
            MsgText.text = msg;

        CancelInvoke(nameof(ClearMsgText));
        Invoke(nameof(ClearMsgText), 2f);

        Debug.Log(msg);
    }

    private void ClearMsgText()
    {
        if (MsgText != null)
            MsgText.text = "";
    }
}
