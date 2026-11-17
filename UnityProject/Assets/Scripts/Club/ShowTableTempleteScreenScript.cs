using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    public GameObject AddDatePrefix;
    public GameObject WaitingScreen;
    public GameObject ClubCreateTableScreen;
    public Text TableCount;

    public GameObject DeleteTempleteConformationScreen;
    public Button DeleteConfirmButton;
    public Button CancelButton;

    private ClubTableTemplateData pendingDeleteTemplate;
    private bool isDeletingTemplate = false;
    private void OnEnable()
    {
        ClubId = ClubCreateTableScreenScript.ClubId;

        selectedVariant = "all";

        SetupTemplateDropdown();
        GenerateVariantButtons();

        if (Adddateprefix_Toggle != null)
            Adddateprefix_Toggle.isOn = false;

        if (AddDatePrefix != null)
            AddDatePrefix.SetActive(false);

        if (SelectAllTebleTemplete != null)
            SelectAllTebleTemplete.SetIsOnWithoutNotify(false);

        LoadTemplates().Forget();
    }


    private void Start()
    {
        Close_Button.onClick.AddListener(Close_ButtonOnTap);
        Confirm_Button.onClick.AddListener(Confirm_ButtonOnTap);

        if (SelectAllTebleTemplete != null)
            SelectAllTebleTemplete.onValueChanged.AddListener(OnSelectAllChanged);

        if (TempleteDropDown != null)
            TempleteDropDown.onValueChanged.AddListener(OnTemplateDropdownChanged);

        if (DeleteConfirmButton != null)
        {
            DeleteConfirmButton.onClick.AddListener(DeleteConfirmButtonOnTap);
        }

        if (CancelButton != null)
        {
            CancelButton.onClick.AddListener(() =>
            {
                pendingDeleteTemplate = null;

                if (DeleteTempleteConformationScreen != null)
                    DeleteTempleteConformationScreen.SetActive(false);
            });
        }
    }
    public void ShowDeleteConfirmation(ClubTableTemplateData template)
    {
        if (template == null)
            return;

        pendingDeleteTemplate = template;

        if (DeleteTempleteConformationScreen != null)
            DeleteTempleteConformationScreen.SetActive(true);
    }

    private async void DeleteConfirmButtonOnTap()
    {
        if (pendingDeleteTemplate == null || isDeletingTemplate)
            return;

        isDeletingTemplate = true;
        DeleteConfirmButton.interactable = false;

        try
        {
            ClubTableTemplateDeleteResponse response =
                await AuthManager.Instance.DeleteClubTableTemplateAsync(
                    ClubId,
                    pendingDeleteTemplate.Slot
                );

            if (response != null && response.Deleted)
            {
                Debug.Log("Template Deleted: " + response.Slot);

                DeleteTempleteConformationScreen.SetActive(false);
                pendingDeleteTemplate = null;

                LoadTemplates().Forget();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Delete Template Error: " + e.Message);
        }
        finally
        {
            isDeletingTemplate = false;
            DeleteConfirmButton.interactable = true;
        }
    }

    private void GenerateVariantButtons()
    {
        ClearVariantButtons();

        CreateVariantButton("all", Loc.Get("all"),false);

        CreateVariantButton("texas_holdem", "NLH", false);
        CreateVariantButton("omaha", "PLO4", false);
        CreateVariantButton("omaha_six", "PLO6", false);
        CreateVariantButton("plo5", "PLO5",true);
        CreateVariantButton("AOF", "AOF", true);
        CreateVariantButton("maubinh", "MauBinh", true);
    }
    private void CreateVariantButton(
    string variantKey,
    string displayName,
    bool islocked)
    {
        GameObject obj =
            Instantiate(VariantPrefab, Variant_Content);

        FilterTableByVariantPrefabScrtipt prefab =
            obj.GetComponent<FilterTableByVariantPrefabScrtipt>();

        prefab.SetData(
            variantKey,
            displayName,
            islocked,
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
            Loc.Get("template_n", ("n", 1)),
            Loc.Get("template_n", ("n", 2)),
            Loc.Get("template_n", ("n", 3)),
            Loc.Get("template_n", ("n", 4)),
            Loc.Get("template_n", ("n", 5))
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
        {
            item.TableSelect_Toogle.isOn = isOn;
        }
    }

    UpdateTotalTableCount();
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

            prefab.Setup(template, this);

            if (prefab.TableSelect_Toogle != null)
                prefab.TableSelect_Toogle.SetIsOnWithoutNotify(false);

            templateItems.Add(prefab);
        }

        if (SelectAllTebleTemplete != null)
            SelectAllTebleTemplete.SetIsOnWithoutNotify(false);

        UpdateTotalTableCount();
    }
    public void EditTemplate(ClubTableTemplateData template)
    {
        if (template == null)
            return;

        if (ClubCreateTableScreenScript != null)
        {
            ClubCreateTableScreenScript.OpenEditTemplate(template);
            gameObject.SetActive(false);
        }
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

    public void UpdateTotalTableCount()
    {
        int totalCount = 0;
        int selectedCount = 0;

        foreach (TableTampletePrefabScript item in templateItems)
        {
            if (item.IsSelected())
            {
                selectedCount++;
                totalCount += item.GetTableCount();
            }
        }

        if (TableCount != null)
        {
            TableCount.text = Loc.Get(
                "table_count",
                ("count", totalCount));
        }

      
        bool allSelected =
            templateItems.Count > 0 &&
            selectedCount == templateItems.Count;

        if (SelectAllTebleTemplete != null)
        {
            SelectAllTebleTemplete.SetIsOnWithoutNotify(allSelected);
        }

        bool isAnyTableSelected = selectedCount > 0;

        if (AddDatePrefix != null)
            AddDatePrefix.SetActive(isAnyTableSelected);

        if (!isAnyTableSelected && Adddateprefix_Toggle != null)
            Adddateprefix_Toggle.isOn = false;
    }
}