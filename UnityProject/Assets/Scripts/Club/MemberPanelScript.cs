using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private List<MemberPrefabScript> memberItems =
        new List<MemberPrefabScript>();

    private List<ClubMemberData> allMembers =
        new List<ClubMemberData>();

    private HashSet<string> onlineUserIds =
        new HashSet<string>();

    public MemberDetail_RoleSelectionScreenScript MemberDetailPopup;

    public InputField Search_InputFiled;
    public TMP_Dropdown FilterDropDown;

    private string currentSortBy = "chips";
    private Coroutine onlineRefreshCoroutine;

    public Text PlayerOnlineCount;

    [Header("Group By Role")]
    public Toggle Groupbyrole;

    private void Start()
    {
        SetupFilterDropdown();

        if (FilterDropDown != null)
        {
            FilterDropDown.onValueChanged.RemoveListener(OnFilterChanged);
            FilterDropDown.onValueChanged.AddListener(OnFilterChanged);
        }

        if (Search_InputFiled != null)
        {
            Search_InputFiled.onValueChanged.RemoveListener(OnSearchChanged);
            Search_InputFiled.onValueChanged.AddListener(OnSearchChanged);
        }

        if (Groupbyrole != null)
        {
            Groupbyrole.isOn = false;
            Groupbyrole.onValueChanged.RemoveListener(OnGroupByRoleChanged);
            Groupbyrole.onValueChanged.AddListener(OnGroupByRoleChanged);
        }
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnMemberOnline += HandleMemberOnline;

        ClubId = ClubContext.SelectedClub != null
            ? ClubContext.SelectedClub.ClubId
            : "";

        if (Search_InputFiled != null)
            Search_InputFiled.text = "";

        LoadMembers().Forget();

        StartOnlineRefresh();
    }

    private void OnDisable()
    {
        StopOnlineRefresh();

        ClubSocketHandler.OnMemberOnline -= HandleMemberOnline;
    }

    private void StartOnlineRefresh()
    {
        StopOnlineRefresh();

        onlineRefreshCoroutine =
            StartCoroutine(OnlineRefreshLoop());
    }

    private void HandleMemberOnline(string playerId)
    {
        StartOnlineRefresh();

        RefreshOnlineMembers().Forget();
    }

    private void StopOnlineRefresh()
    {
        if (onlineRefreshCoroutine != null)
        {
            StopCoroutine(onlineRefreshCoroutine);
            onlineRefreshCoroutine = null;
        }
    }

    private IEnumerator OnlineRefreshLoop()
    {
        while (gameObject.activeInHierarchy)
        {
            RefreshOnlineMembers().Forget();

            yield return new WaitForSeconds(30f);
        }
    }

    private async UniTaskVoid RefreshOnlineMembers()
    {
        if (string.IsNullOrEmpty(ClubId))
            return;

        List<string> onlineList =
            await AuthManager.Instance
                .GetClubOnlineMembersAsync(ClubId);

        onlineUserIds = onlineList != null
            ? new HashSet<string>(onlineList)
            : new HashSet<string>();

        string searchText = Search_InputFiled != null
            ? Search_InputFiled.text.Trim()
            : "";

        GenerateMembers(searchText);
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

    private void OnGroupByRoleChanged(bool isGrouped)
    {
        string searchText = Search_InputFiled != null
            ? Search_InputFiled.text.Trim()
            : "";

        GenerateMembers(searchText);
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

        if (allMembers == null)
            allMembers = new List<ClubMemberData>();

        List<string> onlineList =
            await AuthManager.Instance
                .GetClubOnlineMembersAsync(ClubId);

        onlineUserIds = onlineList != null
            ? new HashSet<string>(onlineList)
            : new HashSet<string>();

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
            : searchText.Trim().ToLower();

        List<ClubMemberData> filteredMembers =
            new List<ClubMemberData>();

        foreach (ClubMemberData member in allMembers)
        {
            if (member == null)
                continue;

            string role = NormalizeRole(member.Role);

            if (role == "CREATOR" || role == "MANAGER")
            {
                managerCount++;
            }
            else if (role == "AGENT")
            {
                agentCount++;
            }
            else if (role == "SUPER_AGENT")
            {
                superAgentCount++;
            }

            if (!string.IsNullOrEmpty(lowerSearch))
            {
                string username =
                    !string.IsNullOrEmpty(member.Username)
                        ? member.Username.ToLower()
                        : "";

                string userId =
                    !string.IsNullOrEmpty(member.UserId)
                        ? member.UserId.ToLower()
                        : "";

                if (!username.Contains(lowerSearch) &&
                    !userId.Contains(lowerSearch))
                {
                    continue;
                }
            }

            filteredMembers.Add(member);
        }

      
        if (Groupbyrole != null && Groupbyrole.isOn)
        {
            filteredMembers = filteredMembers
                .OrderBy(member => GetRoleOrder(member.Role))
                .ToList();
        }

        foreach (ClubMemberData member in filteredMembers)
        {
            bool isOnline =
                !string.IsNullOrEmpty(member.UserId) &&
                onlineUserIds.Contains(member.UserId);

            GameObject obj =
                Instantiate(Member_Prefab, Member_Content);

            MemberPrefabScript prefab =
                obj.GetComponent<MemberPrefabScript>();

            if (prefab == null)
            {
                Debug.LogError(
                    "MemberPrefabScript component Member_Prefab par missing hai."
                );

                Destroy(obj);
                continue;
            }

            prefab.Setup(
                member,
                OnMemberClicked,
                isOnline
            );

            memberItems.Add(prefab);
        }

        if (PlayerOnlineCount != null)
        {
            PlayerOnlineCount.text =
                "Online Player : " + onlineUserIds.Count;
        }

        if (ManagerCount != null)
        {
            ManagerCount.text =
                "Manager : " + managerCount;
        }

        if (AgentCount != null)
        {
            AgentCount.text =
                "Agent : " + agentCount;
        }

        if (SuperAgentCount != null)
        {
            SuperAgentCount.text =
                "Super Agent : " + superAgentCount;
        }
    }

    private string NormalizeRole(string role)
    {
        if (string.IsNullOrEmpty(role))
            return "MEMBER";

        return role
            .Trim()
            .Replace(" ", "_")
            .Replace("-", "_")
            .ToUpper();
    }

    private int GetRoleOrder(string role)
    {
        string normalizedRole = NormalizeRole(role);

        switch (normalizedRole)
        {
            case "CREATOR":
                return 0;

            case "MANAGER":
                return 1;

            case "TABLE_MANAGER":
                return 2;

            case "SUPER_AGENT":
                return 3;

            case "AGENT":
                return 4;

            case "MEMBER":
                return 5;

            default:
                return 6;
        }
    }
    private void ClearMembers()
    {
        memberItems.Clear();

        if (Member_Content == null)
            return;

        for (int i = Member_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Member_Content.GetChild(i).gameObject);
        }
    }

    private void OnMemberClicked(ClubMemberData member)
    {
        if (MemberDetailPopup == null || member == null)
            return;

        MemberDetailPopup.gameObject.SetActive(true);

        MemberDetailPopup.ShowMember(
            ClubId,
            member.UserId
        );
    }
}