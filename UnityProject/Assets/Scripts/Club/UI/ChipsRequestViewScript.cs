using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using ClubPoker.Core;

public class ChipsRequestViewScript : MonoBehaviour
{
    [Header("Top Bar")]
    public TextMeshProUGUI AvailableChips_Text;
    public Button AutoReject_Button;
    public Sprite AutoRejectOn_Sprite;
    public Sprite AutoRejectOff_Sprite;

    [Header("Request List")]
    public Transform RequestList_Content;
    public GameObject RequestRowPrefab;
    public TextMeshProUGUI NoRequests_Text;

    [Header("Bottom Buttons")]
    public Button RejectAll_Button;
    public Button ApproveAll_Button;

    [Header("Confirm Modal")]
    public ConfirmModalScript ConfirmModal;

    [Header("Parent")]
    public CashierPanelScript CashierPanel;

    [Header("Status")]
    public TextMeshProUGUI Status_Text;

    private List<ChipRequestItem>    _pendingItems = new List<ChipRequestItem>();
    private List<ChipRequestRowScript> _rows       = new List<ChipRequestRowScript>();

    private void Start()
    {
        AutoReject_Button.onClick.AddListener(OnAutoRejectTap);
        RejectAll_Button.onClick.AddListener(OnRejectAllTap);
        ApproveAll_Button.onClick.AddListener(OnApproveAllTap);
    }

    public void Init()
    {
        AvailableChips_Text.text = ClubContext.PoolChips.ToString("N0");
        RefreshAutoRejectUI();
        if (Status_Text != null) Status_Text.gameObject.SetActive(false);
        LoadPendingRequests().Forget();
    }

    public void RefreshPoolChips()
    {
        AvailableChips_Text.text = ClubContext.PoolChips.ToString("N0");
    }

    // ── Auto Reject ───────────────────────────────────────────────────────────

    private void OnAutoRejectTap()
    {
        SetAutoReject(!ClubContext.AutoReject).Forget();
    }

