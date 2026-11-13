
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
using ClubPoker.Core;

public class ClubCreateTableScreenScript : MonoBehaviour
{
    public Transform VariantContent;
    public GameObject VariantPrefab;
    public GameObject VariantScreen;
    public GameObject ClubCreateTable_Popup;

    public string ClubId;
    public string Variant_Name;

    [Header("Input Fields")]
    public TMP_InputField TableName_InputField;
    public TMP_InputField Fee_InputField;
    public TMP_InputField Cap_InputField;
    public TMP_InputField BombPotAnte_InputField;
    public TMP_InputField BombPotEverynHands_InputField;

    public GameObject BombPotAnte;
    public GameObject BombPotEverynHands;

    [Header("Scroll")]
    public ScrollRect CreateTableScrollRect;

    [Header("Dropdown")]
    public TMP_Dropdown GameLengthDropdown;

    [Header("Toggle")]
    public Toggle BombPot_Toggle;
    public Toggle RunItTwice_Toggle;
    public Toggle AutoStraddleEnabled_Toggle;
    public Toggle VoluntaryStraddle_Toggle;
    public Toggle JackPoot_Toggle;

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

    private ClubTableVariantResponse clubTableVariantResponse;

    [SerializeField] private VariantSO VariantSO;

    public Button Save_Button;
    public GameObject TableTamplete_Confirm_Screen;
    public Button Confirm_Button;
    public Button Cancel_Button;

    public TMP_Dropdown TempleteDropDown;

    public Button TableTemplete_Button;
    public GameObject TableTempleteScreen;

    public Slider MaxPlayerSlider;
    public Text MaxPlayerCount;

    public Slider MinMaxBlindSlider;
    public Text MinMaxBLindCount;

    public MinMaxBuyRangeSlider MinMaxBuySlider;
    public Text MinBuyCount;
    public Text MaxBuyCount;

    [Header("Action Time Slider")]
    public Slider ActionTimeSlider;
    public Text ActionTimeCount;

    [Header("Game Length Slider")]
    public Slider GameLenghtSlider;
    public Text GameLenghtCount;

    [Header("Ante Slider")]
    public Slider ANTE_BB_Slider;
    public Text ANTE_BB_Count;
    private const int MinAnte = 0;
    private const int MaxAnte = 10;
    private const int DefaultAnte = 2;

    private const int MinAllowedPlayers = 2;

    private const int DefaultActionTime = 30;
    private const int MinActionTime = 8;
    private const int MaxActionTime = 35;

    private const int DefaultGameLengthHours = 1;
    private const int MinGameLengthHours = 1;
    private const int MaxGameLengthHours = 6;

    // Small blind values; big blind is always twice the small blind.
    private readonly int[] BlindSteps =
    {
        1, 2, 3, 4, 5, 10, 15, 20,
        25, 30, 40, 50, 75, 100, 150, 200, 250, 300, 400, 500, 750,
        1000, 1500, 2000, 2500, 3000, 4000, 5000, 7500,
        10000, 15000, 20000, 25000, 30000, 40000, 50000
    };

    private int SelectedSmallBlind = 10;
    private int SelectedBigBlind = 20;
    private bool isConfiguringBlindSlider;

    private void Awake()
    {
        if (MaxPlayerSlider != null)
            MaxPlayerSlider.onValueChanged.AddListener(OnMaxPlayerSliderChanged);

        if (MinMaxBuySlider != null)
            MinMaxBuySlider.OnRangeChanged.AddListener(OnBuyRangeChanged);

        if (MinMaxBlindSlider != null)
            MinMaxBlindSlider.onValueChanged.AddListener(OnBlindSliderChanged);

        // Action Time Slider Listener
        if (ActionTimeSlider != null)
            ActionTimeSlider.onValueChanged.AddListener(OnActionTimeSliderChanged);

        // Game Length Slider Listener
        if (GameLenghtSlider != null)
            GameLenghtSlider.onValueChanged.AddListener(OnGameLengthSliderChanged);

        if (ANTE_BB_Slider != null)
            ANTE_BB_Slider.onValueChanged.AddListener(OnAnteSliderChanged);
    }

