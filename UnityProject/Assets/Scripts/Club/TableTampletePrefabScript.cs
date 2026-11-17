using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Networking.Models;

public class TableTampletePrefabScript : MonoBehaviour
{
    public Toggle TableSelect_Toogle;
    public TextMeshProUGUI Table_Name;
    public TextMeshProUGUI Variant_Name;
    public TextMeshProUGUI SmallBig_Blind;
    public TextMeshProUGUI Player_Count;
    public TextMeshProUGUI Table_Count;
    public Button Table_Subtract_Button;
    public Button Table_Add_Button;
    private ShowTableTempleteScreenScript parentScreen;

    private ClubTableTemplateData templateData;
    private int tableCount = 1;
    public TextMeshProUGUI GameLenghtText;

    public GameObject TableCountBG;
    public GameObject EditDeleteBG;
    public Button EditButton;
    public Button DeleteButton;
       
    public void Setup(
     ClubTableTemplateData data,
     ShowTableTempleteScreenScript screen)
    {
        templateData = data;
        parentScreen = screen;

        tableCount = 1;

        Table_Name.text = data.Name;
        Variant_Name.text = ClubPoker.Core.VariantUtils.ToDisplayName(data.Variant);
        SmallBig_Blind.text = data.SmallBlind + "/" + data.BigBlind;
        Player_Count.text = data.MaxSeats.ToString();
        GameLenghtText.text = (data.DurationMinutes / 60f) + "h";
        UpdateTableCount();

        TableSelect_Toogle.onValueChanged.RemoveAllListeners();

        TableSelect_Toogle.onValueChanged.AddListener((isOn) =>
        {
            UpdateSelectionUI(isOn);

            if (parentScreen != null)
                parentScreen.UpdateTotalTableCount();
        });

        Table_Subtract_Button.onClick.RemoveAllListeners();
        Table_Subtract_Button.onClick.AddListener(SubtractTableCount);

        Table_Add_Button.onClick.RemoveAllListeners();
        Table_Add_Button.onClick.AddListener(AddTableCount);

        if (EditButton != null)
        {
            EditButton.onClick.RemoveAllListeners();
            EditButton.onClick.AddListener(() =>
            {
                if (parentScreen != null)
                    parentScreen.EditTemplate(templateData);
            });
        }

        if (DeleteButton != null)
        {
            DeleteButton.onClick.RemoveAllListeners();

            DeleteButton.onClick.AddListener(() =>
            {
                if (parentScreen != null)
                    parentScreen.ShowDeleteConfirmation(templateData);
            });
        }
    }



    private void AddTableCount()
    {
        tableCount++;

        UpdateTableCount();

        if (parentScreen != null)
            parentScreen.UpdateTotalTableCount();
    }

    private void SubtractTableCount()
    {
        if (tableCount <= 1)
            return;

        tableCount--;

        UpdateTableCount();

        if (parentScreen != null)
            parentScreen.UpdateTotalTableCount();
    }
    private void UpdateTableCount()
    {
        if (Table_Count != null)
            Table_Count.text = tableCount.ToString();
    }

    public bool IsSelected()
    {
        return TableSelect_Toogle != null && TableSelect_Toogle.isOn;
    }

    public ClubTableTemplateData GetTemplateData()
    {
        return templateData;
    }

    public int GetTableCount()
    {
        return tableCount;
    }


    private void UpdateSelectionUI(bool isSelected)
    {
        if (TableCountBG != null)
            TableCountBG.SetActive(isSelected);

        if (EditDeleteBG != null)
            EditDeleteBG.SetActive(!isSelected);
    }
}