using System;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class Incomingreqprefab : MonoBehaviour
{
    public Text PlayerName;
    public Button Req_AcceptButton;
    public Button Req_RejectButton;

    public FriendRequestData Data { get; private set; }

    private Action<Incomingreqprefab, bool> onAction;
    private bool busy;

    private void Awake()
    {
        if (Req_AcceptButton != null)
            Req_AcceptButton.onClick.AddListener(AcceptButtonOnTap);
        if (Req_RejectButton != null)
            Req_RejectButton.onClick.AddListener(RejectButtonOnTap);
    }

    public void SetData(FriendRequestData data, Action<Incomingreqprefab, bool> callback = null)
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
        if (Req_AcceptButton != null)
            Req_AcceptButton.interactable = enabled;
        if (Req_RejectButton != null)
            Req_RejectButton.interactable = enabled;
    }

    private void AcceptButtonOnTap()
    {
        if (!busy && Data != null)
            onAction?.Invoke(this, true);
    }

    private void RejectButtonOnTap()
    {
        if (!busy && Data != null)
            onAction?.Invoke(this, false);
    }

    private void OnDestroy()
    {
        if (Req_AcceptButton != null)
            Req_AcceptButton.onClick.RemoveListener(AcceptButtonOnTap);
        if (Req_RejectButton != null)
            Req_RejectButton.onClick.RemoveListener(RejectButtonOnTap);
    }
}
