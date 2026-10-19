using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPrefab : MonoBehaviour
{
    public Button Leaderboardprefab_Button;
    public Text LeaderboardName_Text;
    public Text StartEnd_DateTime_DoubleScoreTime_Text;
    public Text Variant_Text;
    public Text BB_AnteText;
    public Text TotalPrizePool_Text;
    public Text Status;
    public ClubLeaderboardData Data { get; private set; }

    private Action<ClubLeaderboardData> onTap;

    private void Awake()
    {
        if (Leaderboardprefab_Button != null)
        {
            Leaderboardprefab_Button.onClick.AddListener(
                LeaderboardButtonOnTap);
        }
    }

    public void SetData(
        ClubLeaderboardData data,
        Action<ClubLeaderboardData> callback = null)
    {
        Data = data;
        onTap = callback;

        SetText(LeaderboardName_Text, data != null ? data.Name : null);

        if (data == null)
        {
            SetText(StartEnd_DateTime_DoubleScoreTime_Text, null);
            SetText(Variant_Text, null);
            SetText(BB_AnteText, null);
            SetText(TotalPrizePool_Text, null);
            return;
        }

        string dates = FormatDate(data.StartAt) +"-"+ FormatDate(data.EndAt);

        if (data.DoubleScores)
        {
            dates += " (Double score: " +
                     data.DoubleScoreHour.ToString(
                         "00", CultureInfo.InvariantCulture) +
                     ":00)";
        }

        SetText(StartEnd_DateTime_DoubleScoreTime_Text, dates);

        var variants = new List<string>();

        if (data.VariantFilter != null)
        {
            foreach (string variant in data.VariantFilter)
            {
                if (!string.IsNullOrWhiteSpace(variant))
                    variants.Add(variant);
            }
        }

        SetText(Variant_Text,variants.Count > 0 ? "Games: " + string.Join(", ", variants): "-");

        SetText(BB_AnteText, "BB/Ante range: " + FormatAmount(data.BbMin) +  " - " + FormatAmount(data.BbMax));

        SetText(TotalPrizePool_Text,FormatAmount(data.TotalPrizePool));
        Status.text = data.Status;
    }

    private void LeaderboardButtonOnTap()
    {
        if (Data != null)
            onTap?.Invoke(Data);
    }

    private static string FormatDate(string value)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset date))
        {
            return "-";
        }

        return date.ToLocalTime().ToString(
            "MM/dd HH:mm",
            CultureInfo.InvariantCulture);
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("#,0.##", CultureInfo.InvariantCulture);
    }

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void OnDestroy()
    {
        if (Leaderboardprefab_Button != null)
        {
            Leaderboardprefab_Button.onClick.RemoveListener(
                LeaderboardButtonOnTap);
        }
    }
}