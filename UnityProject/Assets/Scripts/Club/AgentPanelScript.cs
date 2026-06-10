using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using TMPro;
using System.Collections;

public class AgentPanelScript : MonoBehaviour
{
    public Transform Agent_Content;
    public GameObject AgentPrefab;

    public Text TotalAgent;
    public Text TotalSuperAgent;

    public string ClubId;
    public ShowClubTableScreenScript ShowClubTableScreenScript;

    private List<AgentPrefabScript> agentItems =
        new List<AgentPrefabScript>();

    private List<ClubMemberData> allAgents =
        new List<ClubMemberData>();

    public InputField Search_InputField;
    public TMP_Dropdown FilterDropDown;

    private string currentSortBy = "chips";
    public MemberDetail_RoleSelectionScreenScript MemberDetailPopup;
    public Text OnlinePlayerCount;

    private HashSet<string> onlineUserIds = new HashSet<string>();
    private Coroutine onlineRefreshCoroutine;
    private void Start()
    {
        SetupFilterDropdown();

        if (FilterDropDown != null)
            FilterDropDown.onValueChanged.AddListener(OnFilterChanged);

        if (Search_InputField != null)
            Search_InputField.onValueChanged.AddListener(OnSearchChanged);
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnMemberOnline += HandleMemberOnline;

        ClubId = ShowClubTableScreenScript.CLubID;

        if (Search_InputField != null)
            Search_InputField.text = "";

        LoadAgents().Forget();

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
            RefreshOnlineAgents().Forget();
            yield return new WaitForSeconds(30f);
        }
    }

    private void HandleMemberOnline(string playerId)
    {
        RefreshOnlineAgents().Forget();
    }

    private async UniTaskVoid RefreshOnlineAgents()
    {
        if (string.IsNullOrEmpty(ClubId))
            return;

        List<string> onlineList =
            await AuthManager.Instance.GetClubOnlineMembersAsync(ClubId);

        onlineUserIds = new HashSet<string>(onlineList);

        string searchText = Search_InputField != null
            ? Search_InputField.text.Trim()
            : "";

        GenerateAgents(searchText);
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
        string searchText = Search_InputField != null
            ? Search_InputField.text.Trim()
            : "";

        GenerateAgents(searchText);
    }

    private void OnSearchChanged(string search)
    {
        GenerateAgents(search);
    }

    public async UniTaskVoid LoadAgents()
    {
        ClearAgents();

        if (string.IsNullOrEmpty(ClubId))
        {
            Debug.LogError("ClubId missing");
            return;
        }

        allAgents =
            await AuthManager.Instance.GetClubMembersAsync(
                ClubId,
                "AGENT",
                currentSortBy
            );

        List<string> onlineList =
            await AuthManager.Instance.GetClubOnlineMembersAsync(ClubId);

        onlineUserIds = new HashSet<string>(onlineList);

        string searchText = Search_InputField != null
            ? Search_InputField.text.Trim()
            : "";

        GenerateAgents(searchText);
    }

    private async void GenerateAgents(string searchText)
    {
        ClearAgents();

        int agentCount = 0;
        int superAgentCount = 0;

        string lowerSearch = string.IsNullOrEmpty(searchText)
            ? ""
            : searchText.ToLower();

        List<AgentDisplayData> displayAgents =
            new List<AgentDisplayData>();

        foreach (ClubMemberData agent in allAgents)
        {
            if (agent.Role == "AGENT")
                agentCount++;

            if (agent.Role == "SUPER_AGENT")
                superAgentCount++;

            if (!string.IsNullOrEmpty(lowerSearch))
            {
                string username = agent.Username != null
                    ? agent.Username.ToLower()
                    : "";

                string userId = agent.UserId != null
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
            GameObject obj =
                Instantiate(AgentPrefab, Agent_Content);

            AgentPrefabScript prefab =
                obj.GetComponent<AgentPrefabScript>();

            bool isOnline =
    !string.IsNullOrEmpty(item.Member.UserId) &&
    onlineUserIds.Contains(item.Member.UserId);

            prefab.Setup(
                item.Member,
                item.AgentData,
                OnMemberClicked,
                isOnline
            );
            agentItems.Add(prefab);
        }
        if (OnlinePlayerCount != null)
            OnlinePlayerCount.text = "Online Player(" + onlineUserIds.Count + ")";

        TotalAgent.text = "Agent : " + agentCount;
        TotalSuperAgent.text = "SuperAgent : " + superAgentCount;
    }

    private void SortAgents(List<AgentDisplayData> agents)
    {
        int selectedIndex = FilterDropDown != null
            ? FilterDropDown.value
            : 0;

        agents.Sort((a, b) =>
        {
            int bValue = GetAgentSortValue(b.AgentData, selectedIndex);
            int aValue = GetAgentSortValue(a.AgentData, selectedIndex);

            return bValue.CompareTo(aValue);
        });
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

    private void ClearAgents()
    {
        agentItems.Clear();

        for (int i = Agent_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Agent_Content.GetChild(i).gameObject);
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

    private class AgentDisplayData
    {
        public ClubMemberData Member;
        public AgentDataApiResponse AgentData;
    }
}