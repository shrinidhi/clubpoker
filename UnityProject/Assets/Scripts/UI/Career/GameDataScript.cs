using System;
using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class GameDataScript : MonoBehaviour
{
    public Button CloseButton;
    public Image ClubImage;
    public Text ClubName;
    public Text ClubID;
    public Text VariantName;
    public Text TableName;
    public Text TableId;
    public Text DateTime;
    public Text Blind;
    public Text RunningTime;

    public Text BuyInCount;
    public Text HandsCount;
    public Text WinningCount;
    public Text InsuranceCount;

    public Transform HandHistoryContent;
    public GameObject HandHistoryPrefab;

    [Header("Optional")]
    public Text ErrorText;
    public GameObject LoadingPanel;

    private string currentTableId;
    private CareerSessionData currentSession;
    private bool isLoading;
    public CareerHandHistoryPanel CareerHandHistoryPanel;
    private void Start()
    {
        if (CloseButton != null)
        {
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
            CloseButton.onClick.AddListener(CloseButtonOnTap);
        }
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
    }

    public void ShowGameData(CareerSessionData session)
    {
        if (session == null || string.IsNullOrEmpty(session.TableId))
        {
            ShowError("Session data not available");
            return;
        }

        currentSession = session;
        currentTableId = session.TableId;

        gameObject.SetActive(true);

        SetSessionData(session);
        LoadHandHistory().Forget();
    }

    public void ShowGameData(string tableId)
    {
        if (string.IsNullOrEmpty(tableId))
        {
            ShowError("Table ID not available");
            return;
        }

        currentSession = null;
        currentTableId = tableId;

        gameObject.SetActive(true);
        LoadHandHistory().Forget();
    }

    private void SetSessionData(CareerSessionData session)
    {
        if (session == null)
            return;

        if (ClubName != null)
            ClubName.text = string.IsNullOrEmpty(session.ClubName) ? "-" : session.ClubName;

        if (ClubID != null)
            ClubID.text = string.IsNullOrEmpty(session.ClubId) ? "-" : session.ClubId;

        if (VariantName != null)
            VariantName.text = GetVariantName(session.Variant);

        if (TableName != null)
            TableName.text = string.IsNullOrEmpty(session.TableName) ? "-" : session.TableName;

        if (TableId != null)
            TableId.text = string.IsNullOrEmpty(session.TableId) ? "-" : session.TableId;

        if (DateTime != null)
            DateTime.text = string.IsNullOrEmpty(session.Date) ? "-" : session.Date;

        if (Blind != null)
            Blind.text = string.IsNullOrEmpty(session.BlindsLabel) ? "-" : session.BlindsLabel;

        if (RunningTime != null)
            RunningTime.text = string.IsNullOrEmpty(session.DurationLabel) ? "-" : session.DurationLabel;

        if (BuyInCount != null)
            BuyInCount.text = session.BuyIn.ToString();

        if (HandsCount != null)
            HandsCount.text = session.Hands.ToString();

        if (WinningCount != null)
            WinningCount.text = FormatNetResult(session.Winnings);

        if (InsuranceCount != null)
            InsuranceCount.text = "0";
    }

    private async UniTaskVoid LoadHandHistory()
    {
        if (isLoading)
            return;

        if (string.IsNullOrEmpty(currentTableId))
        {
            ShowError("Table ID not available");
            return;
        }

        if (AuthManager.Instance == null)
        {
            ShowError("AuthManager not available");
            return;
        }

        isLoading = true;
        ClearError();
        ClearHandHistory();

        if (LoadingPanel != null)
            LoadingPanel.SetActive(true);

        try
        {
            CareerHandHistoryData historyData =
                await AuthManager.Instance.GetCareerHandHistoryAsync(
                    currentTableId,
                    50
                );

            if (this == null)
                return;

            if (historyData == null)
            {
                ShowError("Hand history could not be loaded");
                return;
            }

            List<CareerHandHistoryItem> hands = historyData.Items;

            if (HandsCount != null)
                HandsCount.text = (hands?.Count ?? 0).ToString();

            if (hands == null || hands.Count == 0)
            {
                ShowError("No hand history found");
                return;
            }

            int totalNetResult = 0;

            foreach (CareerHandHistoryItem hand in hands)
            {
                totalNetResult += hand.NetResult;
                GenerateHandHistoryItem(hand, hands);
            }

            if (WinningCount != null)
            {
                int winning = currentSession != null
                    ? currentSession.Winnings
                    : totalNetResult;

                WinningCount.text = FormatNetResult(winning);
            }

            CareerHandHistoryItem firstHand = hands[0];

            if (currentSession == null)
            {
                if (VariantName != null)
                    VariantName.text = GetVariantName(firstHand.Variant);

                if (TableName != null)
                    TableName.text = string.IsNullOrEmpty(firstHand.TableName) ? "-" : firstHand.TableName;

                if (TableId != null)
                    TableId.text = string.IsNullOrEmpty(firstHand.TableId) ? "-" : firstHand.TableId;

                if (DateTime != null)
                    DateTime.text = FormatDate(firstHand.PlayedAt);
            }

            Debug.Log(
                "Game data loaded | Table: " +
                currentTableId +
                " | Hands: " +
                hands.Count +
                " | Net result: " +
                totalNetResult
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Game data load failed: " + e);
            ShowError(string.IsNullOrEmpty(e.Message) ? "Hand history could not be loaded" : e.Message);
        }
        finally
        {
            isLoading = false;

            if (LoadingPanel != null)
                LoadingPanel.SetActive(false);
        }
    }

    private void GenerateHandHistoryItem(
     CareerHandHistoryItem hand,
     List<CareerHandHistoryItem> allHands)
    {
        if (HandHistoryContent == null ||
            HandHistoryPrefab == null)
        {
            Debug.LogError(
                "HandHistoryContent or HandHistoryPrefab missing"
            );

            return;
        }

        GameObject obj =
            Instantiate(
                HandHistoryPrefab,
                HandHistoryContent
            );

        CareerHandHistoryPrefab prefab =
            obj.GetComponent<CareerHandHistoryPrefab>();

        if (prefab != null)
        {
            prefab.SetData(
                hand,
                allHands,
                CareerHandHistoryPanel
            );
        }
        else
        {
            Destroy(obj);
        }
    }

    private void ClearHandHistory()
    {
        if (HandHistoryContent == null)
            return;

        for (int i = HandHistoryContent.childCount - 1; i >= 0; i--)
            Destroy(HandHistoryContent.GetChild(i).gameObject);
    }

    private string FormatNetResult(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
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
                return string.IsNullOrEmpty(variant) ? "-" : variant;
        }
    }

    private string FormatDate(string date)
    {
        if (string.IsNullOrEmpty(date))
            return "-";

        if (System.DateTime.TryParse(
            date,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out System.DateTime parsedDate))
        {
            return parsedDate.ToLocalTime().ToString("MM/dd HH:mm");
        }

        return date;
    }

    private void ShowError(string message)
    {
        if (ErrorText != null)
            ErrorText.text = message;

        Debug.LogWarning(message);
    }

    private void ClearError()
    {
        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void CloseButtonOnTap()
    {
        ClearHandHistory();
        gameObject.SetActive(false);
    }
}