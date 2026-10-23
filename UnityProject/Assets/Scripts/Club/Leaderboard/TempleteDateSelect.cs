using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;

public class TempleteDateSelect : MonoBehaviour
{
    public Button CloseButton;
    public Button ConfirmButton;

    public Text StartDateTime;
    public Button StartDateSelectButton;

    public Text EndDateTime;
    public Button EndDateSelectButton;

    [Header("Date Picker")]
    public AdminPosterDateSelectScript DatePicker;

    [Header("Optional")]
    public Text MessageText;

    public CreatedClubLeaderboardData CreatedLeaderboard
    {
        get;
        private set;
    }

    private LeaderboardTemplateScreen templateScreen;
    private CreateClubLeaderboardRequest selectedRequest;

    private string clubId;

    private DateTime? selectedStart;
    private DateTime? selectedEnd;

    private bool submitting;
    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (ConfirmButton != null)
            ConfirmButton.onClick.AddListener(ConfirmButtonOnTap);

        if (StartDateSelectButton != null)
        {
            StartDateSelectButton.onClick.AddListener(
                StartDateSelectButtonOnTap);
        }

        if (EndDateSelectButton != null)
        {
            EndDateSelectButton.onClick.AddListener(
                EndDateSelectButtonOnTap);
        }
    }

    public void Open(
        string selectedClubId,
        ClubLeaderboardData template,
        LeaderboardTemplateScreen owner)
    {
        if (submitting)
            return;

        if (string.IsNullOrWhiteSpace(selectedClubId) ||
            template == null)
        {
            Debug.LogError(
                "[Template Create] Select a valid club and template.");
            return;
        }

        if (!string.Equals(
                template.ClubId,
                selectedClubId,
                StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError(
                "[Template Create] The template belongs to a different club.");
            return;
        }

        clubId = selectedClubId;
        templateScreen = owner;

        // Copy settings so later UI changes cannot change this request.
        selectedRequest = CopyTemplate(template);

        selectedStart = null;
        selectedEnd = null;
        CreatedLeaderboard = null;

        ++viewVersion;

        gameObject.SetActive(true);

        SetMessage("");
        RefreshDates();
        RefreshControls();
    }

    private static CreateClubLeaderboardRequest CopyTemplate(
        ClubLeaderboardData template)
    {
        var prizes = new List<ClubLeaderboardPrize>();

        if (template.Prizes != null)
        {
            foreach (var prize in template.Prizes)
            {
                if (prize == null)
                    continue;

                prizes.Add(new ClubLeaderboardPrize
                {
                    Position = prize.Position,
                    Amount = prize.Amount
                });
            }
        }

        return new CreateClubLeaderboardRequest
        {
            Name = template.Name,
            Description = template.Description ?? "",
            Type = template.Type,

            VariantFilter = template.VariantFilter != null
                ? new List<string>(template.VariantFilter)
                : new List<string>(),

            BbMin = ToSliderValue(template.BbMin, "BB minimum"),
            BbMax = ToSliderValue(template.BbMax, "BB maximum"),

            BuyInMin = ToSliderValue(
                template.BuyInMin, "Buy-in minimum"),

            BuyInMax = ToSliderValue(
                template.BuyInMax, "Buy-in maximum"),

            DoubleScores = template.DoubleScores,
            DoubleScoreHour = template.DoubleScoreHour,

            AutoReward = template.AutoReward,
            Prizes = prizes
        };
    }

    private static int ToSliderValue(decimal value, string label)
    {
        if (value < 0 ||
            value > int.MaxValue ||
            decimal.Truncate(value) != value)
        {
            throw new InvalidOperationException(
                label + " must be a non-negative whole number.");
        }

        return decimal.ToInt32(value);
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    private void CloseButtonOnTap()
    {
        if (!submitting)
            gameObject.SetActive(false);
    }

    private void StartDateSelectButtonOnTap()
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

    private void EndDateSelectButtonOnTap()
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

        selectedStart = value;

        SetMessage("");
        RefreshDates();
        RefreshControls();
    }

    private void OnEndDateSelected(DateTime value)
    {
        if (!isActiveAndEnabled || submitting)
            return;

        selectedEnd = value;

        SetMessage("");
        RefreshDates();
        RefreshControls();
    }

    private void RefreshDates()
    {
        SetText(
            StartDateTime,
            selectedStart.HasValue
                ? selectedStart.Value.ToString(
                    "dd/MM/yyyy HH:mm",
                    CultureInfo.InvariantCulture)
                : Loc.Get("select_start_date"));

        SetText(
            EndDateTime,
            selectedEnd.HasValue
                ? selectedEnd.Value.ToString(
                    "dd/MM/yyyy HH:mm",
                    CultureInfo.InvariantCulture)
                : Loc.Get("select_end_date"));
    }

    private void RefreshControls()
    {
        if (CloseButton != null)
            CloseButton.interactable = !submitting;

        if (StartDateSelectButton != null)
            StartDateSelectButton.interactable = !submitting;

        if (EndDateSelectButton != null)
            EndDateSelectButton.interactable = !submitting;

        if (ConfirmButton != null)
        {
            ConfirmButton.interactable =
                !submitting &&
                selectedRequest != null &&
                selectedStart.HasValue &&
                selectedEnd.HasValue;
        }
    }

    private void ConfirmButtonOnTap()
    {
        if (submitting || selectedRequest == null)
            return;

        if (!selectedStart.HasValue || !selectedEnd.HasValue)
        {
            SetMessage(Loc.Get("promo.err_dates_missing"));
            return;
        }

        DateTime startUtc = DateTime.SpecifyKind(
            selectedStart.Value,
            DateTimeKind.Local).ToUniversalTime();

        DateTime endUtc = DateTime.SpecifyKind(
            selectedEnd.Value,
            DateTimeKind.Local).ToUniversalTime();

        if (startUtc <= DateTime.UtcNow)
        {
            SetMessage(Loc.Get("promo.err_start_past"));
            return;
        }

        if (endUtc <= startUtc)
        {
            SetMessage(Loc.Get("promo.err_end_before_start"));
            return;
        }

        selectedRequest.StartAt = startUtc.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture);

        selectedRequest.EndAt = endUtc.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture);

        CreateAsync(
            selectedRequest,
            clubId,
            viewVersion).Forget();
    }

    private async UniTask CreateAsync(
        CreateClubLeaderboardRequest request,
        string requestClubId,
        int version)
    {
        submitting = true;
        RefreshControls();
        SetMessage(Loc.Get("lb.creating"));

        CreateClubLeaderboardResponse response;

        try
        {
            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            response =
                await ClubManager.Instance.CreateClubLeaderboardAsync(
                    requestClubId,
                    request);

            if (response == null ||
                response.Leaderboard == null ||
                string.IsNullOrWhiteSpace(response.Leaderboard.Id))
            {
                throw new InvalidOperationException(
                    "The server did not return a leaderboard ID.");
            }
        }
        catch (Exception e)
        {
            if (this != null)
            {
                submitting = false;
                RefreshControls();

                if (isActiveAndEnabled && version == viewVersion)
                {
                    SetMessage(
                        Loc.Get("lb.create_failed", ("reason", e.Message)));
                }

                Debug.LogError(
                    "[Template Create] Request failed: " + e.Message);
            }

            return;
        }

        if (this == null)
            return;

        submitting = false;
        CreatedLeaderboard = response.Leaderboard;
        RefreshControls();

        if (LeaderboardManager.Instance != null &&
            ClubContext.ClubId == requestClubId)
        {
            LeaderboardManager.Instance.Reload();
        }

        if (!isActiveAndEnabled || version != viewVersion)
            return;

        // Close both popups after successful creation.
        if (templateScreen != null)
            templateScreen.gameObject.SetActive(false);

        if (this != null)
            gameObject.SetActive(false);
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

        if (MessageText == null && !string.IsNullOrEmpty(message))
            Debug.Log("[Template Create] " + message);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (ConfirmButton != null)
            ConfirmButton.onClick.RemoveListener(ConfirmButtonOnTap);

        if (StartDateSelectButton != null)
        {
            StartDateSelectButton.onClick.RemoveListener(
                StartDateSelectButtonOnTap);
        }

        if (EndDateSelectButton != null)
        {
            EndDateSelectButton.onClick.RemoveListener(
                EndDateSelectButtonOnTap);
        }
    }
}