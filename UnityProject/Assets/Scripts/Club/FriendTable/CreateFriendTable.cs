using ClubPoker.Auth;
using ClubPoker.Lobby;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;
using ClubPoker.Networking;

public class CreateFriendTable : MonoBehaviour
{
    public Transform VariantContent;
    public GameObject VariantPrefab;
    public GameObject VariantScreen;
    public GameObject FriendCreateTable_Popup;

    public string Variant_Name;

    [Header("Input Fields")]
    public TMP_InputField Maxplayer_InputField;
    public TMP_InputField SmallBlind_InputField;
    public TMP_InputField BigBlind_InputField;
    public TMP_InputField Min_Amount_InputField;
    public TMP_InputField Max_Amount_InputField;
    public TMP_InputField TableName_InputField;

    [Header("Buttons")]
    public Button FriendCreateTable_Button;
    public Button VariantScreenBack_Button;
    public Button FriendCreateTablePopup_Close_Button;

    [Header("Popup")]
    public GameObject FriendCreateTablePopup;
    public GameObject FriendTableMenu;
    public TextMeshProUGUI ErrorText;
    public TextAsset ClubTableVariantJson;

    [SerializeField] private VariantSO VariantSO;

    private ClubTableVariantResponse clubTableVariantResponse;
    private bool isCreatingTable;

    public GameObject CodeSharePanel;
    public TextMeshProUGUI CodeText;
    public Button ShareButton;

    private string createdTableId;
    private string createdShareCode;
    private string createdTableName;
    public Button ShareCloseButton;

    public Button JoinHost;
    private int createdMinBuyIn;
    private bool isJoiningHost;

    public Slider MaxPlayerSlider;
    public Text MaxPlayerCount;

    public Slider Small_Big_BlindSlider;
    public Text Small_Big_BlindCount;

    public MinMaxBuyRangeSlider MinMaxBuyRangeSlider;
    public Text MinBuyCount;
    public Text MaxBuyCount;

    public Slider GameLengthSlider;
    public Text GameLengthCount;

    public Slider ActionTimeSlider;
    public Text ActionTimeCount;

    private const int DefaultActionTime = 30;
    private const int MinActionTime = 8;
    private const int MaxActionTime = 35;

    private const float DefaultGameLengthHours = 1f;
    private const float MinGameLengthHours = 0.5f;
    private const float MaxGameLengthHours = 6f;

    private int SelectedActionTime = DefaultActionTime;
    private float SelectedGameLengthHours = DefaultGameLengthHours;
    // Same blind and buy-in configuration used by ClubCreateTableScreenScript.
    private readonly int[] BlindSteps =
    {
        1, 2, 3, 4, 5, 10, 15, 20,
        25, 30, 40, 50, 75, 100, 150, 200, 250, 300, 400, 500, 750,
        1000, 1500, 2000, 2500, 3000, 4000, 5000, 7500,
        10000, 15000, 20000, 25000, 30000, 40000, 50000
    };

    private int SelectedSmallBlind = 5;
    private int SelectedBigBlind = 10;
    private const int MinAllowedPlayers = 2;
    private bool isConfiguringBlindSlider;