    private int GetMaxAllowedPlayers()
    {
        return string.Equals(
            Variant_Name, "omaha_six", StringComparison.OrdinalIgnoreCase)
            ? 7
            : 9;
    }

    private int GetDefaultMaxPlayerCount()
    {
        return GetMaxAllowedPlayers() == 7 ? 7 : 4;
    }

    private void ConfigureMaxPlayerSlider(int selectedCount)
    {
        if (MaxPlayerSlider == null)
        {
            if (MaxPlayerCount != null)
                MaxPlayerCount.text = "-";

            Debug.LogError("[Club Create Table] Assign MaxPlayerSlider.");
            return;
        }

        MaxPlayerSlider.wholeNumbers = true;
        MaxPlayerSlider.minValue = MinAllowedPlayers;
        MaxPlayerSlider.maxValue = GetMaxAllowedPlayers();

        int count = Mathf.Clamp(
            selectedCount, MinAllowedPlayers, GetMaxAllowedPlayers());

        MaxPlayerSlider.SetValueWithoutNotify(count);
        UpdateMaxPlayerCount(count);
    }

    private void OnMaxPlayerSliderChanged(float value)
    {
        if (MaxPlayerSlider == null)
            return;

        int count = Mathf.Clamp(
            Mathf.RoundToInt(value),
            MinAllowedPlayers,
            GetMaxAllowedPlayers());

        MaxPlayerSlider.SetValueWithoutNotify(count);
        UpdateMaxPlayerCount(count);

        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void UpdateMaxPlayerCount(int count)
    {
        if (MaxPlayerCount != null)
            MaxPlayerCount.text = "(" + count.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    private void OnDestroy()
    {
        if (MaxPlayerSlider != null)
            MaxPlayerSlider.onValueChanged.RemoveListener(OnMaxPlayerSliderChanged);

        if (MinMaxBuySlider != null)
            MinMaxBuySlider.OnRangeChanged.RemoveListener(OnBuyRangeChanged);

        if (MinMaxBlindSlider != null)
            MinMaxBlindSlider.onValueChanged.RemoveListener(OnBlindSliderChanged);

        if (ActionTimeSlider != null)
            ActionTimeSlider.onValueChanged.RemoveListener(OnActionTimeSliderChanged);

        if (GameLenghtSlider != null)
            GameLenghtSlider.onValueChanged.RemoveListener(OnGameLengthSliderChanged);

        if (ANTE_BB_Slider != null)
            ANTE_BB_Slider.onValueChanged.RemoveListener(OnAnteSliderChanged);
    }

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

        if (AutoStraddleEnabled_Toggle != null)
            AutoStraddleEnabled_Toggle.onValueChanged.AddListener(
                OnAutoStraddleChanged);

        if (VoluntaryStraddle_Toggle != null)
            VoluntaryStraddle_Toggle.onValueChanged.AddListener(
                OnVoluntaryStraddleChanged);

        if (BombPot_Toggle != null)
            BombPot_Toggle.onValueChanged.AddListener(
                OnBombPotChanged);
    }
    // ========================= ANTE SLIDER =========================

    private void SetupAnteSlider()
    {
        if (ANTE_BB_Slider == null)
            return;

        ANTE_BB_Slider.wholeNumbers = true;
        ANTE_BB_Slider.minValue = MinAnte;
        ANTE_BB_Slider.maxValue = MaxAnte;

        ANTE_BB_Slider.SetValueWithoutNotify(DefaultAnte);

        UpdateAnteUI(DefaultAnte);
    }

    private void OnAnteSliderChanged(float value)
    {
        int ante = Mathf.Clamp(
            Mathf.RoundToInt(value),
            MinAnte,
            MaxAnte);

        if (ANTE_BB_Slider != null)
            ANTE_BB_Slider.SetValueWithoutNotify(ante);

        UpdateAnteUI(ante);

        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void UpdateAnteUI(int ante)
    {
        if (ANTE_BB_Count != null)
            ANTE_BB_Count.text = ante.ToString();
    }
    private void SetupTemplateDropdown()
    {
        if (TempleteDropDown == null)
            return;

        TempleteDropDown.ClearOptions();

        List<string> options = new List<string>()
        {
            Loc.Get("template_n", ("n", 1)),
            Loc.Get("template_n", ("n", 2)),
            Loc.Get("template_n", ("n", 3)),
            Loc.Get("template_n", ("n", 4)),
            Loc.Get("template_n", ("n", 5))
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
        if (!ValidateTemplateInputs(
                out SaveClubTableTemplateRequest request))
            return;

        Confirm_Button.interactable = false;

        try
        {
            ClubTableTemplateData template =
                await AuthManager.Instance.SaveClubTableTemplateAsync(
                    ClubId,
                    request
                );

            Debug.Log(
                "Template Saved: " +
                template.Name);

            InformationPrefabScript.Instance.ShowMessage(
                Loc.Get("table_template_created"));

            TableTamplete_Confirm_Screen.SetActive(false);
        }
        catch (Exception e)
        {
            ShowError(Loc.Get("template_save_failed"));
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

        bool bombPotEnabled =
            BombPot_Toggle != null && BombPot_Toggle.isOn;

        int bombPotAmount = 0;
        int bombPotEveryNHands = 0;

        if (bombPotEnabled)
        {
            int.TryParse(BombPotAnte_InputField.text, out bombPotAmount);
            int.TryParse(
                BombPotEverynHands_InputField.text,
                out bombPotEveryNHands);
        }

        int durationMinutes = GetDurationMinutes() ?? 0;

        request = new SaveClubTableTemplateRequest
        {
            Slot = selectedSlot,

            Name = string.IsNullOrWhiteSpace(tableRequest.Name)
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
            DurationMinutes = durationMinutes,

            FeePercent = tableRequest.FeePercent,
            FeeCapBBs = tableRequest.FeeCapBBs,

            BombPot = tableRequest.BombPot,
            BombPotAmount = bombPotAmount,
            BombPotEveryNHands = bombPotEveryNHands,

            JackpotEnabled = tableRequest.JackpotEnabled,
            StraddleEnabled = tableRequest.StraddleEnabled,
            RunItTwice = tableRequest.RunItTwice,
            VoluntaryStraddle = tableRequest.VoluntaryStraddle
        };

        Debug.Log(
            "Template Save Request: " +
            JsonConvert.SerializeObject(request, Formatting.Indented));

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
            Debug.LogError("ClubTableVariantJson Missing");
            return;
        }

        clubTableVariantResponse =
            JsonConvert.DeserializeObject<ClubTableVariantResponse>(
                ClubTableVariantJson.text);
    }

    private void SetInputField()
    {
        ConfigureMaxPlayerSlider(GetDefaultMaxPlayerCount());

        ConfigureBlindSlider(5);

        // Initialize Action Time Slider
        SetupActionTimeSlider();

        // Initialize Game Length Slider
        SetupGameLengthSlider();

        if (Fee_InputField != null)
            Fee_InputField.text = 0.ToString();

        if (Cap_InputField != null)
            Cap_InputField.text = 10.ToString();

        SetupAnteSlider();

        if (BombPotAnte_InputField != null)
            BombPotAnte_InputField.text = 20.ToString();

        if (BombPotEverynHands_InputField != null)
            BombPotEverynHands_InputField.text = 10.ToString();

        if (TableName_InputField != null)
            TableName_InputField.text = "";
    }

    // ========================= ACTION TIME SLIDER =========================

    private void SetupActionTimeSlider()
    {
        if (ActionTimeSlider == null)
            return;

        ActionTimeSlider.wholeNumbers = true;
        ActionTimeSlider.minValue = MinActionTime;
        ActionTimeSlider.maxValue = MaxActionTime;

        ActionTimeSlider.SetValueWithoutNotify(DefaultActionTime);

        UpdateActionTimeUI(DefaultActionTime);
    }

    private void OnActionTimeSliderChanged(float value)
    {
        int seconds = Mathf.Clamp(
            Mathf.RoundToInt(value),
            MinActionTime,
            MaxActionTime);

        if (ActionTimeSlider != null)
            ActionTimeSlider.SetValueWithoutNotify(seconds);

        UpdateActionTimeUI(seconds);

        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void UpdateActionTimeUI(int seconds)
    {
        if (ActionTimeCount != null)
            ActionTimeCount.text = seconds + " sec";
    }

    // ========================= GAME LENGTH SLIDER =========================

    private void SetupGameLengthSlider()
    {
        if (GameLenghtSlider == null)
            return;

        GameLenghtSlider.wholeNumbers = true;
        GameLenghtSlider.minValue = MinGameLengthHours;
        GameLenghtSlider.maxValue = MaxGameLengthHours;

        GameLenghtSlider.SetValueWithoutNotify(DefaultGameLengthHours);

        UpdateGameLengthUI(DefaultGameLengthHours);
    }

    private void OnGameLengthSliderChanged(float value)
    {
        int hours = Mathf.Clamp(
            Mathf.RoundToInt(value),
            MinGameLengthHours,
            MaxGameLengthHours);

        if (GameLenghtSlider != null)
            GameLenghtSlider.SetValueWithoutNotify(hours);

        UpdateGameLengthUI(hours);

        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void UpdateGameLengthUI(int hours)
    {
        if (GameLenghtCount != null)
        {
            GameLenghtCount.text =
                hours + (hours == 1 ? " Hour" : " Hours");
        }
    }

    // ========================= BLIND SLIDER =========================

    private void ConfigureBlindSlider(int defaultSmallBlind)
    {
        if (MinMaxBlindSlider == null)
        {
            Debug.LogError("[Club Create Table] Assign MinMaxBlindSlider.");
            return;
        }

        isConfiguringBlindSlider = true;
        MinMaxBlindSlider.wholeNumbers = true;
        MinMaxBlindSlider.minValue = 0;
        MinMaxBlindSlider.maxValue = BlindSteps.Length - 1;

        int index = FindBlindIndex(defaultSmallBlind);
        MinMaxBlindSlider.SetValueWithoutNotify(index);
        isConfiguringBlindSlider = false;

        UpdateBlindValues(index);
    }

    private int FindBlindIndex(int smallBlind)
    {
        int nearestIndex = 0;
        long nearestDistance = long.MaxValue;

        for (int i = 0; i < BlindSteps.Length; i++)
        {
            long distance = Math.Abs((long)BlindSteps[i] - smallBlind);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    public void OnBlindSliderChanged(float value)
    {
        if (isConfiguringBlindSlider)
            return;

        int index = Mathf.Clamp(
            Mathf.RoundToInt(value), 0, BlindSteps.Length - 1);

        if (MinMaxBlindSlider != null)
            MinMaxBlindSlider.SetValueWithoutNotify(index);

        UpdateBlindValues(index);

        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void UpdateBlindValues(int index)
    {
        index = Mathf.Clamp(index, 0, BlindSteps.Length - 1);

        SelectedSmallBlind = BlindSteps[index];
        SelectedBigBlind = SelectedSmallBlind * 2;

        if (MinMaxBLindCount != null)
        {
            MinMaxBLindCount.text =
                MinMaxBuyRangeSlider.FormatValue(SelectedSmallBlind) + "/" +
                MinMaxBuyRangeSlider.FormatValue(SelectedBigBlind);
        }

        ConfigureBuySlider();
    }

    // ========================= BUY-IN SLIDER =========================


    private void ConfigureBuySlider()
    {
        if (MinMaxBuySlider == null)
        {
            Debug.LogError(
                "[Club Create Table] Assign MinMaxBuySlider.");
            return;
        }

        long minLong = (long)SelectedBigBlind * 10L;
        long maxLong = (long)SelectedBigBlind * 3000L;

        int minBuy = (int)Math.Min(int.MaxValue, minLong);
        int maxBuy = (int)Math.Min(int.MaxValue, maxLong);

        int[] values = GenerateBuyValues(minBuy, maxBuy);

        int initialMin = minBuy;
        int initialMax = (int)Math.Min(
            maxBuy,
            (long)SelectedBigBlind * 20L);

        if (initialMax <= initialMin)
            initialMax = maxBuy;

        // Preserve current handle positions when blind changes.
        MinMaxBuySlider.ConfigureValuesPreservePosition(
            values,
            initialMin,
            initialMax,
            false);

        // Update actual buy-in values in labels.
        UpdateBuyLabels(
            MinMaxBuySlider.LeftValue,
            MinMaxBuySlider.RightValue);
    }


    private int[] GenerateBuyValues(int minValue, int maxValue)
    {
        SortedSet<int> values = new SortedSet<int>();
        values.Add(minValue);
        values.Add(maxValue);

        // 1, 2, 3, 5 progression at each decimal magnitude.
        long magnitude = 1;

        while (magnitude <= maxValue)
        {
            long[] steps = { 1L, 2L, 3L, 5L };

            for (int i = 0; i < steps.Length; i++)
            {
                long candidate = steps[i] * magnitude;

                if (candidate >= minValue &&
                    candidate <= maxValue &&
                    candidate <= int.MaxValue)
                {
                    values.Add((int)candidate);
                }
            }

            if (magnitude > int.MaxValue / 10L)
                break;

            magnitude *= 10L;
        }

        return new List<int>(values).ToArray();
    }

    private void OnBuyRangeChanged(int minBuy, int maxBuy)
    {
        UpdateBuyLabels(minBuy, maxBuy);

        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void UpdateBuyLabels(int minBuy, int maxBuy)
    {
        if (MinBuyCount != null)
            MinBuyCount.text = MinMaxBuyRangeSlider.FormatValue(minBuy);

        if (MaxBuyCount != null)
            MaxBuyCount.text = MinMaxBuyRangeSlider.FormatValue(maxBuy);
    }

    // ========================= TOGGLES =========================

    private void SetupToggles()
    {
        if (BombPot_Toggle != null)
            BombPot_Toggle.isOn = false;

        if (RunItTwice_Toggle != null)
            RunItTwice_Toggle.isOn = false;

        if (AutoStraddleEnabled_Toggle != null)
            AutoStraddleEnabled_Toggle.isOn = false;

        if (VoluntaryStraddle_Toggle != null)
            VoluntaryStraddle_Toggle.isOn = false;

        if (JackPoot_Toggle != null)
            JackPoot_Toggle.isOn = false;

        if (BombPotAnte != null)
            BombPotAnte.SetActive(false);

        if (BombPotEverynHands != null)
            BombPotEverynHands.SetActive(false);

        if (CreateTableScrollRect != null)
            CreateTableScrollRect.verticalNormalizedPosition = 1f;
    }

    private void OnAutoStraddleChanged(bool isOn)
    {
        if (isOn && VoluntaryStraddle_Toggle != null)
            VoluntaryStraddle_Toggle.isOn = false;
    }

    private void OnVoluntaryStraddleChanged(bool isOn)
    {
        if (isOn && AutoStraddleEnabled_Toggle != null)
            AutoStraddleEnabled_Toggle.isOn = false;
    }

    private void OnBombPotChanged(bool isOn)
    {
        if (BombPotAnte != null)
            BombPotAnte.SetActive(isOn);

        if (BombPotEverynHands != null)
            BombPotEverynHands.SetActive(isOn);

        if (isOn)
            ScrollToBottom();
    }

    private async void ScrollToBottom()
    {
        await UniTask.Yield();

        Canvas.ForceUpdateCanvases();

        if (CreateTableScrollRect != null)
        {
            CreateTableScrollRect.verticalNormalizedPosition = 0f;

            Canvas.ForceUpdateCanvases();

            await UniTask.Yield();

            CreateTableScrollRect.verticalNormalizedPosition = 0f;
        }
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

    // Old dropdown setup retained for compatibility.
    private void SetupGameLengthDropdown()
    {
        if (GameLengthDropdown == null)
            return;

        GameLengthDropdown.ClearOptions();

        GameLengthDropdown.AddOptions(
            new List<string>
            {
                Loc.Get("duration_1h"),
                Loc.Get("duration_2h"),
                Loc.Get("duration_4h"),
                Loc.Get("duration_6h"),
                Loc.Get("unlimited")
            });

        GameLengthDropdown.value = 0;
        GameLengthDropdown.RefreshShownValue();
    }

    private void GenerateVariants()
    {
        ClearOldVariants();

        if (clubTableVariantResponse == null ||
            clubTableVariantResponse.ClubTableVariants == null)
        {
            Debug.LogError("Club Variant Data Missing");
            return;
        }

        foreach (ClubTableVariantData variant in
                 clubTableVariantResponse.ClubTableVariants)
        {
            GameObject obj = Instantiate(VariantPrefab, VariantContent);

            Club_VariantPrefabScreen prefab =
                obj.GetComponent<Club_VariantPrefabScreen>();

            Sprite sprite = null;

            if (VariantSO != null)
                sprite = VariantSO.GetVariantSprite(variant.VariantName);

            if (prefab != null)
            {
                prefab.SetData(
                    variant,
                    sprite,
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

    private void OnVariantSelected(ClubTableVariantData variantData)
    {
        Debug.Log("Selected Variant: " + variantData.VariantName);

        Variant_Name = variantData.VariantKey;

        SetInputField();
        SetupToggles();

        if (ErrorText != null)
            ErrorText.text = "";

        VariantScreen.SetActive(false);
        ClubCreateTable_Popup.SetActive(true);
    }

    // ========================= CREATE TABLE =========================

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
                    request);

            Debug.Log("Club Table Created: " + table.Name);
            Debug.Log("Table ID: " + table.Id);

            if (ShowClubTableScreenScript != null)
            {
                ShowClubTableScreenScript.LoadTables().Forget();
            }

            ClubCreateTablePopup.SetActive(false);
            VariantScreen.SetActive(true);
            gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            ShowError(e.Message);

            Debug.LogError(
                "Create Club Table Failed: " + e.Message);
        }

        CreateTable_Button.interactable = true;
    }

    // ========================= VALIDATION =========================

    private bool ValidateInputs(out CreateClubTableRequest request)
    {
        request = null;

        if (string.IsNullOrEmpty(ClubId))
        {
            ShowError(Loc.Get("something_went_wrong"));
            return false;
        }

        if (string.IsNullOrEmpty(Variant_Name))
        {
            ShowError(Loc.Get("select_variant"));
            return false;
        }

        if (MaxPlayerSlider == null ||
            float.IsNaN(MaxPlayerSlider.value) ||
            float.IsInfinity(MaxPlayerSlider.value))
        {
            ShowError(Loc.Get("enter_valid_max_players"));
            return false;
        }

        int maxSeats = Mathf.RoundToInt(MaxPlayerSlider.value);

        int smallBlind = SelectedSmallBlind;
        int bigBlind = SelectedBigBlind;

        if (smallBlind <= 0)
        {
            ShowError(Loc.Get("enter_valid_small_blind"));
            return false;
        }

        if (bigBlind <= smallBlind)
        {
            ShowError(Loc.Get("enter_valid_big_blind"));
            return false;
        }

        if (MinMaxBuySlider == null)
        {
            ShowError(Loc.Get("enter_valid_min_buyin"));
            return false;
        }

        int buyInMin = MinMaxBuySlider.LeftValue;
        int buyInMax = MinMaxBuySlider.RightValue;

        if ((long)buyInMin < (long)bigBlind * 10L)
        {
            ShowError(Loc.Get("enter_valid_min_buyin"));
            return false;
        }

        if ((long)buyInMax > (long)bigBlind * 3000L)
        {
            ShowError(Loc.Get("enter_valid_max_buyin"));
            return false;
        }

        if (buyInMax <= buyInMin)
        {
            ShowError(Loc.Get("max_buyin_gt_min"));
            return false;
        }

        // Action time is now controlled by the slider.
        int ActionTime = DefaultActionTime;

        if (ActionTimeSlider != null)
        {
            ActionTime = Mathf.Clamp(
                Mathf.RoundToInt(ActionTimeSlider.value),
                MinActionTime,
                MaxActionTime);
        }

        if (!int.TryParse(Fee_InputField.text, out int fee))
        {
            ShowError(Loc.Get("enter_valid_fee"));
            return false;
        }

        if (!int.TryParse(Cap_InputField.text, out int cap))
        {
            ShowError(Loc.Get("enter_valid_cap"));
            return false;
        }

        if (ANTE_BB_Slider == null)
        {
            ShowError(Loc.Get("enter_valid_ante"));
            return false;
        }

        int ante = Mathf.Clamp(
            Mathf.RoundToInt(ANTE_BB_Slider.value),
            MinAnte,
            MaxAnte);



        int maxAllowedSeats = GetMaxAllowedPlayers();

        if (maxSeats < MinAllowedPlayers ||
            maxSeats > maxAllowedSeats)
        {
            ShowError(
                maxAllowedSeats == 7
                    ? Loc.Get("plo6_max_players")
                    : Loc.Get("max_players_range"));

            return false;
        }

        if (bigBlind <= smallBlind)
        {
            ShowError(Loc.Get("big_blind_gt_small"));
            return false;
        }

        if (buyInMax <= buyInMin)
        {
            ShowError(Loc.Get("max_buyin_gt_min"));
            return false;
        }

        // Server rule: min buy-in is at least 10 big blinds.
        if (buyInMin < bigBlind * 10)
        {
            ShowError(
                Loc.Get("min_buyin_10bb", ("min", bigBlind * 10)));

            return false;
        }

        if (!int.TryParse(Fee_InputField.text, out int feePercent) ||
            feePercent < 0 || feePercent > 100)
        {
            ShowError(Loc.Get("fee_range"));
            return false;
        }

        if (!int.TryParse(Cap_InputField.text, out int feeCapBBs) ||
            feeCapBBs < 0)
        {
            ShowError(Loc.Get("enter_valid_fee_cap"));
            return false;
        }

        bool bombPotEnabled =
            BombPot_Toggle != null && BombPot_Toggle.isOn;

        int bombPotAmount = 0;
        int bombPotEveryNHands = 0;

        if (bombPotEnabled)
        {
            if (!int.TryParse(
                    BombPotAnte_InputField.text,
                    out bombPotAmount) ||
                bombPotAmount <= 0)
            {
                ShowError(Loc.Get("enter_valid_bomb_pot_amount"));
                return false;
            }

            if (!int.TryParse(
                    BombPotEverynHands_InputField.text,
                    out bombPotEveryNHands) ||
                bombPotEveryNHands <= 0)
            {
                ShowError(Loc.Get("enter_valid_bomb_pot_interval"));
                return false;
            }
        }

        request = new CreateClubTableRequest
        {
            Variant = Variant_Name,

            SmallBlind = smallBlind,
            BigBlind = bigBlind,

            MaxSeats = maxSeats,

            BuyInMin = buyInMin,
            BuyInMax = buyInMax,

            Name = string.IsNullOrWhiteSpace(TableName_InputField.text)
                ? "#NoName#"
                : TableName_InputField.text.Trim(),

            Ante = ante,

            ActionTimeSecs = ActionTime,

            DurationMinutes = GetDurationMinutes(),

            FeePercent = feePercent,
            FeeCapBBs = feeCapBBs,

            JackpotEnabled =
                JackPoot_Toggle != null && JackPoot_Toggle.isOn,

            BombPot = bombPotEnabled,
            BombPotAmount = bombPotAmount,
            BombPotEveryNHands = bombPotEveryNHands,

            RunItTwice =
                RunItTwice_Toggle != null &&
                RunItTwice_Toggle.isOn,

            StraddleEnabled =
                AutoStraddleEnabled_Toggle != null &&
                AutoStraddleEnabled_Toggle.isOn,

            VoluntaryStraddle =
                VoluntaryStraddle_Toggle != null &&
                VoluntaryStraddle_Toggle.isOn
        };

        return true;
    }

    // ========================= GAME DURATION API CONVERSION =========================

    private int? GetDurationMinutes()
    {
        // Slider assigned: selected hours converted into minutes.
        if (GameLenghtSlider != null)
        {
            int selectedHours = Mathf.Clamp(
                Mathf.RoundToInt(GameLenghtSlider.value),
                MinGameLengthHours,
                MaxGameLengthHours);

            int durationMinutes = selectedHours * 60;

            return durationMinutes;
        }

        // Old dropdown fallback remains available.
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
        InformationPrefabScript.Instance.ShowMessage(message);
    }

    private async UniTaskVoid ClearErrorAfterDelay()
    {
        await UniTask.Delay(
            TimeSpan.FromSeconds(5),
            cancellationToken: destroyCancellationToken);

        if (ErrorText != null)
            ErrorText.text = "";
    }
}
