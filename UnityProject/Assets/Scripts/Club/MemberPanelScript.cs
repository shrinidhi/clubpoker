using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using TMPro;

public class MemberPanelScript : MonoBehaviour
{
    public Text ManagerCount;
    public Text AgentCount;
    public Text SuperAgentCount;

    public Transform Member_Content;
    public GameObject Member_Prefab;

    public string ClubId;
    public ShowClubTableScreenScript ShowClubTableScreenScript;

    private List<MemberPrefabScript> memberItems =
        new List<MemberPrefabScript>();

    private List<ClubMemberData> allMembers =
        new List<ClubMemberData>();

    public MemberDetail_RoleSelectionScreenScript MemberDetailPopup;

    public InputField Search_InputFiled;
    public TMP_Dropdown FilterDropDown;

    private string currentSortBy = "chips";

    private void Start()
    {
        SetupFilterDropdown();

        if (FilterDropDown != null)
            FilterDropDown.onValueChanged.AddListener(OnFilterChanged);

        if (Search_InputFiled != null)
            Search_InputFiled.onValueChanged.AddListener(OnSearchChanged);
    }

    private void OnEnable()
    {
        ClubId = ShowClubTableScreenScript.CLubID;

        if (Search_InputFiled != null)
            Search_InputFiled.text = "";

        LoadMembers().Forget();
    }

    private void SetupFilterDropdown()
    {
        if (FilterDropDown == null)
            return;

        FilterDropDown.ClearOptions();

        FilterDropDown.AddOptions(new List<string>
        {
            "Chips",
            "Hands",
            "Winnings",
            "LastLogin"
        });

        FilterDropDown.value = 0;
        FilterDropDown.RefreshShownValue();
    }

    private void OnFilterChanged(int index)
    {
        currentSortBy = GetSortByKey(index);
        LoadMembers().Forget();
    }

    private string GetSortByKey(int index)
    {
        switch (index)
        {
            case 0:
                return "chips";

            case 1:
                return "hands";

            case 2:
                return "winnings";

            case 3:
                return "lastlogin";

            default:
                return "chips";
        }
    }

    private void OnSearchChanged(string value)
    {
        GenerateMembers(value);
    }

    public async UniTaskVoid LoadMembers()
    {
        ClearMembers();

        if (string.IsNullOrEmpty(ClubId))
        {
            Debug.LogError("ClubId missing");
            return;
        }

        allMembers =
            await AuthManager.Instance.GetClubMembersAsync(
                ClubId,
                "ALL",
                currentSortBy
            );

        string searchText = Search_InputFiled != null
            ? Search_InputFiled.text.Trim()
            : "";

        GenerateMembers(searchText);
    }

    private void GenerateMembers(string searchText)
    {
        ClearMembers();

        int managerCount = 0;
        int agentCount = 0;
        int superAgentCount = 0;

        string lowerSearch = string.IsNullOrEmpty(searchText)
            ? ""
            : searchText.ToLower();

        foreach (ClubMemberData member in allMembers)
        {
            if (member.Role == "CREATOR" || member.Role == "MANAGER")
                managerCount++;
            else if (member.Role == "AGENT")
                agentCount++;
            else if (member.Role == "SUPER_AGENT")
                superAgentCount++;

            if (!string.IsNullOrEmpty(lowerSearch))
            {
                string username = member.Username != null
                    ? member.Username.ToLower()
                    : "";

                string userId = member.UserId != null
                    ? member.UserId.ToLower()
                    : "";

                if (!username.Contains(lowerSearch) &&
                    !userId.Contains(lowerSearch))
                {
                    continue;
                }
            }

            GameObject obj = Instantiate(Member_Prefab, Member_Content);

            MemberPrefabScript prefab =
                obj.GetComponent<MemberPrefabScript>();

            prefab.Setup(member, OnMemberClicked);
            memberItems.Add(prefab);
        }

        ManagerCount.text = "Manager : " + managerCount;
        AgentCount.text = "Agent : " + agentCount;
        SuperAgentCount.text = "SuperAgent : " + superAgentCount;
    }

    private void ClearMembers()
    {
        memberItems.Clear();

        for (int i = Member_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Member_Content.GetChild(i).gameObject);
        }
    }

    private void OnMemberClicked(ClubMemberData member)
    {
        if (MemberDetailPopup == null)
            return;

        MemberDetailPopup.gameObject.SetActive(true);

        MemberDetailPopup.ShowMember(
            ClubId,
            member.UserId
        );
    }
}