    private void Start()
    {
        ParseVariantJson();
        GenerateVariants();

        if (FriendCreateTable_Button != null)
        {
            FriendCreateTable_Button.onClick.RemoveListener(CreateTableButtonOnTap);
            FriendCreateTable_Button.onClick.AddListener(CreateTableButtonOnTap);
        }

        if (VariantScreenBack_Button != null)
        {
            VariantScreenBack_Button.onClick.RemoveListener(VariantScreenBack_ButtonOnTap);
            VariantScreenBack_Button.onClick.AddListener(VariantScreenBack_ButtonOnTap);
        }

        if (FriendCreateTablePopup_Close_Button != null)
        {
            FriendCreateTablePopup_Close_Button.onClick.RemoveListener(FriendCreateTablePopup_Close_ButtonOnTap);
            FriendCreateTablePopup_Close_Button.onClick.AddListener(FriendCreateTablePopup_Close_ButtonOnTap);
        }

        if (ShareButton != null)
        {
            ShareButton.onClick.RemoveListener(ShareTableCode);
            ShareButton.onClick.AddListener(ShareTableCode);
        }

        if (JoinHost != null)
        {
            JoinHost.onClick.RemoveListener(JoinHostButtonOnTap);
            JoinHost.onClick.AddListener(JoinHostButtonOnTap);
        }

        if (ShareCloseButton != null)
        {
            ShareCloseButton.onClick.RemoveListener(ShareCloseButtonOnTap);
            ShareCloseButton.onClick.AddListener(ShareCloseButtonOnTap);
        }

        // Slider listeners: same behaviour as ClubCreateTableScreenScript.
        if (MaxPlayerSlider != null)
        {
            MaxPlayerSlider.onValueChanged.RemoveListener(OnMaxPlayerSliderChanged);
            MaxPlayerSlider.onValueChanged.AddListener(OnMaxPlayerSliderChanged);
        }

        if (Small_Big_BlindSlider != null)
        {
            Small_Big_BlindSlider.onValueChanged.RemoveListener(OnBlindSliderChanged);
            Small_Big_BlindSlider.onValueChanged.AddListener(OnBlindSliderChanged);
        }

        if (MinMaxBuyRangeSlider != null)
        {
            MinMaxBuyRangeSlider.OnRangeChanged.RemoveListener(OnBuyRangeChanged);
            MinMaxBuyRangeSlider.OnRangeChanged.AddListener(OnBuyRangeChanged);
        }

        if (ActionTimeSlider != null)
        {
            ActionTimeSlider.onValueChanged.RemoveListener(OnActionTimeSliderChanged);
            ActionTimeSlider.onValueChanged.AddListener(OnActionTimeSliderChanged);
        }

        if (GameLengthSlider != null)
        {
            GameLengthSlider.onValueChanged.RemoveListener(OnGameLengthSliderChanged);
            GameLengthSlider.onValueChanged.AddListener(OnGameLengthSliderChanged);
        }
        SetupInputListeners();
    }

    private void OnEnable()
    {
        SetInputField();
        ClearError();
        if (VariantScreen != null)
            VariantScreen.SetActive(true);
    }

    private void OnDestroy()
    {
        if (FriendCreateTable_Button != null) FriendCreateTable_Button.onClick.RemoveListener(CreateTableButtonOnTap);
        if (VariantScreenBack_Button != null) VariantScreenBack_Button.onClick.RemoveListener(VariantScreenBack_ButtonOnTap);
        if (FriendCreateTablePopup_Close_Button != null) FriendCreateTablePopup_Close_Button.onClick.RemoveListener(FriendCreateTablePopup_Close_ButtonOnTap);
        if (ShareButton != null) ShareButton.onClick.RemoveListener(ShareTableCode);
        if (ShareCloseButton != null) ShareCloseButton.onClick.RemoveListener(ShareCloseButtonOnTap);
        if (JoinHost != null) JoinHost.onClick.RemoveListener(JoinHostButtonOnTap);

        if (MaxPlayerSlider != null) MaxPlayerSlider.onValueChanged.RemoveListener(OnMaxPlayerSliderChanged);
        if (Small_Big_BlindSlider != null) Small_Big_BlindSlider.onValueChanged.RemoveListener(OnBlindSliderChanged);
        if (MinMaxBuyRangeSlider != null) MinMaxBuyRangeSlider.OnRangeChanged.RemoveListener(OnBuyRangeChanged);

        if (ActionTimeSlider != null)
            ActionTimeSlider.onValueChanged.RemoveListener(OnActionTimeSliderChanged);

        if (GameLengthSlider != null)
            GameLengthSlider.onValueChanged.RemoveListener(OnGameLengthSliderChanged);
    }

    private void ParseVariantJson()
    {
        if (ClubTableVariantJson == null)
        {
            Debug.LogError("ClubTableVariantJson Missing");
            return;
        }

        try
        {
            clubTableVariantResponse = JsonConvert.DeserializeObject<ClubTableVariantResponse>(ClubTableVariantJson.text);
        }
        catch (Exception e)
        {
            Debug.LogError("Club variant JSON parse failed: " + e.Message);
        }
    }

