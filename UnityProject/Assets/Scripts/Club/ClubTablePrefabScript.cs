using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;
using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;

public class ClubTablePrefabScript : MonoBehaviour
{
    public Text TableName_Text;
    public Text Variant_Text;
    public Text SB_BB_Text;
    public Text PlayerSeat_Text;

    [Header("Remaining Time")]
    public Text RunnigTime_Text;

    public Image VariantCard;
    public List<Sprite> VariantCardSprite;

    private ClubTableData tableData;

    public Button DeleteButton;
    public Button ExtendButton;
    public Button JoinButton;

    private Action<ClubTableData> onDeleteClick;
    private Action<ClubTableData> onExtendClick;
    private Action<ClubTableData> onJoinClick;

    private Coroutine remainingTimeCoroutine;

    public void Setup(
        ClubTableData data,
        Action<ClubTableData> deleteCallback = null,
        Action<ClubTableData> extendCallback = null,
        Action<ClubTableData> joinCallback = null)
    {
        tableData = data;

        onDeleteClick = deleteCallback;
        onExtendClick = extendCallback;
        onJoinClick = joinCallback;

        if (TableName_Text != null)
            TableName_Text.text = data.Name;

        if (Variant_Text != null)
            Variant_Text.text = ClubPoker.Core.VariantUtils.ToDisplayName(data.Variant);

        if (SB_BB_Text != null)
            SB_BB_Text.text = $"{data.SmallBlind}/{data.BigBlind}";

        if (PlayerSeat_Text != null)
            PlayerSeat_Text.text = $"{data.PlayerCount}/{data.MaxSeats}";

        SetVariantCard(data.Variant);
        StartRemainingTimeTimer();

        bool isCreator =
            ClubContext.UserRole == ClubRole.Creator ||
            ClubContext.UserRole == ClubRole.Agent;

        if (JoinButton != null)
            JoinButton.gameObject.SetActive(true);

        if (DeleteButton != null)
        {
            DeleteButton.onClick.RemoveAllListeners();
            DeleteButton.onClick.AddListener(OnDeleteButtonClick);
        }

        if (ExtendButton != null)
        {
            ExtendButton.onClick.RemoveAllListeners();
            ExtendButton.onClick.AddListener(OnExtendButtonClick);
        }

        if (JoinButton != null)
        {
            JoinButton.onClick.RemoveAllListeners();
            JoinButton.onClick.AddListener(OnJoinButtonClick);
        }
    }

    private void StartRemainingTimeTimer()
    {
        if (remainingTimeCoroutine != null)
        {
            StopCoroutine(remainingTimeCoroutine);
            remainingTimeCoroutine = null;
        }

        UpdateRemainingTime();

        if (tableData == null)
            return;

        if (!tableData.Live)
            return;

        if (string.IsNullOrEmpty(tableData.ExpiresAt))
            return;

        remainingTimeCoroutine = StartCoroutine(RemainingTimeRoutine());
    }

    private IEnumerator RemainingTimeRoutine()
    {
        while (true)
        {
            bool hasTimeRemaining = UpdateRemainingTime();

            if (!hasTimeRemaining)
            {
                remainingTimeCoroutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private bool UpdateRemainingTime()
    {
        if (RunnigTime_Text == null)
            return false;

        if (tableData == null)
        {
            RunnigTime_Text.text = "";
            return false;
        }

        if (string.IsNullOrEmpty(tableData.ExpiresAt))
        {
            RunnigTime_Text.text = "";
            return false;
        }

        if (!DateTimeOffset.TryParse(
                tableData.ExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset expiryTime))
        {
            Debug.LogError("Invalid expiresAt: " + tableData.ExpiresAt);

            RunnigTime_Text.text = "";
            return false;
        }

        TimeSpan remaining =
            expiryTime.UtcDateTime - DateTime.UtcNow;

        if (remaining.TotalSeconds <= 0)
        {
            RunnigTime_Text.text = "0m left";
            return false;
        }

        int totalMinutes =
            Mathf.RoundToInt((float)remaining.TotalMinutes);

        if (totalMinutes <= 0)
            totalMinutes = 1;

        if (totalMinutes >= 60)
        {
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            if (minutes == 0)
            {
                RunnigTime_Text.text =
                    $"{hours}h left";
            }
            else
            {
                RunnigTime_Text.text =
                    $"{hours}h {minutes}m left";
            }
        }
        else
        {
            RunnigTime_Text.text =
                $"{totalMinutes}m left";
        }

        return true;
    }

    public void SetVariantCard(string variant)
    {
        if (VariantCard == null ||
            VariantCardSprite == null ||
            VariantCardSprite.Count == 0)
            return;

        switch (variant?.ToLower())
        {
            case "nlh":
            case "texas_holdem":

                if (VariantCardSprite.Count > 0)
                    VariantCard.sprite = VariantCardSprite[0];

                break;

            case "plo4":
            case "omaha":

                if (VariantCardSprite.Count > 1)
                    VariantCard.sprite = VariantCardSprite[1];

                break;

            case "plo5":

                if (VariantCardSprite.Count > 2)
                    VariantCard.sprite = VariantCardSprite[2];

                break;

            case "plo6":
            case "omaha_six":

                if (VariantCardSprite.Count > 3)
                    VariantCard.sprite = VariantCardSprite[3];

                break;

            default:

                if (VariantCardSprite.Count > 0)
                    VariantCard.sprite = VariantCardSprite[0];

                break;
        }
    }

    private void OnDeleteButtonClick()
    {
        if (tableData == null)
            return;

        onDeleteClick?.Invoke(tableData);
    }

    private void OnExtendButtonClick()
    {
        if (tableData == null)
            return;

        onExtendClick?.Invoke(tableData);
    }

    private void OnJoinButtonClick()
    {
        if (tableData == null)
            return;

        onJoinClick?.Invoke(tableData);
    }

    private void OnDisable()
    {
        if (remainingTimeCoroutine != null)
        {
            StopCoroutine(remainingTimeCoroutine);
            remainingTimeCoroutine = null;
        }
    }
}