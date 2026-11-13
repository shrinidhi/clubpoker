using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;

/// <summary>
/// The key button on the table (top-left) that the host uses to review buy-in
/// requests. Visible only while the host has Auth-Buyin on, with a red dot while
/// any request is waiting.
///
/// Put this on an ALWAYS-ACTIVE object, not on the key button itself — the
/// button is what gets hidden, and a hidden object's Start never runs.
/// </summary>
public class BuyInAuthKeyButton : MonoBehaviour
{
    [SerializeField] private Button keyButton;

    [Tooltip("Buy-in authorization request list.")]
    [SerializeField] private GameObject authListPanel;

    [Header("Pending badge")]
    [Tooltip("Red dot on the key button, shown while any request is pending.")]
    [SerializeField] private GameObject redDot;
    [Tooltip("Optional — count inside the dot.")]
    [SerializeField] private TextMeshProUGUI redDotCount;

    [Tooltip("How often to check for new requests. Interim: the server sends no " +
             "event for a new buy-in request yet, so this polls.")]
    [SerializeField] private float pollSeconds = 5f;

    private CancellationTokenSource _pollCts;

    private void Start()
    {
        if (keyButton != null)
            keyButton.onClick.AddListener(OnKeyTap);

        // Always start closed, whatever state the scene was saved in — an
        // already-active panel would swallow the first tap (SetActive(true) on
        // an active object runs no OnEnable, so nothing slides in or loads).
        if (authListPanel != null)
            authListPanel.SetActive(false);

        SetDot(0);

        TableContext.OnClubRowChanged += Apply;
        Apply();

        // The join snapshot may be stale or, after a cold start, missing the
        // settings entirely — read the row once on arrival.
        if (TableContext.IsClub)
            HostTableService.RefreshAsync().Forget();

        _pollCts = new CancellationTokenSource();
        PollPendingAsync(_pollCts.Token).Forget();
    }

    private void OnDestroy()
    {
        TableContext.OnClubRowChanged -= Apply;

        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private bool ShouldShow =>
        TableContext.IsHost && TableContext.Info != null && TableContext.Info.AuthBuyIn;

    private void Apply()
    {
        bool show = ShouldShow;

        if (keyButton != null)
            keyButton.gameObject.SetActive(show);

        // Auth-Buyin turned off while the list was open — nothing left to review.
        if (!show && authListPanel != null)
            authListPanel.SetActive(false);

        if (!show)
            SetDot(0);
    }

    private void OnKeyTap()
    {
        Debug.Log($"[BuyInAuthKey] Tapped — panel " +
                  $"{(authListPanel == null ? "NOT ASSIGNED" : authListPanel.name)}");

        if (authListPanel == null) return;

        // Re-open from scratch if it's somehow still up, so it slides and reloads.
        if (authListPanel.activeSelf)
            authListPanel.SetActive(false);

        authListPanel.SetActive(true);
    }

    // ── Pending badge ────────────────────────────────────────────────────────

    /// <summary>
    /// Check the pending count every pollSeconds while the key is showing. Skips
    /// while the list is open — it polls the same endpoint itself, and the host
    /// is already looking at the requests — and checks straight away once it
    /// closes, so approvals made there clear the dot without a wait.
    /// </summary>
    private async UniTaskVoid PollPendingAsync(CancellationToken ct)
    {
        bool listWasOpen = false;
        float sinceCheck = float.MaxValue;   // check on the first pass

        while (!ct.IsCancellationRequested)
        {
            bool listOpen = authListPanel != null && authListPanel.activeInHierarchy;

            if (!ShouldShow)
            {
                sinceCheck = float.MaxValue;
            }
            else if (!listOpen)
            {
                if (listWasOpen || sinceCheck >= pollSeconds)
                {
                    sinceCheck = 0f;
                    await RefreshDotAsync();
                    if (ct.IsCancellationRequested) return;
                }
            }

            listWasOpen = listOpen;

            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(0.5f),
                                                 cancellationToken: ct)
                                          .SuppressCancellationThrow();
            if (cancelled) return;

            sinceCheck += 0.5f;
        }
    }

    private async UniTask RefreshDotAsync()
    {
        TableInfo info = TableContext.Info;
        if (info == null || string.IsNullOrEmpty(info.ClubId) ||
            string.IsNullOrEmpty(info.ClubTableRowId) || AuthManager.Instance == null)
            return;

        try
        {
            var requests = await AuthManager.Instance.GetBuyInRequestsAsync(
                info.ClubId, info.ClubTableRowId);

            if (this == null) return;

            // Auth-Buyin may have gone off while the call was out.
            SetDot(ShouldShow ? requests?.Count ?? 0 : 0);
        }
        catch (Exception e)
        {
            // Keep whatever the dot showed — one failed poll isn't "no requests".
            Debug.LogWarning($"[BuyInAuthKey] Pending check failed: {e.Message}");
        }
    }

    private void SetDot(int count)
    {
        if (redDot != null)
            redDot.SetActive(count > 0);

        if (redDotCount != null)
            redDotCount.text = count > 9 ? "9+" : count.ToString();
    }
}
