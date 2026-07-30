using ClubPoker.Networking.Models;
using System.Collections;
using System.Collections.Generic;
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

    public void SetData(CareerSessionData data)
    {
        sessionData = data;

        if (data == null)
            return;
        string dateTime = data.Date;

        string[] parts = dateTime.Split(' ');

        DateText.text = parts[0];
        Time.text = parts[1];
        // if (DateText != null)
        // DateText.text = string.IsNullOrEmpty(data.Date) ? "-" : data.Date;

        if (VariantName != null)
            VariantName.text = GetVariantName(data.Variant);

        if (ClubName != null)
            ClubName.text = !string.IsNullOrEmpty(data.ClubName)
                ? data.ClubName
                : !string.IsNullOrEmpty(data.TableName)
                    ? data.TableName
                    : "-";

        //if (Time != null)
        //Time.text = string.IsNullOrEmpty(data.DurationLabel)
        //   ? "-"
        // : data.DurationLabel;

        if (Blind != null)
            Blind.text = string.IsNullOrEmpty(data.BlindsLabel)
                ? "-"
                : data.BlindsLabel;

        if (Chip != null)
            Chip.text = FormatWinnings(data.Winnings);


    }

    private void Start()
    {
        prefabButton.onClick.AddListener(PrefabButtonOnTap);
    }

    private void PrefabButtonOnTap()
    {
        Debug.Log("ButtonTap");
        if (sessionData == null)
        {
            Debug.LogError("Career session data not available");
            return;
        }

        if (string.IsNullOrEmpty(sessionData.TableId))
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
        return winnings > 0
            ? "+" + winnings
            : winnings.ToString();
    }

    private string GetVariantName(string variant)
    {
        switch (variant)
        {
            case "texas_holdem":
                return "NLH";

            case "omaha":
            case "plo4":
                return "PLO4";

            case "omaha_six":
            case "plo6":
                return "PLO6";

            default:
                return string.IsNullOrEmpty(variant)
                    ? "-"
                    : variant;
        }
    }

}
