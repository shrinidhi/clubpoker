using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using TMPro;

public class AgentPanelScript : MonoBehaviour
{
    public Transform Agent_Content;
    public GameObject AgentPrefab;

    public Text TotalAgent;
    public Text TotalSuperAgent;

    public string ClubId;

    private List<AgentPrefabScript> agentItems =
        new List<AgentPrefabScript>();

    private List<ClubMemberData> allAgents =
        new List<ClubMemberData>();

    private HashSet<string> onlineUserIds =
        new HashSet<string>();

    public InputField Search_InputField;
    public TMP_Dropdown FilterDropDown;

    private string currentSortBy = "chips";
    private Coroutine onlineRefreshCoroutine;

    public MemberDetail_RoleSelectionScreenScript MemberDetailPopup;
    public Text OnlinePlayerCount;

    [Header("Group By Role")]
    public Toggle GroupbyRole;

    private void Start()
    {
        SetupFilterDropdown();

        if (FilterDropDown != null)
        {
            FilterDropDown.onValueChanged.RemoveListener(OnFilterChanged);
            FilterDropDown.onValueChanged.AddListener(OnFilterChanged);
        }

        if (Search_InputField != null)
        {
            Search_InputField.onValueChanged.RemoveListener(OnSearchChanged);
            Search_InputField.onValueChanged.AddListener(OnSearchChanged);
        }

        if (GroupbyRole != null)
        {
            GroupbyRole.isOn = false;
            GroupbyRole.onValueChanged.RemoveListener(OnGroupByRoleChanged);
            GroupbyRole.onValueChanged.AddListener(OnGroupByRoleChanged);
        }
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnMemberOnline += HandleMemberOnline;

        ClubId = ClubContext.SelectedClub != null
            ? ClubContext.SelectedClub.ClubId
            : "";

        if (Search_InputField != null)
            Search_InputField.text = "";

        LoadAgents().Forget();
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
        if (!string.IsNullOrEmpty(playerId))
            onlineUserIds.Add(playerId);

        GenerateAgents(GetSearchText());
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
            yield return new WaitForSeconds(30f);

            RefreshOnlineAgents().Forget();
        }
    }

    private async UniTaskVoid RefreshOnlineAgents()
    {
        if (string.IsNullOrEmpty(ClubId))
            return;

        List<string> onlineList =
            await AuthManager.Instance
                .GetClubOnlineMembersAsync(ClubId);

        onlineUserIds = onlineList != null
            ? new HashSet<string>(onlineList)
            : new HashSet<string>();

        GenerateAgents(GetSearchText());
    }

    private void SetupFilterDropdown()
    {
        if (FilterDropDown == null)
            return;

        FilterDropDown.ClearOptions();

        FilterDropDown.AddOptions(new List<string>
        {
            "This Week Winnings",
            "This Week Fee",
            "This Week Hands",

            "Last Week Winnings",
            "Last Week Fee",
            "Last Week Hands",

            "Total Winnings",
            "Total Fee",
            "Total Hands"
        });

        FilterDropDown.value = 0;
        FilterDropDown.RefreshShownValue();
    }

    private void OnFilterChanged(int index)
    {
        GenerateAgents(GetSearchText());
    }

    private void OnSearchChanged(string search)
    {
        GenerateAgents(search);
    }

    private void OnGroupByRoleChanged(bool isGrouped)
    {
        GenerateAgents(GetSearchText());
    }

    public async UniTaskVoid LoadAgents()
    {
        ClearAgents();
        StopOnlineRefresh();

        if (string.IsNullOrEmpty(ClubId))
        {
            Debug.LogError("ClubId missing");
            return;
        }

      
        allAgents =
            await AuthManager.Instance.GetClubMembersAsync(
                ClubId,
                "ALL",
                currentSortBy
            );

        if (allAgents == null)
            allAgents = new List<ClubMemberData>();

        List<string> onlineList =
            await AuthManager.Instance
                .GetClubOnlineMembersAsync(ClubId);

        onlineUserIds = onlineList != null
            ? new HashSet<string>(onlineList)
            : new HashSet<string>();

        GenerateAgents(GetSearchText());

        StartOnlineRefresh();
    }

    private async void GenerateAgents(string searchText)
    {
        ClearAgents();

        int agentCount = 0;
        int superAgentCount = 0;
        int tableManagerCount = 0;

        string lowerSearch = string.IsNullOrEmpty(searchText)
            ? ""
            : searchText.Trim().ToLower();

        List<AgentDisplayData> displayAgents =
            new List<AgentDisplayData>();

        foreach (ClubMemberData agent in allAgents)
        {
            if (agent == null)
                continue;

            string normalizedRole =
                NormalizeRole(agent.Role);

            bool validAgentRole =
                normalizedRole == "AGENT" ||
                normalizedRole == "SUPER_AGENT" ||
                normalizedRole == "TABLE_MANAGER";

            if (!validAgentRole)
                continue;

            if (normalizedRole == "AGENT")
                agentCount++;
            else if (normalizedRole == "SUPER_AGENT")
                superAgentCount++;
            else if (normalizedRole == "TABLE_MANAGER")
                tableManagerCount++;

            if (!string.IsNullOrEmpty(lowerSearch))
            {
                string username =
                    !string.IsNullOrEmpty(agent.Username)
                        ? agent.Username.ToLower()
                        : "";

                string userId =
                    !string.IsNullOrEmpty(agent.UserId)
                        ? agent.UserId.ToLower()
                        : "";

                if (!username.Contains(lowerSearch) &&
                    !userId.Contains(lowerSearch))
                {
                    continue;
                }
            }

            AgentDataApiResponse agentData =
                await AuthManager.Instance.GetAgentDataAsync(
                    ClubId,
                    agent.UserId
                );

            displayAgents.Add(new AgentDisplayData
            {
                Member = agent,
                AgentData = agentData
            });
        }

        SortAgents(displayAgents);

        foreach (AgentDisplayData item in displayAgents)
        {
            if (item == null || item.Member == null)
                continue;

            bool isOnline =
                !string.IsNullOrEmpty(item.Member.UserId) &&
                onlineUserIds.Contains(item.Member.UserId);

            GameObject obj =
                Instantiate(
                    AgentPrefab,
                    Agent_Content
                );

            AgentPrefabScript prefab =
                obj.GetComponent<AgentPrefabScript>();

            if (prefab == null)
            {
                Debug.LogError(
                    "AgentPrefabScript AgentPrefab par missing hai."
                );

                Destroy(obj);
                continue;
            }

            prefab.Setup(
                item.Member,
                item.AgentData,
                OnMemberClicked,
                isOnline
            );

            agentItems.Add(prefab);
        }

        if (OnlinePlayerCount != null)
        {
            OnlinePlayerCount.text =
                "Online Player : " + onlineUserIds.Count;
        }

        if (TotalAgent != null)
        {
            TotalAgent.text =
                "Agent : " + agentCount;
        }

        if (TotalSuperAgent != null)
        {
            TotalSuperAgent.text =
                "Super Agent : " + superAgentCount;
        }

      
    }

    private void SortAgents(List<AgentDisplayData> agents)
    {
        if (agents == null)
            return;

        int selectedIndex = FilterDropDown != null
            ? FilterDropDown.value
            : 0;

        bool groupByRole =
            GroupbyRole != null &&
            GroupbyRole.isOn;

        agents.Sort((a, b) =>
        {
            if (groupByRole)
            {
                int aRoleOrder =
                    GetRoleOrder(
                        a != null && a.Member != null
                            ? a.Member.Role
                            : ""
                    );

                int bRoleOrder =
                    GetRoleOrder(
                        b != null && b.Member != null
                            ? b.Member.Role
                            : ""
                    );

                int roleComparison =
                    aRoleOrder.CompareTo(bRoleOrder);

                if (roleComparison != 0)
                    return roleComparison;
            }

           
            int aValue =
                GetAgentSortValue(
                    a != null ? a.AgentData : null,
                    selectedIndex
                );

            int bValue =
                GetAgentSortValue(
                    b != null ? b.AgentData : null,
                    selectedIndex
                );

            return bValue.CompareTo(aValue);
        });
    }

    private int GetRoleOrder(string role)
    {
        string normalizedRole =
            NormalizeRole(role);

        switch (normalizedRole)
        {
            case "TABLE_MANAGER":
                return 0;

            case "SUPER_AGENT":
                return 1;

            case "AGENT":
                return 2;

            default:
                return 3;
        }
    }

    private string NormalizeRole(string role)
    {
        if (string.IsNullOrEmpty(role))
            return "";

        return role
            .Trim()
            .Replace(" ", "_")
            .Replace("-", "_")
            .ToUpper();
    }

    private int GetAgentSortValue(
        AgentDataApiResponse data,
        int index)
    {
        if (data == null || data.Stats == null)
            return 0;

        switch (index)
        {
            case 0:
                return data.Stats.ThisWeek != null
                    ? data.Stats.ThisWeek.Winnings
                    : 0;

            case 1:
                return data.Stats.ThisWeek != null
                    ? data.Stats.ThisWeek.Fee
                    : 0;

            case 2:
                return data.Stats.ThisWeek != null
                    ? data.Stats.ThisWeek.Hands
                    : 0;

            case 3:
                return data.Stats.LastWeek != null
                    ? data.Stats.LastWeek.Winnings
                    : 0;

            case 4:
                return data.Stats.LastWeek != null
                    ? data.Stats.LastWeek.Fee
                    : 0;

            case 5:
                return data.Stats.LastWeek != null
                    ? data.Stats.LastWeek.Hands
                    : 0;

            case 6:
                return data.Stats.Total != null
                    ? data.Stats.Total.Winnings
                    : 0;

            case 7:
                return data.Stats.Total != null
                    ? data.Stats.Total.Fee
                    : 0;

            case 8:
                return data.Stats.Total != null
                    ? data.Stats.Total.Hands
                    : 0;

            default:
                return 0;
        }
    }

    private string GetSearchText()
    {
        return Search_InputField != null
            ? Search_InputField.text.Trim()
            : "";
    }

    private void ClearAgents()
    {
        agentItems.Clear();

        if (Agent_Content == null)
            return;

        for (int i = Agent_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(
                Agent_Content.GetChild(i).gameObject
            );
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

    private class AgentDisplayData
    {
        public ClubMemberData Member;
        public AgentDataApiResponse AgentData;
    }
}