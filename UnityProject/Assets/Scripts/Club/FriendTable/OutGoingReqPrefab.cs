using System;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class OutGoingReqPrefab : MonoBehaviour
{
    public Text PlayerName;
    public Button CancelButton;

    public FriendRequestData Data { get; private set; }

    private Action<OutGoingReqPrefab> onAction;
    private bool busy;

    private void Awake()
    {
        if (CancelButton != null)
            CancelButton.onClick.AddListener(CancelButtonOnTap);
    }

    public void SetData(FriendRequestData data, Action<OutGoingReqPrefab> callback = null)
    {
        Data = data;
        onAction = callback;
        busy = false;

        if (PlayerName != null)
        {
            PlayerName.supportRichText = false;
            PlayerName.text = data == null
                ? "-"
                : !string.IsNullOrWhiteSpace(data.Nickname)
                    ? data.Nickname
                    : !string.IsNullOrWhiteSpace(data.Username)
                        ? data.Username
                        : "-";
        }

        UpdateControls();
    }

    public void SetBusy(bool value)
    {
        busy = value;
        UpdateControls();
    }

    private void UpdateControls()
    {
        bool enabled = !busy && Data != null &&
                       !string.IsNullOrWhiteSpace(Data.Id) && onAction != null;
        if (CancelButton != null)
            CancelButton.interactable = enabled;
    }

    private void CancelButtonOnTap()
    {
        if (!busy && Data != null)
            onAction?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (CancelButton != null)
            CancelButton.onClick.RemoveListener(CancelButtonOnTap);
    }
}
