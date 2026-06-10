using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Networking.Models;
using System;
using System.Globalization;

public class ClubTablePrefabScript : MonoBehaviour
{
    public TextMeshProUGUI TableName_Text;
    public TextMeshProUGUI Variant_Text;
    public TextMeshProUGUI SB_BB_Text;
    public TextMeshProUGUI PlayerSeat_Text;
    public TextMeshProUGUI RunnigTime_Text;

    private ClubTableData tableData;

    public Button DeleteButton;
    public Button ExtendButton;
    public Button JoinButton;

    private Action<ClubTableData> onDeleteClick;
    private Action<ClubTableData> onExtendClick;
    private Action<ClubTableData> onJoinClick;

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
            SB_BB_Text.text = $"SB {data.SmallBlind} / BB {data.BigBlind}";

        if (PlayerSeat_Text != null)
            PlayerSeat_Text.text = $"{data.PlayerCount}/{data.MaxSeats} Seated";

        UpdateRunningTime(data);

        bool isCreator =
            ClubContext.UserRole == ClubRole.Creator ||
            ClubContext.UserRole == ClubRole.Agent;

        if (DeleteButton != null)
            DeleteButton.gameObject.SetActive(isCreator);

        if (ExtendButton != null)
            ExtendButton.gameObject.SetActive(isCreator);

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

    private void UpdateRunningTime(ClubTableData data)
    {
        if (RunnigTime_Text == null)
            return;

        if (data == null || !data.Live || string.IsNullOrEmpty(data.CreatedAt))
        {
            RunnigTime_Text.text = "";
            return;
        }

        RunnigTime_Text.text = GetRunningTimeText(data.CreatedAt);
    }

    private string GetRunningTimeText(string createdAt)
    {
        if (!DateTime.TryParse(
                createdAt,
                null,
                DateTimeStyles.AdjustToUniversal,
                out DateTime createdTime))
        {
            Debug.LogError("Invalid createdAt: " + createdAt);
            return "";
        }

        TimeSpan elapsed = DateTime.UtcNow - createdTime.ToUniversalTime();

        if (elapsed.TotalMinutes < 0)
            elapsed = TimeSpan.Zero;

        int totalMinutes = Mathf.FloorToInt((float)elapsed.TotalMinutes);

        if (totalMinutes >= 60)
        {
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            return $"{hours}h {minutes}m running";
        }

        return $"{totalMinutes}m running";
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
}