using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

public class MemberDetail_RoleSelectionScreenScript : MonoBehaviour
{
    public TextMeshProUGUI RoleType;
    public Text PlayerName;
    public Text PlayerID;
    public Text NickName;
    public Text LastLogin;
    public Text WinningCount;
    public Text HandsCount;
    public Text BB_100_Count;
    public Text Fee_Count;
    public Text Remark;
    public Button Manager_Button;
    public Button Agent_Button;
    public Button SuperAgent_Button;
    public Button Member_Button;

    public Sprite SelectButtonSprite;
    public Sprite UnSelectButtonSprite;

    public Button CloseButton;


    
    public string ClubId;
    private string userId;
    public GameObject RoleTypeGrid;
    public GameObject CreatorPanel;
    public GameObject ButtonPanel;
    public MemberPanelScript MemberPanelScript;

    public Button TracePlayer_Button;
    public Button AuthorizationRestriction_Button;
    public Button SuspendAccess_Button;
    public Button AgentCredit_Button;
    public Button DownlineManagement_Button;
    public Button AgentData_Button;
    public Button NoSpeech_Button;
    public Button RestrictAccess_Button;
    public Button SendGift_Button;
    public Button DeleteMember_Button;

    // Start is called before the first frame update
    void Start()
    {
        Manager_Button.onClick.AddListener(() =>
        {
            ChangeRole("MANAGER");
            ShowMember(ClubId, userId);
        });

        Agent_Button.onClick.AddListener(() =>
        {
            ChangeRole("AGENT");
            ShowMember(ClubId, userId);
        });

        SuperAgent_Button.onClick.AddListener(() =>
        {
            ChangeRole("SUPER AGENT");
            ShowMember(ClubId, userId);
        });

        Member_Button.onClick.AddListener(() =>
        {
            ChangeRole("MEMBER");
            ShowMember(ClubId, userId);
        });

        if (CloseButton != null)
            CloseButton.onClick.AddListener(() => gameObject.SetActive(false));

        if (DeleteMember_Button != null)
            DeleteMember_Button.onClick.AddListener(DeleteMemberButtonOnTap);
    }

    public async void ShowMember(
    string clubId,
    string memberUserId)
    {
        ClubId = clubId;
        userId = memberUserId;

        ClubMemberData member =
            await AuthManager.Instance.GetMemberDetailAsync(
                clubId,
                memberUserId);

        if (member == null)
            return;

        RoleType.text = member.Role;

        PlayerName.text = member.Username;
        PlayerID.text = member.UserId.Substring(0, 8); 
        NickName.text = "Nickname : "+member.Username;
        Remark.text = member.Remark;
        LastLogin.text = member.LastLoginAt;
        WinningCount.text = member.TotalWinnings.ToString();
        HandsCount.text = member.HandsPlayed.ToString();
        BB_100_Count.text = member.BB100.ToString();
        Fee_Count.text = member.TotalFee.ToString();

        UpdateRoleButtons(member.Role);
        MemberPanelScript.LoadMembers().Forget();
    }

    private async void ChangeRole(string role)
    {
        bool success =
            await AuthManager.Instance.UpdateMemberRoleAsync(
                ClubId,
                userId,
                role);

        if (!success)
            return;

        RoleType.text = role;

        UpdateRoleButtons(role);

        Debug.Log("Role Updated : " + role);
    }

    private void UpdateRoleButtons(string role)
    {
        bool isCreator = role == "CREATOR";
        bool isManager = role == "MANAGER";
        bool isAgent = role == "AGENT";
        bool isMember = role == "MEMBER";
        bool isSuperAgent = role == "SUPER AGENT";
        if (RoleTypeGrid != null)
            RoleTypeGrid.SetActive(!isCreator);

        if (CreatorPanel != null)
            CreatorPanel.SetActive(isCreator);

        if (ButtonPanel != null)
            ButtonPanel.SetActive(isCreator);

        SetActionButtons(role);

        SetRoleSelected(Manager_Button, isManager);
        SetRoleSelected(Agent_Button, isAgent);
        SetRoleSelected(SuperAgent_Button, isSuperAgent);
        SetRoleSelected(Member_Button, isMember);
    }

    private void SetActionButtons(string role)
    {
        bool tracePlayer = false;
        bool authorizationRestriction = false;
        bool suspendAccess = false;
        bool agentCredit = false;
        bool downlineManagement = false;
        bool agentData = false;
        bool noSpeech = false;
        bool restrictAccess = false;
        bool sendGift = false;
        bool deleteMember = false;

        switch (role)
        {
            case "MANAGER":
                tracePlayer = true;
                authorizationRestriction = true;
                sendGift = true;
                deleteMember = true;
                ButtonPanel.SetActive(true);
                break;

            case "AGENT":
                tracePlayer = true;
                authorizationRestriction = true;
                suspendAccess = true;
                agentCredit = true;
                downlineManagement = true;
                agentData = true;
                noSpeech = true;
                restrictAccess = true;
                sendGift = true;
                deleteMember = true;
                ButtonPanel.SetActive(true);
                break;

            case "MEMBER":
                tracePlayer = true;
                noSpeech = true;
                restrictAccess = true;
                sendGift = true;
                deleteMember = true;
                ButtonPanel.SetActive(true);
                break;

            case "CREATOR":
            default:
                break;
        }

        SetButtonActive(TracePlayer_Button, tracePlayer);
        SetButtonActive(AuthorizationRestriction_Button, authorizationRestriction);
        SetButtonActive(SuspendAccess_Button, suspendAccess);
        SetButtonActive(AgentCredit_Button, agentCredit);
        SetButtonActive(DownlineManagement_Button, downlineManagement);
        SetButtonActive(AgentData_Button, agentData);
        SetButtonActive(NoSpeech_Button, noSpeech);
        SetButtonActive(RestrictAccess_Button, restrictAccess);
        SetButtonActive(SendGift_Button, sendGift);
        SetButtonActive(DeleteMember_Button, deleteMember);
    }

    private void SetButtonActive(Button button, bool active)
    {
        if (button != null)
            button.gameObject.SetActive(active);
    }
    private void SetRoleSelected(
    Button button,
    bool isSelected)
    {
        Sprite targetSprite =
            isSelected ? SelectButtonSprite : UnSelectButtonSprite;

        if (button != null && button.image != null)
            button.image.sprite = targetSprite;

       
    }

    private async void DeleteMemberButtonOnTap()
    {
        if (string.IsNullOrEmpty(ClubId) || string.IsNullOrEmpty(userId))
            return;

        DeleteMember_Button.interactable = false;

        try
        {
            DeleteClubMemberResponse response =
                await AuthManager.Instance.DeleteClubMemberAsync(
                    ClubId,
                    userId
                );

            if (response != null && response.Removed)
            {
                Debug.Log("Member deleted: " + response.UserId);

                if (MemberPanelScript != null)
                    MemberPanelScript.LoadMembers().Forget();

                gameObject.SetActive(false);
            }
        }
        catch
        {
            Debug.LogError("Member delete failed");
        }

        DeleteMember_Button.interactable = true;
    }
 
}
