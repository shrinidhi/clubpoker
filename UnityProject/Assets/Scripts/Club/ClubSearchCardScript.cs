using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class ClubSearchCardScript : MonoBehaviour
{
    public Image ClubBadge_Image;
    public Text ClubName_Text;
    public Text ClubCode_Text;

    public Button Apply_Button;
    public GameObject Pending_Badge;
    public Text ApplyButton_Text;

    private ClubSearchData clubData;
    private ClubSearchScreenScript manager;

    public Button Close_Button;
    private void Start()
    {
        Close_Button.onClick.AddListener(Close_ButtonOnTap);
    }


    void Close_ButtonOnTap()
    {
        gameObject.SetActive(false);
        manager.ClubSearchScreen.SetActive(true);
    }
    public void Setup(
        ClubSearchData data,
        Sprite badgeSprite,
        ClubSearchScreenScript screen)
    {
        clubData = data;
        manager = screen;

        ClubName_Text.text = data.Name;
        ClubCode_Text.text = "ID: " + data.ClubCode;


        if (badgeSprite != null)
            ClubBadge_Image.sprite = badgeSprite;

        bool isPending =
            data.JoinStatus == "PENDING" ||
            data.JoinStatus == "REQUESTED";

        SetPending(isPending);

        Apply_Button.onClick.RemoveAllListeners();
        Apply_Button.onClick.AddListener(OnApplyClick);
    }

    private void OnApplyClick()
    {
        if (clubData == null)
            return;

        manager.ApplyToClub(clubData.Id, this);

        gameObject.SetActive(false);
        manager.ClubSearchScreen.SetActive(true);
    }

    public void SetPending(bool pending)
    {
        if (Pending_Badge != null)
            Pending_Badge.SetActive(pending);

        if (Apply_Button != null)
            Apply_Button.interactable = !pending;

        if (ApplyButton_Text != null)
            ApplyButton_Text.text = pending ? "Pending" : "Apply to Join";
    }
}