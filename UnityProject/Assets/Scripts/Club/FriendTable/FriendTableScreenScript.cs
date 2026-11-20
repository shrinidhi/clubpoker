
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FriendTableScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Text FriendCount_Text;
    public Transform Friend_Content;
    public GameObject Friend_Prefab;
    public Transform FriendTable_Content;
    public GameObject FriendTablePrefab;
    public GameObject FriendTableMenuScreen;

    // My Tables
    public Transform MyTableContent;
    public GameObject MyTablePrefab;

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
    private readonly List<GameObject> myTables = new List<GameObject>();

    private readonly HashSet<string> removingIds = new HashSet<string>();
    private readonly HashSet<string> deletingMyTableIds = new HashSet<string>();

    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (AddfriendButton != null)
            AddfriendButton.onClick.AddListener(AddFriendOnTap);
    }

    void AddFriendOnTap()
    {
        if (AddFriendScreen != null)
            AddFriendScreen.SetActive(true);

        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ClubSocketHandler.OnFriendsChanged += OnSocketFriendsChanged;

        if (SocketManager.Instance != null)
            SocketManager.Instance.On("friend:table_updated", OnFriendTableUpdated);

        Reload();
    }

    private void OnFriendTableUpdated(string json)
    {
        Reload();
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ++viewVersion;

        if (SocketManager.Instance != null)
            SocketManager.Instance.Off("friend:table_updated");
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        int version = ++viewVersion;

        SelectedTable = null;

        ClearFriends();
        ClearTables();
        ClearMyTables();

        SetText(FriendCount_Text, "-");

        LoadAsync(version).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadAsync(int version)
    {
        await UniTask.WhenAll(
            LoadFriendsAsync(version),
            LoadMyTablesAsync(version),
            LoadTablesAsync(version)
            
        );

        await RefreshLayoutAsync(version);
    }

    // --------------------------------------------------
    // FRIENDS
    // --------------------------------------------------

    private async UniTask LoadFriendsAsync(int version)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            if (Friend_Content == null ||
                Friend_Prefab == null ||
                Friend_Prefab.GetComponent<Friendprefab>() == null)
            {
                throw new InvalidOperationException(
                    "Assign Friend_Content and a prefab with Friendprefab.");
            }

            var manager = AuthManager.Instance;
            string sessionId = manager.Session?.Id;

            var response = await manager.GetFriendsAsync();

            if (!IsCurrent(version) || !IsSameSession(sessionId))
                return;

            var ids = new HashSet<string>();

            if (response.Friends != null)
            {
                foreach (var data in response.Friends)
                {
                    if (data == null ||
                        string.IsNullOrWhiteSpace(data.FriendshipId) ||
                        !ids.Add(data.FriendshipId))
                        continue;

                    GameObject instance = Instantiate(
                        Friend_Prefab,
                        Friend_Content);

                    var row = instance.GetComponent<Friendprefab>();

                    friends.Add(row);

                    row.SetData(data, RemoveFriendOnTap);
                    row.SetBusy(removingIds.Contains(data.FriendshipId));

                    instance.SetActive(true);
                }
            }

            SetText(
                FriendCount_Text,
                Loc.Get("friend.my_friends_count",
                ("count", friends.Count.ToString(CultureInfo.InvariantCulture))));

            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            ClearFriends();
            SetText(FriendCount_Text, "-");

            Debug.LogError("[Friends] Load failed: " + e.Message);

            await RefreshLayoutAsync(version);
        }
    }

    // --------------------------------------------------
    // FRIEND TABLES
    // --------------------------------------------------

    private async UniTask LoadTablesAsync(int version)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            if (FriendTable_Content == null ||
                FriendTablePrefab == null ||
                FriendTablePrefab.GetComponent<global::FriendTablePrefab>() == null)
            {
                throw new InvalidOperationException(
                    "Assign FriendTable_Content and a prefab with FriendTablePrefab.");
            }

            var manager = AuthManager.Instance;
            string sessionId = manager.Session?.Id;

            var response = await manager.GetFriendTablesAsync();

            if (!IsCurrent(version) || !IsSameSession(sessionId))
                return;

            var ids = new HashSet<string>();

            if (response.Tables != null)
            {
                foreach (var data in response.Tables)
                {
                    if (data == null ||
                        string.IsNullOrWhiteSpace(data.Id) ||
                        !ids.Add(data.Id))
                        continue;

                    GameObject instance = Instantiate(
                        FriendTablePrefab,
                        FriendTable_Content);

                    tables.Add(instance);

                    instance.GetComponent<global::FriendTablePrefab>()
                        .SetData(data, JoinTableOnTap);

                    instance.SetActive(true);
                }
            }

            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            ClearTables();

            Debug.LogError("[Friend Tables] Load failed: " + e.Message);

            await RefreshLayoutAsync(version);
        }
    }

    // --------------------------------------------------
    // MY TABLES
    // --------------------------------------------------

    private async UniTask LoadMyTablesAsync(int version)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            Debug.Log("[My Tables] Loading started");

            if (AuthManager.Instance == null)
            {
                Debug.LogError("[My Tables] AuthManager is NULL");
                return;
            }

            if (MyTableContent == null)
            {
                Debug.LogError("[My Tables] MyTableContent is NOT assigned in Inspector");
                return;
            }

            if (MyTablePrefab == null)
            {
                Debug.LogError("[My Tables] MyTablePrefab is NOT assigned in Inspector");
                return;
            }

            Debug.Log("[My Tables] Content Name: " + MyTableContent.name);
            Debug.Log("[My Tables] Content Active: " + MyTableContent.gameObject.activeInHierarchy);
            Debug.Log("[My Tables] Prefab Name: " + MyTablePrefab.name);

            var prefabComponent = MyTablePrefab.GetComponentInChildren<Mytableprefab>(true);

            if (prefabComponent == null)
            {
                Debug.LogError("[My Tables] Mytableprefab component missing in prefab!");
                return;
            }

            string sessionId = AuthManager.Instance.Session?.Id;

            var response = await AuthManager.Instance.GetMyLobbyTablesAsync();
            Debug.Log("[My Tables] Response null: " + (response == null));
           

            if (!IsCurrent(version) || !IsSameSession(sessionId))
                return;

            if (response == null)
            {
                Debug.LogError("[My Tables] Response is NULL");
                return;
            }
            if (response.Items == null)
            {
                Debug.LogError("[My Tables] Response.Data.Items is NULL");
                return;
            }

            foreach (var data in response.Items)
            {
                if (data == null)
                {
                    Debug.LogWarning("[My Tables] Item is NULL, skipping");
                    continue;
                }

                Debug.Log("[My Tables] Creating Table: " + data.Name + " | ID: " + data.Id);

                if (string.IsNullOrWhiteSpace(data.Id))
                {
                    Debug.LogWarning("[My Tables] Table ID is empty, skipping");
                    continue;
                }

                GameObject instance = Instantiate(MyTablePrefab, MyTableContent, false);

                instance.name = "MyTable_" + data.Name;
                instance.SetActive(true);

                Debug.Log("[My Tables] Instantiated: " + instance.name);

                var row = instance.GetComponentInChildren<Mytableprefab>(true);

                if (row == null)
                {
                    Debug.LogError("[My Tables] Mytableprefab component not found on instantiated object!");
                    Destroy(instance);
                    continue;
                }

                myTables.Add(instance);

                row.SetData(
                    data,
                    JoinMyTableOnTap,
                    DeleteMyTableOnTap
                );

                Debug.Log("[My Tables] SetData completed: " + data.Name);
            }

            await RefreshLayoutAsync(version);

            Debug.Log("[My Tables] Loading completed. Generated: " + myTables.Count);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            ClearMyTables();

            Debug.LogError("[My Tables] Load failed: " + e);

            await RefreshLayoutAsync(version);
        }
    }

    // My Table Join Button
    private void JoinMyTableOnTap(string tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId))
            return;

        JoinMyTableAsync(tableId).Forget();
    }

    private async UniTaskVoid JoinMyTableAsync(string tableId)
    {
        try
        {
            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            int buyInAmount = 0;

            // Fetch table details
            TableData tableData =
                await AuthManager.Instance.GetTableDetailAsync(tableId);

            if (tableData == null)
                return;

            // Use table minimum buy-in
            buyInAmount = tableData.MinBuyIn;

            if (buyInAmount <= 0)
                throw new Exception("Invalid table buy-in amount.");

            // Join table / rejoin existing seat
            await AuthManager.Instance.BuyInAndJoinAsync(
                tableId,
                buyInAmount
            );

            // Enter from lobby
            TableContext.EnterFromLobby(tableId, tableData);

            ClubPoker.Game.TableJoinHandler.Instance.JoinTable(tableId);
        }
        catch (Exception e)
        {
            Debug.LogError("[My Tables] Join failed: " + e);
        }
    }

    // My Table Delete Button
    private void DeleteMyTableOnTap(string tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId))
            return;

        if (!deletingMyTableIds.Add(tableId))
            return;

        DeleteMyTableAsync(tableId).Forget();
    }

    private async UniTask DeleteMyTableAsync(string tableId)
    {
        string sessionId = AuthManager.Instance != null
            ? AuthManager.Instance.Session?.Id
            : null;

        try
        {
            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            // DELETE /api/lobby/tables/{tableId}
            var response = await AuthManager.Instance.DeleteMyLobbyTableAsync(tableId);

            if (this == null || !IsSameSession(sessionId))
                return;

            if (response != null)
            {
                // Refresh My Tables after successful deletion
                if (isActiveAndEnabled)
                    Reload();
            }
            else
            {
                Debug.LogWarning("[My Tables] Delete API did not confirm deletion.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[My Tables] Delete failed: " + e);
        }
        finally
        {
            if (this != null)
                deletingMyTableIds.Remove(tableId);
        }
    }

    // --------------------------------------------------
    // REMOVE FRIEND
    // --------------------------------------------------

    private void RemoveFriendOnTap(Friendprefab row)
    {
        if (row == null ||
            row.Data == null ||
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
            ? AuthManager.Instance.Session?.Id
            : null;

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

        if (this == null ||
            response == null ||
            !IsSameSession(sessionId))
            return;

        if (isActiveAndEnabled)
            Reload();

        OnFriendRemoved?.Invoke(response);
    }

    // --------------------------------------------------
    // FRIEND TABLE JOIN
    // --------------------------------------------------

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

            TableData tableData =
                await AuthManager.Instance.GetTableDetailAsync(data.Id);

            if (buyInAmount <= 0)
                buyInAmount = tableData.MinBuyIn;

            // Already seated (G016) counts as success — rejoin that seat.
            await AuthManager.Instance.BuyInAndJoinAsync(
                data.Id,
                buyInAmount
            );

            // Friend table host — created from main menu, so lobby origin.
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

    // --------------------------------------------------
    // SCREEN BUTTONS
    // --------------------------------------------------

    private void CloseButtonOnTap()
    {
        if (FriendTableMenuScreen != null)
            FriendTableMenuScreen.SetActive(true);

        gameObject.SetActive(false);
    }

    // --------------------------------------------------
    // SESSION CHECK
    // --------------------------------------------------

    private static bool IsSameSession(string id)
    {
        return AuthManager.Instance != null &&
               string.Equals(
                   AuthManager.Instance.Session?.Id,
                   id,
                   StringComparison.Ordinal
               );
    }

    // --------------------------------------------------
    // LAYOUT REFRESH
    // --------------------------------------------------

    private async UniTask RefreshLayoutAsync(int version)
    {
        await UniTask.NextFrame();

        if (!IsCurrent(version))
            return;

        Canvas.ForceUpdateCanvases();

        Rebuild(Friend_Content);
        Rebuild(FriendTable_Content);
        Rebuild(MyTableContent);

        Canvas.ForceUpdateCanvases();
    }

    private static void Rebuild(Transform content)
    {
        if (content == null || !content.gameObject.activeInHierarchy)
            return;

        var rects = content.GetComponentsInChildren<RectTransform>(false);

        for (int i = rects.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rects[i]);

        Transform parent = content.parent;

        while (parent != null)
        {
            if (parent is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            if (parent.GetComponent<Canvas>() != null)
                break;

            parent = parent.parent;
        }
    }

    // --------------------------------------------------
    // CLEAR LISTS
    // --------------------------------------------------

    private void ClearFriends()
    {
        foreach (var row in friends)
        {
            if (row == null)
                continue;

            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        friends.Clear();
    }

    private void ClearTables()
    {
        foreach (var row in tables)
        {
            if (row == null)
                continue;

            row.SetActive(false);
            Destroy(row);
        }

        tables.Clear();
    }

    private void ClearMyTables()
    {
        foreach (var row in myTables)
        {
            if (row == null)
                continue;

            row.SetActive(false);
            Destroy(row);
        }

        myTables.Clear();
    }

    // --------------------------------------------------
    // COMMON HELPERS
    // --------------------------------------------------

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

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
        if (target != null)
            target.SetActive(value);
    }

    // --------------------------------------------------
    // SOCKET EVENTS
    // --------------------------------------------------

    private void OnSocketFriendsChanged()
    {
        if (isActiveAndEnabled)
            Reload();
    }

    // --------------------------------------------------
    // DESTROY
    // --------------------------------------------------

    private void OnDestroy()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;

        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (AddfriendButton != null)
            AddfriendButton.onClick.RemoveListener(AddFriendOnTap);
    }
}
