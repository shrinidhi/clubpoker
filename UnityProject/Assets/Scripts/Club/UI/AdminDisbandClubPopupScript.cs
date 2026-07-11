using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Disband this club. The creator must type the club's code (shown on the club
/// home as "ID : xxxxxx") to confirm — a client-side match gate that keeps Confirm disabled
/// until it matches. On confirm: DELETE /api/clubs/{clubId} → clear context → back to menu.
/// Supports an on-screen numpad (digit + backspace buttons) or the device keyboard.
/// </summary>
public class AdminDisbandClubPopupScript : MonoBehaviour
{
    [Header("Header")]
    public Button Close_Button;

    [Header("Body")]
    public TextMeshProUGUI CodeHint_Text;      // optional — "ID : 566515"

    [Header("Input")]
    public TMP_InputField Code_Input;          // set Content Type = Number (device numpad)

    [Header("Submit")]
    public Button Confirm_Button;

    private bool _busy;

    private string ExpectedCode =>
        ClubContext.SelectedClub != null ? ClubContext.SelectedClub.ClubCode : null;

    private void Start()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(Close);
        if (Confirm_Button != null) Confirm_Button.onClick.AddListener(OnConfirmTap);

        if (Code_Input != null)
            Code_Input.onValueChanged.AddListener(_ => RefreshConfirmState());
    }

    private void OnEnable()
    {
        if (Code_Input != null) Code_Input.text = "";
        if (CodeHint_Text != null && ExpectedCode != null)
            CodeHint_Text.text = "ID: " + ExpectedCode;
        RefreshConfirmState();
    }

    // Confirm is enabled only when the typed code matches the club's code.
    private void RefreshConfirmState()
    {
        if (Confirm_Button == null) return;
        string typed = Code_Input != null ? Code_Input.text.Trim() : "";
        Confirm_Button.interactable =
            !_busy && !string.IsNullOrEmpty(ExpectedCode) && typed == ExpectedCode;
    }

    private void OnConfirmTap()
    {
        string typed = Code_Input != null ? Code_Input.text.Trim() : "";
        if (string.IsNullOrEmpty(ExpectedCode) || typed != ExpectedCode)
        {
            ShowToast("Club ID does not match");
            return;
        }
        Disband().Forget();
    }

    private async UniTaskVoid Disband()
    {
        _busy = true;
        RefreshConfirmState();
        try
        {
            await ClubManager.Instance.DisbandClubAsync(ClubContext.ClubId);
            ShowToast("Club disbanded");
            ClubContext.Clear();
            if (ClubViewController.Instance != null)
                ClubViewController.Instance.BackToMainMenu();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminDisbandClubPopupScript] disband error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to disband" : e.Message);
            _busy = false;
            RefreshConfirmState();
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
