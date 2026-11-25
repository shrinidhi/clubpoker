using ClubPoker.Networking.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

public class Mytableprefab : MonoBehaviour
{
    public Text TableName;
    public Text JoinCode;
    public Button CopyJoinCodeButton;
    public Text Variant;
    public Text PlayerCount;
    public Text SB_BB_Blind;
    public Button JoinButton;
    public Button DeleteButt;

    private string tableId;
    private string shareCode;

    private Action<string> onJoin;
    private Action<string> onDelete;

    public void SetData(
        MyLobbyTableItem data,
        Action<string> joinCallback,
        Action<string> deleteCallback)
    {
        if (data == null) return;

        tableId = data.Id;
        shareCode = data.ShareCode;

        onJoin = joinCallback;
        onDelete = deleteCallback;

        if (TableName != null)
            TableName.text = data.Name;

        if (JoinCode != null)
            JoinCode.text = data.ShareCode;

        if (Variant != null)
            Variant.text = FormatVariant(data.Variant);

        if (PlayerCount != null)
            PlayerCount.text = data.CurrentPlayers + "/" + data.MaxPlayers;

        if (SB_BB_Blind != null)
            SB_BB_Blind.text = data.SmallBlind + "/" + data.BigBlind;

        // Copy Code Button
        if (CopyJoinCodeButton != null)
        {
            CopyJoinCodeButton.onClick.RemoveAllListeners();
            CopyJoinCodeButton.onClick.AddListener(CopyCode);
        }

        // Join Button
        if (JoinButton != null)
        {
            JoinButton.onClick.RemoveAllListeners();
            JoinButton.onClick.AddListener(() => onJoin?.Invoke(tableId));
        }

        // Delete Button
        if (DeleteButt != null)
        {
            DeleteButt.onClick.RemoveAllListeners();
            DeleteButt.onClick.AddListener(() => onDelete?.Invoke(tableId));
        }
    }

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(shareCode))
            return;

        GUIUtility.systemCopyBuffer = shareCode;
        Debug.Log("Join code copied: " + shareCode);
    }

    private static string FormatVariant(string value)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "texas_holdem":
            case "nlh": return "NLH";
            case "omaha":
            case "plo":
            case "plo4": return "PLO4";
            case "omaha_five":
            case "plo5": return "PLO5";
            case "omaha_six":
            case "plo6": return "PLO6";
            case "aof": return "AOF";
            default: return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}