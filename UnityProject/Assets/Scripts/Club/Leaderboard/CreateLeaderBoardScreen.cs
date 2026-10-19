using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LeaderboardVariantOption
{
    public string Code;
    public string Label;
    public bool SelectedByDefault = false;

    public LeaderboardVariantOption(string code, string label)
    {
        Code = code;
        Label = label;
    }
}

public class CreateLeaderBoardScreen : MonoBehaviour
{
    public Button BackButton;
    public Button InformationButton;

    public InputField Leaderboard_InputField;
    public InputField Description_Inputfield;

    public GameObject Variantprefab;
    public Transform VariantContent;

    public Text BB_AnteRange_Text;
    public RangeSliderUI BB_AnteSlider;

    public GameObject BuyInRangePanel;
    public Text BuyInRange_Text;
    public RangeSliderUI BuyInRangeSlider;

    public Button StartDateTime_Button;
    public Button EndDateTime_Button;

    public Button DoubleScoreOn_offButton;
    public InputField DoubleScoreHourInputField;
    public RectTransform DoubleScorePanel;
    public GameObject DoubleScoreContentData;

    public Button AutoRewardOn_offButton;

    public GameObject AutoRewardPanel;
    public GameObject AutoRewardRankPrefab;
    public Transform AutoRewardRankContent;

    public Text ClubChip;
    public Text TotalRewardChips;
    public Button AutoRewardIncrese_Button;

    [Header("Create")]
    public Button CreateButton;
    public Text MessageText;
    public GameObject InformationPanel;

    [Header("Date Picker")]
    public AdminPosterDateSelectScript DatePicker;
    public Text StartDateTimeText;
    public Text EndDateTimeText;

    [Header("Switch Sprites")]
    public Sprite OnSprite;
    public Sprite OffSprite;

    public Button SaveTemleteButton;

    [Range(0, 23)]
    public int DefaultDoubleScoreHour = 14;

    [Header("Default Prizes")]
    public List<int> DefaultPrizeAmounts =
        new List<int> { 100, 100, 100, 50, 20 };

    [Header("Available Variants")]
    public List<LeaderboardVariantOption> Variants =
        new List<LeaderboardVariantOption>
        {
            new LeaderboardVariantOption("NLH", "NLH"),
            new LeaderboardVariantOption("PLO", "PLO"),
            new LeaderboardVariantOption("PLO5", "PLO5"),
            new LeaderboardVariantOption("PLO6", "PLO6"),
            new LeaderboardVariantOption("AOF", "AOF")
        };

    public CreatedClubLeaderboardData CreatedLeaderboard
    {
        get;
        private set;
    }

    private readonly List<LeaderboardVariantPrefab> variantRows =
        new List<LeaderboardVariantPrefab>();

    private readonly List<RankRewardPrefab> rewardRows =
        new List<RankRewardPrefab>();

    private string clubId;
    private string leaderboardType;

    private DateTime? startDate;
    private DateTime? endDate;

    private bool initialized;
    private bool submitting;
    private bool doubleScores;
    private bool autoReward;

    private int viewVersion;
    private int layoutVersion;

    private int initialBBMin;
    private int initialBBMax;
    private int initialBuyInMin;
    private int initialBuyInMax;

