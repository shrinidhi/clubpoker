using System;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class Friendprefab : MonoBehaviour
{
    public Text FriendName;
    public Button RemoveButton;

    public FriendListItemData Data { get; private set; }

    private Action<Friendprefab> onRemove;
    private bool busy;

    private void Awake()
    {
        if (RemoveButton != null)
            RemoveButton.onClick.AddListener(RemoveButtonOnTap);
    }

    public void SetData(FriendListItemData data, Action<Friendprefab> callback)
    {
        Data = data;
        onRemove = callback;
        busy = false;

        if (FriendName != null)
        {
            FriendName.supportRichText = false;
            FriendName.text = data == null ? "-" :
                !string.IsNullOrWhiteSpace(data.Nickname) ? data.Nickname :
                data.Username ?? "-";
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
        if (RemoveButton != null)
            RemoveButton.interactable = !busy && Data != null &&
                !string.IsNullOrWhiteSpace(Data.FriendshipId) && onRemove != null;
    }

    private void RemoveButtonOnTap()
    {
        if (!busy && Data != null)
            onRemove?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (RemoveButton != null)
            RemoveButton.onClick.RemoveListener(RemoveButtonOnTap);
    }
}
