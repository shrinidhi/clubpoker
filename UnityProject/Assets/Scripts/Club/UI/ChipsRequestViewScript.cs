using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

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
            await ClubChipManager.Instance.SetAutoRejectAsync(ClubContext.ClubId, value);
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
        if (ApproveAll_Button != null) ApproveAll_Button.interactable = !on;

        foreach (var row in _rows)
            if (row != null) row.SetApproveInteractable(!on);
    }

    // ── Load pending requests ─────────────────────────────────────────────────

    private async UniTaskVoid LoadPendingRequests()
    {
        ClearList();

        try
        {
            var res = await ClubChipManager.Instance.GetPendingRequestsAsync(ClubContext.ClubId);

            if (res?.Requests == null || res.Requests.Count == 0)
            {
                if (NoRequests_Text != null) NoRequests_Text.gameObject.SetActive(true);
                return;
            }

            if (NoRequests_Text != null) NoRequests_Text.gameObject.SetActive(false);
            _pendingItems = res.Requests;

            foreach (var item in res.Requests)
            {
                var obj = Instantiate(RequestRowPrefab, RequestList_Content);
                var row = obj.GetComponent<ChipRequestRowScript>();
                if (row != null)
                {
                    row.Setup(item,
                        id => OnApprove(id).Forget(),
                        id => OnReject(id).Forget(),
                        ClubContext.AutoReject);
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
            await ClubChipManager.Instance.ApproveRequestAsync(ClubContext.ClubId, requestId);
            await ClubChipManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            RefreshPoolChips();
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
        }
        catch (System.Exception e)
        {
            ShowStatus("Approve failed: " + e.Message);
            Debug.LogError($"[ChipsRequestViewScript] Approve error: {e.Message}");
        }
    }

    private async UniTaskVoid OnReject(string requestId)
    {
        try
        {
            await ClubChipManager.Instance.RejectRequestAsync(ClubContext.ClubId, requestId);
            await ClubChipManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
        }
        catch (System.Exception e)
        {
            ShowStatus("Reject failed: " + e.Message);
            Debug.LogError($"[ChipsRequestViewScript] Reject error: {e.Message}");
        }
    }

    // ── Approve All / Reject All ──────────────────────────────────────────────

    private void OnApproveAllTap()
    {
        if (_pendingItems.Count == 0) return;
        int count = _pendingItems.Count;
        if (ConfirmModal != null)
            ConfirmModal.Show("Approve all pending chip requests?", () => ApproveAll(count).Forget());
        else
            ApproveAll(count).Forget();
    }

    private void OnRejectAllTap()
    {
        if (_pendingItems.Count == 0) return;
        int count = _pendingItems.Count;
        if (ConfirmModal != null)
            ConfirmModal.Show("Reject all pending chip requests?", () => RejectAll().Forget());
        else
            RejectAll().Forget();
    }

    private async UniTaskVoid ApproveAll(int count)
    {
        ApproveAll_Button.interactable = false;
        RejectAll_Button.interactable  = false;
        try
        {
            await ClubChipManager.Instance.ApproveAllAsync(ClubContext.ClubId);
            await ClubChipManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            RefreshPoolChips();
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
            ShowStatus($"Approved {count} request(s)");
        }
        catch (System.Exception e)
        {
            ShowStatus("Approve all failed: " + e.Message);
            Debug.LogError($"[ChipsRequestViewScript] ApproveAll error: {e.Message}");
        }
        finally
        {
            ApproveAll_Button.interactable = !ClubContext.AutoReject;
            RejectAll_Button.interactable  = true;
        }
    }

    private async UniTaskVoid RejectAll()
    {
        ApproveAll_Button.interactable = false;
        RejectAll_Button.interactable  = false;
        try
        {
            await ClubChipManager.Instance.RejectAllAsync(ClubContext.ClubId);
            await ClubChipManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
            if (CashierPanel != null) CashierPanel.RefreshBadge();
            LoadPendingRequests().Forget();
        }
        catch (System.Exception e)
        {
            ShowStatus("Reject all failed: " + e.Message);
            Debug.LogError($"[ChipsRequestViewScript] RejectAll error: {e.Message}");
        }
        finally
        {
            ApproveAll_Button.interactable = !ClubContext.AutoReject;
            RejectAll_Button.interactable  = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