    private void Awake()
    {
        // Capture the Inspector ranges before the user can change them.
        if (BB_AnteSlider != null)
        {
            initialBBMin = BB_AnteSlider.LeftValue;
            initialBBMax = BB_AnteSlider.RightValue;
        }

        if (BuyInRangeSlider != null)
        {
            initialBuyInMin = BuyInRangeSlider.LeftValue;
            initialBuyInMax = BuyInRangeSlider.RightValue;
        }

        if (BackButton != null)
            BackButton.onClick.AddListener(BackButtonOnTap);

        if (InformationButton != null)
            InformationButton.onClick.AddListener(InformationButtonOnTap);

        if (StartDateTime_Button != null)
            StartDateTime_Button.onClick.AddListener(StartDateTimeOnTap);

        if (EndDateTime_Button != null)
            EndDateTime_Button.onClick.AddListener(EndDateTimeOnTap);

        if (DoubleScoreOn_offButton != null)
            DoubleScoreOn_offButton.onClick.AddListener(DoubleScoreOnTap);

        if (AutoRewardOn_offButton != null)
            AutoRewardOn_offButton.onClick.AddListener(AutoRewardOnTap);

        if (AutoRewardIncrese_Button != null)
            AutoRewardIncrese_Button.onClick.AddListener(AddRewardOnTap);

        if (CreateButton != null)
            CreateButton.onClick.AddListener(CreateButtonOnTap);

        if (SaveTemleteButton != null)
            SaveTemleteButton.onClick.AddListener(SaveTemleteButtonOnTap);

        if (BB_AnteSlider != null)
            BB_AnteSlider.OnRangeChanged.AddListener(OnBBRangeChanged);

        if (BuyInRangeSlider != null)
            BuyInRangeSlider.OnRangeChanged.AddListener(OnBuyInRangeChanged);
    }

    private void OnEnable()
    {
        clubId = ClubContext.ClubId;

        leaderboardType = LeaderboardManager.Instance != null
            ? LeaderboardManager.Instance.Type
            : "";

        SetMessage("");

        try
        {
            EnsureInitialized();
            ResetForm();
            RefreshSwitches();
            RefreshDates();
            RefreshTotal();
            RefreshRangeLabels();

            RefreshScreenAsync(++viewVersion, clubId).Forget();
        }
        catch (Exception e)
        {
            SetMessage(e.Message);
            Debug.LogError("[Leaderboard] Setup failed: " + e.Message);
        }
    }

    private void OnDisable()
    {
        ++viewVersion;
        ++layoutVersion;
    }

    private void ResetForm()
    {
        if (Leaderboard_InputField != null)
            Leaderboard_InputField.SetTextWithoutNotify("");

        if (Description_Inputfield != null)
            Description_Inputfield.SetTextWithoutNotify("");

        startDate = null;
        endDate = null;
        CreatedLeaderboard = null;
        doubleScores = false;
        autoReward = false;

        if (DoubleScoreHourInputField != null)
            DoubleScoreHourInputField.SetTextWithoutNotify("14");

        if (BB_AnteSlider != null)
            BB_AnteSlider.SetRange(initialBBMin, initialBBMax, false);

        if (BuyInRangeSlider != null)
            BuyInRangeSlider.SetRange(initialBuyInMin, initialBuyInMax, false);

        // Restore the original variant selections without firing toggle events.
        for (int i = 0; i < variantRows.Count; i++)
        {
            var row = variantRows[i];
            if (row == null)
                continue;

            var option = Variants[i];
            row.SetData(
                option.Code.Trim(),
                string.IsNullOrWhiteSpace(option.Label)
                    ? option.Code.Trim()
                    : option.Label,
                option.SelectedByDefault);
        }

        // Remove added ranks and restore the original five reward rows.
        for (int i = rewardRows.Count - 1; i >= 5; i--)
        {
            var row = rewardRows[i];
            rewardRows.RemoveAt(i);
            if (row == null)
                continue;
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        while (rewardRows.Count < 5)
            AddRewardRow(0);

        for (int i = 0; i < rewardRows.Count; i++)
        {
            decimal amount = DefaultPrizeAmounts != null &&
                             i < DefaultPrizeAmounts.Count
                ? Math.Max(0, DefaultPrizeAmounts[i])
                : 0;

            rewardRows[i].SetData(
                i + 1, amount, DeleteRewardRow, OnPrizeChanged);
        }

        if (InformationPanel != null)
            InformationPanel.SetActive(false);

        if (DatePicker != null && DatePicker.gameObject.activeSelf)
            DatePicker.gameObject.SetActive(false);

        SetText(ClubChip, "—");
        SetText(MessageText, "");

        // Preserve an in-flight request lock if the screen was hidden externally.
        SetBusy(submitting);
        RefreshDates();
        RefreshTotal();
        RefreshRangeLabels();
        RebuildLayout();
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        if (VariantContent == null ||
            Variantprefab == null ||
            Variantprefab.GetComponent<LeaderboardVariantPrefab>() == null)
        {
            throw new InvalidOperationException(
                "Assign the variant content and prefab component.");
        }

        if (AutoRewardRankContent == null ||
            AutoRewardRankPrefab == null ||
            AutoRewardRankPrefab.GetComponent<RankRewardPrefab>() == null)
        {
            throw new InvalidOperationException(
                "Assign the reward content and prefab component.");
        }

        if (BB_AnteSlider == null || BuyInRangeSlider == null)
            throw new InvalidOperationException(
                "Assign both range sliders.");

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Variants == null || Variants.Count == 0)
            throw new InvalidOperationException(
                "Add at least one variant to the Inspector list.");

        // Validate before creating any rows.
        foreach (var option in Variants)
        {
            if (option == null ||
                string.IsNullOrWhiteSpace(option.Code) ||
                !codes.Add(option.Code.Trim()))
            {
                throw new InvalidOperationException(
                    "Variant codes must be non-empty and unique.");
            }
        }

        foreach (var option in Variants)
        {
            GameObject instance = Instantiate(Variantprefab, VariantContent);

            var row = instance.GetComponent<LeaderboardVariantPrefab>();

            row.SetData(
                option.Code.Trim(),
                string.IsNullOrWhiteSpace(option.Label)
                    ? option.Code.Trim()
                    : option.Label,
                option.SelectedByDefault);

            variantRows.Add(row);
            instance.SetActive(true);
        }

        // Always create five initial rank rows.
        for (int i = 0; i < 5; i++)
        {
            decimal amount =
                DefaultPrizeAmounts != null &&
                i < DefaultPrizeAmounts.Count
                    ? Math.Max(0, DefaultPrizeAmounts[i])
                    : 0;

            AddRewardRow(amount);
        }


        if (DoubleScoreHourInputField != null)
        {
            DoubleScoreHourInputField.contentType =
                InputField.ContentType.IntegerNumber;

            DoubleScoreHourInputField.characterLimit = 2;
            DoubleScoreHourInputField.SetTextWithoutNotify("14");
        }

        initialized = true;
    }

