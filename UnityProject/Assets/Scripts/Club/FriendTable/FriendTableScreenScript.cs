using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

public class FriendTableScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Text FriendCount_Text;
    public Transform Friend_Content;
    public GameObject Friend_Prefab;
    public Transform FriendTable_Content;
    public GameObject FriendTablePrefab;
    public GameObject FriendTableMenuScreen;

    public Button AddfriendButton;
    public GameObject AddFriendScreen;

    [Serializable]
    public class JoinTableSelectedEvent : UnityEvent<string> { }

    [Header("Connect to your existing table entry flow")]
    public JoinTableSelectedEvent OnJoinTableSelected = new JoinTableSelectedEvent();

    public event Action<FriendTableData> OnTableSelected;
    public event Action<RemoveFriendResponse> OnFriendRemoved;

    public FriendTableData SelectedTable { get; private set; }

    private readonly List<Friendprefab> friends = new List<Friendprefab>();
    private readonly List<GameObject> tables = new List<GameObject>();
    private readonly HashSet<string> removingIds = new HashSet<string>();
    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        AddfriendButton.onClick.AddListener(AddFriendOnTap);
    }
    void AddFriendOnTap()
    {
        gameObject.SetActive(false);
        AddFriendScreen.SetActive(true);
    }
    private void OnEnable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ClubSocketHandler.OnFriendsChanged += OnSocketFriendsChanged;
        Reload();
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ++viewVersion;
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        int version = ++viewVersion;
        SelectedTable = null;
        ClearFriends();
        ClearTables();
        SetText(FriendCount_Text, "-");
     
        LoadAsync(version).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null && isActiveAndEnabled && version == viewVersion;
    }

    private async UniTask LoadAsync(int version)
    {
        await UniTask.WhenAll(LoadFriendsAsync(version), LoadTablesAsync(version));
        await RefreshLayoutAsync(version);
    }

    private async UniTask LoadFriendsAsync(int version)
    {
        try
        {
            await UniTask.Yield();
            if (!IsCurrent(version)) return;

            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");
            if (Friend_Content == null || Friend_Prefab == null ||
                Friend_Prefab.GetComponent<Friendprefab>() == null)
                throw new InvalidOperationException("Assign Friend_Content and a prefab with Friendprefab.");

            var manager = AuthManager.Instance;
            string sessionId = manager.Session?.Id;
            var response = await manager.GetFriendsAsync();

            if (!IsCurrent(version) || !IsSameSession(sessionId)) return;

            var ids = new HashSet<string>();

            if (response.Friends != null)
            {
                foreach (var data in response.Friends)
                {
                    if (data == null || string.IsNullOrWhiteSpace(data.FriendshipId) ||
                        !ids.Add(data.FriendshipId))
                        continue;

                    GameObject instance = Instantiate(Friend_Prefab, Friend_Content);
                    var row = instance.GetComponent<Friendprefab>();
                    friends.Add(row);
                    row.SetData(data, RemoveFriendOnTap);
                    row.SetBusy(removingIds.Contains(data.FriendshipId));
                    instance.SetActive(true);
                }
            }

            SetText(FriendCount_Text, Loc.Get("friend.my_friends_count",
                ("count", friends.Count.ToString(CultureInfo.InvariantCulture))));
            bool empty = friends.Count == 0;
           
            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version)) return;
            ClearFriends();
            SetText(FriendCount_Text, "-");
          
            Debug.LogError("[Friends] Load failed: " + e.Message);
            await RefreshLayoutAsync(version);
        }
    }

    private async UniTask LoadTablesAsync(int version)
    {
        try
        {
            await UniTask.Yield();
            if (!IsCurrent(version)) return;

            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");
            if (FriendTable_Content == null || FriendTablePrefab == null ||
                FriendTablePrefab.GetComponent<global::FriendTablePrefab>() == null)
                throw new InvalidOperationException("Assign FriendTable_Content and a prefab with FriendTablePrefab.");

            var manager = AuthManager.Instance;
            string sessionId = manager.Session?.Id;
            var response = await manager.GetFriendTablesAsync();

            if (!IsCurrent(version) || !IsSameSession(sessionId)) return;

            var ids = new HashSet<string>();

            if (response.Tables != null)
            {
                foreach (var data in response.Tables)
                {
                    if (data == null || string.IsNullOrWhiteSpace(data.Id) ||
                        !ids.Add(data.Id))
                        continue;

                    GameObject instance = Instantiate(FriendTablePrefab, FriendTable_Content);
                    tables.Add(instance);
                    instance.GetComponent<global::FriendTablePrefab>().SetData(data, JoinTableOnTap);
                    instance.SetActive(true);
                }
            }

            bool empty = tables.Count == 0;
          
            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version)) return;
            ClearTables();
           
            Debug.LogError("[Friend Tables] Load failed: " + e.Message);
            await RefreshLayoutAsync(version);
        }
    }

    private void RemoveFriendOnTap(Friendprefab row)
    {
        if (row == null || row.Data == null ||
            string.IsNullOrWhiteSpace(row.Data.FriendshipId) ||
            !removingIds.Add(row.Data.FriendshipId))
            return;

        row.SetBusy(true);
        RemoveFriendAsync(row.Data.FriendshipId).Forget();
    }

    private async UniTask RemoveFriendAsync(string friendshipId)
    {
        RemoveFriendResponse response = null;
        string sessionId = AuthManager.Instance != null
            ? AuthManager.Instance.Session?.Id : null;

        try
        {
            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            response = await AuthManager.Instance.RemoveFriendAsync(friendshipId);
        }
        catch (Exception e)
        {
            Debug.LogError("[Friends] Remove failed: " + e.Message);
        }
        finally
        {
            if (this != null)
            {
                removingIds.Remove(friendshipId);
                foreach (var row in friends)
                {
                    if (row != null && row.Data != null)
                        row.SetBusy(removingIds.Contains(row.Data.FriendshipId));
                }
            }
        }

        if (this == null || response == null || !IsSameSession(sessionId))
            return;

        // Refresh tables as well, because removing a friend can change that list.
        if (isActiveAndEnabled)
            Reload();

        OnFriendRemoved?.Invoke(response);
    }

    private void JoinTableOnTap(FriendTableData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.Id))
            return;

        SelectedTable = data;
        OnTableSelected?.Invoke(data);
        OnJoinTableSelected?.Invoke(data.Id);
        JoinTableAsync(data).Forget();
    }


    private async UniTaskVoid JoinTableAsync(FriendTableData data)
    {

        try
        {
            int buyInAmount = data.MinBuyIn;
            TableData tableData = await AuthManager.Instance.GetTableDetailAsync(data.Id);
            if (buyInAmount <= 0)
            {
                buyInAmount = tableData.MinBuyIn;
            }

            await AuthManager.Instance.BuyInAsync(data.Id, buyInAmount);

            try
            {
                await AuthManager.Instance.JoinTableAsync(data.Id, buyInAmount);
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("Already seated"))
                    throw;
            }

            // Friend table host — created from the main menu, so lobby origin.
            TableContext.EnterFromLobby(data.Id, tableData);
            ClubPoker.Game.TableJoinHandler.Instance.JoinTable(data.Id);
        }
        catch (Exception e)
        {
            Debug.LogError("Host join failed: " + e);
        }
        finally
        {
        }
    }

    private void CloseButtonOnTap()
    {
        FriendTableMenuScreen.SetActive(true);
        gameObject.SetActive(false);
    }

    private static bool IsSameSession(string id)
    {
        return AuthManager.Instance != null &&
            string.Equals(AuthManager.Instance.Session?.Id, id, StringComparison.Ordinal);
    }

    private async UniTask RefreshLayoutAsync(int version)
    {
        await UniTask.NextFrame();
        if (!IsCurrent(version)) return;
        Canvas.ForceUpdateCanvases();
        Rebuild(Friend_Content);
        Rebuild(FriendTable_Content);
        Canvas.ForceUpdateCanvases();
    }

    private static void Rebuild(Transform content)
    {
        if (content == null || !content.gameObject.activeInHierarchy) return;
        var rects = content.GetComponentsInChildren<RectTransform>(false);
        for (int i = rects.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rects[i]);

        Transform parent = content.parent;
        while (parent != null)
        {
            if (parent is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            if (parent.GetComponent<Canvas>() != null) break;
            parent = parent.parent;
        }
    }

    private void ClearFriends()
    {
        foreach (var row in friends)
        {
            if (row == null) continue;
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        friends.Clear();
    }

    private void ClearTables()
    {
        foreach (var row in tables)
        {
            if (row == null) continue;
            row.SetActive(false);
            Destroy(row);
        }
        tables.Clear();
    }

    private static void SetText(Text target, string value)
    {
        if (target == null) return;
        target.supportRichText = false;
        target.text = value;
    }

    private static void SetMessage(Text target, string value)
    {
        SetText(target, value);
        if (target != null)
            target.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null) target.SetActive(value);
    }

    private void OnSocketFriendsChanged()
    {
        if (isActiveAndEnabled)
            Reload();
    }

    private void OnDestroy()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
    }
}
