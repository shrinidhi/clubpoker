using ClubPoker.Networking.Models;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class Days_7SessionPrefab : MonoBehaviour
{
    public Text DateText;
    public Text VariantName;
    public Text ClubName;
    public Text Time;
    public Text Blind;
    public Text Chip;
    public Button prefabButton;

    private CareerSessionData sessionData;

    [HideInInspector]
    public GameDataScript GameDataScreen;

    private void Awake()
    {
        if (prefabButton != null)
            prefabButton.onClick.AddListener(PrefabButtonOnTap);
    }

    public void SetData(CareerSessionData data)
    {
        sessionData = data;

        SetText(DateText, "-");
        SetText(Time, "-");
        SetText(VariantName, "-");
        SetText(ClubName, "-");
        SetText(Blind, "-");
        SetText(Chip, "-");

        if (prefabButton != null)
            prefabButton.interactable = data != null;

        if (data == null) return;

        if (!string.IsNullOrWhiteSpace(data.Date) &&
            DateTimeOffset.TryParse(
                data.Date,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset sessionDate))
        {
            // UTC API date ko device ke local time mein display karo.
            DateTime localDate = sessionDate.LocalDateTime;

            SetText(DateText, localDate.ToString(
                "dd/MM", CultureInfo.InvariantCulture));

            SetText(Time, localDate.ToString(
                "HH:mm", CultureInfo.InvariantCulture));
        }
        else
        {
            Debug.LogWarning("[Career] Invalid session date: " + data.Date);
        }

        SetText(VariantName, GetVariantName(data.Variant));

        SetText(ClubName,
            !string.IsNullOrWhiteSpace(data.ClubName)
                ? data.ClubName
                : !string.IsNullOrWhiteSpace(data.TableName)
                    ? data.TableName
                    : "-");

        SetText(Blind,
            string.IsNullOrWhiteSpace(data.BlindsLabel)
                ? "-"
                : data.BlindsLabel);

        SetText(Chip, FormatWinnings(data.Winnings));
    }

    private void PrefabButtonOnTap()
    {
        if (sessionData == null)
        {
            Debug.LogError("Career session data not available");
            return;
        }

        if (string.IsNullOrWhiteSpace(sessionData.TableId))
        {
            Debug.LogError("Career session table ID not available");
            return;
        }

        if (GameDataScreen == null)
        {
            Debug.LogError("GameDataScreen not assigned");
            return;
        }

        GameDataScreen.ShowGameData(sessionData);
    }

    private string FormatWinnings(int winnings)
    {
        string value = winnings.ToString(CultureInfo.InvariantCulture);
        return winnings > 0 ? "+" + value : value;
    }

    private string GetVariantName(string variant)
    {
        switch ((variant ?? "").Trim().ToLowerInvariant())
        {
            case "nlh":
            case "texas_holdem":
                return "NLH";

            case "omaha":
            case "plo4":
                return "PLO4";

            case "omaha_six":
            case "plo6":
                return "PLO6";

            default:
                return string.IsNullOrWhiteSpace(variant) ? "-" : variant;
        }
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private void OnDestroy()
    {
        if (prefabButton != null)
            prefabButton.onClick.RemoveListener(PrefabButtonOnTap);
    }
}