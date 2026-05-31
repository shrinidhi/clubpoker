using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Newtonsoft.Json;
using ClubPoker.Lobby;

public class ClubCreateTableScreenScript : MonoBehaviour
{
    public Transform VariantContent;
    public GameObject VariantPrefab;
    public GameObject VariantScreen;
    public GameObject ClubCreateTable_Popup;

    public string ClubId;
    public string Variant_Name;

    [Header("Input Fields")]
    public TMP_InputField Maxplayer_InputField;
    public TMP_InputField SmallBlind_InputField;
    public TMP_InputField BigBlind_InputField;
    public TMP_InputField Min_Amount_InputField;
    public TMP_InputField Max_Amount_InputField;
    public TMP_InputField TableName_InputField;
    public TMP_InputField ActionTime_InputField;
    public TMP_InputField ANTE_BB_InputField;

    [Header("Dropdown")]
    public TMP_Dropdown GameLengthDropdown;

    [Header("Toggle")]
    public Toggle BombPot_Toggle;
    public Toggle RunItTwice_Toggle;
    public Toggle StraddleEnabled_Toggle;
    public Toggle VoluntaryStraddle_Toggle;

    [Header("Buttons")]
    public Button CreateTable_Button;
    public Button VariantScreenBack_Button;
    public Button ClubCreateTablePopup_Close_Button;

    [Header("Popup")]
    public GameObject ClubCreateTablePopup;

    [Header("References")]
    public ShowClubTableScreenScript ShowClubTableScreenScript;
    public TextMeshProUGUI ErrorText;

    public TextAsset ClubTableVariantJson;

    private ClubTableVariantResponse
        clubTableVariantResponse;

    [SerializeField] private VariantSO VariantSO;

    public Button Save_Button;
    public GameObject TableTamplete_Confirm_Screen;
    public Button Confirm_Button;
    public Button Cancel_Button;

    public TMP_Dropdown TempleteDropDown;

    public Button TableTemplete_Button;
    public GameObject TableTempleteScreen;

    private void Start()
    {
        ParseVariantJson();
        GenerateVariants();
        SetupGameLengthDropdown();
        SetupTemplateDropdown();
        if (CreateTable_Button != null)
            CreateTable_Button.onClick.AddListener(CreateTableButtonOnTap);

        if (VariantScreenBack_Button != null)
            VariantScreenBack_Button.onClick.AddListener(
                VariantScreenBack_ButtonOnTap);

        if (ClubCreateTablePopup_Close_Button != null)
            ClubCreateTablePopup_Close_Button.onClick.AddListener(
                ClubCreateTablePopup_Close_ButtonOnTap);

        Save_Button.onClick.AddListener(Save_ButtonOnTap);
        Confirm_Button.onClick.AddListener(Confirm_ButtonOnTap);
        Cancel_Button.onClick.AddListener(Cancel_ButtonOnTap);
        TableTemplete_Button.onClick.AddListener(TableTemplete_ButtonOnTap);
    }

    private void SetupTemplateDropdown()
    {
        if (TempleteDropDown == null)
            return;

        TempleteDropDown.ClearOptions();

        List<string> options = new List<string>()
    {
        "Templete 1",
        "Templete 2",
        "Templete 3",
        "Templete 4",
        "Templete 5"
    };

        TempleteDropDown.AddOptions(options);

        TempleteDropDown.value = 0;
        TempleteDropDown.RefreshShownValue();
    }
    private void OnEnable()
    {
        SetupToggles();
        SetInputField();
    }


    void Save_ButtonOnTap()
    {
        TableTamplete_Confirm_Screen.SetActive(true);
    }


