
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
    public TextMeshProUGUI ErrorText;

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
            ClubSearchScreen.gameObject.SetActive(false);
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

    public async void ApplyToClub(string clubId, ClubSearchCardScript card)
    {
        if (card == null)
            return;

        string message = card.MSG_InputField.text.Trim();

        if (card.Apply_Button != null)
            card.Apply_Button.interactable = false;

        bool success =
            await AuthManager.Instance.ApplyToClubAsync(clubId, message);

        card.SetPending(true);

        if (success)
            ShowError("");
        else
            ShowError("Request already pending");
    }
    private void ClearResult()
    {
        if (ErrorText != null)
            ErrorText.text = "";

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
        if (ErrorText != null)
            ErrorText.text = msg;

        Debug.Log(msg);
    }
}
