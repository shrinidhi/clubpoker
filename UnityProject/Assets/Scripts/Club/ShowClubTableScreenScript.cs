using ClubPoker.Auth;
using ClubPoker.Networking.Models;
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
        if (ClubListData.Role == "CREATOR")
        {
            Club_CreateTable_Button.interactable = true;
        }
        else
        {
            Club_CreateTable_Button.interactable = false;
        }
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

            prefab.Setup(table, OnDeleteTableClicked);
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