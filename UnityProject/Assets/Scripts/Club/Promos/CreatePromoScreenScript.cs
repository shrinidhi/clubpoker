using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CreatePromoScreenScript : MonoBehaviour
{
    public Button BackButton;

    [Header("TypePanel")]
    public GameObject TypePanel;
    public InputField PromNameInputField;
    public Dropdown ScopeDropDown;

    public Button BadBeatJackpot_Button;
    public Button HighHandButton;
    public Button Leaderboard_Button;
    public Button Bounty_Button;
    public Button RakeRace_Button;

    public Sprite SelectButtonSprite;
    public Sprite UnSelectButtonSprite;

    public Button CancelButton;
    public Button SaveDraftButton;
    public Button ConfigureButton;

    [Header("ConfigurePanel")]
    public GameObject ConfigurePanel;
    public InputField PrizeAmount_InputField;
    public Dropdown RakeContributionRate_DropDown;
    public InputField PrizeCap_InputField;
    public Dropdown MinHandRank_DropDown;
    public Dropdown Metric_DropDown;
    public InputField TimerDurationMinutes_InputField;
    public InputField TargetMember_InputField;
    public InputField MinPot_InputField;
    public InputField MinPlayerDealt_InputField;
    public Toggle MemberOnly_Toggle;
    public InputField AnnouncementOnStart_InputFiled;

    public Button ConfigureCancelButton;
    public Button ConfigureSaveDraftButton;
    public Button ConfigureBackButton;
    public Button ScheduleButton;

    [Header("High Hand")]
    public int HighHandPrizeAmount = 5000;
    public int HighHandTimerDurationMinutes = 120;
    public int HighHandMinHandRank = 7;

    [Header("Configuration Field Containers")]
    public GameObject RakeContributionRatePanel;
    public GameObject PrizeCapPanel;
    public GameObject HighHandTimerPanel;
    public GameObject MetricPanel;
    public GameObject TargetMemberPanel;
    public GameObject MinHandRankPanel;
    public GameObject PrizeAmountPanel;
    public GameObject MinPotPanel;
    public GameObject MinPlayerDealtPanel;
    public GameObject MemberOnlyPanel;
    public GameObject AnnouncementOnStartPanel;

    [Header("Leaderboard defaults")]
    public int LeaderboardPrizeAmount = 10000;
    public int LeaderboardDurationMinutes = 10080;
    public int LeaderboardPrizePositions = 3;
    public float[] LeaderboardPrizePercentages = { 50f, 30f, 20f };
    public string[] MetricValues =
        { "most_chips_won", "most_hands_won", "best_bb_per_100", "most_hands_played" };

    [Header("Bounty defaults")]
    public int BountyPrizeAmount = 2000;
    public string BountyTargetMemberId = "";
    public bool BountyReboardOnExpiry = false;
    public int RakeRaceDurationMinutes = 120;

    [Header("Confirm these additional typeConfig keys with your backend")]
    public string PrizeCapConfigKey = "prizeCap";
    public string TargetMemberConfigKey = "targetMemberId";
    public string DurationConfigKey = "timerDurationMinutes";

    [Header("SchedulePanel")]
    public GameObject SchedulePanel;
    public Dropdown StartModeDropDown;
    public GameObject DateTimeInputfielddPanel;
    public InputField Start_DateTime_InputField;
    public InputField End_DateTime_InputField;
    public Dropdown RepeatDropDown;
    public GameObject DaysPanel;

    public Toggle Announcewhenlive_Toogle;
    public Toggle AnnounceleaderChanges_Toogle;
    public Toggle Ten_minutewarnig_Toogle;
    public Toggle AnnounceWinner_Toogle;

    public Button ScheduleCancelButton;
    public Button ScheduleSaveDraftButton;
    public Button ScheduleBackButton;
    public Button ReviewButton;

    [Header("Schedule References")]
    public PromoSelectDateTimeScript DateTimePicker;
    public DayGridScript RepeatDaysGrid;
    public Button StartDateButton;
    public Button EndDateButton;

    [Header("ReviewPanel")]
    public GameObject ReviewPanel;
    public Text TypeText;
    public Text ScopeText;
    public Text PrizeText;
    public Text ScheduleText;
    public Text RepeatText;

    public Text AnnouncementPreview_1;
    public Text AnnouncementPreview_2;
    public Text AnnouncementPreview_3;

    public Button ReviewCancelButton;
    public Button ReviewSaveDraftButton;
    public Button ReviewBackButton;
    public Button CratePromoButton;

    [Header("Additional Review Text")]
    public Text ReviewPromoNameText;
    public Text AnnouncementPreview_4;

    [Header("Rake Race draft values")]
    public int PrizeAmount = 8000;
    public int PrizePositions = 5;
    public float SelfFundingRate = 0f;

    [Header("Bad Beat draft defaults")]
    public int BadBeatSeedAmount = 5000;
    public float BadBeatRakeContributionRate = 0.1f;

    [Header("Dropdown options ke order mein backend rank codes")]
    public int[] MinHandRankValues;

    [Header("References")]
    public ClubPromosManagementScreenScript PromosManagementScreen;
    public Text ErrorText;

    public event Action<CreateClubPromoRequest> ReviewRequested;

    private readonly Dictionary<string, ConfigDraft> configDrafts = new Dictionary<string, ConfigDraft>();
    private class ConfigDraft
    {
        public string Prize, Duration, Cap, Target;
        public int Rake, Hand, Metric;
    }

    // Remember a successfully created draft until activation is confirmed.
    private string pendingActivationPromoId;
    private string pendingActivationClubId;
    private bool loadingEdit;
    private string editingPromoId;
    private string editingClubId;
    private ClubPromoData loadedDraft;
    private decimal? loadedCustomRake;
    public bool IsEditingDraft => !string.IsNullOrEmpty(editingPromoId);
    private bool saving;
    private string selectedPromoType = "BAD_BEAT_JACKPOT";
    private int viewVersion;

    private DateTime? selectedStartDate;
    private DateTime? selectedEndDate;

    private const string DateDisplayFormat = "dd/MM/yyyy hh:mm tt";
    private const string DatePayloadFormat = "yyyy-MM-dd'T'HH:mm";

    private readonly List<ButtonBinding> buttonBindings =
        new List<ButtonBinding>();

    private class ButtonBinding
    {
        public Button Button;
        public UnityAction Callback;
    }

    private void Awake()
    {
        Bind(BackButton, Close);
        Bind(CancelButton, Close);
        Bind(SaveDraftButton, SaveDraft);

        Bind(BadBeatJackpot_Button, SelectBadBeat);
        Bind(HighHandButton, SelectHighHand);
        Bind(Leaderboard_Button, SelectLeaderboard);
        Bind(Bounty_Button, SelectBounty);
        Bind(RakeRace_Button, SelectRakeRace);

        Bind(ConfigureButton, OpenConfigure);
        Bind(ConfigureCancelButton, Close);
        Bind(ConfigureSaveDraftButton, SaveConfiguredDraft);
        Bind(ConfigureBackButton, BackToType);
        Bind(ScheduleButton, OnScheduleClicked);

        Bind(StartDateButton, OpenStartDatePicker);
        Bind(EndDateButton, OpenEndDatePicker);

        Bind(ScheduleCancelButton, Close);
        Bind(ScheduleSaveDraftButton, SaveScheduledDraft);
        Bind(ScheduleBackButton, BackToConfigure);
        Bind(ReviewButton, OnReviewClicked);

        Bind(ReviewCancelButton, Close);
        Bind(ReviewSaveDraftButton, SaveScheduledDraft);
        Bind(ReviewBackButton, BackToSchedule);


        Bind(CratePromoButton, CreateAndActivate);

        SetDropdown(ScopeDropDown, new List<string>
        {
            "Table",
            "Club Wide"
        }, 1);

        SetupDropdowns();
        SetupMetricDropdown();
        SetupScheduleControls();
    }

    private void Bind(Button button, UnityAction callback)
    {
        if (button == null) return;

        button.onClick.AddListener(callback);

        buttonBindings.Add(new ButtonBinding
        {
            Button = button,
            Callback = callback
        });
    }

    private static void SetDropdown(
        Dropdown dropdown,
        List<string> options,
        int selectedIndex)
    {
        if (dropdown == null) return;

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(selectedIndex);
        dropdown.RefreshShownValue();
    }

    private void SetupDropdowns()
    {
        SetDropdown(RakeContributionRate_DropDown, new List<string>
        {
            "5% of each hand's rake",
            "10% (default)",
            "15%",
            "20%"
        }, 1);

        MinHandRankValues = new int[] { 5, 6, 7, 8, 9 };

        SetDropdown(MinHandRank_DropDown, new List<string>
        {
            "5 — Straight",
            "6 — Flush",
            "7 — Full House (default)",
            "8 — Four of a Kind",
            "9 — Straight Flush"
        }, 2);
    }

    private void SetupMetricDropdown()
    {
        SetDropdown(Metric_DropDown, new List<string>
        {
            "Most chips won",
            "Most hands won",
            "Best BB/100",
            "Most hands played"
        }, 0);
    }

    private string ReadMetricText()
    {
        if (Metric_DropDown == null ||
            Metric_DropDown.options.Count != 4 ||
            Metric_DropDown.value < 0 ||
            Metric_DropDown.value >= 4)
        {
            throw new InvalidOperationException(
                "Metric dropdown is not configured.");
        }

        return Metric_DropDown.options[Metric_DropDown.value].text;
    }

    private void SetupScheduleControls()
    {
        SetDropdown(StartModeDropDown, new List<string>
        {
            "Manual — I'll activate it myself",
            "Scheduled — auto-activate at a time"
        }, 0);

        SetDropdown(RepeatDropDown, new List<string>
        {
            "No repeat — one-time",
            "Daily",
            "Weekly on selected days"
        }, 0);

        if (StartModeDropDown != null)
            StartModeDropDown.onValueChanged.AddListener(OnStartModeChanged);

        if (RepeatDropDown != null)
            RepeatDropDown.onValueChanged.AddListener(OnRepeatChanged);

        if (Start_DateTime_InputField != null)
            Start_DateTime_InputField.readOnly = true;

        if (End_DateTime_InputField != null)
            End_DateTime_InputField.readOnly = true;

        UpdateScheduleVisibility();
    }

    private void OnEnable()
    {
        ++viewVersion;
        if (string.IsNullOrEmpty(pendingActivationPromoId))
        {
            editingPromoId = null;
            editingClubId = null;
            loadedDraft = null;
            if (loadedCustomRake.HasValue) SetupDropdowns();
            loadedCustomRake = null;
        }
        UpdatePromoButtonSprites();
        if (!string.IsNullOrEmpty(pendingActivationPromoId))
            ShowPanel(ReviewPanel);
        else
            ShowTypePanel();
        UpdateScheduleVisibility();
        ShowError("");
        UpdateControls();
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    private void OnDestroy()
    {
        foreach (var binding in buttonBindings)
        {
            if (binding.Button != null)
                binding.Button.onClick.RemoveListener(binding.Callback);
        }

        buttonBindings.Clear();

        if (StartModeDropDown != null)
            StartModeDropDown.onValueChanged.RemoveListener(OnStartModeChanged);

        if (RepeatDropDown != null)
            RepeatDropDown.onValueChanged.RemoveListener(OnRepeatChanged);
    }

    private void SelectBadBeat() => SelectPromo("BAD_BEAT_JACKPOT");
    private void SelectHighHand() => SelectPromo("HIGH_HAND");
    private void SelectRakeRace() => SelectPromo("RAKE_RACE");

    private void SelectLeaderboard()
    {
        SelectPromo("LEADERBOARD");
    }

    private void SelectBounty()
    {
        SelectPromo("BOUNTY");
    }

    private void SelectPromo(string promoType)
    {
        if (saving || loadingEdit) return;

        if (selectedPromoType != promoType)
        {
            configDrafts[selectedPromoType] = CaptureConfigDraft();
            selectedPromoType = promoType;
            RestoreConfigDraft();
        }
        UpdatePromoButtonSprites();
        UpdateConfigurationVisibility();
        ShowError("");

        Debug.Log("[Promos] Selected type: " + selectedPromoType);
    }

    private void UpdatePromoButtonSprites()
    {
        SetPromoButtonSprite(BadBeatJackpot_Button, selectedPromoType == "BAD_BEAT_JACKPOT");
        SetPromoButtonSprite(HighHandButton, selectedPromoType == "HIGH_HAND");
        SetPromoButtonSprite(Leaderboard_Button, selectedPromoType == "LEADERBOARD");
        SetPromoButtonSprite(Bounty_Button, selectedPromoType == "BOUNTY");
        SetPromoButtonSprite(RakeRace_Button, selectedPromoType == "RAKE_RACE");
    }

    private void SetPromoButtonSprite(Button button, bool selected)
    {
        if (button == null || button.image == null) return;
        Sprite sprite = selected ? SelectButtonSprite : UnSelectButtonSprite;
        if (sprite == null) return;
        button.image.sprite = sprite;
        // Prevent Sprite Swap from overriding the chosen background on hover/click.
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

    public void OpenEdit(string clubId, string promoId)
    {
        if (saving || loadingEdit) return;
        if (!string.IsNullOrEmpty(pendingActivationPromoId))
        {
            gameObject.SetActive(true);
            ShowPanel(ReviewPanel);
            ShowError("Finish the pending activation before editing another draft.");
            return;
        }
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        editingPromoId = null;
        editingClubId = null;
        loadedDraft = null;
        loadingEdit = true;
        UpdateControls();
        LoadDraftForEditAsync(clubId, promoId, ++viewVersion).Forget();
    }

    private async UniTask LoadDraftForEditAsync(string clubId, string promoId, int version)
    {
        try
        {
            if (ClubManager.Instance == null) throw new InvalidOperationException("ClubManager is missing.");
            if (clubId != ClubContext.ClubId) throw new InvalidOperationException("Draft belongs to a different club.");
            var result = await ClubManager.Instance.GetClubPromoAsync(clubId, promoId);
            if (this == null || !isActiveAndEnabled || version != viewVersion) return;
            if (!string.Equals(result.Promo.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only DRAFT promos can be edited.");
            if (ConfigurePanel == null) throw new InvalidOperationException("Assign ConfigurePanel.");
            PopulateDraft(result.Promo);
            loadedDraft = result.Promo;
            editingPromoId = promoId;
            editingClubId = clubId;
            ShowPanel(ConfigurePanel);
            ShowError("");
        }
        catch (Exception e)
        {
            if (this != null && isActiveAndEnabled && version == viewVersion)
            {
                gameObject.SetActive(false);
                ShowError("Cannot load draft: " + e.Message);
            }
        }
        finally
        {
            if (this != null)
            {
                loadingEdit = false;
                UpdateControls();
            }
        }
    }

    private static void FillInput(InputField input, string value)
    {
        if (input != null) input.text = value ?? "";
    }

    private void PopulateDraft(ClubPromoData promo)
    {
        switch (promo.PromoType)
        {
            case "BAD_BEAT_JACKPOT": case "HIGH_HAND": case "LEADERBOARD": case "BOUNTY": case "RAKE_RACE": break;
            default: throw new InvalidOperationException("Unsupported promo type: " + promo.PromoType);
        }
        configDrafts.Clear();
        selectedPromoType = promo.PromoType;
        SetupDropdowns();
        loadedCustomRake = null;
        var config = promo.TypeConfig;
        var rules = promo.EligibilityRules;
        FillInput(PromNameInputField, promo.Name);
        if (ScopeDropDown != null) ScopeDropDown.SetValueWithoutNotify(promo.Scope == "TABLE" ? 0 : 1);
        FillInput(PrizeAmount_InputField, promo.PrizeAmount.ToString(CultureInfo.InvariantCulture));
        int duration = config != null && config.TimerDurationMinutes.HasValue
            ? config.TimerDurationMinutes.Value : DefaultDurationMinutes();
        if (config != null && config.AdditionalFields != null && config.AdditionalFields.TryGetValue(DurationConfigKey, out JToken durationValue))
            duration = durationValue.Value<int>();
        FillInput(TimerDurationMinutes_InputField, duration.ToString(CultureInfo.InvariantCulture));
        decimal? cap = config != null ? config.PrizeCap : null;
        string target = config != null ? config.TargetMemberId : "";
        if (config != null && config.AdditionalFields != null)
        {
            if (config.AdditionalFields.TryGetValue(PrizeCapConfigKey, out JToken capValue)) cap = capValue.Value<decimal?>();
            if (config.AdditionalFields.TryGetValue(TargetMemberConfigKey, out JToken targetValue)) target = targetValue.Value<string>();
        }
        FillInput(PrizeCap_InputField, cap.HasValue ? cap.Value.ToString(CultureInfo.InvariantCulture) : "");
        FillInput(TargetMember_InputField, target);
        FillInput(MinPot_InputField, (rules != null ? rules.MinPotBBs : 0m).ToString(CultureInfo.InvariantCulture));
        FillInput(MinPlayerDealt_InputField, (rules != null && rules.MinPlayersDealt >= 2 ? rules.MinPlayersDealt : 2).ToString(CultureInfo.InvariantCulture));
        if (MemberOnly_Toggle != null) MemberOnly_Toggle.SetIsOnWithoutNotify(rules != null && rules.MembersOnly);
        FillInput(AnnouncementOnStart_InputFiled, promo.Description);
        int rank = rules != null && rules.MinHandRank > 0 ? rules.MinHandRank
            : config != null && config.MinHandRank.HasValue ? config.MinHandRank.Value : HighHandMinHandRank;
        int handIndex = Array.IndexOf(MinHandRankValues, rank);
        if (handIndex < 0 && (selectedPromoType == "BAD_BEAT_JACKPOT" || selectedPromoType == "HIGH_HAND" || selectedPromoType == "BOUNTY"))
            throw new InvalidOperationException("Unsupported saved minimum hand rank: " + rank);
        if (MinHandRank_DropDown != null) MinHandRank_DropDown.SetValueWithoutNotify(Math.Max(0, handIndex));
        if (selectedPromoType == "BAD_BEAT_JACKPOT" && RakeContributionRate_DropDown != null && config != null)
        {
            decimal[] rates = { 0.05m, 0.10m, 0.15m, 0.20m };
            int index = Array.IndexOf(rates, config.RakeContributionRate);
            if (index < 0)
            {
                loadedCustomRake = config.RakeContributionRate;
                RakeContributionRate_DropDown.AddOptions(new List<string>
                    { (config.RakeContributionRate * 100m).ToString(CultureInfo.InvariantCulture) + "% (saved)" });
                index = 4;
            }
            RakeContributionRate_DropDown.SetValueWithoutNotify(index);
        }
        if (selectedPromoType == "LEADERBOARD" && config != null)
        {
            string metric = string.IsNullOrWhiteSpace(config.Metric) ? "most_chips_won" : config.Metric;
            int index = MetricValues != null ? Array.IndexOf(MetricValues, metric) : -1;
            if (index < 0) throw new InvalidOperationException("Add saved metric to MetricValues: " + metric);
            if (Metric_DropDown != null) Metric_DropDown.SetValueWithoutNotify(index);
            LeaderboardPrizePositions = config.PrizePositions ?? 3;
            if (config.PrizePositionsStructure != null && config.PrizePositionsStructure.Count > 0)
            {
                var positions = new List<ClubPromoPrizePosition>(config.PrizePositionsStructure);
                positions.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                LeaderboardPrizePositions = positions.Count;
                LeaderboardPrizePercentages = positions.ConvertAll(x => (float)x.Pct).ToArray();
            }
        }
        if (selectedPromoType == "RAKE_RACE" && config != null)
        {
            PrizePositions = config.PrizePositions ?? 5;
            SelfFundingRate = (float)(config.SelfFundingRate ?? 0m);
        }
        if (selectedPromoType == "BOUNTY" && config != null) BountyReboardOnExpiry = config.ReboardOnExpiry ?? false;
        selectedStartDate = ParseSavedDate(promo.StartsAt);
        selectedEndDate = ParseSavedDate(promo.EndsAt);
        FillInput(Start_DateTime_InputField, selectedStartDate.HasValue ? selectedStartDate.Value.ToString(DateDisplayFormat, CultureInfo.InvariantCulture) : "");
        FillInput(End_DateTime_InputField, selectedEndDate.HasValue ? selectedEndDate.Value.ToString(DateDisplayFormat, CultureInfo.InvariantCulture) : "");
        if (StartModeDropDown != null) StartModeDropDown.SetValueWithoutNotify(selectedStartDate.HasValue ? 1 : 0);
        string frequency = promo.RepeatCadence != null ? promo.RepeatCadence.Frequency : "";
        if (RepeatDropDown != null) RepeatDropDown.SetValueWithoutNotify(frequency == "weekly" ? 2 : frequency == "daily" ? 1 : 0);
        if (RepeatDaysGrid != null) RepeatDaysGrid.SetSelectedDays(promo.RepeatCadence != null ? promo.RepeatCadence.DaysOfWeek : null);
        var announce = promo.AnnounceConfig;
        if (Announcewhenlive_Toogle != null) Announcewhenlive_Toogle.SetIsOnWithoutNotify(announce == null || announce.AnnounceOnStart);
        if (AnnounceleaderChanges_Toogle != null) AnnounceleaderChanges_Toogle.SetIsOnWithoutNotify(announce != null && announce.AnnounceLeaderChanges);
        if (Ten_minutewarnig_Toogle != null) Ten_minutewarnig_Toogle.SetIsOnWithoutNotify(announce != null && announce.AnnounceWarning);
        if (AnnounceWinner_Toogle != null) AnnounceWinner_Toogle.SetIsOnWithoutNotify(announce != null && announce.AnnounceOnEnd);
        UpdatePromoButtonSprites();
        UpdateConfigurationVisibility();
        UpdateScheduleVisibility();
        configDrafts[selectedPromoType] = CaptureConfigDraft();
    }

    private static DateTime? ParseSavedDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date))
            throw new InvalidOperationException("Invalid saved date: " + value);
        return date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;
    }

    private void PreserveDraftExtraFields(CreateClubPromoRequest request)
    {
        if (loadedDraft == null || request.PromoType != loadedDraft.PromoType) return;
        var config = request.TypeConfig as Dictionary<string, object>;
        if (config != null && loadedDraft.TypeConfig != null)
        {
            if (loadedDraft.TypeConfig.AdditionalFields != null)
                foreach (var entry in loadedDraft.TypeConfig.AdditionalFields)
                    if (!config.ContainsKey(entry.Key) && entry.Key != PrizeCapConfigKey)
                        config[entry.Key] = entry.Value.DeepClone();
            if (request.PromoType == "BAD_BEAT_JACKPOT" && loadedDraft.TypeConfig.PayoutStructure != null)
                config["payoutStructure"] = loadedDraft.TypeConfig.PayoutStructure;
        }
        if (loadedDraft.EligibilityRules != null && loadedDraft.EligibilityRules.AdditionalFields != null)
            foreach (var entry in loadedDraft.EligibilityRules.AdditionalFields)
                if (!request.EligibilityRules.ContainsKey(entry.Key)) request.EligibilityRules[entry.Key] = entry.Value.DeepClone();
    }

    private ConfigDraft CaptureConfigDraft()
    {
        return new ConfigDraft
        {
            Prize = PrizeAmount_InputField != null ? PrizeAmount_InputField.text : "",
            Duration = TimerDurationMinutes_InputField != null ? TimerDurationMinutes_InputField.text : "",
            Cap = PrizeCap_InputField != null ? PrizeCap_InputField.text : "",
            Target = TargetMember_InputField != null ? TargetMember_InputField.text : "",
            Rake = RakeContributionRate_DropDown != null ? RakeContributionRate_DropDown.value : 1,
            Hand = MinHandRank_DropDown != null ? MinHandRank_DropDown.value : 2,
            Metric = Metric_DropDown != null ? Metric_DropDown.value : 0
        };
    }

    private void RestoreConfigDraft()
    {
        if (!configDrafts.TryGetValue(selectedPromoType, out ConfigDraft draft))
            draft = new ConfigDraft
            {
                Prize = DefaultPrizeAmount().ToString(CultureInfo.InvariantCulture),
                Duration = DefaultDurationMinutes().ToString(CultureInfo.InvariantCulture),
                Cap = "",
                Target = BountyTargetMemberId,
                Rake = 1,
                Hand = 2,
                Metric = 0
            };
        if (PrizeAmount_InputField != null) PrizeAmount_InputField.text = draft.Prize;
        if (TimerDurationMinutes_InputField != null) TimerDurationMinutes_InputField.text = draft.Duration;
        if (PrizeCap_InputField != null) PrizeCap_InputField.text = draft.Cap;
        if (TargetMember_InputField != null) TargetMember_InputField.text = draft.Target;
        if (RakeContributionRate_DropDown != null) RakeContributionRate_DropDown.SetValueWithoutNotify(draft.Rake);
        if (MinHandRank_DropDown != null) MinHandRank_DropDown.SetValueWithoutNotify(draft.Hand);
        if (Metric_DropDown != null) Metric_DropDown.SetValueWithoutNotify(draft.Metric);
    }

    private void ShowPendingType(string typeName)
    {
        if (saving || loadingEdit) return;

        ShowError("Select " + typeName + " and open Configure.");
    }

    private void ShowPanel(GameObject panel)
    {
        if (TypePanel != null)
            TypePanel.SetActive(TypePanel == panel);

        if (ConfigurePanel != null)
            ConfigurePanel.SetActive(ConfigurePanel == panel);

        if (SchedulePanel != null)
            SchedulePanel.SetActive(SchedulePanel == panel);

        if (ReviewPanel != null)
            ReviewPanel.SetActive(ReviewPanel == panel);
    }

    private void ShowTypePanel()
    {
        ShowPanel(TypePanel);
    }

    private void OpenConfigure()
    {
        if (saving || loadingEdit) return;

        ShowError("");

        if (ConfigurePanel == null)
        {
            ShowError("Assign ConfigurePanel in Inspector.");
            return;
        }

        if (PrizeAmount_InputField != null && string.IsNullOrWhiteSpace(PrizeAmount_InputField.text))
            PrizeAmount_InputField.text = DefaultPrizeAmount().ToString(CultureInfo.InvariantCulture);
        if (TimerDurationMinutes_InputField != null && string.IsNullOrWhiteSpace(TimerDurationMinutes_InputField.text))
            TimerDurationMinutes_InputField.text = DefaultDurationMinutes().ToString(CultureInfo.InvariantCulture);
        if (TargetMember_InputField != null && string.IsNullOrWhiteSpace(TargetMember_InputField.text))
            TargetMember_InputField.text = BountyTargetMemberId;
        if (MinPot_InputField != null && string.IsNullOrWhiteSpace(MinPot_InputField.text))
            MinPot_InputField.text = "0";
        if (MinPlayerDealt_InputField != null && string.IsNullOrWhiteSpace(MinPlayerDealt_InputField.text))
            MinPlayerDealt_InputField.text = "2";

        UpdateConfigurationVisibility();
        ShowPanel(ConfigurePanel);
        UpdateControls();
    }

    private decimal DefaultPrizeAmount()
    {
        switch (selectedPromoType)
        {
            case "BAD_BEAT_JACKPOT": return BadBeatSeedAmount;
            case "HIGH_HAND": return HighHandPrizeAmount;
            case "LEADERBOARD": return LeaderboardPrizeAmount;
            case "BOUNTY": return BountyPrizeAmount;
            case "RAKE_RACE": return PrizeAmount;
            default: throw new InvalidOperationException("Select a valid promo type.");
        }
    }

    private int DefaultDurationMinutes()
    {
        switch (selectedPromoType)
        {
            case "LEADERBOARD": return LeaderboardDurationMinutes;
            case "RAKE_RACE": return RakeRaceDurationMinutes;
            default: return HighHandTimerDurationMinutes;
        }
    }

    private void UpdateConfigurationVisibility()
    {
        bool badBeat = selectedPromoType == "BAD_BEAT_JACKPOT";
        bool highHand = selectedPromoType == "HIGH_HAND";
        bool leaderboard = selectedPromoType == "LEADERBOARD";
        bool bounty = selectedPromoType == "BOUNTY";
        bool rakeRace = selectedPromoType == "RAKE_RACE";
        SetFieldVisible(PrizeAmountPanel, PrizeAmount_InputField, true);
        SetFieldVisible(RakeContributionRatePanel, RakeContributionRate_DropDown, badBeat);
        SetFieldVisible(PrizeCapPanel, PrizeCap_InputField, badBeat);
        // Reuse the existing timer container for all three duration fields.
        SetFieldVisible(HighHandTimerPanel, TimerDurationMinutes_InputField, highHand || leaderboard || rakeRace);
        SetFieldVisible(MinHandRankPanel, MinHandRank_DropDown, badBeat || highHand || bounty);
        SetFieldVisible(MetricPanel, Metric_DropDown, leaderboard);
        SetFieldVisible(TargetMemberPanel, TargetMember_InputField, bounty);
        SetFieldVisible(MinPotPanel, MinPot_InputField, true);
        SetFieldVisible(MinPlayerDealtPanel, MinPlayerDealt_InputField, true);
        SetFieldVisible(MemberOnlyPanel, MemberOnly_Toggle, true);
        SetFieldVisible(AnnouncementOnStartPanel, AnnouncementOnStart_InputFiled, true);
    }

    private static void SetFieldVisible(
        GameObject container,
        Component field,
        bool visible)
    {
        if (container != null)
            container.SetActive(visible);
        else if (field != null)
            field.gameObject.SetActive(visible);
    }

    private void BackToType()
    {
        if (saving || loadingEdit) return;

        ShowTypePanel();
        ShowError("");
        UpdateControls();
    }

    private void OnScheduleClicked()
    {
        if (saving || loadingEdit) return;

        try
        {
            BuildRequest(true);

            if (SchedulePanel == null)
                throw new InvalidOperationException(
                    "Assign SchedulePanel in Inspector.");

            ShowPanel(SchedulePanel);
            UpdateScheduleVisibility();
            ShowError("");
            UpdateControls();
        }
        catch (Exception e)
        {
            ShowError(e.Message);
        }
    }

    private void BackToConfigure()
    {
        if (saving || loadingEdit) return;

        if (ConfigurePanel == null)
        {
            ShowError("Assign ConfigurePanel in Inspector.");
            return;
        }

        UpdateConfigurationVisibility();
        ShowPanel(ConfigurePanel);
        ShowError("");
        UpdateControls();
    }

    private void BackToSchedule()
    {
        if (saving || loadingEdit) return;

        if (SchedulePanel == null)
        {
            ShowError("Assign SchedulePanel in Inspector.");
            return;
        }

        ShowPanel(SchedulePanel);
        UpdateScheduleVisibility();
        ShowError("");
        UpdateControls();
    }

    private void OnStartModeChanged(int value)
    {
        UpdateScheduleVisibility();
    }

    private void OnRepeatChanged(int value)
    {
        UpdateScheduleVisibility();
    }

    private void UpdateScheduleVisibility()
    {
        bool scheduled =
            StartModeDropDown != null &&
            StartModeDropDown.value == 1;

        bool weekly =
            RepeatDropDown != null &&
            RepeatDropDown.value == 2;

        if (DateTimeInputfielddPanel != null)
            DateTimeInputfielddPanel.SetActive(scheduled);

        if (DaysPanel != null)
            DaysPanel.SetActive(weekly);
    }

    private void OpenStartDatePicker() => OpenDatePicker(true);
    private void OpenEndDatePicker() => OpenDatePicker(false);

    private void OpenDatePicker(bool selectingStart)
    {
        if (saving || loadingEdit) return;

        if (DateTimePicker == null)
        {
            ShowError("Assign DateTimePicker in Inspector.");
            return;
        }

        int version = viewVersion;

        DateTimePicker.Open(date =>
        {
            if (this == null ||
                !isActiveAndEnabled ||
                version != viewVersion ||
                saving)
            {
                return;
            }

            string display = date.ToString(
                DateDisplayFormat,
                CultureInfo.InvariantCulture);

            if (selectingStart)
            {
                selectedStartDate = date;

                if (Start_DateTime_InputField != null)
                    Start_DateTime_InputField.text = display;
            }
            else
            {
                selectedEndDate = date;

                if (End_DateTime_InputField != null)
                    End_DateTime_InputField.text = display;
            }

            ShowError("");
        });
    }

    private void CreateAndActivate() => SaveDraftAsync(true, true, true).Forget();

    private void SaveDraft() => SaveDraftAsync(false).Forget();
    private void SaveConfiguredDraft() => SaveDraftAsync(true).Forget();
    private void SaveScheduledDraft() => SaveDraftAsync(true, true).Forget();

    private decimal ReadAmount(
        InputField field,
        string label,
        bool allowZero = false)
    {
        if (field == null)
            throw new InvalidOperationException(
                "Assign " + label + " input.");

        if (!decimal.TryParse(
                field.text.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value) ||
            (allowZero ? value < 0m : value <= 0m))
        {
            throw new InvalidOperationException(
                "Enter a valid " + label + ".");
        }

        return value;
    }

    private int ReadPositiveInteger(InputField field, string label)
    {
        if (field == null ||
            !int.TryParse(
                field.text.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value) ||
            value <= 0)
        {
            throw new InvalidOperationException(
                "Enter a valid " + label + ".");
        }

        return value;
    }

    private decimal ReadRakeRate()
    {
        if (RakeContributionRate_DropDown == null ||
            (RakeContributionRate_DropDown.options.Count != 4 &&
             !(loadedCustomRake.HasValue && RakeContributionRate_DropDown.options.Count == 5)))
        {
            throw new InvalidOperationException(
                "Rake contribution dropdown is not configured.");
        }

        switch (RakeContributionRate_DropDown.value)
        {
            case 0: return 0.05m;
            case 1: return 0.10m;
            case 2: return 0.15m;
            case 3: return 0.20m;
            case 4:
                if (loadedCustomRake.HasValue) return loadedCustomRake.Value;
                throw new InvalidOperationException("Custom rake value is missing.");
            default:
                throw new InvalidOperationException(
                    "Select a valid rake contribution.");
        }
    }

    private int ReadMinHandRank()
    {
        if (MinHandRank_DropDown == null ||
            MinHandRankValues == null ||
            MinHandRankValues.Length == 0 ||
            MinHandRankValues.Length != MinHandRank_DropDown.options.Count ||
            MinHandRank_DropDown.value < 0 ||
            MinHandRank_DropDown.value >= MinHandRankValues.Length)
        {
            throw new InvalidOperationException(
                "Assign MinHandRankValues matching the dropdown options.");
        }

        return MinHandRankValues[MinHandRank_DropDown.value];
    }

    private Dictionary<string, object> BuildEligibilityRules(int? minHandRank)
    {
        if (MemberOnly_Toggle == null)
            throw new InvalidOperationException("Assign MemberOnly_Toggle.");
        int minPlayers = ReadPositiveInteger(MinPlayerDealt_InputField, "minimum players dealt");
        if (minPlayers < 2)
            throw new InvalidOperationException("Minimum players dealt must be at least 2.");
        var rules = new Dictionary<string, object>
        {
            { "minPotBBs", ReadAmount(MinPot_InputField, "minimum pot in BBs", true) },
            { "minPlayersDealt", minPlayers },
            { "membersOnly", MemberOnly_Toggle.isOn }
        };
        if (minHandRank.HasValue) rules.Add("minHandRank", minHandRank.Value);
        return rules;
    }

    private string ReadMetricCode()
    {
        ReadMetricText();
        if (MetricValues == null || MetricValues.Length != Metric_DropDown.options.Count ||
            string.IsNullOrWhiteSpace(MetricValues[Metric_DropDown.value]))
            throw new InvalidOperationException("Assign backend MetricValues matching the dropdown options.");
        return MetricValues[Metric_DropDown.value].Trim();
    }

    private List<ClubPromoPrizePosition> BuildLeaderboardPositions()
    {
        if (LeaderboardPrizePositions < 1 || LeaderboardPrizePercentages == null ||
            LeaderboardPrizePercentages.Length != LeaderboardPrizePositions)
            throw new InvalidOperationException("Leaderboard percentages must match prize positions.");
        var positions = new List<ClubPromoPrizePosition>();
        decimal sum = 0m;
        for (int i = 0; i < LeaderboardPrizePercentages.Length; i++)
        {
            float value = LeaderboardPrizePercentages[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f || value > 100f)
                throw new InvalidOperationException("Enter valid leaderboard payout percentages.");
            decimal pct = (decimal)value;
            sum += pct;
            positions.Add(new ClubPromoPrizePosition { Rank = i + 1, Pct = pct });
        }
        if (Math.Abs(sum - 100m) > 0.001m)
            throw new InvalidOperationException("Leaderboard payouts must total 100%.");
        return positions;
    }

    private static void AddConfigValue(Dictionary<string, object> config, string key, object value, string label)
    {
        if (string.IsNullOrWhiteSpace(key) || config.ContainsKey(key.Trim()))
            throw new InvalidOperationException("Set a valid, unique backend key for " + label + ".");
        config.Add(key.Trim(), value);
    }

    private CreateClubPromoRequest BuildRequest(bool fromConfigure)
    {
        string name = PromNameInputField != null ? PromNameInputField.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a promo name.");
        if (ScopeDropDown == null || ScopeDropDown.options.Count != 2 ||
            ScopeDropDown.value < 0 || ScopeDropDown.value > 1)
            throw new InvalidOperationException("Select a valid scope.");

        bool badBeat = selectedPromoType == "BAD_BEAT_JACKPOT";
        bool highHand = selectedPromoType == "HIGH_HAND";
        bool leaderboard = selectedPromoType == "LEADERBOARD";
        bool bounty = selectedPromoType == "BOUNTY";
        bool rakeRace = selectedPromoType == "RAKE_RACE";
        if (!badBeat && !highHand && !leaderboard && !bounty && !rakeRace)
            throw new InvalidOperationException("Select a valid promo type.");
        decimal prize = fromConfigure ? ReadAmount(PrizeAmount_InputField, "prize amount") : DefaultPrizeAmount();
        if (prize <= 0m) throw new InvalidOperationException("Prize must be greater than zero.");

        var config = new Dictionary<string, object>();
        var request = new CreateClubPromoRequest
        {
            Name = name,
            PromoType = selectedPromoType,
            Scope = ScopeDropDown.value == 0 ? "TABLE" : "CLUB_WIDE",
            PrizeType = badBeat ? "ACCUMULATED" : "FIXED_POOL",
            PrizeAmount = prize,
            TypeConfig = config,
            EligibilityRules = new Dictionary<string, object>()
        };
        int? minHand = null;
        if (badBeat || highHand || bounty)
            minHand = fromConfigure ? ReadMinHandRank() : HighHandMinHandRank;
        if (minHand.HasValue && (minHand.Value < 5 || minHand.Value > 9))
            throw new InvalidOperationException("Minimum hand rank must be between 5 and 9.");

        if (badBeat)
        {
            if (float.IsNaN(BadBeatRakeContributionRate) || float.IsInfinity(BadBeatRakeContributionRate))
                throw new InvalidOperationException("Invalid default rake contribution.");
            decimal rate = fromConfigure ? ReadRakeRate() : (decimal)BadBeatRakeContributionRate;
            if (rate < 0m || rate > 1m)
                throw new InvalidOperationException("Rake contribution must be between 0 and 1.");
            config.Add("rakeContributionRate", rate);
            config.Add("seedAmount", prize);
            config.Add("payoutStructure", new ClubPromoPayoutStructure
            { Loser = 0.65m, Winner = 0.25m, TableShare = 0.10m });
            // Blank / zero cap means uncapped: do not transmit a zero cap.
            if (fromConfigure && PrizeCap_InputField != null && !string.IsNullOrWhiteSpace(PrizeCap_InputField.text))
            {
                decimal cap = ReadAmount(PrizeCap_InputField, "prize cap", true);
                if (cap > 0m)
                {
                    if (cap < prize) throw new InvalidOperationException("Prize cap cannot be below the seed prize.");
                    AddConfigValue(config, PrizeCapConfigKey, cap, "prize cap");
                }
            }
        }
        if (highHand || leaderboard || rakeRace)
        {
            int minutes = fromConfigure
                ? ReadPositiveInteger(TimerDurationMinutes_InputField, "duration in minutes")
                : DefaultDurationMinutes();
            if (minutes < (highHand ? 5 : 1))
                throw new InvalidOperationException(highHand ? "High Hand minimum duration is 5 minutes." : "Duration must be positive.");
            // High Hand uses the proven API key; other durations are configurable.
            AddConfigValue(config, highHand ? "timerDurationMinutes" : DurationConfigKey, minutes, "duration");
        }
        if (highHand) config.Add("minHandRank", minHand.Value);
        if (leaderboard)
        {
            config.Add("metric", fromConfigure ? ReadMetricCode() : "most_chips_won");
            config.Add("prizePositions", LeaderboardPrizePositions);
            config.Add("prizeStructure", BuildLeaderboardPositions());
        }
        if (bounty)
        {
            config.Add("reboardOnExpiry", BountyReboardOnExpiry);
            string target = fromConfigure
                ? (TargetMember_InputField != null ? TargetMember_InputField.text.Trim() : "")
                : (BountyTargetMemberId ?? "").Trim();
            if (fromConfigure && string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException("Enter the target member ID (not the display name).");
            if (!string.IsNullOrWhiteSpace(target))
                AddConfigValue(config, TargetMemberConfigKey, target, "target member");
        }
        if (rakeRace)
        {
            if (PrizePositions < 1 || float.IsNaN(SelfFundingRate) || float.IsInfinity(SelfFundingRate) ||
                SelfFundingRate < 0f || SelfFundingRate > 1f)
                throw new InvalidOperationException("Invalid Rake Race prize positions or self-funding rate.");
            config.Add("prizePositions", PrizePositions);
            config.Add("selfFundingRate", (decimal)SelfFundingRate);
        }
        if (fromConfigure)
        {
            request.EligibilityRules = BuildEligibilityRules(minHand);
            request.Description = AnnouncementOnStart_InputFiled != null
                ? AnnouncementOnStart_InputFiled.text.Trim() : "";
            request.AnnounceConfig = ReadAnnouncementConfig();
        }
        return request;
    }

    private ClubPromoAnnounceConfig ReadAnnouncementConfig()
    {
        return new ClubPromoAnnounceConfig
        {
            AnnounceOnStart = Announcewhenlive_Toogle != null ? Announcewhenlive_Toogle.isOn : true,
            AnnounceLeaderChanges = AnnounceleaderChanges_Toogle != null && AnnounceleaderChanges_Toogle.isOn,
            AnnounceWarning = Ten_minutewarnig_Toogle != null && Ten_minutewarnig_Toogle.isOn,
            AnnounceOnEnd = AnnounceWinner_Toogle != null && AnnounceWinner_Toogle.isOn
        };
    }

    private void ApplySchedule(CreateClubPromoRequest request)
    {
        if (StartModeDropDown == null ||
            RepeatDropDown == null ||
            StartModeDropDown.options.Count != 2 ||
            RepeatDropDown.options.Count != 3 ||
            StartModeDropDown.value < 0 ||
            StartModeDropDown.value > 1 ||
            RepeatDropDown.value < 0 ||
            RepeatDropDown.value > 2)
        {
            throw new InvalidOperationException(
                "Schedule dropdowns are not configured.");
        }

        if (Announcewhenlive_Toogle == null ||
            AnnounceleaderChanges_Toogle == null ||
            Ten_minutewarnig_Toogle == null ||
            AnnounceWinner_Toogle == null)
        {
            throw new InvalidOperationException(
                "Assign all announcement toggles.");
        }

        request.AnnounceConfig = ReadAnnouncementConfig();

        request.StartsAt = null;
        request.EndsAt = null;
        request.RepeatCadence = null;

        if (StartModeDropDown.value == 1)
        {
            if (!selectedStartDate.HasValue ||
                !selectedEndDate.HasValue)
            {
                throw new InvalidOperationException(
                    "Select start and end date/time.");
            }

            DateTime now = DateTime.Now;
            DateTime currentMinute = new DateTime(
                now.Year, now.Month, now.Day,
                now.Hour, now.Minute, 0);

            if (selectedStartDate.Value < currentMinute)
                throw new InvalidOperationException(
                    "Start date/time cannot be in the past.");

            if (selectedEndDate.Value <= selectedStartDate.Value)
                throw new InvalidOperationException(
                    "End date/time must be after start date/time.");

            // Same local date/time format as the supplied POST payload.
            request.StartsAt = selectedStartDate.Value.ToString(
                DatePayloadFormat, CultureInfo.InvariantCulture);

            request.EndsAt = selectedEndDate.Value.ToString(
                DatePayloadFormat, CultureInfo.InvariantCulture);
        }

        switch (RepeatDropDown.value)
        {
            case 0:
                break;

            case 1:
                request.RepeatCadence = new ClubPromoRepeatCadence
                {
                    Frequency = "daily"
                };
                break;

            case 2:
                if (RepeatDaysGrid == null)
                    throw new InvalidOperationException(
                        "Assign RepeatDaysGrid.");

                List<int> days = RepeatDaysGrid.GetSelectedDays();

                if (days.Count == 0)
                    throw new InvalidOperationException(
                        "Select at least one repeat day.");

                request.RepeatCadence = new ClubPromoRepeatCadence
                {
                    Frequency = "weekly",
                    DaysOfWeek = days
                };
                break;
        }
    }

    private void OnReviewClicked()
    {
        if (saving || loadingEdit) return;

        try
        {
            CreateClubPromoRequest request = BuildRequest(true);
            ApplySchedule(request);

            if (ReviewPanel == null)
                throw new InvalidOperationException(
                    "Assign ReviewPanel in Inspector.");

            PopulateReview(request);
            ShowPanel(ReviewPanel);
            ShowError("");
            UpdateControls();

            Canvas.ForceUpdateCanvases();

            if (ReviewPanel.transform is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            Canvas.ForceUpdateCanvases();
            ReviewRequested?.Invoke(request);
        }
        catch (Exception e)
        {
            ShowError(e.Message);
        }
    }

    private void PopulateReview(CreateClubPromoRequest request)
    {
        string typeName;

        switch (request.PromoType)
        {
            case "HIGH_HAND":
                typeName = "High Hand";
                break;
            case "BAD_BEAT_JACKPOT":
                typeName = "Bad Beat Jackpot";
                break;
            case "RAKE_RACE":
                typeName = "Rake Race";
                break;
            default:
                typeName = request.PromoType.Replace("_", " ");
                break;
        }

        string prize = request.PrizeAmount.ToString(
            "#,0.##", CultureInfo.InvariantCulture);

        SetReviewText(ReviewPromoNameText, request.Name);
        SetReviewText(TypeText, typeName);
        SetReviewText(
            ScopeText,
            request.Scope == "CLUB_WIDE" ? "Club-wide" : "Table");

        SetReviewText(PrizeText, "♦ " + prize);

        SetReviewText(
            ScheduleText,
            string.IsNullOrEmpty(request.StartsAt)
                ? "Manual activation"
                : FormatReviewDate(request.StartsAt) +
                  "\nTo " + FormatReviewDate(request.EndsAt));

        string repeat = "No repeat — one-time";

        if (request.RepeatCadence != null)
        {
            if (request.RepeatCadence.Frequency == "daily")
            {
                repeat = "Daily";
            }
            else if (request.RepeatCadence.Frequency == "weekly")
            {
                string[] names =
                {
                    "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
                };

                var selectedNames = new List<string>();

                if (request.RepeatCadence.DaysOfWeek != null)
                {
                    foreach (int day in request.RepeatCadence.DaysOfWeek)
                    {
                        if (day >= 0 && day < names.Length)
                            selectedNames.Add(names[day]);
                    }
                }

                repeat = "Weekly — " + string.Join(", ", selectedNames);
            }
        }

        SetReviewText(RepeatText, repeat);

        ClubPromoAnnounceConfig announcements = request.AnnounceConfig;

        string scopeMessage = request.Scope == "CLUB_WIDE"
            ? "All club tables count."
            : "Selected table scope.";

        SetAnnouncementPreview(
            AnnouncementPreview_1,
            announcements != null && announcements.AnnounceOnStart,
            string.IsNullOrWhiteSpace(request.Description)
                ? request.Name + " is live — prize: ♦ " + prize + ". " + scopeMessage
                : request.Description);

        SetAnnouncementPreview(
            AnnouncementPreview_2,
            announcements != null && announcements.AnnounceLeaderChanges,
            "Leader changes will be announced.");

        string warning = request.Name + " ends in 10 minutes.";
        string winner = "Winner announced — prize: ♦ " + prize + ".";

        if (AnnouncementPreview_4 != null)
        {
            SetAnnouncementPreview(
                AnnouncementPreview_3,
                announcements != null && announcements.AnnounceWarning,
                warning);

            SetAnnouncementPreview(
                AnnouncementPreview_4,
                announcements != null && announcements.AnnounceOnEnd,
                winner);
        }
        else
        {
            var messages = new List<string>();

            if (announcements != null && announcements.AnnounceWarning)
                messages.Add(warning);

            if (announcements != null && announcements.AnnounceOnEnd)
                messages.Add(winner);

            SetAnnouncementPreview(
                AnnouncementPreview_3,
                messages.Count > 0,
                string.Join("\n", messages));
        }
    }

    private static string FormatReviewDate(string value)
    {
        if (DateTime.TryParseExact(
            value,
            DatePayloadFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime date))
        {
            return date.ToString(
                DateDisplayFormat, CultureInfo.InvariantCulture);
        }

        return value ?? "";
    }

    private static void SetReviewText(Text target, string value)
    {
        if (target == null) return;

        target.supportRichText = false;
        target.text = value ?? "";
    }

    private static void SetAnnouncementPreview(
        Text target,
        bool visible,
        string message)
    {
        if (target == null) return;

        target.gameObject.SetActive(visible);
        SetReviewText(target, visible ? message : "");
    }

    private async UniTask SaveDraftAsync(
        bool fromConfigure,
        bool fromSchedule = false,
        bool activateAfterCreate = false)
    {
        if (saving || loadingEdit || !isActiveAndEnabled) return;
        ShowError("");
        string clubId = ClubContext.ClubId;
        CreateClubPromoRequest request = null;
        bool retryActivation = !string.IsNullOrEmpty(pendingActivationPromoId);
        ClubManager manager = ClubManager.Instance;

        try
        {
            if (string.IsNullOrWhiteSpace(clubId) || manager == null)
                throw new InvalidOperationException("Current club or ClubManager is missing.");
            if (retryActivation)
            {
                if (!activateAfterCreate)
                    throw new InvalidOperationException("A promo was already created. Tap Create Promo to retry its activation.");
                if (pendingActivationClubId != clubId)
                    throw new InvalidOperationException("Reopen the original club to retry this promo's activation.");
                // The existing draft contains the previously submitted settings.
                // Do not rebuild the request or create a second draft on retry.
            }
            else
            {
                if (IsEditingDraft && editingClubId != clubId)
                    throw new InvalidOperationException("The draft belongs to a different club.");
                request = BuildRequest(fromConfigure || IsEditingDraft);
                if (fromSchedule || IsEditingDraft) ApplySchedule(request);
                PreserveDraftExtraFields(request);
            }
        }
        catch (Exception e)
        {
            ShowError(e.Message);
            return;
        }

        int version = viewVersion;
        saving = true;
        UpdateControls();
        try
        {
            ClubPromoData promo;
            if (retryActivation)
            {
                // A previous activation may have succeeded even if its response was lost.
                // Read current status before posting activation again.
                var detail = await manager.GetClubPromoAsync(clubId, pendingActivationPromoId);
                promo = detail.Promo;
                if (promo == null || !string.Equals(promo.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    var activated = await manager.ActivateClubPromoAsync(clubId, pendingActivationPromoId);
                    promo = activated.Promo;
                }
            }
            else
            {
                var created = IsEditingDraft
                    ? await manager.UpdateClubPromoAsync(clubId, editingPromoId, request)
                    : await manager.CreateClubPromoAsync(clubId, request);
                if (created == null || created.Promo == null || string.IsNullOrWhiteSpace(created.Promo.Id))
                    throw new InvalidOperationException("Create promo response was empty.");
                promo = created.Promo;
                if (activateAfterCreate)
                {
                    // Record the ID before activation so failures can safely be retried.
                    pendingActivationPromoId = promo.Id;
                    pendingActivationClubId = clubId;
                    var activated = await manager.ActivateClubPromoAsync(clubId, promo.Id);
                    promo = activated.Promo;
                }
            }

            if (activateAfterCreate)
            {
                if (!string.Equals(promo.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Promo is not ACTIVE.");
                pendingActivationPromoId = null;
                pendingActivationClubId = null;
            }
            Debug.Log("[Promos] " + (activateAfterCreate ? "Promo activated: " : "Draft created: ") + promo.Id);
            RefreshPromosManagement(clubId);
            if (this != null && isActiveAndEnabled && version == viewVersion)
                gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            bool activationPending = activateAfterCreate && !string.IsNullOrEmpty(pendingActivationPromoId);
            string message = activationPending
                ? "Promo created, but activation is not confirmed. Tap Create Promo again to retry the same promo. " + e.Message
                : e.Message;
            Debug.LogError("[Promos] " + message);
            // Include the created draft in the management list even after activation failure.
            RefreshPromosManagement(clubId);
            if (this != null && isActiveAndEnabled && version == viewVersion)
                ShowError(message);
        }
        finally
        {
            saving = false;
            if (this != null) UpdateControls();
        }
    }

    private void RefreshPromosManagement(string clubId)
    {
        if (this != null && PromosManagementScreen != null &&
            PromosManagementScreen.isActiveAndEnabled && PromosManagementScreen.ClubId == clubId)
            PromosManagementScreen.Reload();
    }

    private void UpdateControls()
    {
        bool enabled = !saving && !loadingEdit && string.IsNullOrEmpty(pendingActivationPromoId);

        foreach (var binding in buttonBindings)
        {
            if (binding.Button != null)
                binding.Button.interactable = enabled;
        }

        if (PromNameInputField != null)
            PromNameInputField.interactable = enabled;

        if (ScopeDropDown != null)
            ScopeDropDown.interactable = enabled;

        SetPanelControls(ConfigurePanel, enabled);
        SetPanelControls(SchedulePanel, enabled);
        SetPanelControls(ReviewPanel, enabled);

        if (TimerDurationMinutes_InputField != null)
            TimerDurationMinutes_InputField.interactable = enabled;

        if (Metric_DropDown != null)
            Metric_DropDown.interactable = enabled;

        if (TargetMember_InputField != null)
            TargetMember_InputField.interactable = enabled;

        if (RepeatDaysGrid != null)
            RepeatDaysGrid.SetBusy(!enabled);

        // Retry and close remain available when a created promo is awaiting activation.
        if (CratePromoButton != null) CratePromoButton.interactable = !saving && !loadingEdit;
        if (BackButton != null) BackButton.interactable = !saving && !loadingEdit;
        if (CancelButton != null) CancelButton.interactable = !saving && !loadingEdit;
        if (ConfigureCancelButton != null) ConfigureCancelButton.interactable = !saving && !loadingEdit;
        if (ScheduleCancelButton != null) ScheduleCancelButton.interactable = !saving && !loadingEdit;
        if (ReviewCancelButton != null) ReviewCancelButton.interactable = !saving && !loadingEdit;
    }

    private static void SetPanelControls(GameObject panel, bool enabled)
    {
        if (panel == null) return;

        foreach (var input in panel.GetComponentsInChildren<InputField>(true))
            input.interactable = enabled;

        foreach (var dropdown in panel.GetComponentsInChildren<Dropdown>(true))
            dropdown.interactable = enabled;

        foreach (var toggle in panel.GetComponentsInChildren<Toggle>(true))
            toggle.interactable = enabled;
    }

    private void Close()
    {
        if (!saving && !loadingEdit)
            gameObject.SetActive(false);
    }

    private void ShowError(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            if (InformationPrefabScript.Instance != null)
                InformationPrefabScript.Instance.ShowMessage(message);
            else
                Debug.LogWarning("[Promos] " + message);
        }
    }
}