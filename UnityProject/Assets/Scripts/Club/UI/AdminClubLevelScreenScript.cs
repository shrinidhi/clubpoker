using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Club Level. Lists the level tiers (from GET /level/config), highlights the
/// current level, and lets the creator upgrade to a higher tier (POST /level/upgrade).
/// Two sub-popups, both the shared AlertPopup:
///  - Tip (?)   → info only.
///  - Level tap → "Upgrade to {label} for {cost} diamonds? Valid for 30 days." confirm.
/// Backend is LIVE for this feature.
/// </summary>
public class AdminClubLevelScreenScript : MonoBehaviour
{
    [Header("Header")]
    public Button Back_Button;
    public Button Tip_Button;                 // question mark

    [TextArea] public string TipMessage =
        "Higher club levels raise your agent and member caps. Upgrades are paid in diamonds " +
        "and last 30 days.";

    [Header("Current Level (top info)")]
    public TextMeshProUGUI Top_Level_Text;        // e.g. "Club Level 0" / "Free"
    public TextMeshProUGUI Top_MaxAgents_Text;
    public TextMeshProUGUI Top_MaxMembers_Text;
    public TextMeshProUGUI Top_DiamondCost_Text;

    [Header("List")]
    public ScrollRect ScrollView;              // to reset scroll to top on open
    public Transform ListContainer;            // ScrollView ▸ Content
    public ClubLevelRowScript RowPrefab;

    [Header("Shared")]
    public AlertPopup AlertPopup;

    private readonly List<ClubLevelRowScript> _rows = new List<ClubLevelRowScript>();
    private int _currentLevel;
    private bool _busy;

    private void Start()
    {
        if (Back_Button != null) Back_Button.onClick.AddListener(Close);
        if (Tip_Button  != null) Tip_Button.onClick.AddListener(OnTipTap);
    }

    private void OnEnable()
    {
        Load().Forget();
    }

    private async UniTaskVoid Load()
    {
        try
        {
            var cfg = await ClubManager.Instance.GetClubLevelConfigAsync(ClubContext.ClubId);
            Populate(cfg);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminClubLevelScreenScript] load error: {e.Message}");
            ShowToast("Failed to load club levels");
        }
    }

    private void Populate(ClubLevelConfigResponse cfg)
    {
        if (cfg == null) return;
        _currentLevel = cfg.ClubLevel;

        foreach (var r in _rows)
            if (r != null) Destroy(r.gameObject);
        _rows.Clear();

        if (cfg.Config == null) return;

        // Current level (matches clubLevel; fall back to the first entry) → top info block.
        ClubLevelItem current = cfg.Config.Find(c => c.Level == _currentLevel)
                                ?? (cfg.Config.Count > 0 ? cfg.Config[0] : null);
        ApplyTopInfo(current);

        if (RowPrefab == null || ListContainer == null) return;

        // Remaining tiers → scroll list (current level shown on top, excluded here).
        foreach (var item in cfg.Config)
        {
            if (current != null && item.Level == current.Level) continue;

            var row = Instantiate(RowPrefab, ListContainer);
            bool isUpgradeable = item.Level > _currentLevel;
            row.Setup(item, isCurrent: false, isUpgradeable, OnLevelTap);
            _rows.Add(row);
        }

        ResetScroll();
    }

    // Rebuild the layout first so content height is known, then snap to top.
    private void ResetScroll()
    {
        if (ScrollView == null) return;
        Canvas.ForceUpdateCanvases();
        ScrollView.verticalNormalizedPosition = 1f;
    }

    private void ApplyTopInfo(ClubLevelItem item)
    {
        if (item == null) return;
        if (Top_Level_Text       != null) Top_Level_Text.text       = "LV. " + item.Level;
        if (Top_MaxAgents_Text   != null) Top_MaxAgents_Text.text   = item.MaxAgents.ToString();
        if (Top_MaxMembers_Text  != null) Top_MaxMembers_Text.text  = item.MaxMembers.ToString();
        if (Top_DiamondCost_Text != null) Top_DiamondCost_Text.text = item.DiamondCost.ToString("N0");
    }

    private void OnTipTap()
    {
        if (AlertPopup != null)
            AlertPopup.Show("Super Agent",TipMessage, showCancel: false, onConfirm: null);
    }

    private void OnLevelTap(ClubLevelItem item)
    {
        if (_busy) return;

        string msg = $"Upgrade to {item.Label} for {item.DiamondCost:N0} diamonds? " +
                     "Valid for 30 days.";

        if (AlertPopup != null)
            AlertPopup.Show("Confirm Upgrade", msg, showCancel: true,
                onConfirm: () => Upgrade(item).Forget());
        else
            Upgrade(item).Forget();
    }

    private async UniTaskVoid Upgrade(ClubLevelItem item)
    {
        _busy = true;
        try
        {
            var cfg = await ClubManager.Instance
                .UpgradeClubLevelAsync(ClubContext.ClubId, item.Level);

            // POST is expected to return the refreshed config; refetch if it doesn't.
            if (cfg == null || cfg.Config == null)
                cfg = await ClubManager.Instance.GetClubLevelConfigAsync(ClubContext.ClubId);

            Populate(cfg);
            ShowToast($"Upgraded to {item.Label}");
        }
        catch (Exception e)
        {
            // Economy errors (e.g. not enough diamonds) surface here.
            Debug.LogError($"[AdminClubLevelScreenScript] upgrade error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Upgrade failed" : e.Message);
        }
        finally
        {
            _busy = false;
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
