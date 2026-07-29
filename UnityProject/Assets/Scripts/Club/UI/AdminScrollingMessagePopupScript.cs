using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;

/// <summary>
/// Admin ▸ Scrolling Message. A club-wide ticker message (≤200 chars) with an optional
/// "jump to table" attached via the Choose-a-table sub-popup. Saved with
/// POST /api/clubs/{clubId}/scroll-message { message, tableId }.
/// Backend is LIVE.
/// </summary>
public class AdminScrollingMessagePopupScript : MonoBehaviour
{
    private const int ContentMax = 200;

    [Header("Header")]
    public Button Close_Button;

    [Header("Message")]
    public TMP_InputField Content_Input;

    [Header("Jump to table")]
    public Button ChooseTable_Button;
    public TextMeshProUGUI ChooseTable_Label;      // "Choose a game" / selected table name
    public Button ClearTable_Button;                // optional — clears the selection
    public string ChooseTablePlaceholder = "Choose a game";

    [Header("Submit")]
    public Button Confirm_Button;
    public Button Cancel_Button;

    [Header("Sub-popup")]
    public AdminTableSelectPopupScript TableSelectPopup;

    private string _selectedTableId;

    // Session-only — set on a successful send, cleared on fresh app launch (static resets
    // with the process, not read from server so it never survives a restart).
    private static string _lastSentThisSession = "";

    private void Start()
    {
        if (Close_Button   != null) Close_Button.onClick.AddListener(Close);
        if (Cancel_Button  != null) Cancel_Button.onClick.AddListener(Close);
        if (Confirm_Button != null) Confirm_Button.onClick.AddListener(OnConfirmTap);

        if (Content_Input != null) Content_Input.characterLimit = ContentMax;

        if (ChooseTable_Button != null) ChooseTable_Button.onClick.AddListener(OnChooseTableTap);
        if (ClearTable_Button  != null) ClearTable_Button.onClick.AddListener(ClearTable);
    }

    private void OnEnable()
    {
        // Prefill from this session's last send only — not the server value, so a fresh
        // app launch starts empty even though the server keeps the message indefinitely.
        // The jump-to-table selection always starts empty too (not tracked across opens).
        if (Content_Input != null)
            Content_Input.text = _lastSentThisSession;

        ClearTable();
    }

    private void OnChooseTableTap()
    {
        if (TableSelectPopup != null)
            TableSelectPopup.Open(OnTablePicked);
    }

    private void OnTablePicked(ClubTableData table)
    {
        if (table == null) return;
        // TableId (game-session id), not Id (club-table row id) — must match what
        // ShowClubTableScreenScript.JoinTableById looks up for the Go button.
        _selectedTableId = table.TableId;
        if (ChooseTable_Label != null) ChooseTable_Label.text = table.Name;
        if (ClearTable_Button != null) ClearTable_Button.gameObject.SetActive(true);
    }

    private void ClearTable()
    {
        _selectedTableId = null;
        if (ChooseTable_Label != null) ChooseTable_Label.text = ChooseTablePlaceholder;
        if (ClearTable_Button != null) ClearTable_Button.gameObject.SetActive(false);
    }

    private void OnConfirmTap()
    {
        string message = Content_Input != null ? Content_Input.text.Trim() : "";
        if (string.IsNullOrEmpty(message))
        {
            ShowToast("The content cannot be empty");
            return;
        }

        Save(message).Forget();
    }

    private async UniTaskVoid Save(string message)
    {
        if (Confirm_Button != null) Confirm_Button.interactable = false;
        try
        {
            await ClubManager.Instance.SetScrollMessageAsync(
                ClubContext.ClubId, message, _selectedTableId);

            // Keep the cached detail in step.
            if (ClubContext.ClubDetail != null)
                ClubContext.ClubDetail.ScrollMessage = message;

            _lastSentThisSession = message;

            // Refresh our own strip immediately — the club:scroll_message socket push may
            // exclude the sender's own connection.
            if (ClubScrollingMessageMarquee.Instance != null)
                ClubScrollingMessageMarquee.Instance.SetMessage(ClubContext.ClubName, message, _selectedTableId);

            ShowToast("Sent successfully");
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminScrollingMessagePopupScript] save error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to save" : e.Message);
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

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
