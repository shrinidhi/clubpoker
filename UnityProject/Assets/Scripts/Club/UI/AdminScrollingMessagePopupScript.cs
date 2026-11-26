using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;
using ClubPoker.Core;
using ClubPoker.Networking;

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
    private static string ChooseTablePlaceholder => Loc.Get("push.choose_a_game");

    [Header("Submit")]
    public Button Confirm_Button;
    public Button Cancel_Button;

    [Header("Sub-popup")]
    public AdminTableSelectPopupScript TableSelectPopup;

    private string _selectedTableId;

    // Session-only — cleared on a fresh app launch (static resets with the process, not
    // read from the server, so it never survives a restart).
    // Keyed by club id, so each club reopens with its own text. Holds what was last
    // typed (sent or not), and lives only as long as the process.
    private static readonly System.Collections.Generic.Dictionary<string, string> _draftByClub =
        new System.Collections.Generic.Dictionary<string, string>();

    private static string DraftKey => ClubContext.ClubId ?? "";

    private void Start()
    {
        if (Close_Button   != null) Close_Button.onClick.AddListener(Close);
        if (Cancel_Button  != null) Cancel_Button.onClick.AddListener(Close);
        if (Confirm_Button != null) Confirm_Button.onClick.AddListener(OnConfirmTap);

        if (Content_Input != null)
        {
            Content_Input.characterLimit = ContentMax;
            Content_Input.onValueChanged.AddListener(text =>
            {
                _draftByClub[DraftKey] = text;
                RefreshConfirm();
            });
        }

        if (ChooseTable_Button != null) ChooseTable_Button.onClick.AddListener(OnChooseTableTap);
        if (ClearTable_Button  != null) ClearTable_Button.onClick.AddListener(ClearTable);
    }

    private void OnEnable()
    {
        // Prefill with what was last typed for this club this session — not the server
        // value, so a fresh app launch starts empty even though the server keeps it.
        // The jump-to-table selection always starts empty too (not tracked across opens).
        if (Content_Input != null)
            Content_Input.text = _draftByClub.TryGetValue(DraftKey, out string draft) ? draft : "";

        ClearTable();
        _saving = false;
        RefreshConfirm();
    }

    // Grey (disabled sprite) until there's a message to send, normal once the
    // admin has typed something — and grey again while it's sending.
    private bool _saving;

    private void RefreshConfirm()
    {
        if (Confirm_Button == null) return;
        bool hasText = Content_Input != null && !string.IsNullOrWhiteSpace(Content_Input.text);
        Confirm_Button.interactable = hasText && !_saving;
    }

    private void OnChooseTableTap()
    {
        if (TableSelectPopup != null)
            TableSelectPopup.Open(OnTablePicked);
    }

    private void OnTablePicked(ClubTableData table)
    {
        if (table == null) return;
        // Id (the club-table row id, stable across game sessions), not TableId —
        // must match what ShowClubTableScreenScript.JoinTableById looks up for Go.
        _selectedTableId = table.Id;
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
            ShowToast(Loc.Get("push.content_empty"));
            return;
        }

        Save(message).Forget();
    }

    private async UniTaskVoid Save(string message)
    {
        _saving = true;
        RefreshConfirm();
        try
        {
            await ClubManager.Instance.SetScrollMessageAsync(
                ClubContext.ClubId, message, _selectedTableId);

            // Keep the cached detail in step.
            if (ClubContext.ClubDetail != null)
                ClubContext.ClubDetail.ScrollMessage = message;

            _draftByClub[DraftKey] = message;

            // Refresh our own strip immediately — the club:scroll_message socket push may
            // exclude the sender's own connection.
            ClubScrollBanner.Show(new ClubScrollMessagePayload
            {
                ClubId = ClubContext.ClubId,
                ClubName = ClubContext.ClubName,
                Message = message,
                TableId = _selectedTableId
            });

            ShowToast(Loc.Get("sent_successfully"));
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminScrollingMessagePopupScript] save error: {e.Message}");
            ShowToast(PlayerError.Text(e, Loc.Get("save_failed")));
        }
        finally
        {
            _saving = false;
            RefreshConfirm();
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
