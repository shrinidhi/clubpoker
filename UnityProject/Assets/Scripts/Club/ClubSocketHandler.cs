using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;
using ClubPoker.Core;

public class ClubSocketHandler : MonoBehaviour
{
    public static ClubSocketHandler Instance;
    public static event Action<ClubRoleChangedPayload> OnRoleChanged;
    public static event Action<ClubNewApplicationPayload> OnNewApplication;
    public static event Action<ClubMembershipApprovedPayload> OnMembershipApproved;
    public static event Action<ClubKickedPayload> OnKicked;
    public static event Action<ClubTableUpdatedPayload> OnTableUpdated;
    public static event Action<ClubScrollMessagePayload> OnScrollMessage;

    public static event Action<string> OnJoinNotification;
    public static event Action<string> OnMemberOnline;

    private const string EVENT_MEMBER_ONLINE = "club:member_online";
    private const string EVENT_JOIN_CLUB = "player:join_club";
    private const string EVENT_JOIN_NOTIFICATION = "player:join_notification";
    private const string EVENT_NEW_APPLICATION = "club:new_application";
    private const string EVENT_MEMBERSHIP_APPROVED = "club:membership_approved";
    private const string EVENT_KICKED = "club:kicked";
    private const string EVENT_TABLE_UPDATED = "club:table_updated";
    private const string EVENT_SCROLL_MESSAGE = "club:scroll_message";
    private const string EVENT_ROLE_CHANGE = "club:role_changed";
    private const string EVENT_FRIEND_REQUEST_RECEIVED = "friend:request_received";
    private const string EVENT_FRIEND_REQUEST_ACCEPTED = "friend:request_accepted";
    private const string EVENT_FRIEND_REMOVED = "friend:removed";

    public static event Action<FriendRequestReceivedPayload> OnFriendRequestReceived;
    public static event Action<FriendRequestAcceptedPayload> OnFriendRequestAccepted;
    public static event Action<FriendRemovedPayload> OnFriendRemoved;
    public static event Action OnFriendsChanged;

    private SocketManager boundSocket;
    private string notificationPlayerId;
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