    private async UniTask RefreshScreenAsync(int version, string selectedClubId)
    {
        try
        {
            // Let the existing range sliders complete Start first.
            await UniTask.NextFrame();

            if (!IsCurrent(version))
                return;

            RefreshRangeLabels();
            RebuildLayout();

            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var response =
                await ClubManager.Instance.GetClubDetailAsync(selectedClubId);

            if (!IsCurrent(version))
                return;

            if (response == null || response.Club == null)
                throw new InvalidOperationException(
                    "Club detail response was empty.");

            SetText(
                ClubChip,
                response.Club.ChipPool.ToString(
                    "#,0.##", CultureInfo.InvariantCulture));
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
            {
                SetText(ClubChip, "—");
                Debug.LogError(
                    "[Leaderboard] Club balance load failed: " + e.Message);
            }
        }
        finally
        {
            if (IsCurrent(version))
                RebuildLayout();
        }
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private void BackButtonOnTap()
    {
        if (!submitting)
            gameObject.SetActive(false);
    }

    private void InformationButtonOnTap()
    {
        if (!submitting && InformationPanel != null)
            InformationPanel.SetActive(true);
    }

    private void StartDateTimeOnTap()
    {
        if (submitting)
            return;

        if (DatePicker == null)
        {
            SetMessage("Assign the date picker.");
            return;
        }

        DatePicker.Open(OnStartDateSelected);
    }

    private void EndDateTimeOnTap()
    {
        if (submitting)
            return;

        if (DatePicker == null)
        {
            SetMessage("Assign the date picker.");
            return;
        }

        DatePicker.Open(OnEndDateSelected);
    }

    private void OnStartDateSelected(DateTime value)
    {
        if (!isActiveAndEnabled || submitting)
            return;

        startDate = value;
        RefreshDates();
    }

    private void OnEndDateSelected(DateTime value)
    {
        if (!isActiveAndEnabled || submitting)
            return;

        endDate = value;
        RefreshDates();
    }

    private void RefreshDates()
    {
        SetText(
            StartDateTimeText,
            startDate.HasValue
                ? startDate.Value.ToString(
                    "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : "Select start date");

        SetText(
            EndDateTimeText,
            endDate.HasValue
                ? endDate.Value.ToString(
                    "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : "Select end date");
    }

    private void DoubleScoreOnTap()
    {
        if (submitting || !IsScoreType())
            return;

        doubleScores = !doubleScores;
        RefreshSwitches();
    }

    private void AutoRewardOnTap()
    {
        if (submitting)
            return;

        autoReward = !autoReward;
        RefreshSwitches();
        RefreshTotal();
        RebuildLayout();
    }

    private bool IsScoreType()
    {
        return string.Equals(
            leaderboardType, "SCORE", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshSwitches()
    {
        bool scoreType = IsScoreType();
        bool enabledDoubleScore = scoreType && doubleScores;

        SetSwitchSprite(DoubleScoreOn_offButton, enabledDoubleScore);
        SetSwitchSprite(AutoRewardOn_offButton, autoReward);

        if (AutoRewardPanel != null)
            AutoRewardPanel.SetActive(autoReward);

        if (DoubleScoreOn_offButton != null)
            DoubleScoreOn_offButton.interactable = scoreType && !submitting;

        if (DoubleScoreHourInputField != null)
            DoubleScoreHourInputField.interactable =
                enabledDoubleScore && !submitting;

        if (DoubleScoreContentData != null)
            DoubleScoreContentData.SetActive(enabledDoubleScore);

        if (DoubleScorePanel != null)
        {
            DoubleScorePanel.gameObject.SetActive(scoreType);
            if (BuyInRangePanel != null)
                BuyInRangePanel.SetActive(scoreType);

            float height = enabledDoubleScore ? 410f : 136f;

            // Prevent automatic vertical sizing from overriding the fixed height.
            var fitter = DoubleScorePanel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Supply the height to any parent layout group as well.
            var element = DoubleScorePanel.GetComponent<LayoutElement>();
            if (element == null)
                element = DoubleScorePanel.gameObject.AddComponent<LayoutElement>();

            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;

            DoubleScorePanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, height);
        }

        RebuildLayout();
    }

    private int GetDoubleScoreTime()
    {
        if (!IsScoreType() || !doubleScores)
            return 14;

        string input = DoubleScoreHourInputField != null
            ? DoubleScoreHourInputField.text.Trim()
            : "";

        if (!int.TryParse(
                input,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int hour) ||
            hour < 1 || hour > 23)
        {
            throw new InvalidOperationException(
                "Enter a double-score hour between 1 and 23.");
        }

        return hour;
    }

    private void SetSwitchSprite(Button button, bool value)
    {
        Sprite sprite = value ? OnSprite : OffSprite;

        if (button == null || button.image == null || sprite == null)
            return;

        button.image.sprite = sprite;

        if (button.transition == Selectable.Transition.SpriteSwap)
        {
            SpriteState state = button.spriteState;
            state.highlightedSprite = sprite;
            state.pressedSprite = sprite;
            state.selectedSprite = sprite;
            state.disabledSprite = sprite;
            button.spriteState = state;
        }
    }

    private void AddRewardOnTap()
    {
        if (submitting)
            return;

        AddRewardRow(0);
        RefreshTotal();
        RebuildLayout();
    }

    private void AddRewardRow(decimal amount)
    {
        GameObject instance =
            Instantiate(AutoRewardRankPrefab, AutoRewardRankContent);

        var row = instance.GetComponent<RankRewardPrefab>();

        row.SetData(
            rewardRows.Count + 1,
            amount,
            DeleteRewardRow,
            OnPrizeChanged);

        rewardRows.Add(row);
        instance.SetActive(true);
        RebuildLayout();
    }

    private void DeleteRewardRow(RankRewardPrefab row)
    {
        if (submitting || row == null || !rewardRows.Remove(row))
            return;

        row.gameObject.SetActive(false);
        Destroy(row.gameObject);

        for (int i = 0; i < rewardRows.Count; i++)
            rewardRows[i].SetPosition(i + 1);

        RefreshTotal();
        RebuildLayout();
    }

    private void OnPrizeChanged()
    {
        RefreshTotal();
    }

    private void RefreshTotal()
    {
        decimal total = 0;

        try
        {
            if (autoReward)
            {
                foreach (var row in rewardRows)
                {
                    if (!row.TryGetAmount(out decimal amount))
                    {
                        SetText(TotalRewardChips, "—");
                        return;
                    }

                    total += amount;
                }
            }

            SetText(
                TotalRewardChips,
                total.ToString("#,0.##", CultureInfo.InvariantCulture));
        }
        catch (OverflowException)
        {
            SetText(TotalRewardChips, "—");
        }
    }

    private void OnBBRangeChanged(int min, int max)
    {
        SetText(
            BB_AnteRange_Text,
            RangeSliderUI.FormatValue(min) + " - " +
            RangeSliderUI.FormatValue(max));
    }

    private void OnBuyInRangeChanged(int min, int max)
    {
        SetText(
            BuyInRange_Text,
            RangeSliderUI.FormatValue(min) + " - " +
            RangeSliderUI.FormatValue(max));
    }

    private void RefreshRangeLabels()
    {
        if (BB_AnteSlider != null)
        {
            OnBBRangeChanged(
                BB_AnteSlider.LeftValue,
                BB_AnteSlider.RightValue);
        }

        if (BuyInRangeSlider != null)
        {
            OnBuyInRangeChanged(
                BuyInRangeSlider.LeftValue,
                BuyInRangeSlider.RightValue);
        }
    }

    private void SaveTemleteButtonOnTap()
    {
        if (submitting || !initialized)
            return;

        try
        {
            var request = BuildRequest(false);
            SaveTemplateAsync(request, clubId).Forget();
        }
        catch (Exception e)
        {
            SetMessage(e.Message);
        }
    }

    private async UniTask SaveTemplateAsync(
        CreateClubLeaderboardRequest request,
        string selectedClubId)
    {
        int version = viewVersion;
        SetBusy(true);
        SetMessage("Saving template...");

        try
        {
            if (ClubManager.Instance == null)
                throw new InvalidOperationException("ClubManager is missing.");

            await ClubManager.Instance.SaveClubLeaderboardTemplateAsync(
                selectedClubId, request);

            if (this != null && isActiveAndEnabled &&
                version == viewVersion && clubId == selectedClubId)
            {
                SetMessage("Template saved successfully.");
                RebuildLayout();
            }
        }
        catch (Exception e)
        {
            if (this != null && isActiveAndEnabled &&
                version == viewVersion && clubId == selectedClubId)
            {
                SetMessage("Failed to save template: " + e.Message);
                Debug.LogError("[Leaderboard] Template save failed: " + e.Message);
            }
        }
        finally
        {
            if (this != null)
                SetBusy(false);
        }
    }

    private void CreateButtonOnTap()
    {
        if (submitting || !initialized)
            return;

        try
        {
            var request = BuildRequest();
            CreateAsync(request, clubId).Forget();
        }
        catch (Exception e)
        {
            SetMessage(e.Message);
        }
    }

    private CreateClubLeaderboardRequest BuildRequest(bool requireDates = true)
    {
        string name = Leaderboard_InputField != null
            ? Leaderboard_InputField.text.Trim()
            : "";

        if (string.IsNullOrWhiteSpace(clubId))
            throw new InvalidOperationException("Club ID is missing.");

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException(
                "Enter a leaderboard name.");

        if (leaderboardType != "HANDS" &&
            leaderboardType != "SCORE" &&
            leaderboardType != "WINNINGS")
        {
            throw new InvalidOperationException(
                "Select a leaderboard type first.");
        }

        DateTime startUtc = default(DateTime);
        DateTime endUtc = default(DateTime);

        if (requireDates)
        {
            if (!startDate.HasValue || !endDate.HasValue)
                throw new InvalidOperationException(
                    "Select both start and end dates.");

            // The poster picker returns device-local date and time.
            startUtc = DateTime.SpecifyKind(
                startDate.Value, DateTimeKind.Local).ToUniversalTime();
            endUtc = DateTime.SpecifyKind(
                endDate.Value, DateTimeKind.Local).ToUniversalTime();

            if (startUtc <= DateTime.UtcNow)
                throw new InvalidOperationException(
                    "The start date must be in the future.");

            if (endUtc <= startUtc)
                throw new InvalidOperationException(
                    "The end date must be after the start date.");
        }

        var selectedVariants = new List<string>();

        foreach (var row in variantRows)
        {
            if (row.IsSelected)
                selectedVariants.Add(row.VariantCode);
        }

        if (selectedVariants.Count == 0)
            throw new InvalidOperationException(
                "Select at least one variant.");

        if (BB_AnteSlider.LeftValue < 0 ||
            BB_AnteSlider.RightValue < BB_AnteSlider.LeftValue ||
            BuyInRangeSlider.LeftValue < 0 ||
            BuyInRangeSlider.RightValue < BuyInRangeSlider.LeftValue)
        {
            throw new InvalidOperationException(
                "Select valid BB and buy-in ranges.");
        }

        var prizes = new List<ClubLeaderboardPrize>();
        decimal total = 0;

        if (autoReward)
        {
            if (rewardRows.Count == 0)
                throw new InvalidOperationException(
                    "Add at least one reward rank.");

            foreach (var row in rewardRows)
            {
                if (!row.TryGetAmount(out decimal amount) || amount <= 0)
                {
                    throw new InvalidOperationException(
                        "Enter a prize greater than zero for rank " +
                        row.Position + ".");
                }

                total += amount;

                prizes.Add(new ClubLeaderboardPrize
                {
                    Position = row.Position,
                    Amount = amount
                });
            }
        }

        return new CreateClubLeaderboardRequest
        {
            Name = name,
            Description = Description_Inputfield != null
                ? Description_Inputfield.text.Trim()
                : "",

            Type = leaderboardType,
            VariantFilter = selectedVariants,

            BbMin = BB_AnteSlider.LeftValue,
            BbMax = BB_AnteSlider.RightValue,
            BuyInMin = BuyInRangeSlider.LeftValue,
            BuyInMax = BuyInRangeSlider.RightValue,

            StartAt = requireDates ? startUtc.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture) : null,

            EndAt = requireDates ? endUtc.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture) : null,

            DoubleScores = IsScoreType() && doubleScores,
            DoubleScoreHour = GetDoubleScoreTime(),

            AutoReward = autoReward,
            Prizes = prizes
        };
    }

    private async UniTask CreateAsync(
        CreateClubLeaderboardRequest request,
        string selectedClubId)
    {
        int version = viewVersion;
        SetBusy(true);
        SetMessage("Creating leaderboard...");

        try
        {
            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var response =
                await ClubManager.Instance.CreateClubLeaderboardAsync(
                    selectedClubId, request);

            if (this == null)
                return;

            if (!IsCurrent(version) || clubId != selectedClubId)
            {
                if (LeaderboardManager.Instance != null &&
                    ClubContext.ClubId == selectedClubId)
                    LeaderboardManager.Instance.Reload();
                return;
            }

            CreatedLeaderboard = response.Leaderboard;

            SetMessage("Leaderboard created successfully.");
            if (LeaderboardManager.Instance != null)
                LeaderboardManager.Instance.Reload();
            // Close after a successful server response.
            gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            if (IsCurrent(version) && clubId == selectedClubId)
            {
                SetMessage("Failed to create leaderboard: " + e.Message);
                Debug.LogError("[Leaderboard] Create failed: " + e.Message);
            }
        }
        finally
        {
            if (this != null)
                SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        submitting = value;

        if (BackButton != null) BackButton.interactable = !value;
        if (InformationButton != null)
            InformationButton.interactable = !value;
        if (CreateButton != null) CreateButton.interactable = !value;
        if (SaveTemleteButton != null)
            SaveTemleteButton.interactable = !value;

        if (Leaderboard_InputField != null)
            Leaderboard_InputField.interactable = !value;
        if (Description_Inputfield != null)
            Description_Inputfield.interactable = !value;

        if (StartDateTime_Button != null)
            StartDateTime_Button.interactable = !value;
        if (EndDateTime_Button != null)
            EndDateTime_Button.interactable = !value;

        if (DoubleScoreOn_offButton != null)
            DoubleScoreOn_offButton.interactable = !value;
        if (AutoRewardOn_offButton != null)
            AutoRewardOn_offButton.interactable = !value;
        if (AutoRewardIncrese_Button != null)
            AutoRewardIncrese_Button.interactable = !value;

        foreach (var row in variantRows)
            row.SetBusy(value);

        foreach (var row in rewardRows)
            row.SetBusy(value);

        RefreshSwitches();
    }

    private void RebuildLayout()
    {
        if (!isActiveAndEnabled)
            return;

        ForceRefreshLayoutAsync(++layoutVersion).Forget();
    }

    private async UniTask ForceRefreshLayoutAsync(int version)
    {
        // Wait for newly created rows and text to finish updating.
        await UniTask.NextFrame();

        if (this == null ||
            !isActiveAndEnabled ||
            version != layoutVersion)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        RebuildContentAndParents(VariantContent);
        RebuildContentAndParents(AutoRewardRankContent);

        // Refresh the outer layout even when the double-score panel is hidden.
        if (DoubleScorePanel != null && DoubleScorePanel.parent != null)
            RebuildContentAndParents(DoubleScorePanel.parent);

        Canvas.ForceUpdateCanvases();
    }

    private void RebuildContentAndParents(Transform content)
    {
        if (content == null || !content.gameObject.activeInHierarchy)
            return;

        // Rebuild nested row layouts before rebuilding the content.
        var layouts = content.GetComponentsInChildren<RectTransform>(false);

        for (int i = layouts.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layouts[i]);

        // Update parent layouts, including the outer scroll content.
        Transform current = content.parent;

        while (current != null)
        {
            if (current is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            if (current.GetComponent<Canvas>() != null)
                break;

            current = current.parent;
        }
    }

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = value;
    }

    private void SetMessage(string message)
    {
        SetText(MessageText, message);
        Debug.Log("[Leaderboard] " + message);
        InformationPrefabScript.Instance.ShowMessage(message);

    }

    private void OnDestroy()
    {
        if (BackButton != null)
            BackButton.onClick.RemoveListener(BackButtonOnTap);
        if (InformationButton != null)
            InformationButton.onClick.RemoveListener(InformationButtonOnTap);
        if (StartDateTime_Button != null)
            StartDateTime_Button.onClick.RemoveListener(StartDateTimeOnTap);
        if (EndDateTime_Button != null)
            EndDateTime_Button.onClick.RemoveListener(EndDateTimeOnTap);
        if (DoubleScoreOn_offButton != null)
            DoubleScoreOn_offButton.onClick.RemoveListener(DoubleScoreOnTap);
        if (AutoRewardOn_offButton != null)
            AutoRewardOn_offButton.onClick.RemoveListener(AutoRewardOnTap);
        if (AutoRewardIncrese_Button != null)
            AutoRewardIncrese_Button.onClick.RemoveListener(AddRewardOnTap);
        if (CreateButton != null)
            CreateButton.onClick.RemoveListener(CreateButtonOnTap);
        if (SaveTemleteButton != null)
            SaveTemleteButton.onClick.RemoveListener(SaveTemleteButtonOnTap);

        if (BB_AnteSlider != null)
            BB_AnteSlider.OnRangeChanged.RemoveListener(OnBBRangeChanged);

        if (BuyInRangeSlider != null)
            BuyInRangeSlider.OnRangeChanged.RemoveListener(
                OnBuyInRangeChanged);
    }
}