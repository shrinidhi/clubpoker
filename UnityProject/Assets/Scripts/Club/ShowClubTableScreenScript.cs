using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.Game;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System;


public class ShowClubTableScreenScript : MonoBehaviour
{
    public Button Back_Button;
    public Transform Table_Content;
    public GameObject ClubTable_Prefab;

    public ClubListData ClubListData;

    public Image ClubBadge_Image;
    public Text ClubName;
    public Text ClubCode;

    public ClubBadgeSO ClubBadgeSO;

    [Header("Create Table")]
    public Button Club_CreateTable_Button;
    public GameObject ClubCreateTable_Screen;
    public ClubCreateTableScreenScript ClubCreateTableScreenScript;

    [Header("Game Variants Info")]
    public Transform Variant_Content;
    public GameObject FilterTableByVariantPrefab;
    public TextAsset ClubTableVariantJson;

    [Header("Member View")]

    private string ClubID="";

    private ClubTableVariantResponse clubTableVariantResponse;

    private List<ClubTableData> allTables = new List<ClubTableData>();
    private List<FilterTableByVariantPrefabScrtipt> variantItems =
        new List<FilterTableByVariantPrefabScrtipt>();

    private const int PollIntervalMs = 5000;
    private bool _polling;
    private string _tablesSignature;

    private string selectedVariantKey = "all";
    private FilterTableByVariantPrefabScrtipt selectedVariantItem;

    public GameObject CreateTableButton;
    public GameObject PrivateTableButton;

    public Toggle OpenSeat_Toggle;
    public Toggle Running_Toggle;

    public Text Chips_Count;
    public Text DescriptionText;

    private void Start()
    {
        if (Back_Button != null)
            Back_Button.onClick.AddListener(BackButtonOnTap);

        if (Club_CreateTable_Button != null)
            Club_CreateTable_Button.onClick.AddListener(Club_CreateTable_ButtonOnTap);

        // Cashier + Member Management moved to ClubViewController (bottom bar).
        ParseVariantJson();
        GenerateVariantFilters();

        OpenSeat_Toggle.isOn = false;
        Running_Toggle.isOn = false;

        OpenSeat_Toggle.onValueChanged.AddListener(delegate { ApplyVariantFilter(); });
        Running_Toggle.onValueChanged.AddListener(delegate { ApplyVariantFilter(); });

        FetchAndDisplayChipsAsync().Forget();
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnTableUpdated += HandleTableUpdated;
        ClubContext.OnClubDetailChanged  += OnClubDetailChanged;
        ClubContext.OnClubTablesChanged  += OnClubTablesChanged;

        _polling = true;
        PollTables().Forget();
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnTableUpdated -= HandleTableUpdated;
        ClubContext.OnClubDetailChanged  -= OnClubDetailChanged;
        ClubContext.OnClubTablesChanged  -= OnClubTablesChanged;

        _polling = false;
    }

    // Background refresh: another member creating + linking a real table (or
    // seats filling / a table going live) only shows up here on a re-fetch.
    private async UniTaskVoid PollTables()
    {
        while (_polling)
        {
            await UniTask.Delay(PollIntervalMs, cancellationToken: destroyCancellationToken);

            if (!_polling || ClubListData == null)
                continue;

            await RefreshTablesSilent();
        }
    }

    // Re-fetch without touching the variant filter / toggles, and only rebuild
    // the rows when something actually changed (avoids scroll + click resets).
    private async UniTask RefreshTablesSilent()
    {
        try
        {
            List<ClubTableData> tables =
                await AuthManager.Instance.GetClubTablesAsync(ClubListData.ClubId);

            if (tables == null) return;

            string signature = BuildTablesSignature(tables);
            if (signature == _tablesSignature) return;

            _tablesSignature = signature;
            allTables = tables;

            ApplyVariantFilter();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[ShowClubTableScreenScript] Table poll failed: {e.Message}");
        }
    }

    // Only the fields the row UI + join flow care about.
    private static string BuildTablesSignature(List<ClubTableData> tables)
    {
        var sb = new System.Text.StringBuilder();

        foreach (ClubTableData t in tables)
        {
            sb.Append(t.Id).Append('|')
              .Append(t.TableId).Append('|')
              .Append(t.PlayerCount).Append('|')
              .Append(t.Live ? 1 : 0).Append('|')
              .Append(t.Status).Append(';');
        }

        return sb.ToString();
    }

