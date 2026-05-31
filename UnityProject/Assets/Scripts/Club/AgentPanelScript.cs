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
    public ShowClubTableScreenScript ShowClubTableScreenScript;

    private List<AgentPrefabScript> agentItems =
        new List<AgentPrefabScript>();

    private List<ClubMemberData> allAgents =
        new List<ClubMemberData>();

    public InputField Search_InputField;
    public TMP_Dropdown FilterDropDown;

    private string currentSortBy = "chips";

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
        ClubId = ShowClubTableScreenScript.CLubID;

        if (Search_InputField != null)
            Search_InputField.text = "";

        LoadAgents().Forget();
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
        LoadAgents().Forget();
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
                return "lastLogin";

            default:
                return "chips";
        }
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

            GameObject obj =
                Instantiate(AgentPrefab, Agent_Content);

            AgentPrefabScript prefab =
                obj.GetComponent<AgentPrefabScript>();

            prefab.Setup(agent, agentData);
            agentItems.Add(prefab);
        }

        TotalAgent.text = "Agent : " + agentCount;
        TotalSuperAgent.text = "SuperAgent : " + superAgentCount;
    }

    private void ClearAgents()
    {
        agentItems.Clear();

        for (int i = Agent_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Agent_Content.GetChild(i).gameObject);
        }
    }
}