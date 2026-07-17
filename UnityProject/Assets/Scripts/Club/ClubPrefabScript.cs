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
    public Text MemberCount_Text;
    public Text ClubName_Text;
    public Text Club_ID_Text;
    public Text RoleTpyeText;

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
       

        RoleTpyeText.text =
            string.IsNullOrEmpty(data.Role)
                ? ""
                : data.Role.Substring(0, 1);

        if (badgeSprite != null)
            ClubBadge_Image.sprite = badgeSprite;

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