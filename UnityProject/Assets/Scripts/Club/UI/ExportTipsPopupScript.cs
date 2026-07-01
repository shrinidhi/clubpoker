using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable tips / confirmation popup. Shown two ways:
///  - Tips (?) → Confirm only (info).
///  - Export confirm → Confirm + Cancel (Confirm runs the action).
/// </summary>
public class ExportTipsPopupScript : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeButton;

    private Action _onConfirm;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
    }

    /// <summary>
    /// Show the popup. <paramref name="showCancel"/> false = Confirm-only (tips).
    /// <paramref name="message"/> null keeps the existing/static text.
    /// </summary>
    public void Show(string message, bool showCancel, Action onConfirm)
    {
        if (messageText != null && message != null)
            messageText.text = message;

        if (cancelButton != null)
            cancelButton.gameObject.SetActive(showCancel);

        _onConfirm = onConfirm;
        gameObject.SetActive(true);
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
