using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Generic alert / confirmation popup. Shown three ways:
///  - Tips / info    → Confirm only (showCancel:false).
///  - Confirm action → Confirm + Cancel (Confirm runs the action).
///  - Please-wait    → ShowAutoClose(): Confirm disabled, closes itself after N seconds.
/// Reused across Data, Admin, and any feature needing a confirm/info dialog.
/// </summary>
public class AlertPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;      // optional header
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeButton;

    private Action _onConfirm;
    private CancellationTokenSource _autoCloseCts;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        CancelAutoClose();
    }

    /// <summary>
    /// Show the popup. <paramref name="showCancel"/> false = Confirm-only (tips).
    /// <paramref name="message"/> null keeps the existing/static text.
    /// Header keeps whatever the prefab already shows.
    /// </summary>
    public void Show(string message, bool showCancel, Action onConfirm)
    {
        Show(null, message, showCancel, onConfirm);
    }

    /// <summary>
    /// Show with a custom header. <paramref name="title"/> null keeps the prefab's static
    /// header; <paramref name="message"/> null keeps the static body.
    /// </summary>
    public void Show(string title, string message, bool showCancel, Action onConfirm)
    {
        CancelAutoClose();

        if (titleText != null && title != null)
            titleText.text = title;

        if (messageText != null && message != null)
            messageText.text = message;

        if (cancelButton != null)
            cancelButton.gameObject.SetActive(showCancel);

        if (confirmButton != null)
            confirmButton.interactable = true;   // reset after a ShowAutoClose

        _onConfirm = onConfirm;
        transform.SetAsLastSibling();   // always render above whatever opened it
        gameObject.SetActive(true);
    }

    /// <summary>
    /// "Please wait" popup: info-only, Confirm greyed out, closes itself after
    /// <paramref name="seconds"/>. Tapping the X still closes it early.
    /// </summary>
    public void ShowAutoClose(string title, string message, float seconds = 2f)
    {
        Show(title, message, showCancel: false, onConfirm: null);

        if (confirmButton != null)
            confirmButton.interactable = false;

        _autoCloseCts = new CancellationTokenSource();
        AutoClose(seconds, _autoCloseCts.Token).Forget();
    }

    private async UniTaskVoid AutoClose(float seconds, CancellationToken token)
    {
        try
        {
            await UniTask.Delay((int)(seconds * 1000f), cancellationToken: token);
            Hide();
        }
        catch (OperationCanceledException) { /* closed early or reused */ }
    }

    private void CancelAutoClose()
    {
        _autoCloseCts?.Cancel();
        _autoCloseCts?.Dispose();
        _autoCloseCts = null;
    }

    private void OnConfirm()
    {
        Action cb = _onConfirm;
        _onConfirm = null;
        gameObject.SetActive(false);
        cb?.Invoke();
    }

    private void Hide()
    {
        _onConfirm = null;
        gameObject.SetActive(false);
    }
}
