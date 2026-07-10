using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;

/// <summary>
/// Export Data modal. Two tabs (Export Data / Export Record). On the Export Data
/// tab the user picks a date range (via the shared picker), toggles which data
/// sets to export, sees the live diamond cost, enters an email, and exports.
/// Its date range is independent of the main Data screen.
/// </summary>
public class ExportDataModalScript : MonoBehaviour
{
    [Header("Tabs")]
    public Button ExportDataTab_Button;
    public Button ExportRecordTab_Button;
    public GameObject ExportDataPanel;
    public GameObject ExportRecordPanel;
    public Sprite SelectedTabSprite;
    public Sprite UnselectedTabSprite;

    [Header("Navigation")]
    public Button Close_Button;

    [Header("Date Range")]
    public TextMeshProUGUI DateRange_Text;
    public Button DateRange_Button;          // date label / calendar icon
    public DateRangePopupView DateRangePopupView;

    [Header("Export Items")]
    public ExportTypesSO ExportTypesConfig;        // type list + costs (edit the asset)
    public Transform ExportTypes_Content;          // parent for the generated rows
    public ExportTypeRowView ExportTypeRowPrefab;
    public TextMeshProUGUI Total_Text;
    public Color TotalCostColor = new Color(1f, 0.82f, 0.29f, 1f);   // cost highlight

    [Header("Export")]
    public TMP_InputField Email_Input;
    public Button Export_Button;

    [Header("Tips / Confirm Popup")]                
    public AlertPopup TipsPopup;
    [TextArea] public string TipsMessage;          // shown for the (?) tips

    private readonly List<ExportTypeRowView> _rows = new();

    private DateTime _from;
    private DateTime _to;
    private bool _exporting;

    private void Start()
    {
        if (Close_Button != null)           Close_Button.onClick.AddListener(CloseOnTap);
        if (ExportDataTab_Button != null)   ExportDataTab_Button.onClick.AddListener(() => ShowTab(true));
        if (ExportRecordTab_Button != null) ExportRecordTab_Button.onClick.AddListener(() => ShowTab(false));
        if (DateRange_Button != null)       DateRange_Button.onClick.AddListener(OpenDatePicker);
        if (Export_Button != null)          Export_Button.onClick.AddListener(ExportOnTap);

        // Fallback range if the modal was opened without SetRange() seeding it.
        if (_from == default)
        {
            _from = DateTime.Today;
            _to = DateTime.Today;
        }

        BuildRows();
        ShowTab(true);
        UpdateRangeLabel();
        UpdateTotal();
    }

    /// <summary>Seed the modal with a date range (e.g. the main Data screen's range).</summary>
    public void SetRange(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
        UpdateRangeLabel();
    }

    // ── Tabs ──────────────────────────────────────────────────────────────

    private void ShowTab(bool exportData)
    {
        if (ExportDataPanel != null)   ExportDataPanel.SetActive(exportData);
        if (ExportRecordPanel != null) ExportRecordPanel.SetActive(!exportData);

        SetTabSprite(ExportDataTab_Button, exportData);
        SetTabSprite(ExportRecordTab_Button, !exportData);
    }

    private void SetTabSprite(Button button, bool selected)
    {
        if (button == null || button.image == null || SelectedTabSprite == null || UnselectedTabSprite == null)
            return;
        button.image.sprite = selected ? SelectedTabSprite : UnselectedTabSprite;
    }

    public void OpenTipsPopUp()
    {
        // Tips: Confirm-only, no action.
        if (TipsPopup != null)
            TipsPopup.Show(TipsMessage, showCancel: false, onConfirm: null);
    }

    // ── Date range ────────────────────────────────────────────────────────

    public void OpenDatePicker()
    {
        // Edits only this modal's range — not the main Data screen.
        if (DateRangePopupView != null)
            DateRangePopupView.Open(_from, _to, OnRangePicked);
    }


    private void OnRangePicked(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
        UpdateRangeLabel();
    }

    private void UpdateRangeLabel()
    {
        if (DateRange_Text != null)
            DateRange_Text.text = $"{_from:yyyy.MM.dd} - {_to:yyyy.MM.dd}";
    }

    // ── Rows / total ──────────────────────────────────────────────────────

    private void BuildRows()
    {
        _rows.Clear();

        if (ExportTypes_Content == null || ExportTypeRowPrefab == null || ExportTypesConfig == null)
            return;

        // Clear any design-time placeholder rows.
        for (int i = ExportTypes_Content.childCount - 1; i >= 0; i--)
            Destroy(ExportTypes_Content.GetChild(i).gameObject);

        foreach (ExportTypeData type in ExportTypesConfig.Types)
        {
            ExportTypeRowView row = Instantiate(ExportTypeRowPrefab, ExportTypes_Content);
            row.Setup(type, UpdateTotal);
            _rows.Add(row);
        }
    }

    private void UpdateTotal()
    {
        int total = 0;
        bool anySelected = false;
        foreach (ExportTypeRowView row in _rows)
        {
            if (row.IsOn)
            {
                total += row.Cost;
                anySelected = true;
            }
        }

        if (Total_Text != null)
        {
            string hex = ColorUtility.ToHtmlStringRGB(TotalCostColor);
            Total_Text.text = $"Total: <color=#{hex}>{total}</color>";
        }

        // Disable export while nothing is selected (or mid-export).
        if (Export_Button != null)
            Export_Button.interactable = anySelected && !_exporting;
    }

    // ── Export ────────────────────────────────────────────────────────────

    private void ExportOnTap()
    {
        if (_exporting)
            return;

        string email = Email_Input != null ? Email_Input.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(email))
        {
            ShowToast("Please enter an email");
            return;
        }

        List<string> types = SelectedTypes();
        if (types.Count == 0)
        {
            ShowToast("Select at least one data type");
            return;
        }

        // Confirm before exporting: same popup, now with Confirm + Cancel.
        if (TipsPopup != null)
        {
            string confirmMsg =
                $"Confirm to export data of {_from:yyyy.MM.dd}-{_to:yyyy.MM.dd} to {email}?";
            TipsPopup.Show(confirmMsg, showCancel: true,
                onConfirm: () => ExportAsync(email, types).Forget());
        }
        else
        {
            ExportAsync(email, types).Forget();
        }
    }

    private List<string> SelectedTypes()
    {
        var types = new List<string>();
        foreach (ExportTypeRowView row in _rows)
            if (row.IsOn)
                types.Add(row.TypeKey);
        return types;
    }

    private async UniTaskVoid ExportAsync(string email, List<string> types)
    {
        _exporting = true;
        if (Export_Button != null) Export_Button.interactable = false;

        try
        {
            var req = new ExportDataRequest
            {
                Email = email,
                Types = types,
                From = _from.ToString("yyyy-MM-dd"),
                To = _to.ToString("yyyy-MM-dd"),
            };

            await ClubManager.Instance.ExportClubDataAsync(ClubContext.ClubId, req);

            ShowToast("Export started — check your email");
            CloseOnTap();
        }
        catch (ApiException e)
        {
            // V001 (invalid email), G003 (insufficient diamonds), etc.
            ShowToast(e.Message);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ExportDataModal] Export failed: {e.Message}");
            ShowToast("Export failed. Please try again.");
        }
        finally
        {
            _exporting = false;
            UpdateTotal();   // re-enable export only if something is still selected
        }
    }

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }

    private void CloseOnTap()
    {
        gameObject.SetActive(false);
    }
}
