using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Club Admin main screen. Creator-only (reached via the Creator-only bottom bar).
/// Shows the header stats grid and the tappable row list; each row opens its own
/// sub-screen / popup (nested under this panel). Mirrors CashierPanelScript's
/// Init / Back / self-close pattern.
/// </summary>
public class AdminPanelScript : MonoBehaviour
{
    [Header("Header")]
    public Button Back_Button;

    [Header("Stats Grid (Today / ThisWeek / LastWeek / Overall)")]
    public StatRow Fee_Row;
    public StatRow Games_Row;
    public StatRow PlayerWinnings_Row;
    public StatRow InsuranceEV_Row;

    [Header("Row Buttons")]
    public Button ClubLevel_Button;
    public Button ClubCareer_Button;
    public Button MobilePush_Button;
    public Button Notification_Button;
    public Button FeeAllocation_Button;
    public Button ScrollingMessage_Button;
    public Button ClubPoster_Button;
    public Button NotificationSetting_Button;
    public Button DisbandEmptyTables_Button;
    public Button PersonalTradeRecord_Button;
    public Button DisbandClub_Button;
    public Button ClubBadgeName_Button;

    [Header("Row Targets (nested screens / popups)")]
    public GameObject ClubLevel_Screen;
    public GameObject ClubCareer_Screen;
    public GameObject MobilePush_Popup;
    public GameObject Notification_Popup;
    public GameObject FeeAllocation_Popup;
    public GameObject ScrollingMessage_Popup;
    public GameObject ClubPoster_Screen;
    public GameObject NotificationSetting_Popup;
    public GameObject PersonalTradeRecord_Screen;
    public GameObject DisbandClub_Popup;
    public GameObject ClubBadgeName_Popup;

    [Header("Row Inline Values")]
    public TextMeshProUGUI FeeAllocation_ValueText;   // e.g. "0%"

    [Header("Shared")]
    public AlertPopup AlertPopup;                      // reused for Disband Empty Tables confirm

    [Serializable]
    public class StatRow
    {
        public TextMeshProUGUI Today;
        public TextMeshProUGUI ThisWeek;
        public TextMeshProUGUI LastWeek;
        public TextMeshProUGUI Overall;

        public void Set(AdminStatValue v)
        {
            if (v == null) return;
            if (Today    != null) Today.text    = v.Today.ToString("N0");
            if (ThisWeek != null) ThisWeek.text = v.ThisWeek.ToString("N0");
            if (LastWeek != null) LastWeek.text = v.LastWeek.ToString("N0");
            if (Overall  != null) Overall.text  = v.Overall.ToString("N0");
        }
    }

    private void Start()
    {
        if (Back_Button != null) Back_Button.onClick.AddListener(OnBackTap);

        Bind(ClubLevel_Button,           ClubLevel_Screen);
        Bind(ClubCareer_Button,          ClubCareer_Screen);
        Bind(MobilePush_Button,          MobilePush_Popup);
        Bind(Notification_Button,        Notification_Popup);
        Bind(FeeAllocation_Button,       FeeAllocation_Popup);
        Bind(ScrollingMessage_Button,    ScrollingMessage_Popup);
        Bind(ClubPoster_Button,          ClubPoster_Screen);
        Bind(NotificationSetting_Button, NotificationSetting_Popup);
        Bind(PersonalTradeRecord_Button, PersonalTradeRecord_Screen);
        Bind(DisbandClub_Button,         DisbandClub_Popup);
        Bind(ClubBadgeName_Button,       ClubBadgeName_Popup);

        if (DisbandEmptyTables_Button != null)
            DisbandEmptyTables_Button.onClick.AddListener(OnDisbandEmptyTablesTap);
    }

    public void Init()
    {
        InitAsync().Forget();
    }

    private async UniTaskVoid InitAsync()
    {
        try
        {
            var stats = await ClubManager.Instance.GetAdminStatsAsync(ClubContext.ClubId);
            ApplyStats(stats);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminPanelScript] stats fetch error: {e.Message}");
        }

        // Club detail was cached on scene entry (ClubViewController.LoadClubDetail).
        SetFeeAllocationValue(ClubContext.FeeAllocPercent);
    }

    // Called on open and by AdminFeeAllocationPopupScript after a successful save.
    public void SetFeeAllocationValue(int percent)
    {
        if (FeeAllocation_ValueText != null)
            FeeAllocation_ValueText.text = percent + "%";
    }

    private void ApplyStats(AdminStatsData stats)
    {
        if (stats == null) return;
        Fee_Row.Set(stats.Fee);
        Games_Row.Set(stats.Games);
        PlayerWinnings_Row.Set(stats.PlayerWinnings);
        InsuranceEV_Row.Set(stats.InsuranceEV);
    }

    private void Bind(Button button, GameObject target)
    {
        if (button == null) return;
        button.onClick.AddListener(() => OpenTarget(target));
    }

    private void OpenTarget(GameObject target)
    {
        if (target == null) return;          // sub-screen not built yet — no-op placeholder
        target.transform.SetAsLastSibling();  // render above the admin list
        target.SetActive(true);
    }

    // Two Tips popups, both the shared AlertPopup:
    //  1. confirm + cancel → 2. "please wait" (Confirm greyed, auto-closes after 2s)
    // while the request runs in the background.
    private void OnDisbandEmptyTablesTap()
    {
        if (AlertPopup == null) return;
        AlertPopup.Show(
            "Tips",
            "You are about to close all tables which has less than two players " +
            "(excluding MTT). Confirm to close?",
            showCancel: true,
            onConfirm: OnDisbandEmptyTablesConfirmed);
    }

    private void OnDisbandEmptyTablesConfirmed()
    {
        AlertPopup.ShowAutoClose(
            "Tips",
            "All empty tables are disbanded.Please wait...",
            seconds: 2f);

        DisbandEmptyTables().Forget();
    }

    private async UniTaskVoid DisbandEmptyTables()
    {
        try
        {
            await ClubManager.Instance.DisbandEmptyTablesAsync(ClubContext.ClubId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminPanelScript] disband empty tables error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to disband tables" : e.Message);
        }
    }

    private void OnBackTap()
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
