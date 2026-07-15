using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;

/// <summary>
/// Single API access point for everything club-scoped: chips, data/export, and admin
/// settings. Replaces ClubChipManager / ClubDataManager / ClubAdminManager, which were
/// three copies of the same singleton scaffold wrapping thin _api calls.
///
/// Views go through this, never ApiClient directly.
///
/// Scene-scoped (ClubScene only) — lives on the ClubViewController GameObject. It holds no
/// state beyond the ApiClient handle, so it does not need to survive scene loads.
///
/// CLIENT-FIRST: a few admin endpoints are not ready yet. While <see cref="UseStub"/> is
/// true those methods return mock data after a short delay so the UI is driveable. The real
/// endpoint path sits in a // TODO next to each — flip UseStub to false once the server is up.
/// </summary>
public class ClubManager : MonoBehaviour
{
    public static ClubManager Instance { get; private set; }

    // Per-endpoint stub gates. Flip each to false once that endpoint is live.
    private const bool StubAdminStats = true;   // GET /admin/stats — not implemented yet

    // Resolved lazily: callers can hit this from their own Start(), which may run before
    // this manager's Start().
    private ApiClient _api => ApiClient.Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    #region Chips
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<SendChipsResponse> SendChipsAsync(
        string clubId, List<string> memberIds, long amount)
    {
        var req = new SendChipsRequest { MemberIds = memberIds, Amount = amount };
        return await _api.Post<SendChipsResponse>($"/api/clubs/{clubId}/chips/send-bulk", req);
    }

    public async UniTask<ClaimChipsResponse> ClaimChipsAsync(
        string clubId, List<string> memberIds, long amount, bool claimAll = false)
    {
        var req = new ClaimChipsRequest { MemberIds = memberIds, Amount = amount, ClaimAll = claimAll };
        return await _api.Post<ClaimChipsResponse>($"/api/clubs/{clubId}/chips/claim-bulk", req);
    }

