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
using TMPro;
using ClubPoker.Core;

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
    private int viewVersion;
    private int loadVersion;
    private bool refreshingOnline;

    public Text PlayerOnlineCount;

    [Header("Group By Role")]
    public Toggle Groupbyrole;

    private void Awake()
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
                Debug.LogError("[Members] Online refresh failed: " + e.Message);
        }
        finally
        {
            refreshingOnline = false;
        }
    }

    private bool IsCurrent(int version, string clubId)
    {
        return this != null && isActiveAndEnabled &&
               version == viewVersion && clubId == ClubId;
    }

    private string GetSearchText()
    {
        return Search_InputFiled != null
            ? Search_InputFiled.text.Trim()
            : "";
    }

    private void SetupFilterDropdown()
    {
        if (FilterDropDown == null)
            return;

        FilterDropDown.ClearOptions();

        FilterDropDown.AddOptions(new List<string>
        {
            Loc.Get("chips"),
            Loc.Get("hands"),
            Loc.Get("winnings"),
            Loc.Get("last_login"),
            Loc.Get("fees")
        });

        FilterDropDown.SetValueWithoutNotify(0);
        FilterDropDown.RefreshShownValue();
    }

    private void OnFilterChanged(int index)
    {
        currentSortBy = GetSortByKey(index);
        GenerateMembers(GetSearchText());
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

            case 4:
                return "fees";

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
                throw new InvalidOperationException("AuthManager is missing.");

            // Sort locally so Fees does not require a new server sort key.
            var members = await AuthManager.Instance.GetClubMembersAsync(
                clubId, "ALL", "chips");

            if (!IsCurrent(version, clubId) || request != loadVersion)
                return;

            allMembers = members ?? new List<ClubMemberData>();
            GenerateMembers(GetSearchText());

            await RefreshOnlineMembers();
        }
        catch (Exception e)
        {
            if (IsCurrent(version, clubId) && request == loadVersion)
                Debug.LogError("[Members] Load failed: " + e.Message);
        }
    }

    private void GenerateMembers(string searchText)
    {
        ClearMembers();

        if (Member_Content == null || Member_Prefab == null)
        {
            Debug.LogError("[Members] Assign Member_Content and Member_Prefab.");
            return;
        }

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
            ClubRole currentRole = ClubContext.SelectedClub != null
                ? ClubContext.ParseRole(ClubContext.SelectedClub.Role)
                : ClubRole.Member;
            if (currentRole == ClubRole.Agent && (role == "CREATOR" || role == "MANAGER"))
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

        switch (currentSortBy)
        {
            case "hands":
                filteredMembers = filteredMembers.OrderByDescending(m => m.HandsPlayed).ToList();
                break;
            case "winnings":
                filteredMembers = filteredMembers.OrderByDescending(m => m.TotalWinnings).ToList();
                break;
            case "fees":
                filteredMembers = filteredMembers.OrderByDescending(m => m.TotalFee).ToList();
                break;
            case "lastlogin":
                filteredMembers = filteredMembers.OrderByDescending(m => GetLoginDate(m.LastLoginAt)).ToList();
                break;
            default:
                filteredMembers = filteredMembers.OrderByDescending(m => m.Chips).ToList();
                break;
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
            value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset date)
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
        if (FilterDropDown != null)
            FilterDropDown.onValueChanged.RemoveListener(OnFilterChanged);
        if (Search_InputFiled != null)
            Search_InputFiled.onValueChanged.RemoveListener(OnSearchChanged);
        if (Groupbyrole != null)
            Groupbyrole.onValueChanged.RemoveListener(OnGroupByRoleChanged);
    }
}