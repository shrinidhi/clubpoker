using System;
using ClubPoker.Networking.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MessageShowScreenScript : MonoBehaviour
{
    public Text TitleText;
    public Text ContentText;
    public Button CloseButton;
    public Button DeleteMsgButton;

    [Header("Action button (optional)")]
    public Button CtaButton;
    public TextMeshProUGUI CtaLabelText;

    public const string CtaFriendRequests = "friends:requests";
    public const string CtaFriendsList = "friends:list";

    /// Raised with the notification's ctaUrl when its action button is tapped.
    public static event Action<string> OnCtaTapped;

    public static bool HandlesCta(string url) => url == CtaFriendRequests || url == CtaFriendsList;

    private Action onDelete;
    private string ctaUrl;

    private void Awake()
    {
        if (CloseButton != null) CloseButton.onClick.AddListener(Hide);
        if (DeleteMsgButton != null) DeleteMsgButton.onClick.AddListener(Delete);
        if (CtaButton != null) CtaButton.onClick.AddListener(Cta);
    }
    private void OnDestroy()
    {
        if (CloseButton != null) CloseButton.onClick.RemoveListener(Hide);
        if (DeleteMsgButton != null) DeleteMsgButton.onClick.RemoveListener(Delete);
        if (CtaButton != null) CtaButton.onClick.RemoveListener(Cta);
    }
    public void Show(NotificationData data, Action deleteAction)
    {
        onDelete = deleteAction;
        if (TitleText != null) { TitleText.supportRichText = false; TitleText.text = data.Title ?? ""; }
        if (ContentText != null) { ContentText.supportRichText = false; ContentText.text = data.Content ?? ""; }

        ctaUrl = data.CtaUrl;
        bool hasCta = HandlesCta(ctaUrl);
        if (CtaButton != null) CtaButton.gameObject.SetActive(hasCta);
        if (CtaLabelText != null && hasCta) CtaLabelText.text = data.CtaLabel ?? "";

        gameObject.SetActive(true);
    }

    private void Cta()
    {
        string url = ctaUrl;
        Hide();
        OnCtaTapped?.Invoke(url);
    }
    public void Hide() { gameObject.SetActive(false); }
    public void SetBusy(bool busy) { if (DeleteMsgButton != null) DeleteMsgButton.interactable = !busy; }
    private void Delete() { onDelete?.Invoke(); }
}