    void TableTemplete_ButtonOnTap()
    {
        TableTempleteScreen.SetActive(true);
    }
    async void Confirm_ButtonOnTap()
    {
        if (!ValidateTemplateInputs(out SaveClubTableTemplateRequest request))
            return;

        Confirm_Button.interactable = false;

        try
        {
            ClubTableTemplateData template =
                await AuthManager.Instance.SaveClubTableTemplateAsync(
                    ClubId,
                    request
                );

            Debug.Log("Template Saved: " + template.Name);
            InformationPrefabScript.Instance.ShowMessage("Table Tamplete is Created");
            TableTamplete_Confirm_Screen.SetActive(false);
        }
        catch (Exception e)
        {
            ShowError("Template save failed");
            Debug.LogError(e.Message);
        }

        Confirm_Button.interactable = true;
    }
    private bool ValidateTemplateInputs(
     out SaveClubTableTemplateRequest request)
    {
        request = null;

        if (!ValidateInputs(out CreateClubTableRequest tableRequest))
            return false;

        int selectedSlot = 1;

        if (TempleteDropDown != null)
            selectedSlot = TempleteDropDown.value + 1;

        request = new SaveClubTableTemplateRequest
        {
            Slot = selectedSlot,

            Name = string.IsNullOrEmpty(tableRequest.Name)
                ? tableRequest.Variant + " " +
                  tableRequest.SmallBlind + "/" +
                  tableRequest.BigBlind
                : tableRequest.Name,

            Variant = tableRequest.Variant,
            SmallBlind = tableRequest.SmallBlind,
            BigBlind = tableRequest.BigBlind,
            Ante = tableRequest.Ante,
            BuyInMin = tableRequest.BuyInMin,
            BuyInMax = tableRequest.BuyInMax,
            MaxSeats = tableRequest.MaxSeats,
            ActionTimeSecs = tableRequest.ActionTimeSecs,
            BombPot = tableRequest.BombPot,
            StraddleEnabled = tableRequest.StraddleEnabled,
            RunItTwice = tableRequest.RunItTwice,
            VoluntaryStraddle = tableRequest.VoluntaryStraddle
        };

        Debug.Log("Selected Slot: " + selectedSlot);

        return true;
    }
    void Cancel_ButtonOnTap()
    {
        TableTamplete_Confirm_Screen.SetActive(false);
    }

    private void ParseVariantJson()
    {
        if (ClubTableVariantJson == null)
        {
            Debug.LogError(
                "ClubTableVariantJson Missing");
            return;
        }

        clubTableVariantResponse =
            JsonConvert.DeserializeObject
            <ClubTableVariantResponse>(
                ClubTableVariantJson.text);
    }


    private void SetInputField()
    {
        Maxplayer_InputField.text = 4.ToString();
        SmallBlind_InputField.text = 5.ToString();
        BigBlind_InputField.text = 10.ToString();
        Min_Amount_InputField.text = 1000.ToString();
        Max_Amount_InputField.text = 2000.ToString();
        ActionTime_InputField.text = 30.ToString();
        ANTE_BB_InputField.text = 2.ToString();
        TableName_InputField.text = "";
    }
    private void SetupToggles()
    {
        if (BombPot_Toggle != null)
            BombPot_Toggle.isOn = false;

        if (RunItTwice_Toggle != null)
            RunItTwice_Toggle.isOn = false;

        if (StraddleEnabled_Toggle != null)
            StraddleEnabled_Toggle.isOn = false;

        if (VoluntaryStraddle_Toggle != null)
            VoluntaryStraddle_Toggle.isOn = false;
    }

    private void VariantScreenBack_ButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private void ClubCreateTablePopup_Close_ButtonOnTap()
    {
        ClubCreateTablePopup.SetActive(false);
        VariantScreen.SetActive(true);
    }

    private void SetupGameLengthDropdown()
    {
        if (GameLengthDropdown == null)
            return;

        GameLengthDropdown.ClearOptions();

        GameLengthDropdown.AddOptions(new List<string>
        {
            "1 Hour",
            "2 Hours",
            "4 Hours",
            "6 Hours",
            "Unlimited"
        });

        GameLengthDropdown.value = 0;
        GameLengthDropdown.RefreshShownValue();
    }

    private void GenerateVariants()
    {
        ClearOldVariants();

        if (clubTableVariantResponse == null ||
            clubTableVariantResponse
            .ClubTableVariants == null)
        {
            Debug.LogError(
                "Club Variant Data Missing");
            return;
        }

        foreach (ClubTableVariantData variant
                 in clubTableVariantResponse
                 .ClubTableVariants)
        {
            GameObject obj =
                Instantiate(
                    VariantPrefab,
                    VariantContent);

            Club_VariantPrefabScreen prefab =
                obj.GetComponent
                <Club_VariantPrefabScreen>();

            Sprite sprite = null;

            if (VariantSO != null)
                sprite = VariantSO.GetVariantSprite(variant.VariantName);

            if (prefab != null)
            {
                prefab.SetData(
                    variant, sprite,
                    OnVariantSelected);
            }
        }
    }

    private void ClearOldVariants()
    {
        for (int i = VariantContent.childCount - 1; i >= 0; i--)
        {
            Destroy(VariantContent.GetChild(i).gameObject);
        }
    }