    // Our own action changed the tables (e.g. Admin ▸ Disband Empty Tables) → reload.
    private void OnClubTablesChanged()
    {
        LoadTables().Forget();
    }
    private void HandleTableUpdated(ClubTableUpdatedPayload payload)
    {
        if (payload == null)
            return;

        if (ClubListData == null)
            return;

        if (payload.ClubId != ClubListData.ClubId)
            return;

        Debug.Log("Club table updated, refreshing tables");

        LoadTables().Forget();
    }
    private void Club_CreateTable_ButtonOnTap()
    {
        ClubCreateTable_Screen.SetActive(true);
    }

    // Cached club detail changed (e.g. Admin ▸ Club Badge & Name) → refresh the header.
    private void OnClubDetailChanged(ClubDetailData detail)
    {
        if (detail != null) UpdateNameAndBadge(detail.Name, detail.Badge , detail.Description);
    }

    /// Live-refresh the home header after Admin ▸ Club Badge & Name edits it.
    public void UpdateNameAndBadge(string name, string badge ,string discription)
    {
        if (!string.IsNullOrEmpty(name))
        {
            if (ClubName != null) ClubName.text = name;
            if (ClubListData != null) ClubListData.Name = name;
        }

        if (!string.IsNullOrEmpty(badge))
        {
            Sprite sprite = GetBadgeSprite(badge);
            if (sprite != null && ClubBadge_Image != null) ClubBadge_Image.sprite = sprite;
            if (ClubListData != null) ClubListData.Badge = badge;
        }
        if(string.IsNullOrEmpty(discription))
        {
            DescriptionText.text = "Welcome to X-Poker";
        }
        else
        {
            DescriptionText.text = discription;
        }
    }

    public void ShowData(ClubListData clubListData)
    {
        ClubListData = clubListData;

        ClubName.text = clubListData.Name;
        ClubCode.text = "ID: " + clubListData.ClubCode;
        ClubID = clubListData.ClubId;

        // ClubContext already set by ClubContext.SelectClub before this runs.
        bool isCreator = ClubContext.ParseRole(clubListData.Role) == ClubRole.Creator;
        Club_CreateTable_Button.gameObject.SetActive(isCreator);
        // if (TablesBg != null) TablesBg.SetActive(!isCreator);
        ClubCreateTableScreenScript.ClubId = ClubListData.ClubId;

        Sprite badgeSprite = GetBadgeSprite(clubListData.Badge);
        if (badgeSprite != null)
            ClubBadge_Image.sprite = badgeSprite;

      

        LoadTables().Forget();
    }

    private void ParseVariantJson()
    {
        if (ClubTableVariantJson == null)
        {
            Debug.LogError("ClubTableVariantJson missing");
            return;
        }

        clubTableVariantResponse =
            JsonConvert.DeserializeObject<ClubTableVariantResponse>(
                ClubTableVariantJson.text
            );
    }

    private void GenerateVariantFilters()
    {
        ClearVariantFilters();

        CreateVariantFilter("all", "All",false);

        if (clubTableVariantResponse == null ||
            clubTableVariantResponse.ClubTableVariants == null)
            return;

        foreach (ClubTableVariantData variant in
                 clubTableVariantResponse.ClubTableVariants)
        {
            CreateVariantFilter(
                variant.VariantKey,
                variant.VariantName,
                variant.IsLocked
            );
        }

        SelectDefaultAllVariant();
    }

    private void CreateVariantFilter(string key, string displayName, bool islocked)
    {
        GameObject obj = Instantiate(
            FilterTableByVariantPrefab,
            Variant_Content
        );

        FilterTableByVariantPrefabScrtipt prefab =
            obj.GetComponent<FilterTableByVariantPrefabScrtipt>();

        prefab.SetData(
            key,
            displayName,
            islocked,
            OnVariantFilterSelected
        );

        variantItems.Add(prefab);
    }

