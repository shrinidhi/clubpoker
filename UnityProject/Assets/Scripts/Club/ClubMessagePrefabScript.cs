using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class ClubMessagePrefabScript : MonoBehaviour
{
    public Text TitleText;
    public Text ContentText;
    public Text DateText;
    public GameObject ReadDot;
    public Button PrefabButton;
    private ClubMessageData data;
    private Action<ClubMessageData> onOpen;

    private void Awake()
    {
        if (PrefabButton != null) PrefabButton.onClick.AddListener(Open);
    }
    private void OnDestroy()
    {
        if (PrefabButton != null) PrefabButton.onClick.RemoveListener(Open);
    }
    public void Setup(ClubMessageData notification, Action<ClubMessageData> onClick)
    {
        data = notification;
        onOpen = onClick;
        if (TitleText != null) { TitleText.supportRichText = false; TitleText.text = data.Title ?? ""; }
        if (ContentText != null) { ContentText.supportRichText = false; ContentText.text = GetPreview(data.Content); }
        if (ReadDot != null) ReadDot.SetActive(!data.IsRead);
        if (DateText != null)
        {
            DateText.text = DateTimeOffset.TryParse(
                data.CreatedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date)
                ? date.ToLocalTime().ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)
                : "";
        }
    }
    private string GetPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";
        string[] words = content.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 7 ? string.Join(" ", words, 0, 7) + "...." : content;
    }
    public void SetBusy(bool busy) { if (PrefabButton != null) PrefabButton.interactable = !busy; }
    private void Open() { if (data != null) onOpen?.Invoke(data); }
}
