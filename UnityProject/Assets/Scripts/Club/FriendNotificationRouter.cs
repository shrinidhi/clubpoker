using System;
using ClubPoker.Auth;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Main menu glue for notifications: shows the unread dot on the Messages button
/// and opens the Friends screens from a notification's action button.
///
/// Lives in the main menu (MainMenuView sits in the UI assembly and can't see the
/// club scripts). Put it on any always-active object there.
/// </summary>
public class FriendNotificationRouter : MonoBehaviour
{
    [Header("Unread dot")]
    [Tooltip("Dot on the Messages button, shown while there are unread notifications.")]
    public GameObject UnreadDot;

    [Header("Screens")]
    public GameObject MessageScreen;
    [Tooltip("Main friend screen (FriendTableScreen, FriendTableMenuScreen).")]
    public GameObject FriendsMenuScreen;
    [Tooltip("Friends list and their tables (FriendTable_Screen, FriendTableScreenScript).")]
    public GameObject FriendsListScreen;
    [Tooltip("Friend requests (FriendRequestScreenScript) — opens on the Incoming tab.")]
    public GameObject FriendRequestScreen;

    private int _refreshVersion;

    private void OnEnable()
    {
        MessageShowScreenScript.OnCtaTapped += OpenCta;
        MessageScreenScript.OnClosed += RefreshUnreadDot;
        ClubSocketHandler.OnFriendRequestReceived += OnFriendRequestReceived;
        ClubSocketHandler.OnFriendRequestAccepted += OnFriendRequestAccepted;

        RefreshUnreadDot();
    }

    private void OnDisable()
    {
        MessageShowScreenScript.OnCtaTapped -= OpenCta;
        MessageScreenScript.OnClosed -= RefreshUnreadDot;
        ClubSocketHandler.OnFriendRequestReceived -= OnFriendRequestReceived;
        ClubSocketHandler.OnFriendRequestAccepted -= OnFriendRequestAccepted;
    }

    // Each also saves an Inbox notification server-side, so the count goes up.
    private void OnFriendRequestReceived(FriendRequestReceivedPayload _) => RefreshUnreadDot();
    private void OnFriendRequestAccepted(FriendRequestAcceptedPayload _) => RefreshUnreadDot();

    private void OpenCta(string ctaUrl)
    {
        if (!MessageShowScreenScript.HandlesCta(ctaUrl))
            return;

        if (MessageScreen != null)
            MessageScreen.SetActive(false);

        // Same as tapping through the friend menu: Requests sits over the menu, the
        // list replaces it (and its Close goes back to the menu).
        if (ctaUrl == MessageShowScreenScript.CtaFriendRequests)
        {
            if (FriendsMenuScreen != null)
                FriendsMenuScreen.SetActive(true);

            if (FriendRequestScreen != null)
            {
                FriendRequestScreen.SetActive(true);
                FriendRequestScreen.transform.SetAsLastSibling();
            }
        }
        else if (FriendsListScreen != null)
        {
            FriendsListScreen.SetActive(true);
        }
    }

    private void RefreshUnreadDot() => RefreshUnreadDotAsync(++_refreshVersion).Forget();

    private async UniTaskVoid RefreshUnreadDotAsync(int version)
    {
        if (UnreadDot == null || AuthManager.Instance == null || AuthManager.Instance.Session.IsGuest)
            return;

        try
        {
            // 30, not 1: the server's unreadCount only counts the page it returns.
            var data = await AuthManager.Instance.GetNotificationsAsync(1, 30);

            if (this == null || version != _refreshVersion || UnreadDot == null)
                return;

            UnreadDot.SetActive(data != null && data.UnreadCount > 0);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FriendNotificationRouter] Unread count failed: " + e.Message);
        }
    }
}
