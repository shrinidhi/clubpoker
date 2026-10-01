using System;
using System.Globalization;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class MessagePrefab : MonoBehaviour
{
    public Text TitleText;
    public Text ContentText;
    public Text DateText;
    public GameObject ReadDot;
    public Button PrefabButton;
    private NotificationData data;
    private Action<NotificationData> onOpen;

    private void Awake()
    {
        if (PrefabButton != null) PrefabButton.onClick.AddListener(Open);
    }
    private void OnDestroy()
    {
        if (PrefabButton != null) PrefabButton.onClick.RemoveListener(Open);
    }
    public void Setup(NotificationData notification, Action<NotificationData> onClick)
    {
        data = notification;
        onOpen = onClick;
        if (TitleText != null) { TitleText.supportRichText = false; TitleText.text = data.Title ?? ""; }
        if (ContentText != null)
        {
            ContentText.text = GetPreview(data.Content);
        }
        if (ReadDot != null) ReadDot.SetActive(!data.IsRead);
        if (DateText != null)
        {
            DateText.text = DateTimeOffset.TryParse(data.CreatedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date)
                ? date.ToLocalTime().ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "";
        }
    }

    private string GetPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";

        string[] words = content.Split(
            (char[])null,
            StringSplitOptions.RemoveEmptyEntries
        );

        return words.Length > 7
            ? string.Join(" ", words, 0, 7) + "...."
            : content;
    }

    public void SetBusy(bool busy) { if (PrefabButton != null) PrefabButton.interactable = !busy; }
    private void Open() { if (data != null) onOpen?.Invoke(data); }
}
