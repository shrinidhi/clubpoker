using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class ClubPrefabScript : MonoBehaviour
{
    public Button Club_Button;
    public Image ClubBadge_Image;
    public Text MemberCount_Text;
    public Text ClubName_Text;
    public Text Club_ID_Text;

    private ClubListData clubData;
    private ShowClubPanelScript manager;

    public void Setup(ClubListData data, Sprite badgeSprite, ShowClubPanelScript panelScript)
    {
        clubData = data;
        manager = panelScript;

        ClubName_Text.text = data.Name;
        Club_ID_Text.text = "ID: " + data.ClubCode;
        MemberCount_Text.text = ": "+data.MemberCount.ToString();

        if (badgeSprite != null)
            ClubBadge_Image.sprite = badgeSprite;

        Club_Button.onClick.RemoveAllListeners();
        Club_Button.onClick.AddListener(OnClickClub);
    }

    void OnClickClub()
    {
        manager.OnClubSelected(clubData);
    }
}