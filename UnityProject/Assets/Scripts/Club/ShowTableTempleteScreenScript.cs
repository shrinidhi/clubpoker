using System.Collections.Generic;
using UnityEngine;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ShowTableTempleteScreenScript : MonoBehaviour
{
    public Transform Content;
    public GameObject TableTampletePrefab;

    public string ClubId;

    private List<TableTampletePrefabScript> templateItems =
        new List<TableTampletePrefabScript>();

    private List<ClubTableTemplateData> allTemplates =
        new List<ClubTableTemplateData>();

    public ClubCreateTableScreenScript ClubCreateTableScreenScript;

    public Button Close_Button;
    public Button Confirm_Button;
    public Toggle SelectAllTebleTemplete;

    public TMP_Dropdown TempleteDropDown;
    public Transform Variant_Content;
    public GameObject VariantPrefab;

    private string selectedVariant = "all";
    private FilterTableByVariantPrefabScrtipt currentSelectedVariant;
    public Toggle Adddateprefix_Toggle;

    public GameObject WaitingScreen;
    public GameObject ClubCreateTableScreen;
    private void OnEnable()
    {
        ClubId = ClubCreateTableScreenScript.ClubId;

        SetupTemplateDropdown();
        GenerateVariantButtons();

        selectedVariant = "all";

        LoadTemplates().Forget();

        if (Adddateprefix_Toggle != null)
            Adddateprefix_Toggle.isOn = false;
    }

  
    private void Start()
    {
        Close_Button.onClick.AddListener(Close_ButtonOnTap);
        Confirm_Button.onClick.AddListener(Confirm_ButtonOnTap);

        if (SelectAllTebleTemplete != null)
            SelectAllTebleTemplete.onValueChanged.AddListener(OnSelectAllChanged);

        if (TempleteDropDown != null)
            TempleteDropDown.onValueChanged.AddListener(OnTemplateDropdownChanged);
    }


    private void GenerateVariantButtons()
    {
        ClearVariantButtons();

        CreateVariantButton("all", "All");

        CreateVariantButton("texas_holdem", "NLH");
        CreateVariantButton("omaha", "PLO4");
        CreateVariantButton("omaha_six", "PLO6");
        CreateVariantButton("plo5", "PLO5");
        CreateVariantButton("AOF", "AOF");
        CreateVariantButton("maubinh", "MauBinh");
    }
    private void CreateVariantButton(
    string variantKey,
    string displayName)
    {
        GameObject obj =
            Instantiate(VariantPrefab, Variant_Content);

        FilterTableByVariantPrefabScrtipt prefab =
            obj.GetComponent<FilterTableByVariantPrefabScrtipt>();

        prefab.SetData(
            variantKey,
            displayName,
            OnVariantSelected
        );

        if (variantKey == "all")
        {
            currentSelectedVariant = prefab;
            prefab.SetSelected(true);
        }
    }
    private void OnVariantSelected(
   string variantKey,
   FilterTableByVariantPrefabScrtipt selectedPrefab)
    {
        selectedVariant = variantKey;

        if (currentSelectedVariant != null)
            currentSelectedVariant.SetSelected(false);

        currentSelectedVariant = selectedPrefab;

        if (currentSelectedVariant != null)
            currentSelectedVariant.SetSelected(true);

        GenerateSelectedSlotTemplate();
    }


    private void SetupTemplateDropdown()
    {
        if (TempleteDropDown == null)
            return;

        TempleteDropDown.ClearOptions();

        TempleteDropDown.AddOptions(new List<string>
        {
            "Templete 1",
            "Templete 2",
            "Templete 3",
            "Templete 4",
            "Templete 5"
        });

        TempleteDropDown.value = 0;
        TempleteDropDown.RefreshShownValue();
    }

    private void OnTemplateDropdownChanged(int index)
    {
        GenerateSelectedSlotTemplate();
    }

    private int GetSelectedSlot()
    {
        if (TempleteDropDown == null)
            return 1;

        return TempleteDropDown.value + 1;
    }

    void Close_ButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    void OnSelectAllChanged(bool isOn)
    {
        foreach (TableTampletePrefabScript item in templateItems)
        {
            if (item.TableSelect_Toogle != null)
                item.TableSelect_Toogle.isOn = isOn;
        }
    }

    async void Confirm_ButtonOnTap()
    {
        BulkCreateClubTablesRequest request =
            new BulkCreateClubTablesRequest
            {
                Items = new List<BulkCreateClubTableItem>()
            };

        foreach (TableTampletePrefabScript item in templateItems)
        {
            if (!item.IsSelected())
                continue;

            ClubTableTemplateData template = item.GetTemplateData();

            request.Items.Add(new BulkCreateClubTableItem
            {
                Slot = template.Slot,
                Quantity = item.GetTableCount(),
                AddDatePrefix = Adddateprefix_Toggle != null && Adddateprefix_Toggle.isOn
            });
        }

        if (request.Items.Count == 0)
        {
            Debug.LogWarning("Please select at least one template");
            return;
        }

        Confirm_Button.interactable = false;

        try
        {
            BulkCreateClubTablesApiResponse response =
                await AuthManager.Instance.BulkCreateClubTablesAsync(
                    ClubId,
                    request
                );

            Debug.Log("Created Tables: " + response.Created);

            
            StartCoroutine(ShowWaitingScreen());
            
        }
        catch
        {
            Debug.LogError("Bulk table create failed");
        }

        Confirm_Button.interactable = true;
    }

    public async UniTaskVoid LoadTemplates()
    {
        ClearTemplates();

        if (string.IsNullOrEmpty(ClubId))
        {
            Debug.LogError("ClubId missing");
            return;
        }

        allTemplates =
            await AuthManager.Instance.GetClubTableTemplatesAsync(ClubId);

        GenerateSelectedSlotTemplate();
    }

    private void GenerateSelectedSlotTemplate()
    {
        ClearTemplates();

        int selectedSlot = GetSelectedSlot();

        if (allTemplates == null)
            return;

        foreach (ClubTableTemplateData template in allTemplates)
        {
            if (template == null)
                continue;

            if (template.Slot != selectedSlot)
                continue;

            if (selectedVariant != "all" &&
                !template.Variant.Equals(
                    selectedVariant,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject obj =
                Instantiate(TableTampletePrefab, Content);

            TableTampletePrefabScript prefab =
                obj.GetComponent<TableTampletePrefabScript>();

            prefab.Setup(template);
            templateItems.Add(prefab);
        }

        if (SelectAllTebleTemplete != null)
            SelectAllTebleTemplete.isOn = false;
    }

    private void ClearTemplates()
    {
        templateItems.Clear();

        for (int i = Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Content.GetChild(i).gameObject);
        }
    }

    private void ClearVariantButtons()
    {
        for (int i = Variant_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(
                Variant_Content.GetChild(i).gameObject
            );
        }
    }



    IEnumerator ShowWaitingScreen()
    {
        WaitingScreen.SetActive(true);
        if (ClubCreateTableScreenScript.ShowClubTableScreenScript != null)
            ClubCreateTableScreenScript.ShowClubTableScreenScript.LoadTables().Forget();
        yield return new WaitForSeconds(2f);
        WaitingScreen.SetActive(false);
        ClubCreateTableScreen.SetActive(false);
        gameObject.SetActive(false);
       
    }
}