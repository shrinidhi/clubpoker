using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class FriendTablePrefab : MonoBehaviour
{
    public Text TableName;
    public Text DetailText;
    public Button JoinTable;

    public FriendTableData Data { get; private set; }

    private Action<FriendTableData> onJoin;

    private void Awake()
    {
        if (JoinTable != null)
            JoinTable.onClick.AddListener(JoinTableOnTap);
    }

    public void SetData(FriendTableData data, Action<FriendTableData> callback)
    {
        Data = data;
        onJoin = callback;

        SetText(TableName, data?.Name);
        SetText(DetailText, "@" + data?.HostUsername + " . " + FormatVariant(data?.Variant) + " . " + FormatAmount(data.SmallBlind) + "/" + FormatAmount(data.BigBlind) + " . " + data.CurrentPlayers + "/" + data.MaxPlayers + " seated");
        
        string status = (data?.Status ?? "").Trim().ToLowerInvariant();
        bool closed = status == "closed" || status == "cancelled" ||
                      status == "completed" || status == "ended";

        if (JoinTable != null)
            JoinTable.interactable = data != null && !closed &&
                !string.IsNullOrWhiteSpace(data.Id) && onJoin != null;
    }

    private void JoinTableOnTap()
    {
        if (Data != null && JoinTable != null && JoinTable.interactable)
            onJoin?.Invoke(Data);
    }

    private static string FormatVariant(string value)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "texas_holdem":
            case "nlh": return "NLH";
            case "omaha":
            case "plo":
            case "plo4": return "PLO";
            case "omaha_five":
            case "plo5": return "PLO5";
            case "omaha_six":
            case "plo6": return "PLO6";
            case "aof": return "AOF";
            default: return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("#,0.##", CultureInfo.InvariantCulture);
    }

    private static void SetText(Text target, string value)
    {
        if (target == null) return;
        target.supportRichText = false;
        target.text = string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void OnDestroy()
    {
        if (JoinTable != null)
            JoinTable.onClick.RemoveListener(JoinTableOnTap);
    }
}
