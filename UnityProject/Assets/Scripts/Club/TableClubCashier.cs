using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Game;
using ClubPoker.Networking.Models;

/// <summary>
/// The club Cashier on the game table: who may open it, opening it, and the red
/// dots for pending chip requests — on the top HUD's menu button and on the
/// Cashier row in the side drawer.
///
/// Same rule as the club screen: the Cashier is for chip staff only
/// (ClubContext.CanManageChips — Creator, or a Manager who isn't a Table
/// Manager), and the dots follow ClubContext.PendingCount, which the club socket
/// keeps current on every scene.
///
/// Put this on an always-active object in GameTableScene. Needs a ClubManager in
/// the scene too — the Cashier loads through it.
/// </summary>
public class TableClubCashier : MonoBehaviour
{
    [Tooltip("The Cashier panel instance in this scene (inactive by default).")]
    [SerializeField] private CashierPanelScript cashierPanel;

    [Header("Pending chip request dots")]
    [Tooltip("Red dot on the top HUD menu button that opens the side drawer.")]
    [SerializeField] private GameObject menuButtonDot;
    [Tooltip("Red dot on the Cashier row inside the side drawer.")]
    [SerializeField] private GameObject cashierRowDot;

    private void Awake()
    {
        TableMenuController.CanOpenCashier = CanOpen;
        TableMenuController.OpenCashier = Open;
    }

    private void OnEnable()
    {
        ClubContext.OnPendingCountChanged += OnPendingCountChanged;
        ClubSocketHandler.OnRoleChanged += OnRoleChanged;
        CashierPanelScript.OnClosed += OnCashierClosed;
    }

    private void OnDisable()
    {
        ClubContext.OnPendingCountChanged -= OnPendingCountChanged;
        ClubSocketHandler.OnRoleChanged -= OnRoleChanged;
        CashierPanelScript.OnClosed -= OnCashierClosed;
    }

    private void Start()
    {
        RefreshDots();
        InitAsync().Forget();
    }

    private async UniTaskVoid InitAsync()
    {
        await EnsureClubContextAsync();
        if (this == null) return;

        // The count carried over from the club screen may be stale (requests came
        // and went in between) — read it fresh on arrival.
        RefreshPendingAsync().Forget();
    }

    /// <summary>
    /// Arriving straight at the table (cold start, reconnect) skips the club
    /// screen, so the role it would have set is missing — or belongs to another
    /// club. Look this table's club up in our club list and set it, so the
    /// Cashier row and dots know who we are.
    /// </summary>
    private async UniTask EnsureClubContextAsync()
    {
        string tableClubId = TableContext.ClubId;

        if (!TableContext.IsClub || string.IsNullOrEmpty(tableClubId) ||
            ClubContext.ClubId == tableClubId || AuthManager.Instance == null)
            return;

        try
        {
            var clubs = await AuthManager.Instance.GetClubsAsync();
            ClubListData club = clubs?.Find(c => c != null && c.ClubId == tableClubId);

            if (club == null)
            {
                Debug.LogWarning($"[TableCashier] Club {tableClubId} not in our club list.");
                return;
            }

            ClubContext.SelectClub(club);
            Debug.Log($"[TableCashier] Club context restored | role={club.Role}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TableCashier] Club lookup failed: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        // Don't leave the menu pointing at a destroyed scene object.
        if (TableMenuController.OpenCashier == (Action)Open)
            TableMenuController.OpenCashier = null;
        if (TableMenuController.CanOpenCashier == (Func<bool>)CanOpen)
            TableMenuController.CanOpenCashier = null;
    }

    // ── Access ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A club table, and the club state we hold is THIS table's club — after a
    /// cold start straight into the table it's empty, and the Cashier would load
    /// nothing (or another club) — and we handle chips there.
    /// </summary>
    private bool CanOpen()
    {
        string tableClubId = TableContext.ClubId;

        return TableContext.IsClub &&
               !string.IsNullOrEmpty(tableClubId) &&
               ClubContext.ClubId == tableClubId &&
               ClubContext.CanManageChips &&
               cashierPanel != null &&
               ClubManager.Instance != null;
    }

    private void Open()
    {
        if (!CanOpen())
        {
            Debug.LogWarning("[TableCashier] Cashier not available here.");
            return;
        }

        // Already open — leave it, a second Init would start a second poll.
        if (cashierPanel.gameObject.activeInHierarchy)
            return;

        cashierPanel.transform.SetAsLastSibling();
        cashierPanel.gameObject.SetActive(true);
        cashierPanel.Init();
    }

    // ── Dots ────────────────────────────────────────────────────────────────

    private void OnPendingCountChanged(int _) => RefreshDots();

    private void RefreshDots()
    {
        bool show = CanOpen() && ClubContext.PendingCount > 0;

        if (menuButtonDot != null) menuButtonDot.SetActive(show);
        if (cashierRowDot != null) cashierRowDot.SetActive(show);
    }

    /// Requests may have been handled in the Cashier — re-read the count.
    private void OnCashierClosed() => RefreshPendingAsync().Forget();

    private async UniTaskVoid RefreshPendingAsync()
    {
        if (!CanOpen())
        {
            RefreshDots();
            return;
        }

        try
        {
            // Sets ClubContext.PendingCount, which raises the dot refresh.
            await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TableCashier] Pending count refresh failed: {e.Message}");
        }

        if (this != null)
            RefreshDots();
    }

    // ── Role change ─────────────────────────────────────────────────────────

    /// <summary>
    /// Promoted or demoted mid-game. The club screen re-reads the role when it
    /// next opens, but here nothing would — so update it now, or a demoted
    /// Manager keeps the Cashier (and the dots) until they leave the table.
    /// </summary>
    private void OnRoleChanged(ClubRoleChangedPayload payload)
    {
        if (payload == null || payload.ClubId != ClubContext.ClubId)
            return;

        bool tableManager = string.Equals(payload.NewRole, "TABLE_MANAGER",
                                          StringComparison.OrdinalIgnoreCase);

        ClubContext.Set(ClubContext.ClubId, ClubContext.ClubName,
                        ClubContext.ParseRole(payload.NewRole),
                        ClubContext.PoolChips, ClubContext.MembersChips,
                        ClubContext.AgentsCredit, tableManager);

        // Lost chip access while the Cashier was open — close it.
        if (!CanOpen() && cashierPanel != null && cashierPanel.gameObject.activeSelf)
            cashierPanel.gameObject.SetActive(false);

        if (CanOpen())
            RefreshPendingAsync().Forget();
        else
            RefreshDots();
    }
}
