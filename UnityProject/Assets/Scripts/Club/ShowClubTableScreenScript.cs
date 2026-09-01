using ClubPoker.Auth;
using ClubPoker.Core;
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

    // Buy-in for a club table happens *inside* GameTable: the player is taken to
    // the table as an observer and ClubBuyInPanel (a GameTable prefab) opens there.
    // This screen only flags that a buy-in is owed — see TableContext.BeginClubBuyIn.

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

    // Addressables key, not the scene's own name.
    private const string SCENE_GAME_TABLE = "Scene_GameTable";

    private const int PollIntervalMs = 5000;
    private bool _polling;
    private string _tablesSignature;

    private string selectedVariantKey = "all";
    private FilterTableByVariantPrefabScrtipt selectedVariantItem;

    public GameObject CreateTableButton;
    public GameObject PrivateTableButton;

    public Toggle OpenSeat_Toggle;
    public Toggle Running_Toggle;

    [Header("Chips")]
    [Tooltip("This member's own club chips — what club tables are played with.")]
    public Text Chips_Count;

    [Tooltip("The club's chip pool. Optional; leave empty to hide the figure.")]
    public Text ClubPool_Count;

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
        ClubContext.OnPoolChipsChanged   += OnPoolChipsChanged;
        ClubWallet.OnChanged             += OnClubChipsChanged;

        _polling = true;
        PollTables().Forget();
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnTableUpdated -= HandleTableUpdated;
        ClubContext.OnClubDetailChanged  -= OnClubDetailChanged;
        ClubContext.OnClubTablesChanged  -= OnClubTablesChanged;
        ClubContext.OnPoolChipsChanged   -= OnPoolChipsChanged;
        ClubWallet.OnChanged             -= OnClubChipsChanged;

        _polling = false;
    }

    // Buy-in, top-up and withdraw all move the club balance; the header follows.
    private void OnClubChipsChanged()
    {
        if (!string.IsNullOrEmpty(ClubID))
            DisplayChips(ClubWallet.Chips);
    }

    // Cashier actions (add to pool, send out, claim back) move the pool.
    private void OnPoolChipsChanged() => DisplayPool(ClubContext.PoolChips);

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

        // The club detail is the one pool figure every member can read — the chips
        // summary endpoint behind ClubContext.PoolChips is a cashier screen.
        DisplayPool(detail?.ChipPool ?? ClubContext.PoolChips);
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
            DescriptionText.text = "Welcome to Club Poker";
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

        // The club id only exists from here on, and the balance shown is club-scoped.
        FetchAndDisplayChipsAsync().Forget();

        // Pool from whatever is already cached; ClubViewController's detail fetch
        // fills it in through OnClubDetailChanged a moment later.
        DisplayPool(ClubContext.ClubDetail?.ChipPool ?? ClubContext.PoolChips);

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

    /// <summary>
    /// Tapping a club table takes the player to GameTable, where ClubBuyInPanel opens
    /// and confirming it is what seats them and starts the game.
    ///
    /// Nothing is created here. A club table row is a template; the engine table
    /// behind it is created on buy-in confirm (ClubSeatFlow.EnsureTableAsync), so
    /// opening a table and backing out leaves no empty table behind.
    ///
    /// Two entries, depending on what the row points at:
    ///   • seat available (live table or none yet) → straight to GameTable with no
    ///     socket join at all; the buy-in creates the table if needed and seats
    ///   • live table, full or mid-hand → watch &amp; wait as a spectator, seated when
    ///     a chair frees (the one case where spectating is the point)
    /// </summary>
    private async void OnJoinTableClicked(ClubTableData table)
    {
        Debug.Log($"[ShowClubTableScreenScript] Join table tapped, tableId={table?.TableId}");
        if (table == null) return;

        try
        {
            TableActiveData active = null;

            // Row already linked → check the real table is still alive before joining.
            if (!string.IsNullOrEmpty(table.TableId))
            {
                active = await AuthManager.Instance.GetTableActiveAsync(table.TableId);

                if (active == null || !active.Active)
                {
                    Debug.LogWarning($"[ShowClubTableScreenScript] Linked table {table.TableId} not active — a new one is created on buy-in");
                    table.TableId = null;
                }
            }

            bool isLive = !string.IsNullOrEmpty(table.TableId);

            // Club origin: the in-game menu unlocks the club-only options and Back
            // returns to this screen. Table id may still be null — EnsureTableAsync
            // re-enters with the real one once it exists.
            TableContext.EnterFromClub(table, table.TableId);

            // No bots on club tables — real members only. Stop any bots left over
            // from a lobby/quick-join session before seating.
            if (UnityBotRunner.Instance != null)
                UnityBotRunner.Instance.StopBots();

            // Table already running → can't sit mid-hand or in a full table.
            // Same rule as the lobby: watch & wait, seat when one frees.
            if (isLive && (active.HandInProgress || table.PlayerCount >= table.MaxSeats))
            {
                await WatchAndWaitAsync(table.TableId, table.BuyInMin);
                return;
            }

            ClubSeatFlow.Begin(table);

            // No spectate and no socket join on the way in — a player heading for a
            // seat has no reason to register as an observer, and TakeSeatAsync would
            // only tear that connection down again to re-handshake as seated. The
            // table screen opens on the buy-in popup; joining happens on confirm.
            //
            // Drop any previous table's state first or the table renders with the
            // last game's seats.
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.Clear();

            if (GameSceneManager.Instance != null)
                GameSceneManager.Instance.LoadScene(SCENE_GAME_TABLE);
            else
                Debug.LogError("[ShowClubTableScreenScript] GameSceneManager.Instance is null");
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

    /// <summary>
    /// Header balance. Inside a club that's the member's club chips — the only
    /// chips these tables can be played with — so it comes from the member record,
    /// not the global wallet.
    /// </summary>
    private async UniTaskVoid FetchAndDisplayChipsAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(ClubID))
            {
                await ClubWallet.RefreshAsync(ClubID)
                    .AttachExternalCancellation(destroyCancellationToken);

                DisplayChips(ClubWallet.Chips);
                return;
            }

            var data = await AuthManager.Instance.GetChipsAsync()
                .AttachExternalCancellation(destroyCancellationToken);

            if (data != null)
                DisplayChips(data.AvailableChips);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerHUDView] Chips fetch failed: {e.Message}");
        }
    }

    private void DisplayChips(long chips)
    {
        if (Chips_Count != null)
            Chips_Count.text = FormatChipCount(chips);
    }

    /// <summary>Club chip pool — the balance the whole club draws from, distinct
    /// from this member's own club chips shown next to it.</summary>
    private void DisplayPool(long pool)
    {
        if (ClubPool_Count != null)
            ClubPool_Count.text = FormatChipCount(pool);
    }
    private static string FormatChipCount(long chips)
    {
        if (chips >= 1_000_000) return $"{chips / 1_000_000f:0.#}M";
        if (chips >= 1_000) return $"{chips / 1_000f:0.#}K";
        return chips.ToString();
    }
    #endregion

}