    private void SetInputField()
    {
        ConfigureMaxPlayerSlider(GetDefaultMaxPlayerCount());
        ConfigureBlindSlider(5);

        SetupActionTimeSlider();
        SetupGameLengthSlider();

        if (TableName_InputField != null)
            TableName_InputField.text = "";
    }
    private void SetupActionTimeSlider()
    {
        if (ActionTimeSlider == null)
            return;

        ActionTimeSlider.wholeNumbers = true;
        ActionTimeSlider.minValue = MinActionTime;
        ActionTimeSlider.maxValue = MaxActionTime;

        ActionTimeSlider.SetValueWithoutNotify(DefaultActionTime);

        SelectedActionTime = DefaultActionTime;

        UpdateActionTimeUI(DefaultActionTime);
    }

    private void OnActionTimeSliderChanged(float value)
    {
        int seconds = Mathf.Clamp(
            Mathf.RoundToInt(value),
            MinActionTime,
            MaxActionTime
        );

        if (ActionTimeSlider != null)
            ActionTimeSlider.SetValueWithoutNotify(seconds);

        SelectedActionTime = seconds;

        UpdateActionTimeUI(seconds);
        ClearError();
    }

    private void UpdateActionTimeUI(int seconds)
    {
        if (ActionTimeCount != null)
            ActionTimeCount.text = seconds + " sec";
    }

    private void SetupGameLengthSlider()
    {
        if (GameLengthSlider == null)
            return;

        GameLengthSlider.wholeNumbers = false;
        GameLengthSlider.minValue = MinGameLengthHours;
        GameLengthSlider.maxValue = MaxGameLengthHours;

        GameLengthSlider.SetValueWithoutNotify(DefaultGameLengthHours);

        SelectedGameLengthHours = DefaultGameLengthHours;

        UpdateGameLengthUI(DefaultGameLengthHours);
    }

    private void OnGameLengthSliderChanged(float value)
    {
        float hours = value < 1f ? 0.5f : Mathf.Round(value);

        hours = Mathf.Clamp(
            hours,
            MinGameLengthHours,
            MaxGameLengthHours
        );

        if (GameLengthSlider != null)
            GameLengthSlider.SetValueWithoutNotify(hours);

        SelectedGameLengthHours = hours;

        UpdateGameLengthUI(hours);
        ClearError();
    }

    private void UpdateGameLengthUI(float hours)
    {
        if (GameLengthCount == null)
            return;

        if (hours == 0.5f)
            GameLengthCount.text = "0.5 Hour";
        else
            GameLengthCount.text = hours.ToString("0") + " Hours";
    }
    private int GetMaxAllowedPlayers()
    {
        return string.Equals(Variant_Name, "omaha_six", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Variant_Name, "plo6", StringComparison.OrdinalIgnoreCase)
            ? 7 : 9;
    }

    private int GetDefaultMaxPlayerCount()
    {
        return GetMaxAllowedPlayers() == 7 ? 7 : 4;
    }

    private void ConfigureMaxPlayerSlider(int selectedCount)
    {
        int maxAllowed = GetMaxAllowedPlayers();
        int count = Mathf.Clamp(selectedCount, MinAllowedPlayers, maxAllowed);

        if (MaxPlayerSlider != null)
        {
            MaxPlayerSlider.wholeNumbers = true;
            MaxPlayerSlider.minValue = MinAllowedPlayers;
            MaxPlayerSlider.maxValue = maxAllowed;
            MaxPlayerSlider.SetValueWithoutNotify(count);
        }

        UpdateMaxPlayerCount(count);
        if (Maxplayer_InputField != null)
            Maxplayer_InputField.SetTextWithoutNotify(count.ToString());
    }

