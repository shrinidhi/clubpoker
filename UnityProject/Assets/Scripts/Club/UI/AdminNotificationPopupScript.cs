using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Core;

/// <summary>
/// Admin ▸ Notification. Compose a club-wide in-app notice (Title ≤20, Content ≤500).
/// Confirm routes through the shared AlertPopup ("send to all club members?") then
/// posts via ClubManager. Template for the other admin form popups.
/// </summary>
public class AdminNotificationPopupScript : MonoBehaviour
{
    private const int TitleMax   = 20;
    private const int ContentMax = 500;

    [Header("Header")]
    public Button Close_Button;

    [Header("Inputs")]
    public TMP_InputField Title_Input;
    public TMP_InputField Content_Input;

    [Header("Submit")]
    public Button Confirm_Button;

    [Header("Shared")]
    public AlertPopup AlertPopup;                // reused confirm/tips popup

    private void Start()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(Close);
        if (Confirm_Button != null) Confirm_Button.onClick.AddListener(OnConfirmTap);

        if (Title_Input   != null) Title_Input.characterLimit   = TitleMax;
        if (Content_Input != null) Content_Input.characterLimit = ContentMax;
    }

    // Reset on every open. Works whether opened via SetActive (AdminPanelScript.OpenTarget)
    // or Show().
    private void OnEnable()
    {
        transform.SetAsLastSibling();
        if (Title_Input   != null) Title_Input.text   = "";
        if (Content_Input != null) Content_Input.text = "";
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    private void OnConfirmTap()
    {
        string title   = Title_Input   != null ? Title_Input.text.Trim()   : "";
        string content = Content_Input != null ? Content_Input.text.Trim() : "";

        // Nothing typed at all.
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
        {
            ShowToast(Loc.Get("push.enter_title_content"));
            return;
        }

        // Title alone isn't enough.
        if (string.IsNullOrEmpty(content))
        {
            ShowToast(Loc.Get("push.content_empty"));
            return;
        }

        if (AlertPopup != null)
        {
            AlertPopup.Show(
                Loc.Get("club.confirm_send_notification"),
                showCancel: true,
                onConfirm: () => Send().Forget());
        }
        else
        {
            Send().Forget();
        }
    }

    private async UniTaskVoid Send()
    {
        string title   = Title_Input   != null ? Title_Input.text.Trim()   : "";
        string content = Content_Input != null ? Content_Input.text.Trim() : "";

        if (Confirm_Button != null) Confirm_Button.interactable = false;
        try
        {
            await ClubManager.Instance.SendNotificationAsync(ClubContext.ClubId, title, content);
            ShowToast(Loc.Get("club.notification_sent"));
            Close();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminNotificationPopupScript] send error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? Loc.Get("send_failed_retry") : e.Message);
        }
        finally
        {
            if (Confirm_Button != null) Confirm_Button.interactable = true;
        }
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    // Same toast used across the club screens (Data/Export).
    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
