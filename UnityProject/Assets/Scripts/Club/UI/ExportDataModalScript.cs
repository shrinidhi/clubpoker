using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;
using ClubPoker.Core;
using ClubPoker.Auth;

/// <summary>
/// Export Data modal. Two tabs (Export Data / Export Record). On the Export Data
/// tab the user picks a date range (via the shared picker), toggles which data
/// sets to export and sees the live diamond cost. Export confirms the cost, then
/// downloads the .xlsx and saves it on the device (ExportFileSaver). Any club
/// member can export. Its date range is independent of the main Data screen.
///
/// Email delivery isn't built on the server yet, so the email field is shown
/// disabled ("coming soon").
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
    public TMP_InputField Email_Input;             // disabled until email export ships
    public Button Export_Button;
    public GameObject Loading_Indicator;           // optional spinner while the file builds

    [Header("Export Record tab")]
    public ExportHistoryPanelScript HistoryPanel;  // refreshed after a new export

    [Header("Not enough diamonds")]
    public GameObject ShopScreen;                  // optional: "Buy diamonds" opens it

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

        DisableEmail();
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
            DateRangePopupView.Open(_from, _to, OnRangePicked, ClubContext.MinSelectableDate);
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
            Total_Text.text = $"{Loc.Get("total_2")} <color=#{hex}>{total}</color>";
        }

        // Disable export while nothing is selected (or mid-export).
        if (Export_Button != null)
            Export_Button.interactable = anySelected && !_exporting;
    }

    // ── Export ────────────────────────────────────────────────────────────

    private void DisableEmail()
    {
        if (Email_Input == null)
            return;

        Email_Input.text = string.Empty;
        Email_Input.interactable = false;

        if (Email_Input.placeholder == null)
            return;

        // The placeholder carries a LocalizedText tag (please_enter_email) that
        // re-applies its key on every enable — so change the key, not the text,
        // or reopening the popup brings "Please enter email" back.
        var localized = Email_Input.placeholder.GetComponent<LocalizedText>();
        if (localized != null)
            localized.Key = "export.email_coming_soon";
        else if (Email_Input.placeholder is TMP_Text placeholder)
            placeholder.text = Loc.Get("export.email_coming_soon");
    }

    private void ExportOnTap()
    {
        if (_exporting)
            return;

        List<string> types = SelectedTypes();
        if (types.Count == 0)
        {
            ShowToast(Loc.Get("export.select_type"));
            return;
        }

        int cost = SelectedCost();

        // Confirming spends diamonds — say how many before going ahead.
        string confirmMsg = Loc.Get("export.confirm_cost",
            ("types", string.Join(", ", SelectedNames())),
            ("range", $"{_from:yyyy.MM.dd} - {_to:yyyy.MM.dd}"),
            ("cost", cost.ToString("N0")));

        if (TipsPopup != null)
            TipsPopup.Show(confirmMsg, showCancel: true, onConfirm: () => ExportAsync(types, cost).Forget());
        else
            ExportAsync(types, cost).Forget();
    }

    private List<string> SelectedTypes()
    {
        var types = new List<string>();
        foreach (ExportTypeRowView row in _rows)
            if (row.IsOn)
                types.Add(row.TypeKey);
        return types;
    }

    private List<string> SelectedNames()
    {
        var names = new List<string>();
        foreach (ExportTypeRowView row in _rows)
            if (row.IsOn)
                names.Add(row.DisplayName);
        return names;
    }

    private int SelectedCost()
    {
        int total = 0;
        foreach (ExportTypeRowView row in _rows)
            if (row.IsOn)
                total += row.Cost;
        return total;
    }

    private async UniTaskVoid ExportAsync(List<string> types, int cost)
    {
        _exporting = true;
        if (Export_Button != null) Export_Button.interactable = false;
        if (Loading_Indicator != null) Loading_Indicator.SetActive(true);

        try
        {
            var req = new ExportDataRequest
            {
                Types = types,
                From = _from.ToString("yyyy-MM-dd"),
                To = _to.ToString("yyyy-MM-dd"),
            };

            ApiFile file = await ClubManager.Instance.ExportClubDataAsync(ClubContext.ClubId, req);

            // Paid for now — refresh the balance the screens show, and the log.
            if (AuthManager.Instance != null)
                AuthManager.Instance.GetPlayerProfileAsync().Forget();
            if (HistoryPanel != null && HistoryPanel.isActiveAndEnabled)
                HistoryPanel.Reload();

            // The server can't send this file again, so only call it done once
            // it's verifiably on the device.
            string fileName = file?.FileName ??
                              $"ClubExport_{req.From}_{req.To}.xlsx";
            ExportFileSaver.Result saved = ExportFileSaver.Save(file?.Bytes, fileName);

            if (!saved.Saved)
            {
                Debug.LogError($"[ExportDataModal] Save failed: {saved.Error}");
                ShowInfo(Loc.Get("export.save_failed"));
                return;
            }

            Debug.Log($"[ExportDataModal] Saved {file.Bytes.Length} bytes → {saved.Location}");

            // Modal stays open: TipsPopup may live inside it.
            ShowSaved(saved);
        }
        catch (ApiException e) when (e.Code == "G003")
        {
            ShowNotEnoughDiamonds(cost);
        }
        catch (ApiException e)
        {
            // V001 (no/invalid types), 403 (not a member), …
            Debug.LogWarning($"[ExportDataModal] Export rejected: {e.Code} {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? Loc.Get("export.failed") : e.Message);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ExportDataModal] Export failed: {e.Message}");
            ShowToast(Loc.Get("export.failed"));
        }
        finally
        {
            _exporting = false;
            if (Loading_Indicator != null) Loading_Indicator.SetActive(false);
            UpdateTotal();   // re-enable export only if something is still selected
        }
    }

    private void ShowSaved(ExportFileSaver.Result saved)
    {
        // Android: it's in Downloads — offer to open it.
        if (saved.InDownloads)
        {
            if (TipsPopup != null)
            {
                TipsPopup.Show(Loc.Get("export.saved_downloads", ("location", saved.Location)),
                    showCancel: true,
                    onConfirm: () =>
                    {
                        if (!ExportFileSaver.Open(saved))
                            ShowToast(Loc.Get("export.no_app", ("location", saved.Location)));
                    });
            }
            else
            {
                ShowToast(Loc.Get("export.saved", ("location", saved.Location)));
            }
            return;
        }

        // iOS / older Android: straight to the share sheet to pick where it goes.
        ShowToast(Loc.Get("export.saved", ("location", saved.Location)));
        ExportFileSaver.Open(saved);
    }

    private void ShowNotEnoughDiamonds(int cost)
    {
        string msg = Loc.Get("export.not_enough_diamonds", ("cost", cost.ToString("N0")));

        if (TipsPopup == null)
        {
            ShowToast(msg);
            return;
        }

        if (ShopScreen == null)
        {
            TipsPopup.Show(msg, showCancel: false, onConfirm: null);
            return;
        }

        TipsPopup.Show(msg, showCancel: true, onConfirm: () =>
        {
            ShopNavigation.Request(ShopTab.Diamond);
            if (ShopScreen.activeSelf) ShopScreen.SetActive(false);   // re-run OnEnable
            ShopScreen.SetActive(true);
        });
    }

    private void ShowInfo(string message)
    {
        if (TipsPopup != null)
            TipsPopup.Show(message, showCancel: false, onConfirm: null);
        else
            ShowToast(message);
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