    private void OnMaxPlayerSliderChanged(float value)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(value), MinAllowedPlayers, GetMaxAllowedPlayers());
        if (MaxPlayerSlider != null)
            MaxPlayerSlider.SetValueWithoutNotify(count);

        UpdateMaxPlayerCount(count);
        if (Maxplayer_InputField != null)
            Maxplayer_InputField.SetTextWithoutNotify(count.ToString());

        ClearError();
    }

    private void UpdateMaxPlayerCount(int count)
    {
        if (MaxPlayerCount != null)
            MaxPlayerCount.text = "(" + count.ToString() + ")";
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

    private void ConfigureBlindSlider(int defaultSmallBlind)
    {
        if (Small_Big_BlindSlider == null)
        {
            Debug.LogError("[Friend Create Table] Assign Small_Big_BlindSlider.");
            SelectedSmallBlind = defaultSmallBlind;
            SelectedBigBlind = defaultSmallBlind * 2;
            UpdateBlindValues(FindBlindIndex(defaultSmallBlind));
            return;
        }

        isConfiguringBlindSlider = true;
        Small_Big_BlindSlider.wholeNumbers = true;
        Small_Big_BlindSlider.minValue = 0;
        Small_Big_BlindSlider.maxValue = BlindSteps.Length - 1;

        int index = FindBlindIndex(defaultSmallBlind);
        Small_Big_BlindSlider.SetValueWithoutNotify(index);
        isConfiguringBlindSlider = false;

        UpdateBlindValues(index);
    }

    private void OnBlindSliderChanged(float value)
    {
        if (isConfiguringBlindSlider)
            return;

        int index = Mathf.Clamp(Mathf.RoundToInt(value), 0, BlindSteps.Length - 1);
        if (Small_Big_BlindSlider != null)
            Small_Big_BlindSlider.SetValueWithoutNotify(index);

        UpdateBlindValues(index);
        ClearError();
    }

    private void UpdateBlindValues(int index)
    {
        index = Mathf.Clamp(index, 0, BlindSteps.Length - 1);
        SelectedSmallBlind = BlindSteps[index];
        SelectedBigBlind = SelectedSmallBlind * 2;

        if (Small_Big_BlindCount != null)
        {
            Small_Big_BlindCount.text =
                MinMaxBuyRangeSlider != null
                    ? MinMaxBuyRangeSlider.FormatValue(SelectedSmallBlind) + "/" + MinMaxBuyRangeSlider.FormatValue(SelectedBigBlind)
                    : SelectedSmallBlind + "/" + SelectedBigBlind;
        }

        if (SmallBlind_InputField != null)
            SmallBlind_InputField.SetTextWithoutNotify(SelectedSmallBlind.ToString());
        if (BigBlind_InputField != null)
            BigBlind_InputField.SetTextWithoutNotify(SelectedBigBlind.ToString());

        ConfigureBuySlider();
    }

    private void ConfigureBuySlider()
    {
        if (MinMaxBuyRangeSlider == null)
        {
            Debug.LogError("[Friend Create Table] Assign MinMaxBuyRangeSlider.");
            return;
        }

        long minLong = (long)SelectedBigBlind * 10L;
        long maxLong = (long)SelectedBigBlind * 3000L;
        int minBuy = (int)Math.Min(int.MaxValue, minLong);
        int maxBuy = (int)Math.Min(int.MaxValue, maxLong);

        int[] values = GenerateBuyValues(minBuy, maxBuy);
        int initialMin = minBuy;
        int initialMax = (int)Math.Min(maxBuy, (long)SelectedBigBlind * 20L);
        if (initialMax <= initialMin)
            initialMax = maxBuy;

        MinMaxBuyRangeSlider.ConfigureValues(values, initialMin, initialMax, false);
        UpdateBuyLabels(MinMaxBuyRangeSlider.LeftValue, MinMaxBuyRangeSlider.RightValue);
    }

    private int[] GenerateBuyValues(int minValue, int maxValue)
    {
        SortedSet<int> values = new SortedSet<int> { minValue, maxValue };
        long magnitude = 1;
        long[] steps = { 1L, 2L, 3L, 5L };

        while (magnitude <= maxValue)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                long candidate = steps[i] * magnitude;
                if (candidate >= minValue && candidate <= maxValue && candidate <= int.MaxValue)
                    values.Add((int)candidate);
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
        ClearError();
    }

    private void UpdateBuyLabels(int minBuy, int maxBuy)
    {
        if (MinBuyCount != null)
            MinBuyCount.text = MinMaxBuyRangeSlider.FormatValue(minBuy);
        if (MaxBuyCount != null)
            MaxBuyCount.text = MinMaxBuyRangeSlider.FormatValue(maxBuy);

        if (Min_Amount_InputField != null)
            Min_Amount_InputField.SetTextWithoutNotify(minBuy.ToString());
        if (Max_Amount_InputField != null)
            Max_Amount_InputField.SetTextWithoutNotify(maxBuy.ToString());
    }

    private void SetupInputListeners()
    {
        if (Maxplayer_InputField != null)
        {
            Maxplayer_InputField.onEndEdit.RemoveAllListeners();
            Maxplayer_InputField.onEndEdit.AddListener(ValidateMaxPlayers);
            Maxplayer_InputField.onValueChanged.AddListener(_ => ClearError());
        }

        if (SmallBlind_InputField != null) SmallBlind_InputField.onValueChanged.AddListener(_ => ClearError());
        if (BigBlind_InputField != null) BigBlind_InputField.onValueChanged.AddListener(_ => ClearError());
        if (Min_Amount_InputField != null) Min_Amount_InputField.onValueChanged.AddListener(_ => ClearError());
        if (Max_Amount_InputField != null) Max_Amount_InputField.onValueChanged.AddListener(_ => ClearError());
        if (TableName_InputField != null) TableName_InputField.onValueChanged.AddListener(_ => ClearError());
    }

    private void ValidateMaxPlayers(string value)
    {
        int maxAllowed = GetMaxAllowedPlayers();

        if (!int.TryParse(value, out int players))
            return;

        int clampedPlayers = Mathf.Clamp(players, MinAllowedPlayers, maxAllowed);
        ConfigureMaxPlayerSlider(clampedPlayers);

        if (players < MinAllowedPlayers)
            ShowError(Loc.Get("min_players_two"));
        else if (players > maxAllowed)
            ShowError(Variant_Name == "omaha_six" ? Loc.Get("plo6_max_players") : Loc.Get("max_players_range"));
    }

    private void VariantScreenBack_ButtonOnTap()
    {
        gameObject.SetActive(false);
        FriendTableMenu.SetActive(true);

    }

    void ShareCloseButtonOnTap()
    {

        CodeSharePanel.SetActive(false);
        FriendTableMenu.SetActive(true);
        gameObject.SetActive(false);
    }

    private void FriendCreateTablePopup_Close_ButtonOnTap()
    {
        if (FriendCreateTablePopup != null) FriendCreateTablePopup.SetActive(false);
        if (FriendCreateTable_Popup != null) FriendCreateTable_Popup.SetActive(false);
        if (VariantScreen != null) VariantScreen.SetActive(true);
    }

    private void GenerateVariants()
    {
        ClearOldVariants();

        if (clubTableVariantResponse?.ClubTableVariants == null)
        {
            Debug.LogError("Club Variant Data Missing");
            return;
        }

        foreach (ClubTableVariantData variant in clubTableVariantResponse.ClubTableVariants)
        {
            GameObject obj = Instantiate(VariantPrefab, VariantContent);
            Club_VariantPrefabScreen prefab = obj.GetComponent<Club_VariantPrefabScreen>();
            Sprite sprite = VariantSO != null ? VariantSO.GetVariantSprite(variant.VariantName) : null;

            if (prefab != null) prefab.SetData(variant, sprite, OnVariantSelected);
        }
    }

    private void ClearOldVariants()
    {
        if (VariantContent == null) return;

        for (int i = VariantContent.childCount - 1; i >= 0; i--)
            Destroy(VariantContent.GetChild(i).gameObject);
    }

    private void OnVariantSelected(ClubTableVariantData variantData)
    {
        if (variantData == null) return;

        Variant_Name = variantData.VariantKey;
        SetInputField();
        ClearError();

        ConfigureMaxPlayerSlider(GetDefaultMaxPlayerCount());

        if (VariantScreen != null) VariantScreen.SetActive(false);
        if (FriendCreateTable_Popup != null) FriendCreateTable_Popup.SetActive(true);

        Debug.Log("Selected Variant: " + variantData.VariantName);
    }

    private async void CreateTableButtonOnTap()
    {
        if (isCreatingTable) return;

        if (!ValidateInputs(out CreateTableRequest request)) return;

        if (AuthManager.Instance == null)
        {
            ShowError(Loc.Get("something_went_wrong"));
            return;
        }

        isCreatingTable = true;
        SetCreateButtonInteractable(false);
        ClearError();

        try
        {
            CreateTableResponse response = await AuthManager.Instance.CreateTableAsync(request);

            if (response == null || string.IsNullOrEmpty(response.TableId))
            {
                ShowError(Loc.Get("table_create_failed"));
                return;
            }

            createdTableId = response.TableId;
            createdShareCode = response.ShareCode;
            createdTableName = request.Name;
            createdMinBuyIn = request.MinBuyIn;
            Debug.Log($"Friend table created | Table ID: {createdTableId} | Share Code: {createdShareCode}");

            if (FriendCreateTable_Popup != null) FriendCreateTable_Popup.SetActive(false);
            if (CodeSharePanel != null) CodeSharePanel.SetActive(true);
            if (CodeText != null) CodeText.text = createdShareCode;
        }
        catch (Exception e)
        {
            ShowError(PlayerError.Text(e, Loc.Get("table_create_failed")));
            Debug.LogError("Create friend table failed: " + e);
        }
        finally
        {
            isCreatingTable = false;
            SetCreateButtonInteractable(true);
        }
    }
    private void ShareTableCode()
    {
        if (string.IsNullOrEmpty(createdShareCode))
        {
            ShowError(Loc.Get("share_code_unavailable"));
            return;
        }

        string message =
            Loc.Get("share_table_message", ("table", createdTableName), ("code", createdShareCode));

        new NativeShare()
            .SetSubject(Loc.Get("share_table_subject"))
            .SetText(message)
            .Share();
    }
    private bool ValidateInputs(out CreateTableRequest request)
    {
        request = null;

        if (string.IsNullOrEmpty(Variant_Name))
        {
            ShowError(Loc.Get("select_variant"));
            return false;
        }

        int maxPlayers;
        if (MaxPlayerSlider != null)
            maxPlayers = Mathf.RoundToInt(MaxPlayerSlider.value);
        else if (!TryGetValue(Maxplayer_InputField, Loc.Get("enter_valid_max_players"), out maxPlayers))
            return false;

        int smallBlind = SelectedSmallBlind;
        int bigBlind = SelectedBigBlind;

        if (Small_Big_BlindSlider == null)
        {
            if (!TryGetValue(SmallBlind_InputField, Loc.Get("enter_valid_small_blind"), out smallBlind)) return false;
            if (!TryGetValue(BigBlind_InputField, Loc.Get("enter_valid_big_blind"), out bigBlind)) return false;
        }

        int minBuyIn;
        int maxBuyIn;
        if (MinMaxBuyRangeSlider != null)
        {
            minBuyIn = MinMaxBuyRangeSlider.LeftValue;
            maxBuyIn = MinMaxBuyRangeSlider.RightValue;
        }
        else
        {
            if (!TryGetValue(Min_Amount_InputField, Loc.Get("enter_valid_min_buyin"), out minBuyIn)) return false;
            if (!TryGetValue(Max_Amount_InputField, Loc.Get("enter_valid_max_buyin"), out maxBuyIn)) return false;
        }

        int maxAllowed = Variant_Name == "omaha_six" ? 7 : 9;

        if (maxPlayers < 2 || maxPlayers > maxAllowed)
        {
            ShowError(Variant_Name == "omaha_six" ? Loc.Get("players_range_7") : Loc.Get("max_players_range"));
            return false;
        }

        if (smallBlind <= 0)
        {
            ShowError(Loc.Get("small_blind_gt_zero"));
            return false;
        }

        if (bigBlind <= smallBlind)
        {
            ShowError(Loc.Get("big_blind_gt_small"));
            return false;
        }

        if (minBuyIn <= 0)
        {
            ShowError(Loc.Get("min_buyin_gt_zero"));
            return false;
        }

        // Server rule: min buy-in is at least 10 big blinds.
        if (minBuyIn < bigBlind * 10)
        {
            ShowError(Loc.Get("min_buyin_10bb", ("min", bigBlind * 10)));
            return false;
        }

        if (maxBuyIn < minBuyIn)
        {
            ShowError(Loc.Get("max_buyin_gt_min"));
            return false;
        }

        // Same upper buy-in limit as ClubCreateTableScreenScript: 3000 BB.
        if ((long)maxBuyIn > (long)bigBlind * 3000L)
        {
            ShowError(Loc.Get("enter_valid_max_buyin"));
            return false;
        }

        string tableName = TableName_InputField != null ? TableName_InputField.text.Trim() : "";

        if (string.IsNullOrEmpty(tableName))
            tableName = $"Friend Table ({GetVariantShortName(Variant_Name)} {smallBlind}/{bigBlind})";

        request = new CreateTableRequest
        {
            Variant = Variant_Name,
            MaxPlayers = maxPlayers,
            SmallBlind = smallBlind,
            BigBlind = bigBlind,
            MinBuyIn = minBuyIn,
            MaxBuyIn = maxBuyIn,
            Name = tableName,
            ActionTimeSecs = SelectedActionTime,
            GameLength = Mathf.RoundToInt(SelectedGameLengthHours * 60f)
        };

        Debug.Log("Create friend table request:\n" + JsonConvert.SerializeObject(request, Formatting.Indented));
        return true;
    }

    private bool TryGetValue(TMP_InputField inputField, string error, out int value)
    {
        value = 0;

        if (inputField != null && int.TryParse(inputField.text.Trim(), out value))
            return true;

        ShowError(error);
        return false;
    }

    private string GetVariantShortName(string variant)
    {
        switch (variant)
        {
            case "texas_holdem": return "NLH";
            case "omaha":
            case "plo4": return "PLO4";
            case "omaha_six":
            case "plo6": return "PLO6";
            default: return variant;
        }
    }

    private void SetCreateButtonInteractable(bool interactable)
    {
        if (FriendCreateTable_Button != null) FriendCreateTable_Button.interactable = interactable;
    }

    private void ShowError(string message)
    {
        if (ErrorText != null) ErrorText.text = message;
        Debug.LogWarning(message);
        InformationPrefabScript.Instance.ShowMessage(message);
        ClearErrorAfterDelay().Forget();
    }

    private void ClearError()
    {
        if (ErrorText != null) ErrorText.text = "";
    }

    private async UniTaskVoid ClearErrorAfterDelay()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: destroyCancellationToken);
        ClearError();
    }


    private void JoinHostButtonOnTap()
    {
        JoinHostTableAsync().Forget();
    }

    private async UniTaskVoid JoinHostTableAsync()
    {
        if (isJoiningHost)
            return;

        if (string.IsNullOrEmpty(createdTableId))
        {
            ShowError(Loc.Get("table_not_created"));
            return;
        }

        if (AuthManager.Instance == null)
        {
            ShowError(Loc.Get("something_went_wrong"));
            return;
        }

        if (ClubPoker.Game.TableJoinHandler.Instance == null)
        {
            ShowError(Loc.Get("something_went_wrong"));
            return;
        }

        isJoiningHost = true;

        if (JoinHost != null)
            JoinHost.interactable = false;

        ClearError();

        try
        {
            int buyInAmount = createdMinBuyIn;
            TableData tableData = await AuthManager.Instance.GetTableDetailAsync(createdTableId);
            if (buyInAmount <= 0)
            {


                if (tableData == null)
                {
                    ShowError(Loc.Get("table_details_load_failed"));
                    return;
                }

                buyInAmount = tableData.MinBuyIn;
            }

            // Already seated (G016) counts as success — rejoin that seat.
            await AuthManager.Instance.BuyInAndJoinAsync(createdTableId, buyInAmount);

            if (CodeSharePanel != null)
                CodeSharePanel.SetActive(false);

            // Friend table host — created from the main menu, so lobby origin.
            TableContext.EnterFromLobby(createdTableId, tableData);
            ClubPoker.Game.TableJoinHandler.Instance.JoinTable(createdTableId);

            Debug.Log(
                $"Host joined friend table | " +
                $"Table ID: {createdTableId} | " +
                $"Buy-In: {buyInAmount}"
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Host join failed: " + e);
            ShowError(PlayerError.Text(e, Loc.Get("join_table_failed")));
        }
        finally
        {
            isJoiningHost = false;

            if (JoinHost != null)
                JoinHost.interactable = true;
        }
    }
}