using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;


public class ClubChipManager : MonoBehaviour
{
    public static ClubChipManager Instance { get; private set; }

    private ApiClient _api;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _api = ApiClient.Instance;
    }

    // ── Send chips to member(s) ────────────────────────────────────────────

    public async UniTask<SendChipsResponse> SendChipsAsync(
        string clubId, List<string> memberIds, long amount)
    {
        var req = new SendChipsRequest { MemberIds = memberIds, Amount = amount };
        return await _api.Post<SendChipsResponse>($"/api/clubs/{clubId}/chips/send-bulk", req);
    }

    // ── Claim chips back from member ──────────────────────────────────────

    public async UniTask<ClaimChipsResponse> ClaimChipsAsync(
        string clubId, List<string> memberIds, long amount, bool claimAll = false)
    {
        var req = new ClaimChipsRequest { MemberIds = memberIds, Amount = amount, ClaimAll = claimAll };
        return await _api.Post<ClaimChipsResponse>($"/api/clubs/{clubId}/chips/claim-bulk", req);
    }

    // ── Chip transaction records ───────────────────────────────────────────

    public async UniTask<ChipRecordsData> GetChipRecordsAsync(
        string clubId, int page = 1, string search = null, string filter = null)
    {
        var query = $"/api/clubs/{clubId}/chips/records?limit=30&page={page}";
        if (!string.IsNullOrEmpty(search))
            query += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(filter))
            query += $"&type={filter}";
        return await _api.Get<ChipRecordsData>(query);
    }

    // ── Member requests chips ──────────────────────────────────────────────

    public async UniTask<ChipRequestResponse> RequestChipsAsync(string clubId, long amount)
    {
        var req = new ChipRequestPayload { Amount = amount };
        return await _api.Post<ChipRequestResponse>($"/api/clubs/{clubId}/chips/request", req);
    }

    // ── Admin fetches pending requests ────────────────────────────────────

    public async UniTask<ChipRequestsData> GetPendingRequestsAsync(string clubId)
    {
        return await _api.Get<ChipRequestsData>(
            $"/api/clubs/{clubId}/chips/requests?status=PENDING&limit=50");
    }

    // ── Admin approves or rejects a request ───────────────────────────────

    public async UniTask ApproveRequestAsync(string clubId, string requestId)
    {
        await _api.Post<object>(
            $"/api/clubs/{clubId}/chips/requests/{requestId}/approve",
            new { });
    }

    public async UniTask RejectRequestAsync(string clubId, string requestId)
    {
        await _api.Post<object>(
            $"/api/clubs/{clubId}/chips/requests/{requestId}/reject",
            new { });
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

    // ── Add chips to club pool ─────────────────────────────────────────────

    public async UniTask<AddChipsResponse> AddChipsAsync(string clubId, long amount)
    {
        return await _api.Post<AddChipsResponse>(
            $"/api/clubs/{clubId}/chips/pool",
            new AddChipsRequest { Amount = amount });
    }

    // ── Chips summary ─────────────────────────────────────────────────────

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

    // ── Club members list ─────────────────────────────────────────────────

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
}
