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

    public void Setup(
     ClubTableTemplateData data,
     ShowTableTempleteScreenScript screen)
    {
        templateData = data;
        parentScreen = screen;

        tableCount = 1;

        Table_Name.text = data.Name;
        Variant_Name.text = data.Variant;
        SmallBig_Blind.text = data.SmallBlind + "/" + data.BigBlind;
        Player_Count.text = data.MaxSeats.ToString();

        UpdateTableCount();

        TableSelect_Toogle.onValueChanged.RemoveAllListeners();
        TableSelect_Toogle.onValueChanged.AddListener((x) =>
        {
            parentScreen.UpdateTotalTableCount();
        });

        Table_Subtract_Button.onClick.RemoveAllListeners();
        Table_Subtract_Button.onClick.AddListener(SubtractTableCount);

        Table_Add_Button.onClick.RemoveAllListeners();
        Table_Add_Button.onClick.AddListener(AddTableCount);
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
}