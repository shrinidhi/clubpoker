using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

public class ShowClubPanelScript : MonoBehaviour
{
    public GameObject ClubPrefab;
    public Transform Club_Content;

    public ClubBadgeSO ClubBadgeSO;

    private List<ClubPrefabScript> clubItems = new List<ClubPrefabScript>();
    public GameObject ShowClub_TableScreen;
    public ShowClubTableScreenScript ShowClubTableScreenScript;
    public GameObject JoinAndCreateClub_Panel;
    void Start()
    {
        LoadClubs().Forget();
    }

     public async UniTaskVoid LoadClubs()
    {
        ClearClubs();

        List<ClubListData> clubs =
            await AuthManager.Instance.GetClubsAsync();

        foreach (ClubListData club in clubs)
        {
            GameObject obj = Instantiate(ClubPrefab, Club_Content);

            ClubPrefabScript prefab =
                obj.GetComponent<ClubPrefabScript>();

            Sprite badgeSprite = GetBadgeSprite(club.Badge);

            prefab.Setup(club, badgeSprite, this);
            clubItems.Add(prefab);

           
        }
        JoinAndCreateClub_Panel.SetActive(clubs.Count == 0);
    }

    void ClearClubs()
    {
        clubItems.Clear();

        for (int i = 0; i < Club_Content.childCount; i++)
        {
            Destroy(Club_Content.GetChild(i).gameObject);
        }
    }

    Sprite GetBadgeSprite(string badgeKey)
    {
        if (ClubBadgeSO == null || ClubBadgeSO.ClubBadges == null)
            return null;

        foreach (ClubBadgeData badge in ClubBadgeSO.ClubBadges)
        {
            if (badge.BadgeName.ToLower() == badgeKey.ToLower())
            {
                return badge.BadgeImage;
            }
        }

        return null;
    }

    public void OnClubSelected(ClubListData club)
    {
        Debug.Log("Selected Club: " + club.Name);
        Debug.Log("Club ID: " + club.ClubId);
        Debug.Log("Club Code: " + club.ClubCode);

        ShowClub_TableScreen.SetActive(true);
        ShowClubTableScreenScript.ShowData(club);
    }
}