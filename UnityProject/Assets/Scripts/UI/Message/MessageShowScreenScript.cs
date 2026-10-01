using System;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class MessageShowScreenScript : MonoBehaviour
{
    public Text TitleText;
    public Text ContentText;
    public Button CloseButton;
    public Button DeleteMsgButton;
    private Action onDelete;

    private void Awake()
    {
        if (CloseButton != null) CloseButton.onClick.AddListener(Hide);
        if (DeleteMsgButton != null) DeleteMsgButton.onClick.AddListener(Delete);
    }
    private void OnDestroy()
    {
        if (CloseButton != null) CloseButton.onClick.RemoveListener(Hide);
        if (DeleteMsgButton != null) DeleteMsgButton.onClick.RemoveListener(Delete);
    }
    public void Show(NotificationData data, Action deleteAction)
    {
        onDelete = deleteAction;
        if (TitleText != null) { TitleText.supportRichText = false; TitleText.text = data.Title ?? ""; }
        if (ContentText != null) { ContentText.supportRichText = false; ContentText.text = data.Content ?? ""; }
        gameObject.SetActive(true);
    }
    public void Hide() { gameObject.SetActive(false); }
    public void SetBusy(bool busy) { if (DeleteMsgButton != null) DeleteMsgButton.interactable = !busy; }
    private void Delete() { onDelete?.Invoke(); }
}
