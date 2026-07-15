using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Mobile Push Notification. Title + content pushed to members' devices for a
/// diamond cost. Balance is loaded on open (GET /api/player/diamonds); the actual cost only
/// comes back in the POST /push response, so "not enough diamonds" is enforced server-side
/// (with an optional client pre-check when CostEstimate is set).
/// Direct send on Confirm — no Tips popup.
/// Backend is LIVE.
/// </summary>
public class AdminMobilePushPopupScript : MonoBehaviour
{
    private const int TitleMax   = 30;
    private const int ContentMax = 150;

    [Header("Header")]
    public Button Close_Button;

    [Header("Inputs")]
    public TMP_InputField Title_Input;
    public TMP_InputField Content_Input;

    [Header("Live Preview (top notification mock)")]
    public TextMeshProUGUI Preview_Title_Text;
    public TextMeshProUGUI Preview_Content_Text;
    [Tooltip("Shown when the matching field is empty.")]
    public string Preview_Title_Placeholder   = "Title";
    public string Preview_Content_Placeholder = "Content";

    [Header("Diamonds")]
    public TextMeshProUGUI Balance_Text;
    public TextMeshProUGUI Cost_Text;              // optional — shows CostEstimate / last cost
    [Tooltip("Optional. >0 enables a client-side 'not enough diamonds' pre-check and a Cost label.")]
    public int CostEstimate = 0;

    [Header("Submit")]
    public Button Confirm_Button;

    private long _available;

    private void Start()
    {
        if (Close_Button   != null) Close_Button.onClick.AddListener(Close);
        if (Confirm_Button != null) Confirm_Button.onClick.AddListener(OnConfirmTap);

        if (Title_Input != null)
        {
            Title_Input.characterLimit = TitleMax;
            Title_Input.onValueChanged.AddListener(_ => RefreshPreview());
        }
        if (Content_Input != null)
        {
            Content_Input.characterLimit = ContentMax;
            Content_Input.onValueChanged.AddListener(_ => RefreshPreview());
        }
    }

    private void OnEnable()
    {
        if (Title_Input   != null) Title_Input.text   = "";
        if (Content_Input != null) Content_Input.text = "";
        if (Cost_Text != null) Cost_Text.text = CostEstimate > 0 ? CostEstimate.ToString() : "-";

        RefreshPreview();
        LoadBalance().Forget();
    }

    // Mirror the inputs into the top notification mock, falling back to placeholders.
    private void RefreshPreview()
    {
        if (Preview_Title_Text != null)
        {
            string t = Title_Input != null ? Title_Input.text : "";
            Preview_Title_Text.text = string.IsNullOrEmpty(t) ? Preview_Title_Placeholder : t;
        }
        if (Preview_Content_Text != null)
        {
            string c = Content_Input != null ? Content_Input.text : "";
            Preview_Content_Text.text = string.IsNullOrEmpty(c) ? Preview_Content_Placeholder : c;
        }
    }

    private async UniTaskVoid LoadBalance()
    {
        try
        {
            var d = await ClubManager.Instance.GetDiamondsAsync();
            _available = d?.Available ?? 0;
            if (Balance_Text != null) Balance_Text.text = _available.ToString("N0");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminMobilePushPopupScript] balance load error: {e.Message}");
            if (Balance_Text != null) Balance_Text.text = "-";
        }
    }

    private void OnConfirmTap()
    {
        string title   = Title_Input   != null ? Title_Input.text.Trim()   : "";
        string content = Content_Input != null ? Content_Input.text.Trim() : "";

        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
        {
            ShowToast("Enter title and content");
            return;
        }

        if (string.IsNullOrEmpty(content))
        {
            ShowToast("The content cannot be empty");
            return;
        }

        // Optional pre-check when a cost estimate is configured.
        if (CostEstimate > 0 && _available < CostEstimate)
        {
            ShowToast("Not enough diamonds");
            return;
        }

        Send().Forget();
    }

    private async UniTaskVoid Send()
    {
        string title   = Title_Input   != null ? Title_Input.text.Trim()   : "";
        string content = Content_Input != null ? Content_Input.text.Trim() : "";

        if (Confirm_Button != null) Confirm_Button.interactable = false;
        try
        {
            var res = await ClubManager.Instance.SendPushAsync(ClubContext.ClubId, title, content);

            _available = Math.Max(0, _available - (res?.DiamondCost ?? 0));
            if (Balance_Text != null) Balance_Text.text = _available.ToString("N0");

            ShowToast("Push Notification Sent");
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminMobilePushPopupScript] send error: {e.Message}");
            ShowToast(ResolveError(e));
        }
        finally
        {
            if (Confirm_Button != null) Confirm_Button.interactable = true;
        }
    }

    // Surface the diamond case with the wording from the prototype; otherwise pass the
    // server message through.
    private static string ResolveError(Exception e)
    {
        string m = e.Message ?? "";
        if (m.IndexOf("diamond", StringComparison.OrdinalIgnoreCase) >= 0 ||
            m.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Not enough diamonds";
        return string.IsNullOrEmpty(m) ? "Failed to send push" : m;
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