    public async UniTask<ChipRecordsData> GetChipRecordsAsync(
        string clubId, int page = 1, string search = null, string filter = null, int limit = 30)
    {
        var query = $"/api/clubs/{clubId}/chips/records?limit={limit}&page={page}";
        if (!string.IsNullOrEmpty(search))
            query += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(filter))
            query += $"&type={filter}";
        return await _api.Get<ChipRecordsData>(query);
    }

    public async UniTask<ChipRequestResponse> RequestChipsAsync(string clubId, long amount)
    {
        var req = new ChipRequestPayload { Amount = amount };
        return await _api.Post<ChipRequestResponse>($"/api/clubs/{clubId}/chips/request", req);
    }

    public async UniTask<ChipRequestsData> GetPendingRequestsAsync(string clubId)
    {
        return await _api.Get<ChipRequestsData>(
            $"/api/clubs/{clubId}/chips/requests?status=PENDING&limit=50");
    }

    public async UniTask ApproveRequestAsync(string clubId, string requestId)
    {
        await _api.Post<object>(
            $"/api/clubs/{clubId}/chips/requests/{requestId}/approve", new { });
    }

    public async UniTask RejectRequestAsync(string clubId, string requestId)
    {
        await _api.Post<object>(
            $"/api/clubs/{clubId}/chips/requests/{requestId}/reject", new { });
    }

    public async UniTask ApproveAllAsync(string clubId)
    {
        await _api.Post<object>($"/api/clubs/{clubId}/chips/requests/approve-all", new { });
    }

    public async UniTask RejectAllAsync(string clubId)
    {
        await _api.Post<object>($"/api/clubs/{clubId}/chips/requests/reject-all", new { });
    }

    public async UniTask SetAutoRejectAsync(string clubId, bool autoReject)
    {
        await _api.Put<object>(
            $"/api/clubs/{clubId}/chips/auto-reject",
            new AutoRejectRequest { AutoReject = autoReject });
    }

    public async UniTask<AddChipsResponse> AddChipsAsync(string clubId, long amount)
    {
        return await _api.Post<AddChipsResponse>(
            $"/api/clubs/{clubId}/chips/pool",
            new AddChipsRequest { Amount = amount });
    }

    /// <summary>Fetches the chips summary and writes it straight into ClubContext.</summary>
    public async UniTask GetChipsSummaryAsync(string clubId)
    {
        var res = await _api.Get<ChipsSummaryData>($"/api/clubs/{clubId}/chips/summary");
        if (res != null)
        {
            ClubContext.UpdatePoolChips(res.PoolChips, res.MembersChips, res.AgentsCredit);
            ClubContext.AutoReject   = res.AutoReject;
            ClubContext.PendingCount = res.PendingCount;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Members
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<ClubMembersData> GetMembersAsync(
        string clubId, string search = null, bool groupByRole = false, string sortBy = null)
    {
        var query = $"/api/clubs/{clubId}/members?";
        if (!string.IsNullOrEmpty(sortBy))
            query += $"sortBy={sortBy}&";
        query += "limit=100";
        if (!string.IsNullOrEmpty(search))
            query += $"&search={Uri.EscapeDataString(search)}";
        if (groupByRole)
            query += "&groupByRole=true";
        return await _api.Get<ClubMembersData>(query);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Club detail
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<ClubDetailResponse> GetClubDetailAsync(string clubId)
    {
        return await _api.Get<ClubDetailResponse>($"/api/clubs/{clubId}");
    }

    /// Partial PUT — send only the fields being changed.
    /// Keys: feeAllocPercent | scrollMessage | name | badge | ...
    public async UniTask<ClubDetailResponse> UpdateClubAsync(
        string clubId, Dictionary<string, object> fields)
    {
        return await _api.Put<ClubDetailResponse>($"/api/clubs/{clubId}", fields);
    }

    public async UniTask<ClubDetailResponse> UpdateFeeAllocPercentAsync(string clubId, int percent)
    {
        return await UpdateClubAsync(clubId,
            new Dictionary<string, object> { { "feeAllocPercent", percent } });
    }

    public async UniTask DisbandClubAsync(string clubId)
    {
        await _api.Delete<object>($"/api/clubs/{clubId}");
    }

    /// GET /api/clubs/{clubId}/tables — live tables (used by the scroll-message picker).
    public async UniTask<ClubPoker.Networking.Models.ClubTablesApiResponse> GetClubTablesAsync(string clubId)
    {
        return await _api.Get<ClubPoker.Networking.Models.ClubTablesApiResponse>(
            $"/api/clubs/{clubId}/tables");
    }

    /// POST /api/clubs/{clubId}/scroll-message  { message, tableId }.
    /// tableId is optional (null when no "jump to table" is attached).
    public async UniTask SetScrollMessageAsync(string clubId, string message, string tableId)
    {
        await _api.Post<object>($"/api/clubs/{clubId}/scroll-message",
            new { message, tableId });
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Data / Export
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<ClubDataResponse> GetClubDataAsync(
        string clubId, DateTime from, DateTime to, string variant)
    {
        string query =
            $"/api/clubs/{clubId}/data?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&variant={variant}";
        return await _api.Get<ClubDataResponse>(query);
    }

    public async UniTask<ExportDataResponse> ExportClubDataAsync(string clubId, ExportDataRequest req)
    {
        return await _api.Post<ExportDataResponse>($"/api/clubs/{clubId}/data/export", req);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Admin — Club Level
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<ClubLevelConfigResponse> GetClubLevelConfigAsync(string clubId)
    {
        return await _api.Get<ClubLevelConfigResponse>($"/api/clubs/{clubId}/level/config");
    }

    /// Upgrades to the target level; returns the refreshed config.
    public async UniTask<ClubLevelConfigResponse> UpgradeClubLevelAsync(string clubId, int targetLevel)
    {
        return await _api.Post<ClubLevelConfigResponse>(
            $"/api/clubs/{clubId}/level/upgrade", new { targetLevel });
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Admin — Notification settings
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<NotificationSettingsData> GetNotificationSettingsAsync(string clubId)
    {
        return await _api.Get<NotificationSettingsData>(
            $"/api/clubs/{clubId}/notification-settings");
    }

    /// Partial update — sends only the flipped flag, e.g. {"clubApplicants":false}.
    /// Keys: clubApplicants | memberLeave | chipsRequest
    public async UniTask<NotificationSettingsData> UpdateNotificationSettingAsync(
        string clubId, string key, bool value)
    {
        var body = new Dictionary<string, bool> { { key, value } };
        return await _api.Put<NotificationSettingsData>(
            $"/api/clubs/{clubId}/notification-settings", body);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Admin — Tables
    // ═══════════════════════════════════════════════════════════════════════════

    /// Closes every table with fewer than two players (MTT excluded, server-side rule).
    public async UniTask DisbandEmptyTablesAsync(string clubId)
    {
        await _api.Post<object>($"/api/clubs/{clubId}/tables/disband-empty", new { });
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Admin — Posters
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<PostersResponse> GetPostersAsync(string clubId)
    {
        return await _api.Get<PostersResponse>($"/api/clubs/{clubId}/posters");
    }

    /// url is a base64 data URI ("data:image/jpeg;base64,...").
    public async UniTask<PosterResponse> UploadPosterAsync(
        string clubId, string url, string filename, long fileSize)
    {
        return await _api.Post<PosterResponse>($"/api/clubs/{clubId}/posters",
            new { url, filename, fileSize });
    }

    public async UniTask DeletePosterAsync(string clubId, string posterId)
    {
        await _api.Delete<object>($"/api/clubs/{clubId}/posters/{posterId}");
    }

    /// Publish/reorder posters ("Post" button). TODO: confirm the real endpoint + body shape.
    /// Assumed: POST /api/clubs/{clubId}/posters/order  { posterIds: [...] } in display order.
    public async UniTask PostPostersAsync(string clubId, List<string> orderedPosterIds)
    {
        await _api.Post<object>($"/api/clubs/{clubId}/posters/order",
            new { posterIds = orderedPosterIds });
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Admin — Stats (stubbed) & Notification
    // ═══════════════════════════════════════════════════════════════════════════

    public async UniTask<AdminStatsData> GetAdminStatsAsync(string clubId)
    {
        // TODO (live): GET /api/clubs/{clubId}/admin/stats — endpoint doesn't exist yet,
        // so return zeros. Flip StubAdminStats to false once it does.
        if (StubAdminStats)
            return StubStats();

        return await _api.Get<AdminStatsData>($"/api/clubs/{clubId}/admin/stats");
    }

    /// POST /api/clubs/{clubId}/notification  { title, content }
    public async UniTask SendNotificationAsync(string clubId, string title, string content)
    {
        await _api.Post<object>($"/api/clubs/{clubId}/notification", new { title, content });
    }

    // ── Mobile Push (LIVE) ────────────────────────────────────────────────────

    /// Player diamond balance (not club-scoped, but the push flow needs it).
    public async UniTask<DiamondsData> GetDiamondsAsync()
    {
        return await _api.Get<DiamondsData>("/api/player/diamonds");
    }

    /// POST /api/clubs/{clubId}/push  { title, content } → cost comes back in the response.
    public async UniTask<PushResponse> SendPushAsync(string clubId, string title, string content)
    {
        return await _api.Post<PushResponse>($"/api/clubs/{clubId}/push",
            new { title, content });
    }

    private static AdminStatsData StubStats()
    {
        return new AdminStatsData
        {
            Fee            = new AdminStatValue { Today = 0, ThisWeek = 0, LastWeek = 0, Overall = 0 },
            Games          = new AdminStatValue { Today = 0, ThisWeek = 0, LastWeek = 0, Overall = 0 },
            PlayerWinnings = new AdminStatValue { Today = 0, ThisWeek = 0, LastWeek = 0, Overall = 0 },
            InsuranceEV    = new AdminStatValue { Today = 0, ThisWeek = 0, LastWeek = 0, Overall = 0 },
        };
    }

    #endregion
}
