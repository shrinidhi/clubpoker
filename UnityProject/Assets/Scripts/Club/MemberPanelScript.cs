
using System;
using System.Globalization;
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

    private string currentSortBy = "chips";
    private Coroutine onlineRefreshCoroutine;
    private int viewVersion;
    private int loadVersion;
    private bool refreshingOnline;

    public Text PlayerOnlineCount;

    [Header("Group By Role")]
    public Toggle Groupbyrole;

    [Header("Custom Filter UI")]
    public TextMeshProUGUI CurrentFilterName;
    public Button FilterButton;
    public GameObject FilterPanel;
    public GameObject FilterPrefab;
    public Transform FilterContent;

    private readonly string[] sortKeys =
    {
        "chips",
        "hands",
        "winnings",
        "lastlogin",
        "fees"
    };

    private readonly string[] sortLabels =
    {
        "Chips",
        "Hands",
        "Winnings",
        "Last Login",
        "Fees"
    };

    private readonly bool[] sortDescending = new bool[5];
    private readonly bool[] sortHasBeenClicked = new bool[5];

    private readonly List<FilterDropDownPrefab> filterItems =
        new List<FilterDropDownPrefab>();

    private int selectedSortIndex = -1;

    private void Awake()
    {
        // Default selected option: Chips
        selectedSortIndex = 0;
        currentSortBy = "chips";

        // Highest chips first by default.
        sortDescending[0] = true;
        sortHasBeenClicked[0] = true;

        SetupFilterOptions();

        if (FilterButton != null)
        {
            FilterButton.onClick.RemoveListener(ToggleFilterPanel);
            FilterButton.onClick.AddListener(ToggleFilterPanel);
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

        if (FilterPanel != null)
            FilterPanel.gameObject.SetActive(false);

        UpdateFilterUI();
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnMemberOnline += HandleMemberOnline;

        ++viewVersion;

        ClubRole role = ClubContext.SelectedClub != null
            ? ClubContext.ParseRole(ClubContext.SelectedClub.Role)
            : ClubRole.Member;

        bool showRoleControls = role != ClubRole.Agent;

        if (Groupbyrole != null)
            Groupbyrole.gameObject.SetActive(showRoleControls);

        if (ManagerCount != null)
            ManagerCount.gameObject.SetActive(showRoleControls);

        if (AgentCount != null)
            AgentCount.gameObject.SetActive(showRoleControls);

        if (SuperAgentCount != null)
            SuperAgentCount.gameObject.SetActive(showRoleControls);

        ClubId = ClubContext.SelectedClub != null
            ? ClubContext.SelectedClub.ClubId
            : "";

        if (Search_InputFiled != null)
            Search_InputFiled.SetTextWithoutNotify("");

        LoadMembers().Forget();

        StartOnlineRefresh();
    }

    private void OnDisable()
    {
        ++viewVersion;
        ++loadVersion;

        StopOnlineRefresh();

        ClubSocketHandler.OnMemberOnline -= HandleMemberOnline;
    }

    private void SetupFilterOptions()
    {
        if (FilterContent == null || FilterPrefab == null)
        {
            Debug.LogError(
                "[Members] Assign FilterContent and FilterPrefab."
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
                    "[Members] FilterPrefab is missing FilterDropDownPrefab."
                );

                Destroy(obj);
                continue;
            }

            string label = GetLocalizedSortLabel(i);

            filter.Setup(label, i, this);

            filterItems.Add(filter);

            obj.SetActive(true);
        }
    }

    private string GetLocalizedSortLabel(int index)
    {
        switch (index)
        {
            case 0:
                return Loc.Get("chips");

            case 1:
                return Loc.Get("hands");

            case 2:
                return Loc.Get("winnings");

            case 3:
                return Loc.Get("last_login");

            case 4:
                return Loc.Get("fees");

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

        // Clicking the currently selected option reverses its order.
        if (selectedSortIndex == index)
        {
            sortDescending[index] = !sortDescending[index];
            sortHasBeenClicked[index] = true;
        }
        else
        {
            // First selection of an option: highest/latest first.
            // Previously selected options remember their direction.
            selectedSortIndex = index;

            if (!sortHasBeenClicked[index])
            {
                sortDescending[index] = true;
                sortHasBeenClicked[index] = true;
            }
        }

        selectedSortIndex = index;
        currentSortBy = sortKeys[index];

        UpdateFilterUI();

        GenerateMembers(GetSearchText());
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
            int index = selectedSortIndex >= 0
                ? selectedSortIndex
                : 0;

            CurrentFilterName.text = GetLocalizedSortLabel(index);
        }

        for (int i = 0; i < filterItems.Count; i++)
        {
            if (filterItems[i] != null)
                filterItems[i].RefreshUI();
        }
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

    private async UniTask RefreshOnlineMembers()
    {
        if (string.IsNullOrEmpty(ClubId) || refreshingOnline)
            return;

        int version = viewVersion;
        string clubId = ClubId;

        refreshingOnline = true;

        try
        {
            if (AuthManager.Instance == null)
                return;

            List<string> onlineList = await AuthManager.Instance
                .GetClubOnlineMembersAsync(clubId);

            if (!IsCurrent(version, clubId))
                return;

            onlineUserIds = onlineList != null
                ? new HashSet<string>(onlineList)
                : new HashSet<string>();

            GenerateMembers(GetSearchText());
        }
        catch (Exception e)
        {
            if (IsCurrent(version, clubId))
                Debug.LogError(
                    "[Members] Online refresh failed: " + e.Message
                );
        }
        finally
        {
            refreshingOnline = false;
        }
    }

    private bool IsCurrent(int version, string clubId)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion &&
               clubId == ClubId;
    }

    private string GetSearchText()
    {
        return Search_InputFiled != null
            ? Search_InputFiled.text.Trim()
            : "";
    }

    private void OnSearchChanged(string value)
    {
        GenerateMembers(value);
    }

    private void OnGroupByRoleChanged(bool isGrouped)
    {
        GenerateMembers(GetSearchText());
    }

    public async UniTaskVoid LoadMembers()
    {
        int version = viewVersion;
        int request = ++loadVersion;
        string clubId = ClubId;

        ClearMembers();
        allMembers.Clear();
        onlineUserIds.Clear();

        if (string.IsNullOrEmpty(clubId))
        {
            Debug.LogError("[Members] Club ID is missing.");
            return;
        }

        try
        {
            if (AuthManager.Instance == null)
                throw new InvalidOperationException(
                    "AuthManager is missing."
                );

            var members = await AuthManager.Instance.GetClubMembersAsync(
                clubId, "ALL", "chips"
            );

            if (!IsCurrent(version, clubId) || request != loadVersion)
                return;

            allMembers = members ?? new List<ClubMemberData>();

            GenerateMembers(GetSearchText());

            await RefreshOnlineMembers();
        }
        catch (Exception e)
        {
            if (IsCurrent(version, clubId) && request == loadVersion)
            {
                Debug.LogError("[Members] Load failed: " + e.Message);
            }
        }
    }

    private void GenerateMembers(string searchText)
    {
        ClearMembers();

        if (Member_Content == null || Member_Prefab == null)
        {
            Debug.LogError(
                "[Members] Assign Member_Content and Member_Prefab."
            );
            return;
        }

        int managerCount = 0;
        int agentCount = 0;
        int superAgentCount = 0;

        string lowerSearch = string.IsNullOrEmpty(searchText)
            ? ""
            : searchText.Trim().ToLowerInvariant();

        List<ClubMemberData> filteredMembers =
            new List<ClubMemberData>();

        ClubRole currentRole = ClubContext.SelectedClub != null
            ? ClubContext.ParseRole(ClubContext.SelectedClub.Role)
            : ClubRole.Member;

        foreach (ClubMemberData member in allMembers)
        {
            if (member == null)
                continue;

            string role = NormalizeRole(member.Role);

            if (currentRole == ClubRole.Agent &&
                (role == "CREATOR" || role == "MANAGER"))
            {
                continue;
            }

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
                        ? member.Username.ToLowerInvariant()
                        : "";

                string userId =
                    !string.IsNullOrEmpty(member.UserId)
                        ? member.UserId.ToLowerInvariant()
                        : "";

                if (!username.Contains(lowerSearch) &&
                    !userId.Contains(lowerSearch))
                {
                    continue;
                }
            }

            filteredMembers.Add(member);
        }

        bool descending = sortDescending[
            Array.IndexOf(sortKeys, currentSortBy) >= 0
                ? Array.IndexOf(sortKeys, currentSortBy)
                : 0
        ];

        switch (currentSortBy)
        {
            case "hands":
                filteredMembers = descending
                    ? filteredMembers.OrderByDescending(m => m.HandsPlayed).ToList()
                    : filteredMembers.OrderBy(m => m.HandsPlayed).ToList();
                break;

            case "winnings":
                filteredMembers = descending
                    ? filteredMembers.OrderByDescending(m => m.TotalWinnings).ToList()
                    : filteredMembers.OrderBy(m => m.TotalWinnings).ToList();
                break;

            case "fees":
                filteredMembers = descending
                    ? filteredMembers.OrderByDescending(m => m.TotalFee).ToList()
                    : filteredMembers.OrderBy(m => m.TotalFee).ToList();
                break;

            case "lastlogin":
                filteredMembers = descending
                    ? filteredMembers.OrderByDescending(
                        m => GetLoginDate(m.LastLoginAt)
                    ).ToList()
                    : filteredMembers.OrderBy(
                        m => GetLoginDate(m.LastLoginAt)
                    ).ToList();
                break;

            default:
                filteredMembers = descending
                    ? filteredMembers.OrderByDescending(m => m.Chips).ToList()
                    : filteredMembers.OrderBy(m => m.Chips).ToList();
                break;
        }

        // Keep role groups in the existing order.
        // Sorting within each role follows the selected sort order.
        if (Groupbyrole != null && Groupbyrole.isOn)
        {
            filteredMembers = filteredMembers
                .OrderBy(member =>
                    GetRoleOrder(member.Role, member.IsTableManager))
                .ToList();
        }

        foreach (ClubMemberData member in filteredMembers)
        {
            bool isOnline =
                !string.IsNullOrEmpty(member.UserId) &&
                onlineUserIds.Contains(member.UserId);

            GameObject obj = Instantiate(Member_Prefab, Member_Content);

            MemberPrefabScript prefab =
                obj.GetComponent<MemberPrefabScript>();

            if (prefab == null)
            {
                Debug.LogError(
                    "MemberPrefabScript component is missing on Member_Prefab."
                );

                Destroy(obj);
                continue;
            }

            prefab.Setup(
                member,
                OnMemberClicked,
                isOnline,
                currentSortBy
            );

            obj.SetActive(true);
            memberItems.Add(prefab);
        }

        if (PlayerOnlineCount != null)
        {
            PlayerOnlineCount.text =
                $"{Loc.Get("online_player")} : {onlineUserIds.Count}";
        }

        if (ManagerCount != null)
        {
            ManagerCount.text =
                $"{Loc.Get("manager")} : {managerCount}";
        }

        if (AgentCount != null)
        {
            AgentCount.text =
                $"{Loc.Get("agent")} : {agentCount}";
        }

        if (SuperAgentCount != null)
        {
            SuperAgentCount.text =
                $"{Loc.Get("superagent")} : {superAgentCount}";
        }
    }

    private static DateTimeOffset GetLoginDate(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset date
        )
            ? date
            : DateTimeOffset.MinValue;
    }

    private string NormalizeRole(string role)
    {
        if (string.IsNullOrEmpty(role))
            return "MEMBER";

        return role
            .Trim()
            .Replace(" ", "_")
            .Replace("-", "_")
            .ToUpperInvariant();
    }

    private int GetRoleOrder(string role, bool isTableManager = false)
    {
        string normalizedRole = NormalizeRole(role);

        switch (normalizedRole)
        {
            case "CREATOR":
                return 0;

            case "MANAGER":
                return isTableManager ? 2 : 1;

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
            GameObject row = Member_Content.GetChild(i).gameObject;

            row.SetActive(false);
            Destroy(row);
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

    private void OnDestroy()
    {
        if (FilterButton != null)
            FilterButton.onClick.RemoveListener(ToggleFilterPanel);

        if (Search_InputFiled != null)
            Search_InputFiled.onValueChanged.RemoveListener(OnSearchChanged);

        if (Groupbyrole != null)
            Groupbyrole.onValueChanged.RemoveListener(OnGroupByRoleChanged);
    }
}
