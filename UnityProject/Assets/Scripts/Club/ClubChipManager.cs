using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ClubPoker.Auth;
using ClubPoker.Networking;

namespace ClubPoker.Club
{
    public class ClubChipManager : MonoBehaviour
    {
        public static ClubChipManager Instance { get; private set; }

        public event Action<BalanceUpdatedEvent>      OnBalanceUpdated;
        public event Action<ChipRequestReceivedEvent> OnChipRequestReceived;

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
            SocketManager.Instance.On("balance:updated",        OnBalanceUpdatedSocket);
            SocketManager.Instance.On("chips:request_received", OnChipRequestReceivedSocket);
        }

        private void OnDestroy()
        {
            if (SocketManager.Instance == null) return;
            SocketManager.Instance.Off("balance:updated");
            SocketManager.Instance.Off("chips:request_received");
        }

        // ── CLUB-678: Send chips to member(s) ────────────────────────────────

        public async UniTask<SendChipsResponse> SendChipsAsync(
            string clubId, List<string> memberIds, long amount)
        {
            var req = new SendChipsRequest { MemberIds = memberIds, Amount = amount };
            var res = await _api.Post<SendChipsResponse>($"/api/clubs/{clubId}/chips/send", req);
            ClubContext.Instance.UpdatePoolChips(res.PoolChips, res.MembersChips, ClubContext.Instance.AgentsCredit);
            return res;
        }

        // ── CLUB-679: Claim chips back from member ────────────────────────────

        public async UniTask<ClaimChipsResponse> ClaimChipsAsync(
            string clubId, string memberId, long amount, bool claimAll = false)
        {
            var req = new ClaimChipsRequest { MemberId = memberId, Amount = amount, ClaimAll = claimAll };
            var res = await _api.Post<ClaimChipsResponse>($"/api/clubs/{clubId}/chips/claim", req);
            ClubContext.Instance.UpdatePoolChips(res.PoolChips, res.MembersChips, ClubContext.Instance.AgentsCredit);
            return res;
        }

        // ── CLUB-680: Chip transaction audit log ──────────────────────────────

        public async UniTask<ChipRecordsResponse> GetChipRecordsAsync(
            string clubId, int page = 1, string search = null)
        {
            var query = $"/api/clubs/{clubId}/chips/record?page={page}&limit=20";
            if (!string.IsNullOrEmpty(search))
                query += $"&search={Uri.EscapeDataString(search)}";
            return await _api.Get<ChipRecordsResponse>(query);
        }

        // ── CLUB-681: Member requests chips ──────────────────────────────────

        public async UniTask<ChipRequestResponse> RequestChipsAsync(string clubId, long amount)
        {
            var req = new ChipRequestPayload { Amount = amount };
            return await _api.Post<ChipRequestResponse>($"/api/clubs/{clubId}/chips/request", req);
        }

        // ── CLUB-681: Admin fetches pending requests ──────────────────────────

        public async UniTask<ChipRequestsResponse> GetPendingRequestsAsync(string clubId)
        {
            return await _api.Get<ChipRequestsResponse>(
                $"/api/clubs/{clubId}/chips/requests?status=PENDING");
        }

        // ── CLUB-681: Admin approves or rejects a request ─────────────────────

        public async UniTask ApproveRequestAsync(string clubId, string requestId)
        {
            await _api.Post<object>(
                $"/api/clubs/{clubId}/chips/requests/{requestId}",
                new ApproveRejectRequest { Action = "approve" });
        }

        public async UniTask RejectRequestAsync(string clubId, string requestId)
        {
            await _api.Post<object>(
                $"/api/clubs/{clubId}/chips/requests/{requestId}",
                new ApproveRejectRequest { Action = "reject" });
        }

        // ── Club members list (Trade tab) ─────────────────────────────────────

        public async UniTask<ClubMembersResponse> GetMembersAsync(string clubId, string search = null)
        {
            var query = $"/api/clubs/{clubId}/members";
            if (!string.IsNullOrEmpty(search))
                query += $"?search={Uri.EscapeDataString(search)}";
            return await _api.Get<ClubMembersResponse>(query);
        }

        // ── Socket handlers ───────────────────────────────────────────────────

        private void OnBalanceUpdatedSocket(string data)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<BalanceUpdatedEvent>(data);
                if (payload.ClubId != ClubContext.Instance?.ClubId) return;

                ClubContext.Instance.UpdatePoolChips(
                    payload.PoolChips, payload.MembersChips, payload.AgentsCredit);

                if (payload.WalletChips > 0)
                    AuthManager.Instance.Session.WalletChips = (int)payload.WalletChips;

                UnityThread.RunOnMainThread(() => OnBalanceUpdated?.Invoke(payload));
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClubChipManager] balance:updated parse error: {e.Message}");
            }
        }

        private void OnChipRequestReceivedSocket(string data)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<ChipRequestReceivedEvent>(data);
                UnityThread.RunOnMainThread(() => OnChipRequestReceived?.Invoke(payload));
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClubChipManager] chips:request_received parse error: {e.Message}");
            }
        }
    }
}