    private void OnVariantSelected(
    ClubTableVariantData variantData)
    {
        Debug.Log(
            "Selected Variant: " +
            variantData.VariantName);

        Variant_Name =
            variantData.VariantKey;

        VariantScreen.SetActive(false);
        ClubCreateTable_Popup.SetActive(true);
    }

    private async void CreateTableButtonOnTap()
    {
        if (ErrorText != null)
            ErrorText.text = "";

        if (!ValidateInputs(out CreateClubTableRequest request))
            return;

        CreateTable_Button.interactable = false;

        try
        {
            ClubTableData table =
                await AuthManager.Instance.CreateClubTableAsync(
                    ClubId,
                    request
                );

            Debug.Log("Club Table Created: " + table.Name);
            Debug.Log("Table ID: " + table.Id);

            if (ShowClubTableScreenScript != null)
                ShowClubTableScreenScript.LoadTables().Forget();

            ClubCreateTablePopup.SetActive(false);
            VariantScreen.SetActive(true);
            gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            ShowError("Something went wrong");
            Debug.LogError(
                "Create Club Table Failed: " + e.Message);
        }

        CreateTable_Button.interactable = true;
    }

    private bool ValidateInputs(
        out CreateClubTableRequest request)
    {
        request = null;

        if (string.IsNullOrEmpty(ClubId))
        {
            ShowError("Club ID missing");
            return false;
        }

        if (string.IsNullOrEmpty(Variant_Name))
        {
            ShowError("Please select variant");
            return false;
        }

        if (!int.TryParse(
                Maxplayer_InputField.text,
                out int maxSeats))
        {
            ShowError("Enter valid Max Player");
            return false;
        }

        if (!int.TryParse(
                SmallBlind_InputField.text,
                out int smallBlind))
        {
            ShowError("Enter valid Small Blind");
            return false;
        }

        if (!int.TryParse(
                BigBlind_InputField.text,
                out int bigBlind))
        {
            ShowError("Enter valid Big Blind");
            return false;
        }

        if (!int.TryParse(
                Min_Amount_InputField.text,
                out int buyInMin))
        {
            ShowError("Enter valid Min Buy-In");
            return false;
        }

        if (!int.TryParse(
                Max_Amount_InputField.text,
                out int buyInMax))
        {
            ShowError("Enter valid Max Buy-In");
            return false;
        }

        if (!int.TryParse(
                ActionTime_InputField.text,
                out int actionTime))
        {
            ShowError("Enter valid Action Time");
            return false;
        }

        if (!int.TryParse(
                ANTE_BB_InputField.text,
                out int ante))
        {
            ShowError("Enter valid Ante");
            return false;
        }

        if (maxSeats < 2 || maxSeats > 9)
        {
            ShowError(
                "Maximum players must be between 2 and 9");
            return false;
        }

        if (bigBlind <= smallBlind)
        {
            ShowError(
                "Big Blind must be greater than Small Blind");
            return false;
        }

        if (buyInMax <= buyInMin)
        {
            ShowError(
                "Max Buy-In must be greater than Min Buy-In");
            return false;
        }

        request = new CreateClubTableRequest
        {
            Variant = Variant_Name,
            SmallBlind = smallBlind,
            BigBlind = bigBlind,
            MaxSeats = maxSeats,
            BuyInMin = buyInMin,
            BuyInMax = buyInMax,

            Name = TableName_InputField.text.Trim(),
            Ante = ante,
            ActionTimeSecs = actionTime,
            DurationMinutes = GetDurationMinutes(),

            BombPot = BombPot_Toggle != null &&
                       BombPot_Toggle.isOn,

            RunItTwice = RunItTwice_Toggle != null &&
                         RunItTwice_Toggle.isOn,

            StraddleEnabled =
                StraddleEnabled_Toggle != null &&
                StraddleEnabled_Toggle.isOn,

            VoluntaryStraddle =
                VoluntaryStraddle_Toggle != null &&
                VoluntaryStraddle_Toggle.isOn
        };

        return true;
    }

    private int? GetDurationMinutes()
    {
        if (GameLengthDropdown == null)
            return 60;

        switch (GameLengthDropdown.value)
        {
            case 0:
                return 60;

            case 1:
                return 120;

            case 2:
                return 240;

            case 3:
                return 360;

            case 4:
                return null;

            default:
                return 60;
        }
    }

    private void ShowError(string message)
    {
        if (ErrorText != null)
            ErrorText.text = message;

        Debug.LogWarning(message);
    }
}