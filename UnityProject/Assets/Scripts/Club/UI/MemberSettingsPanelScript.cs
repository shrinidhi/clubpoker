using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// Club ▸ Settings for a normal member. Three rows: Club Career, Trade Record and
/// Quit from this club.
///
/// Career / Trade Record reuse the very same screen objects the Admin panel opens
/// (AdminClubCareerScreenScript / AdminTradeRecordScreenScript) — both are self-contained,
/// read ClubContext.ClubId and are driven purely by SetActive, so nothing needs copying.
/// For that to work the two screen GameObjects must NOT be children of the Admin panel
/// (a deactivated parent hides them): park them under the shared club canvas root and
/// drag the same object into both AdminPanelScript and this script.
///
/// Quit: Tips popup (confirm + cancel) → POST /api/clubs/{clubId}/leave → back to main menu.
/// </summary>
public class MemberSettingsPanelScript : MonoBehaviour
{
    [Header("Header")]
    public Button Back_Button;

    [Header("Row Buttons")]
    public Button ClubCareer_Button;
    public Button TradeRecord_Button;
    public Button QuitClub_Button;

    [Header("Row Targets (the same objects the Admin panel opens)")]
    public AdminClubCareerScreenScript ClubCareer_Screen;
    public AdminTradeRecordScreenScript TradeRecord_Screen;

    [Header("Shared")]
    public AlertPopup AlertPopup;       // reused for the quit confirm

    private bool _leaving;

    private void Start()
    {
        if (Back_Button != null) Back_Button.onClick.AddListener(Close);
        if (QuitClub_Button != null) QuitClub_Button.onClick.AddListener(OnQuitTap);

        // A member sees only their own figures/rows.
        if (ClubCareer_Button != null && ClubCareer_Screen != null)
            ClubCareer_Button.onClick.AddListener(() => ClubCareer_Screen.Open(personalOnly: true));
        if (TradeRecord_Button != null && TradeRecord_Screen != null)
            TradeRecord_Button.onClick.AddListener(() => TradeRecord_Screen.Open(personalOnly: true));
    }

    // ── Quit from this club ─────────────────────────────────────────────────

    private void OnQuitTap()
    {
        if (_leaving || AlertPopup == null) return;

        AlertPopup.Show(
            "Tips",
            "Are you sure you want to quit this club? Your club chips will be recalled.",
            showCancel: true,
            onConfirm: () => Leave().Forget());
    }

    private async UniTaskVoid Leave()
    {
        if (_leaving) return;
        _leaving = true;
        if (QuitClub_Button != null) QuitClub_Button.interactable = false;

        try
        {
            var res = await ClubManager.Instance.LeaveClubAsync(ClubContext.ClubId);

            ShowToast(res != null && res.ChipsRecalled > 0
                ? $"Left the club. {res.ChipsRecalled:N0} chips recalled."
                : "Left the club");

            ClubContext.Clear();
            if (ClubViewController.Instance != null)
                ClubViewController.Instance.BackToMainMenu();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemberSettingsPanelScript] leave error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to quit the club" : e.Message);
            _leaving = false;
            if (QuitClub_Button != null) QuitClub_Button.interactable = true;
        }
    }

    // The shared screens live at the canvas root, not under this panel — close them explicitly.
    private void Close()
    {
        if (ClubCareer_Screen != null)  ClubCareer_Screen.gameObject.SetActive(false);
        if (TradeRecord_Screen != null) TradeRecord_Screen.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    // Same toast used across the club screens (Data/Export).
    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
