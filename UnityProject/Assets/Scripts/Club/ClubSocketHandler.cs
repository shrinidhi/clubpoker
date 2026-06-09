using System;
using UnityEngine;
using Newtonsoft.Json;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;

public class ClubSocketHandler : MonoBehaviour
{
    public static ClubSocketHandler Instance;

    public static event Action<ClubNewApplicationPayload> OnNewApplication;
    public static event Action<ClubMembershipApprovedPayload> OnMembershipApproved;
    public static event Action<ClubKickedPayload> OnKicked;
    public static event Action<ClubTableUpdatedPayload> OnTableUpdated;

    public static event Action<string> OnJoinNotification;

    private const string EVENT_JOIN_CLUB = "player:join_club";
    private const string EVENT_JOIN_NOTIFICATION = "player:join_notification";
    private const string EVENT_NEW_APPLICATION = "club:new_application";
    private const string EVENT_MEMBERSHIP_APPROVED = "club:membership_approved";
    private const string EVENT_KICKED = "club:kicked";
    private const string EVENT_TABLE_UPDATED = "club:table_updated";

    private string pendingClubId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (SocketManager.Instance != null)
            SocketManager.Instance.OnAuthenticated += OnSocketAuthenticated;
        else
            SocketManager.OnInstanceReady += OnSocketManagerReady;
    }

    private void OnSocketManagerReady()
    {
        SocketManager.OnInstanceReady -= OnSocketManagerReady;

        if (SocketManager.Instance != null)
            SocketManager.Instance.OnAuthenticated += OnSocketAuthenticated;
    }

    private void OnDestroy()
    {
        if (SocketManager.Instance != null)
        {
            SocketManager.Instance.OnAuthenticated -= OnSocketAuthenticated;

            SocketManager.Instance.Off(EVENT_JOIN_NOTIFICATION);
            SocketManager.Instance.Off(EVENT_NEW_APPLICATION);
            SocketManager.Instance.Off(EVENT_MEMBERSHIP_APPROVED);
            SocketManager.Instance.Off(EVENT_KICKED);
            SocketManager.Instance.Off(EVENT_TABLE_UPDATED);
        }

        SocketManager.OnInstanceReady -= OnSocketManagerReady;
    }

    private void OnSocketAuthenticated(SocketAuthenticatedPayload payload)
    {
        Debug.Log("[ClubSocket] Socket authenticated");

        RegisterClubEvents();

        if (!string.IsNullOrEmpty(pendingClubId))
            JoinClubPage(pendingClubId);
    }

    private void RegisterClubEvents()
    {
        SocketManager.Instance.Off(EVENT_JOIN_NOTIFICATION);
        SocketManager.Instance.Off(EVENT_NEW_APPLICATION);
        SocketManager.Instance.Off(EVENT_MEMBERSHIP_APPROVED);
        SocketManager.Instance.Off(EVENT_KICKED);
        SocketManager.Instance.Off(EVENT_TABLE_UPDATED);

        SocketManager.Instance.On(EVENT_JOIN_NOTIFICATION, OnJoinNotificationReceived);
        SocketManager.Instance.On(EVENT_NEW_APPLICATION, OnNewApplicationReceived);
        SocketManager.Instance.On(EVENT_MEMBERSHIP_APPROVED, OnMembershipApprovedReceived);
        SocketManager.Instance.On(EVENT_KICKED, OnKickedReceived);
        SocketManager.Instance.On(EVENT_TABLE_UPDATED, OnTableUpdatedReceived);
    }

    public void JoinClubPage(string clubId)
    {
        if (string.IsNullOrEmpty(clubId))
        {
            Debug.LogError("[ClubSocket] clubId missing");
            return;
        }

        pendingClubId = clubId;

        if (SocketManager.Instance == null)
        {
            Debug.LogError("[ClubSocket] SocketManager null");
            return;
        }

        if (!SocketManager.Instance.IsConnected)
        {
            Debug.LogWarning("[ClubSocket] Socket not connected. Waiting auth...");
            return;
        }

        string playerId = AuthManager.Instance.Session.Id;

        var joinClubPayload = new
        {
            clubId = clubId,
            playerId = playerId
        };

        var notificationPayload = new
        {
            playerId = playerId
        };

        Debug.Log("[ClubSocket] Emit player:join_club => " +
                  JsonConvert.SerializeObject(joinClubPayload));

        SocketManager.Instance.Emit(
            EVENT_JOIN_CLUB,
            joinClubPayload
        );

        Debug.Log("[ClubSocket] Emit player:join_notification => " +
                  JsonConvert.SerializeObject(notificationPayload));

        SocketManager.Instance.Emit(
            EVENT_JOIN_NOTIFICATION,
            notificationPayload
        );
    }

    private void OnJoinNotificationReceived(string json)
    {
        Debug.Log("[ClubSocket] player:join_notification => " + json);

        OnJoinNotification?.Invoke(json);
    }

    private void OnNewApplicationReceived(string json)
    {
        Debug.Log("[ClubSocket] club:new_application => " + json);

        try
        {
            ClubNewApplicationPayload payload =
                JsonConvert.DeserializeObject<ClubNewApplicationPayload>(json);

            OnNewApplication?.Invoke(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[ClubSocket] new_application parse failed: " + e.Message);
        }
    }

    private void OnMembershipApprovedReceived(string json)
    {
        Debug.Log("[ClubSocket] club:membership_approved => " + json);

        try
        {
            ClubMembershipApprovedPayload payload =
                JsonConvert.DeserializeObject<ClubMembershipApprovedPayload>(json);

            OnMembershipApproved?.Invoke(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[ClubSocket] membership_approved parse failed: " + e.Message);
        }
    }

    private void OnKickedReceived(string json)
    {
        Debug.Log("[ClubSocket] club:kicked => " + json);

        try
        {
            ClubKickedPayload payload =
                JsonConvert.DeserializeObject<ClubKickedPayload>(json);

            OnKicked?.Invoke(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[ClubSocket] kicked parse failed: " + e.Message);
        }
    }

    private void OnTableUpdatedReceived(string json)
    {
        Debug.Log("[ClubSocket] club:table_updated => " + json);

        try
        {
            ClubTableUpdatedPayload payload =
                JsonConvert.DeserializeObject<ClubTableUpdatedPayload>(json);

            OnTableUpdated?.Invoke(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[ClubSocket] table_updated parse failed: " + e.Message);
        }
    }
}