    private void ClearVariantFilters()
    {
        variantItems.Clear();

        for (int i = Variant_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Variant_Content.GetChild(i).gameObject);
        }
    }

    private void SelectDefaultAllVariant()
    {
        if (variantItems.Count == 0)
            return;

        OnVariantFilterSelected("all", variantItems[0]);
    }

    private void OnVariantFilterSelected(
        string variantKey,
        FilterTableByVariantPrefabScrtipt selectedItem)
    {
        selectedVariantKey = variantKey;
        selectedVariantItem = selectedItem;

        foreach (FilterTableByVariantPrefabScrtipt item in variantItems)
        {
            item.SetSelected(item == selectedItem);
        }

        ApplyVariantFilter();
    }

    public async UniTaskVoid LoadTables()
    {
        selectedVariantKey = "all";
        SelectDefaultAllVariant();
        ClearTables();

        if (ClubListData == null)
            return;

        allTables =
            await AuthManager.Instance.GetClubTablesAsync(
                ClubListData.ClubId
            );

        _tablesSignature = allTables != null ? BuildTablesSignature(allTables) : null;

        ApplyVariantFilter();
    }

    private void ApplyVariantFilter()
    {
        ClearTables();

        if (allTables == null)
            return;

        foreach (ClubTableData table in allTables)
        {
            // Variant Filter
            if (selectedVariantKey != "all" &&
                table.Variant.ToLower() != selectedVariantKey.ToLower())
            {
                continue;
            }

            // Running Filter
            if (Running_Toggle.isOn && !table.Live)
                continue;

            // Open Seat Filter
            if (OpenSeat_Toggle.isOn && table.PlayerCount >= table.MaxSeats)
                continue;

            GameObject obj = Instantiate(ClubTable_Prefab, Table_Content);

            ClubTablePrefabScript prefab = obj.GetComponent<ClubTablePrefabScript>();
            prefab.Setup(
                table,
                OnDeleteTableClicked,
                OnExtendTableClicked,
                OnJoinTableClicked
            );
        }
    }


    // Entry point for the scrolling-message strip's Go button — jumps straight into
    // the table the admin attached to the message.
    public void JoinTableById(string tableId)
    {
        if (string.IsNullOrEmpty(tableId) || allTables == null)
            return;

        ClubTableData table = allTables.Find(t => t.TableId == tableId);
        Debug.Log($"[ShowClubTableScreenScript] Joining table by ID, tableId={tableId}, found={table != null}");
        if (table != null)
            OnJoinTableClicked(table);
    }

    private async void OnJoinTableClicked(ClubTableData table)
    {
        Debug.Log($"[ShowClubTableScreenScript] Join table tapped, tableId={table?.TableId}");
        if (table == null) return;

        try
        {
            string tableId = table.TableId;
            TableActiveData active = null;

            // Row already linked → check the real table is still alive before joining.
            if (!string.IsNullOrEmpty(tableId))
            {
                active = await AuthManager.Instance.GetTableActiveAsync(tableId);

                if (active == null || !active.Active)
                {
                    Debug.LogWarning($"[ShowClubTableScreenScript] Linked table {tableId} not active — creating a new one");
                    tableId = null;
                }
            }

            // First player to tap a club table creates the real lobby table and
            // links it back to the club table row; everyone after reuses tableId.
            if (string.IsNullOrEmpty(tableId))
            {
                var req = new CreateTableRequest
                {
                    Variant    = table.Variant,
                    MaxPlayers = table.MaxSeats,
                    SmallBlind = table.SmallBlind,
                    BigBlind   = table.BigBlind,
                    MinBuyIn   = table.BuyInMin,
                    MaxBuyIn   = table.BuyInMax,
                    ClubId     = table.ClubId
                };
                var res = await AuthManager.Instance.CreateTableAsync(req);
                tableId = res?.TableId;

                if (string.IsNullOrEmpty(tableId)) return;

                try
                {
                    await AuthManager.Instance.LinkClubTableAsync(tableId, table.ClubId, table.Id);
                    table.TableId = tableId;
                }
                catch (System.Exception linkEx)
                {
                    // Table exists — seat this player anyway, others will re-link on refresh.
                    Debug.LogError($"[ShowClubTableScreenScript] Link club table failed: {linkEx.Message}");
                }
            }

            if (string.IsNullOrEmpty(tableId)) return;

            // Club origin: the in-game menu unlocks the club-only options and Back
            // returns to this screen instead of the main menu. Set after the table
            // id is final — a fresh club table only gets one at create time.
            TableContext.EnterFromClub(table, tableId);

            // No bots on club tables — real members only. Stop any bots left over
            // from a lobby/quick-join session before seating.
            if (UnityBotRunner.Instance != null)
                UnityBotRunner.Instance.StopBots();

            // Table already running → can't sit mid-hand or in a full table.
            // Same rule as the lobby: watch & wait, seat when one frees.
            bool handInProgress = active != null && active.HandInProgress;
            bool isFull         = table.PlayerCount >= table.MaxSeats;

            if (handInProgress || isFull)
            {
                await WatchAndWaitAsync(tableId, table.BuyInMin);
                return;
            }

            await AuthManager.Instance.JoinTableAsync(tableId, table.BuyInMin);
            TableJoinHandler.Instance.JoinTable(tableId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShowClubTableScreenScript] Join failed: {e.Message}");
        }
    }

    // Enter as spectator and queue for a seat — server pushes table:seat_available
    // when one frees, TableJoinHandler converts us to seated.
    private async UniTask WatchAndWaitAsync(string tableId, int buyIn)
    {
        SpectateData spectate = await AuthManager.Instance.SpectateTableAsync(tableId);
        Debug.Log($"[ShowClubTableScreenScript] Spectating {tableId}, state={spectate?.CurrentState?.GameState}");

        TableJoinHandler.Instance.BeginWatchAndWait(tableId, buyIn);

        await AuthManager.Instance.JoinWaitingListAsync(tableId);
    }

    private async void OnExtendTableClicked(ClubTableData table)
    {
        if (table == null)
            return;

        try
        {
            ExtendTableResponse response =
                await AuthManager.Instance.ExtendTableAsync(
                    ClubListData.ClubId,
                    table.Id
                );

            if (response != null)
            {
                Debug.Log(
                    $"Table extended by {response.AddedMinutes} minutes"
                );
            }

            LoadTables().Forget();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Table extend failed: " + e.Message);
        }
    }

    private async void OnDeleteTableClicked(ClubTableData table)
    {
        if (table == null)
            return;

        try
        {
            await AuthManager.Instance.DeleteClubTableAsync(
                ClubListData.ClubId,
                table.Id
            );

            allTables.RemoveAll(t => t.Id == table.Id);

            LoadTables().Forget(); 
        }
        catch
        {
            Debug.LogError("Table delete failed");
        }
    }

    private void ClearTables()
    {
        HashSet<GameObject> ignoreObjects = new HashSet<GameObject>
    {
        CreateTableButton,
        PrivateTableButton
    };

        for (int i = Table_Content.childCount - 1; i >= 0; i--)
        {
            GameObject obj = Table_Content.GetChild(i).gameObject;

            if (ignoreObjects.Contains(obj))
                continue;

            Destroy(obj);
        }
    }

    private Sprite GetBadgeSprite(string badgeKey)
    {
        if (ClubBadgeSO == null || ClubBadgeSO.ClubBadges == null)
            return null;

        foreach (ClubBadgeData badge in ClubBadgeSO.ClubBadges)
        {
            if (badge.BadgeName.ToLower() == badgeKey.ToLower())
                return badge.BadgeImage;
        }

        return null;
    }

    private void BackButtonOnTap()
    {
        gameObject.SetActive(false);
    }


    #region Chips

    private async UniTaskVoid FetchAndDisplayChipsAsync()
    {
        try
        {
            var data = await AuthManager.Instance.GetChipsAsync()
                .AttachExternalCancellation(destroyCancellationToken);

            DisplayChips(data);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerHUDView] Chips fetch failed: {e.Message}");
        }
    }

    private void DisplayChips(ChipsData data)
    {
        if (data == null) return;

         Chips_Count.text = FormatChipCount(data.AvailableChips);
        // lockedChipsText.text = FormatChipCount(data.LockedInTables);
        //Chips_Count.text = FormatChipCount(data.AvailableChips);
    }
    private static string FormatChipCount(long chips)
    {
        if (chips >= 1_000_000) return $"{chips / 1_000_000f:0.#}M";
        if (chips >= 1_000) return $"{chips / 1_000f:0.#}K";
        return chips.ToString();
    }
    #endregion

}