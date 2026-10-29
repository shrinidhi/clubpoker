
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.Core;
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

    private string currentSortBy = "thisweekwinnings";
    private Coroutine onlineRefreshCoroutine;

    public MemberDetail_RoleSelectionScreenScript MemberDetailPopup;
    public Text OnlinePlayerCount;

    [Header("Group By Role")]
    public Toggle GroupbyRole;

    [Header("Custom Filter UI")]
    public TextMeshProUGUI CurrentFilterName;
    public Button FilterButton;
    public GameObject FilterPanel;
    public GameObject FilterPrefab;
    public Transform FilterContent;

    private readonly string[] sortKeys =
    {
        "thisweekwinnings",
        "thisweekfee",
        "lastweekwinnings",
        "lastweekfee",
        "totalwinnings",
        "totalfee"
    };

    private readonly string[] sortLabels =
    {
        "This Week Winnings",
        "This Week Fee",
        "Last Week Winnings",
        "Last Week Fee",
        "Total Winnings",
        "Total Fee"
    };

    // Default: This Week Winnings, highest first.
    private readonly bool[] sortDescending = new bool[6]
    {
        true, true, true, true, true, true
    };

    private readonly bool[] sortHasBeenClicked = new bool[6];

    private readonly List<FilterDropDownPrefab> filterItems =
        new List<FilterDropDownPrefab>();

    private readonly List<AgentDisplayData> displayAgents =
        new List<AgentDisplayData>();

    private int selectedSortIndex = 0;
    private int loadVersion;
    private int viewVersion;
    private bool loadingAgents;

    private void Awake()
    {
        selectedSortIndex = 0;
        currentSortBy = sortKeys[0];

        SetupFilterOptions();

        if (FilterButton != null)
        {
            FilterButton.onClick.RemoveListener(ToggleFilterPanel);
            FilterButton.onClick.AddListener(ToggleFilterPanel);
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

        if (FilterPanel != null)
            FilterPanel.gameObject.SetActive(false);

        UpdateFilterUI();
    }

    private void OnEnable()
    {
        ++viewVersion;

        ClubSocketHandler.OnMemberOnline -= HandleMemberOnline;
        ClubSocketHandler.OnMemberOnline += HandleMemberOnline;

        ClubId = ClubContext.SelectedClub != null
            ? ClubContext.SelectedClub.ClubId
            : "";

        if (Search_InputField != null)
            Search_InputField.SetTextWithoutNotify("");

        LoadAgents().Forget();
    }

    private void OnDisable()
    {
        ++viewVersion;
        ++loadVersion;

        loadingAgents = false;

        StopOnlineRefresh();

        ClubSocketHandler.OnMemberOnline -= HandleMemberOnline;
    }

    // ---------------------------------------------------------
    // CUSTOM FILTER OPTIONS
    // ---------------------------------------------------------

    private void SetupFilterOptions()
    {
        if (FilterContent == null || FilterPrefab == null)
        {
            Debug.LogError(
                "[Agents] Assign FilterContent and FilterPrefab."
            );
            return;
        }

        for (int i = FilterContent.childCount - 1; i >= 0; i--)
        {
            Destroy(FilterContent.GetChild(i).gameObject);
        }

        filterItems.Clear();

        for (int i = 0; i < sortKeys.Length; i++)
        {
            GameObject obj = Instantiate(FilterPrefab, FilterContent);

            FilterDropDownPrefab filter =
                obj.GetComponent<FilterDropDownPrefab>();

            if (filter == null)
            {
                Debug.LogError(
                    "[Agents] FilterPrefab is missing FilterDropDownPrefab."
                );

                Destroy(obj);
                continue;
            }

            filter.Setup(GetLocalizedSortLabel(i), i, this);

            filterItems.Add(filter);
            obj.SetActive(true);
        }
    }

    private string GetLocalizedSortLabel(int index)
    {
        switch (index)
        {
            case 0:
                return Loc.Get("this_week_winnings");

            case 1:
                return Loc.Get("this_week_fee");

            case 2:
                return Loc.Get("last_week_winnings");

            case 3:
                return Loc.Get("last_week_fee");

            case 4:
                return Loc.Get("total_winnings");

            case 5:
                return Loc.Get("total_fee");

            default:
                return sortLabels[index];
        }
    }

    private void ToggleFilterPanel()
    {
        if (FilterPanel == null)
            return;

        bool show = !FilterPanel.gameObject.activeSelf;
        FilterPanel.gameObject.SetActive(show);

        if (show)
            UpdateFilterUI();
    }

    public void SelectSortOption(int index)
    {
        if (index < 0 || index >= sortKeys.Length)
            return;

        if (selectedSortIndex == index)
        {
            // Same option clicked again: reverse its direction.
            sortDescending[index] = !sortDescending[index];
        }
        else
        {
            // First time selecting an option: highest value first.
            selectedSortIndex = index;

            if (!sortHasBeenClicked[index])
                sortDescending[index] = true;
        }

        sortHasBeenClicked[index] = true;
        selectedSortIndex = index;
        currentSortBy = sortKeys[index];

        UpdateFilterUI();
        GenerateAgents(GetSearchText());
    }

    public bool IsSortDescending(int index)
    {
        return index >= 0 &&
               index < sortDescending.Length &&
               sortDescending[index];
    }

    public bool IsSortSelected(int index)
    {
        return selectedSortIndex == index;
    }

    private void UpdateFilterUI()
    {
        if (CurrentFilterName != null)
        {
            int index = Mathf.Clamp(
                selectedSortIndex, 0, sortKeys.Length - 1
            );

            CurrentFilterName.text = GetLocalizedSortLabel(index);
        }

        for (int i = 0; i < filterItems.Count; i++)
        {
            if (filterItems[i] != null)
                filterItems[i].RefreshUI();
        }
    }

    // ---------------------------------------------------------
    // ONLINE STATUS
    // ---------------------------------------------------------

    private void HandleMemberOnline(string playerId)
    {
        if (!string.IsNullOrEmpty(playerId))
            RefreshOnlineAgents().Forget();
    }

    private void StartOnlineRefresh()
    {
        StopOnlineRefresh();
        onlineRefreshCoroutine = StartCoroutine(OnlineRefreshLoop());
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
        if (string.IsNullOrEmpty(ClubId) ||
            AuthManager.Instance == null)
            return;

        int version = viewVersion;
        string requestedClubId = ClubId;

        try
        {
            List<string> onlineList =
                await AuthManager.Instance.GetClubOnlineMembersAsync(
                    requestedClubId
                );

            if (!IsCurrent(version, requestedClubId))
                return;

            onlineUserIds = onlineList != null
                ? new HashSet<string>(onlineList)
                : new HashSet<string>();

            GenerateAgents(GetSearchText());
        }
        catch (Exception e)
        {
            if (IsCurrent(version, requestedClubId))
            {
                Debug.LogError(
                    "[Agents] Online refresh failed: " + e.Message
                );
            }
        }
    }

    private bool IsCurrent(int version, string clubId)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion &&
               clubId == ClubId;
    }

    // ---------------------------------------------------------
    // LOAD AGENTS
    // ---------------------------------------------------------

    public async UniTaskVoid LoadAgents()
    {
        int version = viewVersion;
        int request = ++loadVersion;
        string requestedClubId = ClubId;

        ClearAgents();
        allAgents.Clear();
        displayAgents.Clear();
        onlineUserIds.Clear();

        StopOnlineRefresh();

        if (string.IsNullOrEmpty(requestedClubId))
        {
            Debug.LogError("[Agents] ClubId missing.");
            return;
        }

        if (AuthManager.Instance == null)
        {
            Debug.LogError("[Agents] AuthManager.Instance is missing.");
            return;
        }

        loadingAgents = true;

        try
        {
            allAgents = await AuthManager.Instance.GetClubMembersAsync(
                requestedClubId,
                "ALL",
                "chips"
            );

            if (!IsCurrent(version, requestedClubId) ||
                request != loadVersion)
                return;

            if (allAgents == null)
                allAgents = new List<ClubMemberData>();

            // Fetch each agent's statistics once.
            // Filter changes will sort cached data without refetching.
            foreach (ClubMemberData agent in allAgents)
            {
                if (!IsCurrent(version, requestedClubId) ||
                    request != loadVersion)
                    return;

                if (agent == null)
                    continue;

                string role = NormalizeRole(agent.Role);

                if (role != "AGENT" && role != "SUPER_AGENT")
                    continue;

                AgentDataApiResponse agentData =
                    await AuthManager.Instance.GetAgentDataAsync(
                        requestedClubId,
                        agent.UserId
                    );

                if (!IsCurrent(version, requestedClubId) ||
                    request != loadVersion)
                    return;

                displayAgents.Add(new AgentDisplayData
                {
                    Member = agent,
                    AgentData = agentData
                });
            }

            List<string> onlineList =
                await AuthManager.Instance.GetClubOnlineMembersAsync(
                    requestedClubId
                );

            if (!IsCurrent(version, requestedClubId) ||
                request != loadVersion)
                return;

            onlineUserIds = onlineList != null
                ? new HashSet<string>(onlineList)
                : new HashSet<string>();

            GenerateAgents(GetSearchText());

            StartOnlineRefresh();
        }
        catch (Exception e)
        {
            if (IsCurrent(version, requestedClubId) &&
                request == loadVersion)
            {
                Debug.LogError("[Agents] Load failed: " + e.Message);
            }
        }
        finally
        {
            if (request == loadVersion)
                loadingAgents = false;
        }
    }

    // ---------------------------------------------------------
    // GENERATE AND SORT
    // ---------------------------------------------------------

    private void GenerateAgents(string searchText)
    {
        ClearAgentRows();

        if (Agent_Content == null || AgentPrefab == null)
        {
            Debug.LogError(
                "[Agents] Assign Agent_Content and AgentPrefab."
            );
            return;
        }

        int agentCount = 0;
        int superAgentCount = 0;

        string lowerSearch = string.IsNullOrEmpty(searchText)
            ? ""
            : searchText.Trim().ToLowerInvariant();

        List<AgentDisplayData> filteredAgents =
            new List<AgentDisplayData>();

        foreach (AgentDisplayData item in displayAgents)
        {
            if (item == null || item.Member == null)
                continue;

            string role = NormalizeRole(item.Member.Role);

            if (role == "AGENT")
                agentCount++;
            else if (role == "SUPER_AGENT")
                superAgentCount++;

            if (!string.IsNullOrEmpty(lowerSearch))
            {
                string username = !string.IsNullOrEmpty(item.Member.Username)
                    ? item.Member.Username.ToLowerInvariant()
                    : "";

                string userId = !string.IsNullOrEmpty(item.Member.UserId)
                    ? item.Member.UserId.ToLowerInvariant()
                    : "";

                if (!username.Contains(lowerSearch) &&
                    !userId.Contains(lowerSearch))
                {
                    continue;
                }
            }

            filteredAgents.Add(item);
        }

        SortAgents(filteredAgents);

        foreach (AgentDisplayData item in filteredAgents)
        {
            bool isOnline =
                !string.IsNullOrEmpty(item.Member.UserId) &&
                onlineUserIds.Contains(item.Member.UserId);

            GameObject obj = Instantiate(AgentPrefab, Agent_Content);

            AgentPrefabScript prefab =
                obj.GetComponent<AgentPrefabScript>();

            if (prefab == null)
            {
                Debug.LogError(
                    "AgentPrefabScript component is missing on AgentPrefab."
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

            obj.SetActive(true);
            agentItems.Add(prefab);
        }

        if (OnlinePlayerCount != null)
        {
            OnlinePlayerCount.text =
                $"{Loc.Get("online_player")} : {onlineUserIds.Count}";
        }

        if (TotalAgent != null)
        {
            TotalAgent.text =
                $"{Loc.Get("agent")} : {agentCount}";
        }

        if (TotalSuperAgent != null)
        {
            TotalSuperAgent.text =
                $"{Loc.Get("superagent")} : {superAgentCount}";
        }
    }

    private void SortAgents(List<AgentDisplayData> agents)
    {
        if (agents == null)
            return;

        bool groupByRole =
            GroupbyRole != null && GroupbyRole.isOn;

        bool descending = sortDescending[selectedSortIndex];

        agents.Sort((a, b) =>
        {
            if (groupByRole)
            {
                int aRoleOrder = GetRoleOrder(
                    a != null && a.Member != null ? a.Member.Role : ""
                );

                int bRoleOrder = GetRoleOrder(
                    b != null && b.Member != null ? b.Member.Role : ""
                );

                int roleComparison = aRoleOrder.CompareTo(bRoleOrder);

                if (roleComparison != 0)
                    return roleComparison;
            }

            int aValue = GetAgentSortValue(
                a != null ? a.AgentData : null,
                selectedSortIndex
            );

            int bValue = GetAgentSortValue(
                b != null ? b.AgentData : null,
                selectedSortIndex
            );

            return descending
                ? bValue.CompareTo(aValue)
                : aValue.CompareTo(bValue);
        });
    }

    private int GetRoleOrder(string role)
    {
        switch (NormalizeRole(role))
        {
            case "SUPER_AGENT":
                return 0;

            case "AGENT":
                return 1;

            default:
                return 2;
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
            .ToUpperInvariant();
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
                return data.Stats.LastWeek != null
                    ? data.Stats.LastWeek.Winnings
                    : 0;

            case 3:
                return data.Stats.LastWeek != null
                    ? data.Stats.LastWeek.Fee
                    : 0;

            case 4:
                return data.Stats.Total != null
                    ? data.Stats.Total.Winnings
                    : 0;

            case 5:
                return data.Stats.Total != null
                    ? data.Stats.Total.Fee
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

    private void OnSearchChanged(string search)
    {
        GenerateAgents(search);
    }

    private void OnGroupByRoleChanged(bool isGrouped)
    {
        GenerateAgents(GetSearchText());
    }

    // ---------------------------------------------------------
    // MEMBER DETAIL
    // ---------------------------------------------------------

    private void ClearAgentRows()
    {
        agentItems.Clear();

        if (Agent_Content == null)
            return;

        for (int i = Agent_Content.childCount - 1; i >= 0; i--)
        {
            GameObject row = Agent_Content.GetChild(i).gameObject;
            row.SetActive(false);
            Destroy(row);
        }
    }

    private void ClearAgents()
    {
        ClearAgentRows();
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

    private void OnDestroy()
    {
        if (FilterButton != null)
            FilterButton.onClick.RemoveListener(ToggleFilterPanel);

        if (Search_InputField != null)
            Search_InputField.onValueChanged.RemoveListener(OnSearchChanged);

        if (GroupbyRole != null)
            GroupbyRole.onValueChanged.RemoveListener(OnGroupByRoleChanged);
    }
}