    private async UniTaskVoid SetAutoReject(bool value)
    {
        AutoReject_Button.interactable = false;
        try
        {
            await ClubManager.Instance.SetAutoRejectAsync(ClubContext.ClubId, value);
            ClubContext.AutoReject = value;
            RefreshAutoRejectUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChipsRequestViewScript] SetAutoReject error: {e.Message}");
        }
        finally
        {
            AutoReject_Button.interactable = true;
        }
    }

    private void RefreshAutoRejectUI()
    {
        bool on = ClubContext.AutoReject;
        AutoReject_Button.image.sprite = on ? AutoRejectOn_Sprite : AutoRejectOff_Sprite;
        RefreshBulkButtons();

        foreach (var row in _rows)
            if (row != null) row.SetApproveInteractable(!on);
    }

    // ── Load pending requests ─────────────────────────────────────────────────

    private async UniTaskVoid LoadPendingRequests()
    {
        // No rows yet, so this also keeps the bulk buttons off through a failed load.
        ClearList();
        RefreshBulkButtons();

        try
        {
            var res = await ClubManager.Instance.GetPendingRequestsAsync(ClubContext.ClubId);

            if (res?.Requests == null || res.Requests.Count == 0)
            {
                if (NoRequests_Text != null) NoRequests_Text.gameObject.SetActive(true);
                return;
            }

            if (NoRequests_Text != null) NoRequests_Text.gameObject.SetActive(false);
            _pendingItems = res.Requests;
            RefreshBulkButtons();

            foreach (var item in res.Requests)
            {
                var obj = Instantiate(RequestRowPrefab, RequestList_Content);
                var row = obj.GetComponent<ChipRequestRowScript>();
                if (row != null)
                {
                    row.Setup(item,
                        id => OnApprove(id).Forget(),
                        id => OnReject(id).Forget(),
                        ClubContext.AutoReject,
                        () => PoolCovers(item.Amount));
                    _rows.Add(row);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChipsRequestViewScript] LoadPendingRequests error: {e.Message}");
        }
    }

    // ── Approve / Reject single ───────────────────────────────────────────────

    private async UniTaskVoid OnApprove(string requestId)
    {
        try
        {
            await ClubManager.Instance.ApproveRequestAsync(ClubContext.ClubId, requestId);
            await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            RefreshPoolChips();
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
        }
        catch (System.Exception e)
        {
            ShowStatus(ApproveFailureMessage(e, "chipreq.approve_failed"));
            Debug.LogError($"[ChipsRequestViewScript] Approve error: {e.Message}");
            ReloadAfterFailure().Forget();
        }
    }

    private async UniTaskVoid OnReject(string requestId)
    {
        try
        {
            await ClubManager.Instance.RejectRequestAsync(ClubContext.ClubId, requestId);
            await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
        }
        catch (System.Exception e)
        {
            ShowStatus(Loc.Get("chipreq.reject_failed", ("reason", e.Message)));
            Debug.LogError($"[ChipsRequestViewScript] Reject error: {e.Message}");
            ReloadAfterFailure().Forget();
        }
    }

    // ── Approve All / Reject All ──────────────────────────────────────────────

    private void OnApproveAllTap()
    {
        if (_pendingItems.Count == 0) return;

        long total = 0;
        foreach (var item in _pendingItems)
            total += item.Amount;
        if (!PoolCovers(total)) return;

        // A single request needs no "approve all pending requests?" prompt — there
        // is nothing "all" about it, and the row's amount is already on screen (CP-369).
        int count = _pendingItems.Count;
        if (ConfirmModal != null && count > 1)
            ConfirmModal.Show(Loc.Get("chipreq.confirm_approve_all"), () => ApproveAll(count).Forget());
        else
            ApproveAll(count).Forget();
    }

    private void OnRejectAllTap()
    {
        if (_pendingItems.Count == 0) return;
        int count = _pendingItems.Count;
        if (ConfirmModal != null)
            ConfirmModal.Show(Loc.Get("chipreq.confirm_reject_all"), () => RejectAll().Forget());
        else
            RejectAll().Forget();
    }

    private async UniTaskVoid ApproveAll(int count)
    {
        ApproveAll_Button.interactable = false;
        RejectAll_Button.interactable  = false;
        try
        {
            await ClubManager.Instance.ApproveAllAsync(ClubContext.ClubId);
            await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            RefreshPoolChips();
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
            ShowStatus(Loc.Get("chipreq.approved_count", ("count", count)));
        }
        catch (System.Exception e)
        {
            ShowStatus(ApproveFailureMessage(e, "chipreq.approve_all_failed"));
            Debug.LogError($"[ChipsRequestViewScript] ApproveAll error: {e.Message}");
            ReloadAfterFailure().Forget();
        }
        finally
        {
            RefreshBulkButtons();
        }
    }

    private async UniTaskVoid RejectAll()
    {
        ApproveAll_Button.interactable = false;
        RejectAll_Button.interactable  = false;
        try
        {
            await ClubManager.Instance.RejectAllAsync(ClubContext.ClubId);
            await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
        }
        catch (System.Exception e)
        {
            ShowStatus(Loc.Get("chipreq.reject_all_failed", ("reason", e.Message)));
            Debug.LogError($"[ChipsRequestViewScript] RejectAll error: {e.Message}");
            ReloadAfterFailure().Forget();
        }
        finally
        {
            RefreshBulkButtons();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Pre-check so an approve the pool can't cover never leaves the screen. The pool
    // figure can be up to a poll old, so the server's G003 below is still handled.
    private bool PoolCovers(long amount)
    {
        if (amount <= ClubContext.PoolChips)
            return true;

        ShowStatus(Loc.Get("chipreq.not_enough_chips"));
        return false;
    }

    // G003 = the pool can't cover the request; anything else keeps the server's reason.
    private static string ApproveFailureMessage(System.Exception e, string failedKey)
    {
        return (e as ClubPoker.Networking.ApiException)?.Code == "G003"
            ? Loc.Get("chipreq.not_enough_chips")
            : Loc.Get(failedKey, ("reason", e.Message));
    }

    // A failed action (e.g. G003 pool too low) leaves the tapped row's buttons off
    // and the figures out of date. Rebuild from the server; the error stays showing.
    private async UniTaskVoid ReloadAfterFailure()
    {
        LoadPendingRequests().Forget();

        try
        {
            await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            RefreshPoolChips();
            if (CashierPanel != null) CashierPanel.RefreshBadge();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ChipsRequestViewScript] Summary refresh failed: {e.Message}");
        }
    }

    // Bulk actions need rows to act on; Approve All is also off under Auto Reject.
    private void RefreshBulkButtons()
    {
        bool hasRows = _pendingItems.Count > 0;
        if (RejectAll_Button != null)  RejectAll_Button.interactable  = hasRows;
        if (ApproveAll_Button != null) ApproveAll_Button.interactable = hasRows && !ClubContext.AutoReject;
    }

    private void ClearList()
    {
        _pendingItems.Clear();
        _rows.Clear();
        for (int i = RequestList_Content.childCount - 1; i >= 0; i--)
            Destroy(RequestList_Content.GetChild(i).gameObject);
        if (NoRequests_Text != null) NoRequests_Text.gameObject.SetActive(false);
    }

    private void ShowStatus(string message)
    {
        if (Status_Text == null) return;
        Status_Text.text = message;
        Status_Text.gameObject.SetActive(true);
        FadeStatus().Forget();
    }

    private async UniTaskVoid FadeStatus()
    {
        await UniTask.Delay(2500);
        if (Status_Text != null) Status_Text.gameObject.SetActive(false);
    }
}
