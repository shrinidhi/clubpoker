using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

public class CreateClubScreenScript : MonoBehaviour
{
    public Button Close_Button;
    public TMP_InputField ClubName_InputField;
    public TMP_InputField Description_InputField;
    public Button CreateClub_Button;

    public GameObject ClubBadge_Prefab;
    public Transform ClubBadge_Content;

    public ClubBadgeSO ClubBadgeSO;

    private List<ClubBadgePrefabScript> badgeItems = new List<ClubBadgePrefabScript>();

    private string selectedBadge = "";
    public ShowClubPanelScript ShowClubPanelScript;

    void Start()
    {
        Close_Button.onClick.AddListener(CloseButtonOnTap);
        CreateClub_Button.onClick.AddListener(CreateClubButtonOnTap);

        GenerateBadges();
    }

    void GenerateBadges()
    {
        badgeItems.Clear();

        for (int i = 0; i < ClubBadge_Content.childCount; i++)
        {
            Destroy(ClubBadge_Content.GetChild(i).gameObject);
        }

        foreach (ClubBadgeData data in ClubBadgeSO.ClubBadges)
        {
            GameObject obj = Instantiate(ClubBadge_Prefab, ClubBadge_Content);

            ClubBadgePrefabScript badgePrefab =
                obj.GetComponent<ClubBadgePrefabScript>();

            badgePrefab.Setup(data, this);
            badgeItems.Add(badgePrefab);
        }

        if (ClubBadgeSO.ClubBadges.Count > 0)
        {
            SelectBadge(badgeItems[0], ClubBadgeSO.ClubBadges[0].BadgeName);
        }
    }

    public void SelectBadge(ClubBadgePrefabScript selectedItem, string badgeKey)
    {
        selectedBadge = badgeKey;

        foreach (ClubBadgePrefabScript item in badgeItems)
        {
            item.SetSelected(item == selectedItem);
        }
    }

    async void CreateClubButtonOnTap()
    {
        string clubName = ClubName_InputField.text.Trim();
        string description = Description_InputField.text.Trim();

        if (string.IsNullOrEmpty(clubName))
        {
            Debug.LogWarning("Club name required");
            return;
        }

        if (string.IsNullOrEmpty(selectedBadge))
        {
            Debug.LogWarning("Please select badge");
            return;
        }

        CreateClub_Button.interactable = false;

        try
        {
            ClubData club = await AuthManager.Instance.CreateClubAsync(
                clubName,
                selectedBadge.ToLower(),
                description
            );

            Debug.Log("Club Created: " + club.Name);
            Debug.Log("Club Code: " + club.ClubCode);
            ShowClubPanelScript.LoadClubs().Forget();

            gameObject.SetActive(false);
        }
        catch
        {
            Debug.LogError("Create club failed");
        }

        CreateClub_Button.interactable = true;
    }

    void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }
}