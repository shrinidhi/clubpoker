using System;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;
using ClubPoker.Core;

public class SearchFriendPrefab : MonoBehaviour
{
    public Text PlayerName;
    public Text Status;
    public Button AddFriend;

    public FriendSearchPlayerData Data { get; private set; }

    private Action<SearchFriendPrefab> onAdd;
    private bool busy;
    private bool isSelf;

    private void Awake()
    {
        if (AddFriend != null)
            AddFriend.onClick.AddListener(AddFriendOnTap);
    }

    public void SetData(
        FriendSearchPlayerData data,
        Action<SearchFriendPrefab> callback,
        bool self = false)
    {
        Data = data;
        onAdd = callback;
        isSelf = self;
        busy = false;

        if (PlayerName != null)
        {
            PlayerName.supportRichText = false;
            PlayerName.text = data == null
                ? "-"
                : !string.IsNullOrWhiteSpace(data.Username)
                    ? data.Username
                    : data.Nickname ?? "-";
        }

        RefreshStatus();
    }

    public void SetBusy(bool value)
    {
        busy = value;
        RefreshStatus();
    }

    public void SetFriendStatus(string status)
    {
        if (Data != null)
            Data.FriendStatus = status;

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        string key = (Data?.FriendStatus ?? "").Trim().ToLowerInvariant();
        bool showAdd = Data != null && !isSelf && key == "none";

        if (AddFriend != null)
        {
            AddFriend.gameObject.SetActive(showAdd);
            AddFriend.interactable = showAdd && !busy && onAdd != null;
        }

        if (Status == null)
            return;

        Status.supportRichText = false;
        Status.gameObject.SetActive(!showAdd);

        if (isSelf)
        {
            Status.text = Loc.Get("friend.you");
            return;
        }

        switch (key)
        {
            case "pending_incoming":
                Status.text = Loc.Get("friend.check_request");
                break;
            case "pending_outgoing":
                Status.text = Loc.Get("pending");
                break;
            case "friend":
            case "friends":
            case "accepted":
                Status.text = Loc.Get("friend.friends");
                break;
            case "none":
                Status.text = "";
                break;
            default:
                Status.text = string.IsNullOrWhiteSpace(key)
                    ? "-"
                    : key.Replace("_", " ");
                break;
        }
    }

    private void AddFriendOnTap()
    {
        if (busy || isSelf || Data == null ||
            !string.Equals(Data.FriendStatus?.Trim(), "none",
                StringComparison.OrdinalIgnoreCase))
            return;

        onAdd?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (AddFriend != null)
            AddFriend.onClick.RemoveListener(AddFriendOnTap);
    }
}