        SocketManager.OnInstanceReady += OnSocketManagerReady;
        BindSocketManager();
    }

    private void OnSocketManagerReady()
    {
        BindSocketManager();
    }

    private void BindSocketManager()
    {
        var socket = SocketManager.Instance;

        if (socket == null || socket == boundSocket)
            return;

        if (boundSocket != null)
            boundSocket.OnAuthenticated -= OnSocketAuthenticated;

        boundSocket = socket;
        boundSocket.OnAuthenticated -= OnSocketAuthenticated;
        boundSocket.OnAuthenticated += OnSocketAuthenticated;

        // Handle a handler created after the socket has already connected.
        // OnAuthenticated repeats registration and room joining after authentication.
        if (boundSocket.IsConnected)
        {
            RegisterClubEvents();
            RegisterFriendEvents();
            JoinPlayerNotifications();
        }
    }

    private void OnDestroy()
    {
        // A duplicate destroyed in Awake must not unregister the live singleton.
        if (Instance != this)
            return;

        SocketManager.OnInstanceReady -= OnSocketManagerReady;

        if (boundSocket != null)
        {
            boundSocket.OnAuthenticated -= OnSocketAuthenticated;

            boundSocket.Off(EVENT_JOIN_NOTIFICATION);
            boundSocket.Off(EVENT_NEW_APPLICATION);
            boundSocket.Off(EVENT_MEMBERSHIP_APPROVED);
            boundSocket.Off(EVENT_KICKED);
            boundSocket.Off(EVENT_TABLE_UPDATED);
            boundSocket.Off(EVENT_MEMBER_ONLINE);
            boundSocket.Off(EVENT_SCROLL_MESSAGE);
            boundSocket.Off(EVENT_ROLE_CHANGE);
            boundSocket.Off(EVENT_FRIEND_REQUEST_RECEIVED);
            boundSocket.Off(EVENT_FRIEND_REQUEST_ACCEPTED);
            boundSocket.Off(EVENT_FRIEND_REMOVED);
        }

        Instance = null;
    }

    private void OnSocketAuthenticated(SocketAuthenticatedPayload payload)
    {
        HandleSocketAuthenticatedAsync(payload).Forget();
    }

    private async UniTask HandleSocketAuthenticatedAsync(SocketAuthenticatedPayload payload)
    {
        await UniTask.SwitchToMainThread();

        if (this == null || Instance != this ||
            boundSocket == null || !boundSocket.IsConnected)
            return;

        Debug.Log("[ClubSocket] Socket authenticated for player: " + payload?.PlayerId);

        RegisterClubEvents();
        RegisterFriendEvents();

        // This room is required even when no club or table is open.
        JoinPlayerNotifications(payload?.PlayerId);

        if (!string.IsNullOrWhiteSpace(pendingClubId))
            JoinClubPage(pendingClubId);

        NotifyFriendsChanged();
    }

    public void JoinPlayerNotifications(string playerId = null)
    {
        string id = !string.IsNullOrWhiteSpace(playerId)
            ? playerId
            : AuthManager.Instance?.Session?.Id;

        if (string.IsNullOrWhiteSpace(id))
            return;

        var socket = boundSocket != null ? boundSocket : SocketManager.Instance;
        if (socket == null || !socket.IsConnected)
            return;

        notificationPlayerId = id;

        // There is no acknowledgement for this event.
        socket.Emit(EVENT_JOIN_NOTIFICATION, new { playerId = id });
    }

    private void RegisterFriendEvents()
    {
        var socket = boundSocket != null ? boundSocket : SocketManager.Instance;
        if (socket == null)
            return;

        socket.Off(EVENT_FRIEND_REQUEST_RECEIVED);
        socket.Off(EVENT_FRIEND_REQUEST_ACCEPTED);
        socket.Off(EVENT_FRIEND_REMOVED);

        socket.On(EVENT_FRIEND_REQUEST_RECEIVED, OnFriendRequestReceivedSocket);
        socket.On(EVENT_FRIEND_REQUEST_ACCEPTED, OnFriendRequestAcceptedSocket);
        socket.On(EVENT_FRIEND_REMOVED, OnFriendRemovedSocket);
    }

    private void OnFriendRequestReceivedSocket(string json)
    {
        DispatchFriendEventAsync<FriendRequestReceivedPayload>(
            json, EVENT_FRIEND_REQUEST_RECEIVED, HandleFriendRequestReceived).Forget();
    }

    private void OnFriendRequestAcceptedSocket(string json)
    {
        DispatchFriendEventAsync<FriendRequestAcceptedPayload>(
            json, EVENT_FRIEND_REQUEST_ACCEPTED, HandleFriendRequestAccepted).Forget();
    }

    private void OnFriendRemovedSocket(string json)
    {
        DispatchFriendEventAsync<FriendRemovedPayload>(
            json, EVENT_FRIEND_REMOVED, HandleFriendRemoved).Forget();
    }

    private async UniTask DispatchFriendEventAsync<T>(
        string json, string eventName, Action<T> handler) where T : class
    {
        string roomPlayerId = notificationPlayerId;
        await UniTask.SwitchToMainThread();

        if (this == null || Instance != this ||
            string.IsNullOrWhiteSpace(roomPlayerId) ||
            !string.Equals(roomPlayerId, notificationPlayerId, StringComparison.Ordinal))
            return;

        string sessionId = AuthManager.Instance?.Session?.Id;
        if (!string.Equals(roomPlayerId, sessionId, StringComparison.Ordinal))
            return;

        T payload;
        try
        {
            payload = JsonConvert.DeserializeObject<T>(json);
            if (payload == null)
                throw new InvalidOperationException("Event payload was empty.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Friends Socket] " + eventName + " parse failed: " + e.Message);
            return;
        }

        try
        {
            handler(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[Friends Socket] " + eventName + " handler failed: " + e.Message);
        }
        finally
        {
            NotifyFriendsChanged();
        }
    }

    private void HandleFriendRequestReceived(FriendRequestReceivedPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.RequestId) ||
            string.IsNullOrWhiteSpace(payload.SenderId))
            throw new InvalidOperationException("Request ID or sender ID is missing.");

        string name = string.IsNullOrWhiteSpace(payload.SenderUsername)
            ? "A player" : payload.SenderUsername;

        ShowFriendMessage(name + " sent you a friend request.");
        InvokeSubscribers(OnFriendRequestReceived, payload);
    }

    private void HandleFriendRequestAccepted(FriendRequestAcceptedPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.FriendshipId) ||
            string.IsNullOrWhiteSpace(payload.AccepterId))
            throw new InvalidOperationException("Friendship ID or accepter ID is missing.");

        string name = string.IsNullOrWhiteSpace(payload.AccepterUsername)
            ? "A player" : payload.AccepterUsername;

        ShowFriendMessage(name + " accepted your friend request.");
        InvokeSubscribers(OnFriendRequestAccepted, payload);
    }

    private void HandleFriendRemoved(FriendRemovedPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.ByPlayerId))
            throw new InvalidOperationException("Player ID is missing.");

        ShowFriendMessage("A player removed you from their friends.");
        InvokeSubscribers(OnFriendRemoved, payload);
    }

    private static void ShowFriendMessage(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }

    private static void InvokeSubscribers<T>(Action<T> listeners, T payload)
    {
        if (listeners == null)
            return;

        foreach (Action<T> listener in listeners.GetInvocationList())
        {
            try { listener(payload); }
            catch (Exception e)
            {
                Debug.LogError("[Friends Socket] Subscriber failed: " + e.Message);
            }
        }
    }

    private static void NotifyFriendsChanged()
    {
        if (OnFriendsChanged == null)
            return;

        foreach (Action listener in OnFriendsChanged.GetInvocationList())
        {
            try { listener(); }
            catch (Exception e)
            {
                Debug.LogError("[Friends Socket] Refresh failed: " + e.Message);
            }
        }
    }

    private void RegisterClubEvents()
    {
        SocketManager.Instance.Off(EVENT_JOIN_NOTIFICATION);
        SocketManager.Instance.Off(EVENT_NEW_APPLICATION);
        SocketManager.Instance.Off(EVENT_MEMBERSHIP_APPROVED);
        SocketManager.Instance.Off(EVENT_KICKED);
        SocketManager.Instance.Off(EVENT_TABLE_UPDATED);
        SocketManager.Instance.Off(EVENT_MEMBER_ONLINE);
        SocketManager.Instance.Off(EVENT_SCROLL_MESSAGE);
        SocketManager.Instance.Off(EVENT_ROLE_CHANGE);

        SocketManager.Instance.On(EVENT_MEMBER_ONLINE, OnMemberOnlineReceived);
        SocketManager.Instance.On(EVENT_JOIN_NOTIFICATION, OnJoinNotificationReceived);
        SocketManager.Instance.On(EVENT_NEW_APPLICATION, OnNewApplicationReceived);
        SocketManager.Instance.On(EVENT_MEMBERSHIP_APPROVED, OnMembershipApprovedReceived);
        SocketManager.Instance.On(EVENT_KICKED, OnKickedReceived);
        SocketManager.Instance.On(EVENT_TABLE_UPDATED, OnTableUpdatedReceived);
        SocketManager.Instance.On(EVENT_SCROLL_MESSAGE, OnScrollMessageReceived);
        SocketManager.Instance.On(EVENT_ROLE_CHANGE, OnRoleChangedReceived);
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

        string playerId = AuthManager.Instance?.Session?.Id;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            Debug.LogWarning("[ClubSocket] Player session is not ready.");
            return;
        }

        var joinClubPayload = new
        {
            clubId = clubId,
            playerId = playerId
        };

        Debug.Log("[ClubSocket] Emit player:join_club => " +
                  JsonConvert.SerializeObject(joinClubPayload));

        SocketManager.Instance.Emit(
            EVENT_JOIN_CLUB,
            joinClubPayload
        );

        JoinPlayerNotifications(playerId);
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
            InformationPrefabScript.Instance.ShowMessage(Loc.Get("club.approved_to_join", ("club", payload.ClubName)));
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
            InformationPrefabScript.Instance.ShowMessage(Loc.Get("club.removed_from_club"));
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

    private void OnScrollMessageReceived(string json)
    {
        Debug.Log("[ClubSocket] club:scroll_message => " + json);

        try
        {
            ClubScrollMessagePayload payload =
                JsonConvert.DeserializeObject<ClubScrollMessagePayload>(json);

            if (payload == null) return;

            if (ClubContext.ClubDetail != null && ClubContext.ClubDetail.ClubId == payload.ClubId)
                ClubContext.ClubDetail.ScrollMessage = payload.Message;

            OnScrollMessage?.Invoke(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[ClubSocket] scroll_message parse failed: " + e.Message);
        }
    }

    private void OnMemberOnlineReceived(string json)
    {
        Debug.Log("[ClubSocket] club:member_online => " + json);

        try
        {
            ClubMemberOnlinePayload payload =
                JsonConvert.DeserializeObject<ClubMemberOnlinePayload>(json);

            if (payload != null && !string.IsNullOrEmpty(payload.PlayerId))
                OnMemberOnline?.Invoke(payload.PlayerId);
        }
        catch (Exception e)
        {
            Debug.LogError("[ClubSocket] member_online parse failed: " + e.Message);
        }
    }


    private void OnRoleChangedReceived(string json)
    {

        try
        {
            ClubRoleChangedPayload payload = JsonConvert.DeserializeObject<ClubRoleChangedPayload>(json);

            if (payload == null)
                return;

            if (ClubContext.ClubDetail != null && ClubContext.ClubDetail.ClubId == payload.ClubId)
            {
                ClubContext.ClubDetail.MyRole = payload.NewRole;
            }

            if (InformationPrefabScript.Instance != null)
            {
                //string message = !string.IsNullOrEmpty(payload.Message)? payload.Message.Replace("TABLE_MANAGER", "\nTABLE MANAGER"): "Club permission have been changed.";
                InformationPrefabScript.Instance.ShowMessage(Loc.Get("club.permission_changed"));
            }

            OnRoleChanged?.Invoke(payload);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[ClubSocket] role_changed parse failed: " +
                e.Message
            );
        }
    }
}