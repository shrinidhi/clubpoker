using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.Core;

public class SearchFriendScreenScript : MonoBehaviour
{
    public Button CloseButon;
    public InputField SearchInputField;
    public Transform Content;
    public GameObject SearchFriendPrefab;

    [Header("Optional")]
    public Text MessageText;
    public GameObject NoDataAvailable;
    public FriendRequestScreenScript FriendRequestScreen;
    [Min(0f)] public float SearchDelaySeconds = 0.25f;

    public event Action<SendFriendRequestResponse> OnFriendRequestSent;

    private readonly List<global::SearchFriendPrefab> rows =
        new List<global::SearchFriendPrefab>();

    private readonly HashSet<string> pendingPlayerIds = new HashSet<string>();
    private readonly HashSet<string> sentPlayerIds = new HashSet<string>();
    private int searchVersion;

    private void Awake()
    {
        if (CloseButon != null)
            CloseButon.onClick.AddListener(CloseButtonOnTap);

        if (SearchInputField != null)
            SearchInputField.onValueChanged.AddListener(OnSearchChanged);
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ClubSocketHandler.OnFriendsChanged += OnSocketFriendsChanged;
        ++searchVersion;
        sentPlayerIds.Clear();
        ClearRows();

        if (SearchInputField != null)
            SearchInputField.SetTextWithoutNotify("");

        SetActive(NoDataAvailable, false);
        SetMessage(Loc.Get("friend.enter_search"));
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ++searchVersion;
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == searchVersion;
    }

    private void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private void OnSearchChanged(string value)
    {
        if (!isActiveAndEnabled)
            return;

        int version = ++searchVersion;
        string query = (value ?? "").Trim();

        ClearRows();
        SetActive(NoDataAvailable, false);

        if (string.IsNullOrWhiteSpace(query))
        {
            SetMessage(Loc.Get("friend.enter_search"));
            RefreshLayoutAsync(version).Forget();
            return;
        }

        SetMessage(Loc.Get("searching"));
        SearchAsync(query, version).Forget();
    }

    public void Reload()
    {
        if (isActiveAndEnabled)
            OnSearchChanged(SearchInputField != null ? SearchInputField.text : "");
    }

    private async UniTask SearchAsync(string query, int version)
    {
        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Max(0f, SearchDelaySeconds)),
                ignoreTimeScale: true);

            if (!IsCurrent(version))
                return;

            if (Content == null || SearchFriendPrefab == null)
                throw new InvalidOperationException("Assign Content and SearchFriendPrefab.");

            if (SearchFriendPrefab.GetComponent<global::SearchFriendPrefab>() == null)
                throw new InvalidOperationException("SearchFriendPrefab component is missing.");

            var manager = AuthManager.Instance;
            if (manager == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            string sessionId = manager.Session?.Id;
            var response = await manager.SearchFriendsAsync(query);

            if (!IsCurrent(version) || !IsSameSession(sessionId))
                return;

            var loadedIds = new HashSet<string>();

            if (response.Results != null)
            {
                foreach (var player in response.Results)
                {
                    if (player == null || string.IsNullOrWhiteSpace(player.Id) ||
                        !loadedIds.Add(player.Id))
                        continue;

                    // Prevent an older search snapshot from undoing a confirmed send.
                    if (sentPlayerIds.Contains(player.Id) &&
                        string.Equals(player.FriendStatus, "none",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        player.FriendStatus = "pending_outgoing";
                    }

                    GameObject instance = Instantiate(SearchFriendPrefab, Content);
                    var row = instance.GetComponent<global::SearchFriendPrefab>();
                    rows.Add(row);

                    bool self = !string.IsNullOrWhiteSpace(sessionId) &&
                        string.Equals(player.Id, sessionId, StringComparison.OrdinalIgnoreCase);

                    row.SetData(player, AddFriendOnTap, self);
                    row.SetBusy(pendingPlayerIds.Contains(player.Id));
                    instance.SetActive(true);
                }
            }

            bool empty = rows.Count == 0;
            SetActive(NoDataAvailable, empty);
            SetMessage(empty && NoDataAvailable == null ? Loc.Get("friend.no_players") : "");

            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            ClearRows();
            SetActive(NoDataAvailable, false);
            SetMessage(Loc.Get("friend.search_failed"));
            Debug.LogError("[Friend Search] Search failed: " + e.Message);
            await RefreshLayoutAsync(version);
        }
    }

    private void AddFriendOnTap(global::SearchFriendPrefab row)
    {
        if (row == null || row.Data == null ||
            string.IsNullOrWhiteSpace(row.Data.Id) ||
            !string.Equals(row.Data.FriendStatus, "none", StringComparison.OrdinalIgnoreCase) ||
            !pendingPlayerIds.Add(row.Data.Id))
            return;

        row.SetBusy(true);
        SetMessage("");
        SendRequestAsync(row.Data.Id).Forget();
    }

    private async UniTask SendRequestAsync(string playerId)
    {
        SendFriendRequestResponse response = null;
        string sessionId = AuthManager.Instance != null
            ? AuthManager.Instance.Session?.Id
            : null;
        int version = searchVersion;

        try
        {
            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            response = await AuthManager.Instance.SendFriendRequestAsync(playerId);
        }
        catch (Exception e)
        {
            if (IsCurrent(version) && IsSameSession(sessionId))
                SetMessage(e.Message);

            Debug.LogError("[Friend Search] Send failed: " + e.Message);
        }
        finally
        {
            if (this != null)
            {
                pendingPlayerIds.Remove(playerId);

                foreach (var row in rows)
                {
                    if (row != null && row.Data != null && row.Data.Id == playerId)
                        row.SetBusy(false);
                }
            }
        }

        if (this == null || response == null || !IsSameSession(sessionId))
            return;

        sentPlayerIds.Add(playerId);

        if (isActiveAndEnabled)
            Reload();

        if (FriendRequestScreen != null)
            FriendRequestScreen.Reload();

        OnFriendRequestSent?.Invoke(response);
    }

    private static bool IsSameSession(string id)
    {
        return AuthManager.Instance != null &&
               string.Equals(AuthManager.Instance.Session?.Id, id, StringComparison.Ordinal);
    }

    private async UniTask RefreshLayoutAsync(int version)
    {
        await UniTask.NextFrame();

        if (!IsCurrent(version) || Content == null)
            return;

        Canvas.ForceUpdateCanvases();
        var rects = Content.GetComponentsInChildren<RectTransform>(false);

        for (int i = rects.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rects[i]);

        Transform parent = Content.parent;
        while (parent != null)
        {
            if (parent is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            if (parent.GetComponent<Canvas>() != null)
                break;
            parent = parent.parent;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ClearRows()
    {
        foreach (var row in rows)
        {
            if (row == null)
                continue;

            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        rows.Clear();
    }

    private void SetMessage(string message)
    {
        if (MessageText == null)
            return;

        MessageText.supportRichText = false;
        MessageText.text = message;
        MessageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }

    private void OnSocketFriendsChanged()
    {
        if (isActiveAndEnabled)
            Reload();
    }

    private void OnDestroy()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        if (CloseButon != null)
            CloseButon.onClick.RemoveListener(CloseButtonOnTap);

        if (SearchInputField != null)
            SearchInputField.onValueChanged.RemoveListener(OnSearchChanged);
    }
}
