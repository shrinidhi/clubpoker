using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ClubPrefabScript : MonoBehaviour
{
    public Button Club_Button;
    public Image ClubBadge_Image;
    [Tooltip("Optional: club photo inside the badge (e.g. a child of ClubBadge_Image, within its padding). Empty → the photo replaces the badge sprite.")]
    public Image ExternalPhotoImage;
    public Text MemberCount_Text;
    public Text ClubName_Text;
    public Text Club_ID_Text;
    public Text RoleTpyeText;
    public GameObject RoleTypeBG;
    private ClubListData clubData;
    private ShowClubPanelScript manager;

    private List<ClubTableData> clubTables =
        new List<ClubTableData>();

    public void Setup(
        ClubListData data,
        Sprite badgeSprite,
        ShowClubPanelScript panelScript)
    {
        clubData = data;
        manager = panelScript;

        ClubName_Text.text = data.Name;
        Club_ID_Text.text = "ID: " + data.ClubCode;
       if(data.Role == "MEMBER")
        {
            RoleTypeBG.SetActive(false);
        }

        RoleTpyeText.text =
            string.IsNullOrEmpty(data.Role)
                ? ""
                : data.Role.Substring(0, 1);

        if (data.Role == "MANAGER" && data.IsTableManager)
        {
            RoleTpyeText.text = "TM";
        }

        // Custom photo if the club has one, else its badge.
        ClubLogo.Show(ClubBadge_Image, ExternalPhotoImage, badgeSprite, data.LogoUrl);

        Club_Button.onClick.RemoveAllListeners();
        Club_Button.onClick.AddListener(OnClickClub);

        LoadClubTables(data.ClubId).Forget();
    }

    private async UniTaskVoid LoadClubTables(string clubId)
    {
        clubTables =  await AuthManager.Instance.GetClubTablesAsync(clubId);
        MemberCount_Text.text = clubTables.Count.ToString();
    }

    private void OnClickClub()
    {
        manager.OnClubSelected(clubData);  
    }
}