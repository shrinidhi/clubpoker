using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.Game;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

public class ShowClubTableScreenScript : MonoBehaviour
{
    public Button Back_Button;
    public Transform Table_Content;
    public GameObject ClubTable_Prefab;

    public ClubListData ClubListData;

    public Image ClubBadge_Image;
    public Text ClubName;
    public Text ClubCode;
    public string CLubID;

    public ClubBadgeSO ClubBadgeSO;
    public Button Club_CreateTable_Button;
    public GameObject ClubCreateTable_Screen;
    public ClubCreateTableScreenScript ClubCreateTableScreenScript;

    public Transform Variant_Content;
    public GameObject FilterTableByVariantPrefab;

    public TextAsset ClubTableVariantJson;

    [Header("Cashier")]
    public Button Cashier_Button;
    public CashierPanelScript CashierPanelScript;

    public Button MemberManagement_Button;
    public GameObject MemberManagment_Screen;

    [Header("Member View")]
    public GameObject TablesBg;

    private ClubTableVariantResponse clubTableVariantResponse;

    private List<ClubTableData> allTables = new List<ClubTableData>();
    private List<FilterTableByVariantPrefabScrtipt> variantItems =
        new List<FilterTableByVariantPrefabScrtipt>();

    private string selectedVariantKey = "all";
    private FilterTableByVariantPrefabScrtipt selectedVariantItem;

    private void Start()
    {
        if (Back_Button != null)
            Back_Button.onClick.AddListener(BackButtonOnTap);

        if (Club_CreateTable_Button != null)
            Club_CreateTable_Button.onClick.AddListener(Club_CreateTable_ButtonOnTap);

        if (Cashier_Button != null && CashierPanelScript != null)
        {
            bool isCreator = ClubContext.ParseRole(ClubListData.Role) == ClubRole.Creator;
            Cashier_Button.gameObject.SetActive(isCreator);
            if (isCreator)
                Cashier_Button.onClick.AddListener(OnCashierTap);
        }

        MemberManagement_Button.onClick.AddListener(MemberManagement_ButtonOnTap);
        ParseVariantJson();
        GenerateVariantFilters();
    }



    void MemberManagement_ButtonOnTap()
    {
        MemberManagment_Screen.SetActive(true);
       
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnTableUpdated += HandleTableUpdated;
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnTableUpdated -= HandleTableUpdated;
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

    public void ShowData(ClubListData clubListData)
    {
        ClubListData = clubListData;

        ClubName.text = clubListData.Name;
        ClubCode.text = "ID: " + clubListData.ClubCode;
        CLubID = clubListData.ClubId;
        ClubContext.Set(
            clubListData.ClubId, clubListData.Name,
            ClubContext.ParseRole(clubListData.Role),
            0, 0, 0);

        bool isCreator = ClubContext.IsAdmin;
        Club_CreateTable_Button.gameObject.SetActive(isCreator);
        MemberManagement_Button.gameObject.SetActive(isCreator);
        if (TablesBg != null) TablesBg.SetActive(!isCreator);
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

        CreateVariantFilter("all", "All");

        if (clubTableVariantResponse == null ||
            clubTableVariantResponse.ClubTableVariants == null)
            return;

        foreach (ClubTableVariantData variant in
                 clubTableVariantResponse.ClubTableVariants)
        {
            CreateVariantFilter(
                variant.VariantKey,
                variant.VariantName
            );
        }

        SelectDefaultAllVariant();
    }

    private void CreateVariantFilter(string key, string displayName)
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

        ApplyVariantFilter();
    }

    private void ApplyVariantFilter()
    {
        ClearTables();

        if (allTables == null)
            return;

        foreach (ClubTableData table in allTables)
        {
            if (selectedVariantKey != "all" &&
                table.Variant.ToLower() != selectedVariantKey.ToLower())
            {
                continue;
            }

            GameObject obj = Instantiate(
                ClubTable_Prefab,
                Table_Content
            );

            ClubTablePrefabScript prefab =
                obj.GetComponent<ClubTablePrefabScript>();

            prefab.Setup(table, OnDeleteTableClicked, OnExtendTableClicked, OnJoinTableClicked);
        }
    }


    private async void OnJoinTableClicked(ClubTableData table)
    {
        if (table == null) return;
        try
        {
            string tableId = table.TableId;

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

                // if (!string.IsNullOrEmpty(tableId))
                //     await AuthManager.Instance.LinkClubTableAsync(tableId, table.ClubId, table.Id);
            }

            if (string.IsNullOrEmpty(tableId)) return;

            if (UnityBotRunner.Instance != null)
                UnityBotRunner.Instance.StopBots();

            await AuthManager.Instance.JoinTableAsync(tableId, table.BuyInMin);
            TableJoinHandler.Instance.JoinTable(tableId);

            await UniTask.Delay(1500);

            if (UnityBotRunner.Instance != null)
                await UnityBotRunner.Instance.StartBots(tableId, table.MaxSeats, table.BuyInMin);

            await UniTask.Delay(1500);

            await AuthManager.Instance.StartTableAsync(tableId, 3);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShowClubTableScreenScript] Join failed: {e.Message}");
        }
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
        for (int i = Table_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Table_Content.GetChild(i).gameObject);
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
    private void OnCashierTap()
    {
        ClubContext.Set(
            ClubListData.ClubId,
            ClubListData.Name,
            ClubContext.ParseRole(ClubListData.Role),
            0, 0, 0
        );
        CashierPanelScript.gameObject.SetActive(true);
        CashierPanelScript.Init();
    }

}