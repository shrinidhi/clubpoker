

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


// ── Club Member ───────────────────────────────────────────────────────────

public class ClubMember
{
    // Internal id — what the chips/member APIs take. Never shown to players.
    [JsonProperty("userId")] public string Id { get; set; }
    // Public player ID shown as "ID: …". Can be null until the server assigns one.
    [JsonProperty("playerCode")] public string PlayerCode { get; set; }
    [JsonProperty("username")] public string Username { get; set; }
    // Player's own display name (set on their profile).
    [JsonProperty("nickname")] public string Nickname { get; set; }
    // Club-specific name, if the club gave this member one.
    [JsonProperty("alias")] public string Alias { get; set; }
    [JsonProperty("avatar")] public string Avatar { get; set; }
    [JsonProperty("role")] public string Role { get; set; }
    [JsonProperty("chips")] public long Chips { get; set; }
    [JsonProperty("totalWinnings")] public long TotalWinnings { get; set; }
    [JsonProperty("agentCredit")] public long AgentCredit { get; set; }

    /// <summary>Nickname once set, username until then.</summary>
    [JsonIgnore]
    public string DisplayName => !string.IsNullOrEmpty(Nickname) ? Nickname : Username;
}

public class ClubMembersData
{
    [JsonProperty("members")] public List<ClubMember> Members { get; set; }
    [JsonProperty("total")] public int Total { get; set; }
    [JsonProperty("page")] public int Page { get; set; }
    [JsonProperty("limit")] public int Limit { get; set; }
}

public class ClubMembersResponse
{
    [JsonProperty("data")] public ClubMembersData Data { get; set; }
}

// ── Send Chips ─────────────────────────────────────────────────

public class SendChipsRequest
{
    [JsonProperty("memberIds")] public List<string> MemberIds { get; set; }
    [JsonProperty("amount")] public long Amount { get; set; }
}

public class SendChipsResult
{
    [JsonProperty("memberId")] public string MemberId { get; set; }
    [JsonProperty("success")] public bool Success { get; set; }
    [JsonProperty("newBalance")] public long NewBalance { get; set; }
    [JsonProperty("error")] public string Error { get; set; }
}

public class SendChipsResponse
{
    [JsonProperty("results")] public List<SendChipsResult> Results { get; set; }
}

// ── Claim Chips  ────────────────────────────────────────────────

public class ClaimChipsRequest
{
    [JsonProperty("memberIds")] public List<string> MemberIds { get; set; }
    [JsonProperty("amount")] public long Amount { get; set; }
    [JsonProperty("claimAll")] public bool ClaimAll { get; set; }
}

public class ClaimChipsResult
{
    [JsonProperty("memberId")] public string MemberId { get; set; }
    [JsonProperty("success")] public bool Success { get; set; }
    [JsonProperty("newBalance")] public long NewBalance { get; set; }
    [JsonProperty("error")] public string Error { get; set; }
}

public class ClaimChipsResponse
{
    [JsonProperty("results")] public List<ClaimChipsResult> Results { get; set; }
}

// ── Chip Records  ───────────────────────────────────────────────

public class ChipRecord
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("amount")] public long Amount { get; set; }
    [JsonProperty("memberId")] public string MemberId { get; set; }
    [JsonProperty("memberUsername")] public string MemberName { get; set; }
    [JsonProperty("memberAvatar")] public string MemberAvatar { get; set; }
    [JsonProperty("memberRole")] public string MemberRole { get; set; }
    [JsonProperty("operatorId")] public string OperatorId { get; set; }
    [JsonProperty("operatorUsername")] public string OperatorName { get; set; }
    [JsonProperty("operatorAvatar")] public string OperatorAvatar { get; set; }
    [JsonProperty("operatorRole")] public string OperatorRole { get; set; }
    [JsonProperty("balanceBefore")] public long BalanceBefore { get; set; }
    [JsonProperty("balanceAfter")] public long BalanceAfter { get; set; }
    [JsonProperty("note")] public string Note { get; set; }
    [JsonProperty("createdAt")] public DateTime Timestamp { get; set; }
}

public class ChipRecordsData
{
    [JsonProperty("records")] public List<ChipRecord> Records { get; set; }
    [JsonProperty("total")] public int Total { get; set; }
    [JsonProperty("page")] public int Page { get; set; }
    [JsonProperty("limit")] public int Limit { get; set; }
}

public class ChipRecordsResponse
{
    [JsonProperty("data")] public ChipRecordsData Data { get; set; }
}

// ── Chip Request  ───────────────────────────────────────────────

public class ChipRequestPayload
{
    [JsonProperty("amount")] public long Amount { get; set; }
}

/// POST /chips/request → data: { request: { id, status, amount, … } }
public class ChipRequestResponse
{
    [JsonProperty("request")] public ChipRequestItem Request { get; set; }

    [JsonIgnore] public string RequestId => Request?.Id;
    [JsonIgnore] public string Status => Request?.Status;
}

public class ChipRequestItem
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("requesterId")] public string MemberId { get; set; }
    [JsonProperty("username")] public string MemberName { get; set; }
    [JsonProperty("avatar")] public string Avatar { get; set; }
    [JsonProperty("amount")] public long Amount { get; set; }
    [JsonProperty("status")] public string Status { get; set; }
    [JsonProperty("note")] public string Note { get; set; }
    [JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
}

public class ChipRequestsData
{
    [JsonProperty("requests")] public List<ChipRequestItem> Requests { get; set; }
    [JsonProperty("total")] public int Total { get; set; }
}

public class ChipRequestsResponse
{
    [JsonProperty("data")] public ChipRequestsData Data { get; set; }
}

public class AutoRejectRequest
{
    [JsonProperty("autoReject")] public bool AutoReject { get; set; }
}

// ── Socket: balance:updated ───────────────────────────────────────────────

public class BalanceUpdatedEvent
{
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("poolChips")] public long PoolChips { get; set; }
    [JsonProperty("membersChips")] public long MembersChips { get; set; }
    [JsonProperty("agentsCredit")] public long AgentsCredit { get; set; }
    [JsonProperty("walletChips")] public long WalletChips { get; set; }
}

// ── Add Chips to Club Pool ────────────────────────────────────────────────

public class AddChipsRequest
{
    [JsonProperty("amount")] public long Amount { get; set; }
}

public class AddChipsResponse
{
    [JsonProperty("added")] public bool Added { get; set; }
    [JsonProperty("amount")] public long Amount { get; set; }
    [JsonProperty("newPoolTotal")] public long NewPoolTotal { get; set; }
}

// ── Exchange Diamonds → Club Pool ─────────────────────────────────────────
// POST /api/economy/exchange — same endpoint as the Shop's wallet exchange; clubId
// routes the chips into that club's pool instead of the player's wallet.

public class ExchangeDiamondsRequest
{
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("diamonds")] public long Diamonds { get; set; }
    [JsonProperty("chips")] public long Chips { get; set; }
}

public class ExchangeDiamondsResponse
{
    [JsonProperty("success")] public bool Success { get; set; }
    [JsonProperty("destination")] public string Destination { get; set; }   // "club"
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("diamondsSpent")] public long DiamondsSpent { get; set; }
    [JsonProperty("chipsReceived")] public long ChipsReceived { get; set; }
    [JsonProperty("clubChipPool")] public long ClubChipPool { get; set; }
}

// ── Chips Summary ─────────────────────────────────────────────────────────

public class ChipsSummaryData
{
    [JsonProperty("chipPool")] public long PoolChips { get; set; }
    [JsonProperty("membersChips")] public long MembersChips { get; set; }
    [JsonProperty("agentsCredit")] public long AgentsCredit { get; set; }
    [JsonProperty("autoReject")] public bool AutoReject { get; set; }
    [JsonProperty("pendingCount")] public int PendingCount { get; set; }
}

public class ChipsSummaryResponse
{
    [JsonProperty("data")] public ChipsSummaryData Data { get; set; }
}

// ── Socket: chips:request_received (admin inbox push) ────────────────────

public class ChipRequestReceivedEvent
{
    [JsonProperty("requestId")] public string RequestId { get; set; }
    [JsonProperty("memberId")] public string MemberId { get; set; }
    [JsonProperty("memberName")] public string MemberName { get; set; }
    [JsonProperty("amount")] public long Amount { get; set; }
}




public class ClubNewApplicationPayload
{
    [JsonProperty("clubId")]
    public string ClubId { get; set; }

    [JsonProperty("applicationId")]
    public string ApplicationId { get; set; }

    [JsonProperty("userId")]
    public string UserId { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}

public class ClubMembershipApprovedPayload
{
    [JsonProperty("clubId")]
    public string ClubId { get; set; }

    [JsonProperty("clubName")]
    public string ClubName { get; set; }

    [JsonProperty("badge")]
    public string Badge { get; set; }

    [JsonProperty("logoUrl")]
    public string LogoUrl { get; set; }

    [JsonProperty("role")]
    public string Role { get; set; }
}

public class ClubKickedPayload
{
    [JsonProperty("clubId")]
    public string ClubId { get; set; }

    [JsonProperty("userId")]
    public string UserId { get; set; }

    [JsonProperty("reason")]
    public string Reason { get; set; }

}

public class ClubTableUpdatedPayload
{
    [JsonProperty("tableId")]
    public string TableId { get; set; }
}
public class ClubScrollMessagePayload
{
    [JsonProperty("clubId")]
    public string ClubId { get; set; }
    [JsonProperty("message")]
    public string Message { get; set; }
    [JsonProperty("tableId")]
    public string TableId { get; set; }
}
public class ClubMemberOnlinePayload
{
    [JsonProperty("playerId")]
    public string PlayerId { get; set; }
}

// ── Club Messages / Inbox ───────────────────────────────────────────────────

public class ClubMessageData
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("userId")] public string UserId { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("content")] public string Content { get; set; }
    [JsonProperty("subjectUserId")] public string SubjectUserId { get; set; }
    [JsonProperty("actorId")] public string ActorId { get; set; }
    [JsonProperty("readAt")] public string ReadAt { get; set; }
    [JsonProperty("createdAt")] public string CreatedAt { get; set; }
    [JsonIgnore]
    public bool IsRead
    {
        get => !string.IsNullOrEmpty(ReadAt);
        set => ReadAt = value ? (ReadAt ?? DateTimeOffset.UtcNow.ToString("o")) : null;
    }
    // Keep old C# consumers working; JSON uses id and content.
    [JsonIgnore] public string MessageId { get => Id; set => Id = value; }
    [JsonIgnore] public string Body { get => Content; set => Content = value; }
}
public class ClubMessagesData
{
    [JsonProperty("messages")] public List<ClubMessageData> Messages { get; set; }
    [JsonProperty("total")] public int Total { get; set; }
    [JsonProperty("unreadCount")] public int UnreadCount { get; set; }
    [JsonProperty("page")] public int Page { get; set; }
    [JsonProperty("limit")] public int Limit { get; set; }
}
public class ClubMessageReadData
{
    [JsonProperty("read")] public bool Read { get; set; }
}
public class ClubMessageDeleteData
{
    [JsonProperty("deleted")] public bool Deleted { get; set; }
    [JsonProperty("count")] public int Count { get; set; }
}

// ── Club Data (stats + game list) ────────────────────────────────────────────

public class ClubDataResponse
{
    [JsonProperty("games")] public List<ClubGameData> Games { get; set; }
    [JsonProperty("summary")] public ClubDataSummary Summary { get; set; }
}

public class ClubDataSummary
{
    [JsonProperty("totalGames")] public int TotalGames { get; set; }
    [JsonProperty("playerWinnings")] public long PlayerWinnings { get; set; }
    [JsonProperty("totalFee")] public long TotalFee { get; set; }
    [JsonProperty("insuranceEV")] public long InsuranceEV { get; set; }
}

// NOTE: field names guessed from the UI — verify against a populated `games[]` item.
public class ClubGameData
{
    [JsonProperty("gameId")] public string GameId { get; set; }
    [JsonProperty("createdAt")] public string CreatedAt { get; set; }
    [JsonProperty("creatorId")] public string CreatorId { get; set; }   // user id
    [JsonProperty("tableName")] public string TableName { get; set; }
    [JsonProperty("avatar")] public string Avatar { get; set; }
    [JsonProperty("variant")] public string Variant { get; set; }
    [JsonProperty("rake")] public double Rake { get; set; }   // e.g. 4.5 (%)
    [JsonProperty("smallBlind")] public long SmallBlind { get; set; }
    [JsonProperty("bigBlind")] public long BigBlind { get; set; }
    [JsonProperty("fee")] public long Fee { get; set; }
}

// ── Club Data Export ─────────────────────────────────────────────────────────

public class ExportDataRequest
{
    [JsonProperty("email")] public string Email { get; set; }
    [JsonProperty("types")] public List<string> Types { get; set; }
    [JsonProperty("from")] public string From { get; set; }
    [JsonProperty("to")] public string To { get; set; }
}

public class ExportDataResponse
{
    [JsonProperty("message")] public string Message { get; set; }
}

// ── Admin: header stats grid ───────────────────────────────────────────────────
// Four metrics × four periods (Today / This Week / Last Week / Overall).
// NOTE: field names guessed from the UI — verify against the real /admin/stats payload.

public class AdminStatValue
{
    [JsonProperty("today")] public long Today { get; set; }
    [JsonProperty("thisWeek")] public long ThisWeek { get; set; }
    [JsonProperty("lastWeek")] public long LastWeek { get; set; }
    [JsonProperty("overall")] public long Overall { get; set; }
}

public class AdminStatsData
{
    [JsonProperty("fee")] public AdminStatValue Fee { get; set; }
    [JsonProperty("games")] public AdminStatValue Games { get; set; }
    [JsonProperty("playerWinnings")] public AdminStatValue PlayerWinnings { get; set; }
    [JsonProperty("insuranceEV")] public AdminStatValue InsuranceEV { get; set; }
}

// ── Admin: Club Level ──────────────────────────────────────────────────────────
// GET  /api/clubs/{clubId}/level/config  → ClubLevelConfigResponse
// POST /api/clubs/{clubId}/level/upgrade { level }

public class ClubLevelItem
{
    [JsonProperty("level")] public int Level { get; set; }
    [JsonProperty("maxAgents")] public int MaxAgents { get; set; }
    [JsonProperty("maxMembers")] public int MaxMembers { get; set; }
    [JsonProperty("diamondCost")] public long DiamondCost { get; set; }
    [JsonProperty("label")] public string Label { get; set; }
    [JsonProperty("superAgent")] public bool SuperAgent { get; set; }   // absent → false
}

public class ClubLevelConfigResponse
{
    [JsonProperty("config")] public List<ClubLevelItem> Config { get; set; }
    [JsonProperty("clubLevel")] public int ClubLevel { get; set; }
    [JsonProperty("expiresAt")] public string ExpiresAt { get; set; }   // nullable
}

// ── Admin: Club detail (GET/PUT /api/clubs/{clubId}) ──────────────────────────
// Source of truth for feeAllocPercent + scrollMessage. PUT takes a partial body,
// e.g. {"feeAllocPercent":25} or {"scrollMessage":"..."}.

public class ClubDetailData
{
    [JsonProperty("id")] public string ClubId { get; set; }
    [JsonProperty("clubCode")] public string ClubCode { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("ownerId")] public string OwnerId { get; set; }
    [JsonProperty("chipPool")] public long ChipPool { get; set; }
    [JsonProperty("welcomeMessage")] public string WelcomeMessage { get; set; }
    [JsonProperty("badge")] public string Badge { get; set; }
    [JsonProperty("logoUrl")] public string LogoUrl { get; set; }
    [JsonProperty("badBeatEnabled")] public bool BadBeatEnabled { get; set; }
    [JsonProperty("highHandEnabled")] public bool HighHandEnabled { get; set; }
    [JsonProperty("autoRejectChipRequests")] public bool AutoRejectChipRequests { get; set; }
    [JsonProperty("clubLevel")] public int ClubLevel { get; set; }
    [JsonProperty("clubLevelExpiresAt")] public string ClubLevelExpiresAt { get; set; }
    [JsonProperty("createdAt")] public string CreatedAt { get; set; }
    [JsonProperty("updatedAt")] public string UpdatedAt { get; set; }
    [JsonProperty("feeAllocPercent")] public int FeeAllocPercent { get; set; }
    [JsonProperty("scrollMessage")] public string ScrollMessage { get; set; }
    [JsonProperty("memberCount")] public int MemberCount { get; set; }
    [JsonProperty("activeTableCount")] public int ActiveTableCount { get; set; }
    [JsonProperty("myRole")] public string MyRole { get; set; }
    [JsonProperty("description")] public string Description { get; set; }

}

public class ClubDetailResponse
{
    [JsonProperty("club")] public ClubDetailData Club { get; set; }
}

// ── Member: Quit from this club (POST /api/clubs/{clubId}/leave) ───────────────
// chipsRecalled = club chips the server pulled back into the club pool on exit.

public class LeaveClubResponse
{
    [JsonProperty("left")] public bool Left { get; set; }
    [JsonProperty("userId")] public string UserId { get; set; }
    [JsonProperty("chipsRecalled")] public long ChipsRecalled { get; set; }
}

// ── Admin: Mobile Push ─────────────────────────────────────────────────────────
// GET  /api/player/diamonds        → DiamondsData (balance to display / gate on)
// POST /api/clubs/{clubId}/push    { title, content } → PushResponse (cost comes back here)

public class DiamondsData
{
    [JsonProperty("balance")] public long Balance { get; set; }
    [JsonProperty("lockedDiamonds")] public long LockedDiamonds { get; set; }
    [JsonProperty("available")] public long Available { get; set; }
}

public class PushResponse
{
    [JsonProperty("sent")] public bool Sent { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("content")] public string Content { get; set; }
    [JsonProperty("diamondCost")] public long DiamondCost { get; set; }
    [JsonProperty("remainingQuota")] public int RemainingQuota { get; set; }
    [JsonProperty("resetAt")] public string ResetAt { get; set; }
}

public class PushQuotaData
{
    [JsonProperty("used")] public int Used { get; set; }
    [JsonProperty("limit")] public int Limit { get; set; }
    [JsonProperty("remaining")] public int Remaining { get; set; }
    [JsonProperty("resetAt")] public string ResetAt { get; set; }
}

public class PendingPushData
{
    [JsonProperty("push")] public PendingPushItem Push { get; set; }
}

public class PendingPushItem
{
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("content")] public string Content { get; set; }
    [JsonProperty("tableId")] public string TableId { get; set; }
    [JsonProperty("sentAt")] public string SentAt { get; set; }
}

public class NoificationData
{
    [JsonProperty("notification")] public NotificationItem Notification { get; set; }
}

public class NotificationItem
{
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("content")] public string Content { get; set; }
    [JsonProperty("sentAt")] public string SentAt { get; set; }
}


// ── Admin: Club Posters ────────────────────────────────────────────────────────
// GET    /api/clubs/{clubId}/posters              → PostersResponse
// POST   /api/clubs/{clubId}/posters  { url(base64 data-uri), filename, fileSize } → PosterResponse
// DELETE /api/clubs/{clubId}/posters/{posterId}   → { deleted: true }

public class PosterData
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("url")] public string Url { get; set; }   // base64 data URI
    [JsonProperty("filename")] public string Filename { get; set; }
    [JsonProperty("fileSize")] public long FileSize { get; set; }
    [JsonProperty("isActive")] public bool IsActive { get; set; }
    [JsonProperty("order")] public int Order { get; set; }
    [JsonProperty("expiresAt")] public string ExpiresAt { get; set; }   // nullable
    [JsonProperty("postedAt")] public string PostedAt { get; set; }
    [JsonProperty("createdAt")] public string CreatedAt { get; set; }
}

public class PostersResponse
{
    [JsonProperty("posters")] public List<PosterData> Posters { get; set; }
}

public class PosterResponse
{
    [JsonProperty("poster")] public PosterData Poster { get; set; }
}

// ── Admin: Notification Settings ───────────────────────────────────────────────
// GET /api/clubs/{clubId}/notification-settings → the three flags.
// PUT same, partial body e.g. {"clubApplicants":false} → echoes the full row.

public class NotificationSettingsData
{
    [JsonProperty("clubApplicants")] public bool ClubApplicants { get; set; }
    [JsonProperty("memberLeave")] public bool MemberLeave { get; set; }
    [JsonProperty("chipsRequest")] public bool ChipsRequest { get; set; }

    // Present only on the PUT response.
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("updatedAt")] public string UpdatedAt { get; set; }
}

public class ClubRoleChangedPayload
{
    [JsonProperty("clubId")]
    public string ClubId { get; set; }

    [JsonProperty("newRole")]
    public string NewRole { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}


public class ClubPromosResponse
{
    [JsonProperty("promos")]
    public List<ClubPromoData> Promos { get; set; }

    [JsonProperty("count")]
    public int Count { get; set; }
}

public class ClubPromoData
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("clubId")] public string ClubId { get; set; }
    [JsonProperty("createdBy")] public string CreatedBy { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("promoType")] public string PromoType { get; set; }
    [JsonProperty("scope")] public string Scope { get; set; }
    [JsonProperty("status")] public string Status { get; set; }

    [JsonProperty("prizeAmount")] public decimal PrizeAmount { get; set; }
    [JsonProperty("prizeType")] public string PrizeType { get; set; }

    [JsonProperty("startsAt")] public string StartsAt { get; set; }
    [JsonProperty("endsAt")] public string EndsAt { get; set; }
    [JsonProperty("description")] public string Description { get; set; }

    [JsonProperty("typeConfig")]
    public ClubPromoTypeConfig TypeConfig { get; set; }

    [JsonProperty("eligibilityRules")]
    public ClubPromoEligibilityRules EligibilityRules { get; set; }

    [JsonProperty("prizeStructure")]
    public ClubPromoPrizeStructure PrizeStructure { get; set; }

    [JsonProperty("repeatCadence")]
    public ClubPromoRepeatCadence RepeatCadence { get; set; }

    [JsonProperty("announceConfig")]
    public ClubPromoAnnounceConfig AnnounceConfig { get; set; }

    [JsonProperty("reservedChips")] public decimal ReservedChips { get; set; }
    [JsonProperty("paidOut")] public decimal PaidOut { get; set; }
    [JsonProperty("currentAmount")] public decimal CurrentAmount { get; set; }
    [JsonProperty("participantCount")] public int ParticipantCount { get; set; }

    [JsonProperty("leaderboardSnapshot")]
    public JToken LeaderboardSnapshot { get; set; }

    [JsonProperty("createdAt")] public string CreatedAt { get; set; }
    [JsonProperty("updatedAt")] public string UpdatedAt { get; set; }
}

public class ClubPromoTypeConfig
{
    [JsonProperty("rakeContributionRate")]
    public decimal RakeContributionRate { get; set; }

    [JsonProperty("seedAmount")]
    public decimal SeedAmount { get; set; }

    [JsonProperty("payoutStructure")]
    public ClubPromoPayoutStructure PayoutStructure { get; set; }

    [JsonProperty("prizePositions")]
    public int? PrizePositions { get; set; }

    [JsonProperty("selfFundingRate")]
    public decimal? SelfFundingRate { get; set; }

    [JsonProperty("timerDurationMinutes")]
    public int? TimerDurationMinutes { get; set; }

    [JsonProperty("minHandRank")]
    public int? MinHandRank { get; set; }

    [JsonProperty("prizeCap")] public decimal? PrizeCap { get; set; }
    [JsonProperty("targetMemberId")] public string TargetMemberId { get; set; }
    [JsonProperty("metric")] public string Metric { get; set; }
    [JsonProperty("reboardOnExpiry")] public bool? ReboardOnExpiry { get; set; }
    [JsonProperty("prizeStructure")] public List<ClubPromoPrizePosition> PrizePositionsStructure { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> AdditionalFields { get; set; }
}

public class ClubPromoPayoutStructure
{
    [JsonProperty("loser")] public decimal Loser { get; set; }
    [JsonProperty("winner")] public decimal Winner { get; set; }
    [JsonProperty("tableShare")] public decimal TableShare { get; set; }
}

public class ClubPromoEligibilityRules
{
    [JsonProperty("minHandRank")] public int MinHandRank { get; set; }
    [JsonProperty("minPotBBs")] public decimal MinPotBBs { get; set; }
    [JsonProperty("minPlayersDealt")] public int MinPlayersDealt { get; set; }
    [JsonProperty("membersOnly")] public bool MembersOnly { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> AdditionalFields { get; set; }
}

public class ClubPromoPrizeStructure
{
    [JsonProperty("positions")]
    public List<ClubPromoPrizePosition> Positions { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> AdditionalFields { get; set; }
}

public class ClubPromoPrizePosition
{
    [JsonProperty("rank")] public int Rank { get; set; }
    [JsonProperty("pct")] public decimal Pct { get; set; }
}

public class CreateClubPromoRequest
{
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string Description { get; set; }
    [JsonProperty("promoType")] public string PromoType { get; set; }
    [JsonProperty("scope")] public string Scope { get; set; }
    [JsonProperty("prizeType")] public string PrizeType { get; set; }
    [JsonProperty("prizeAmount")] public decimal PrizeAmount { get; set; }

    [JsonProperty("typeConfig")]
    public object TypeConfig { get; set; }

    [JsonProperty("eligibilityRules")]
    public Dictionary<string, object> EligibilityRules { get; set; }
        = new Dictionary<string, object>();

    [JsonProperty("startsAt", NullValueHandling = NullValueHandling.Ignore)]
    public string StartsAt { get; set; }

    [JsonProperty("endsAt", NullValueHandling = NullValueHandling.Ignore)]
    public string EndsAt { get; set; }

    [JsonProperty("repeatCadence", NullValueHandling = NullValueHandling.Ignore)]
    public ClubPromoRepeatCadence RepeatCadence { get; set; }

    [JsonProperty("announceConfig", NullValueHandling = NullValueHandling.Ignore)]
    public ClubPromoAnnounceConfig AnnounceConfig { get; set; }
}

public class ClubPromoRepeatCadence
{
    [JsonProperty("frequency")]
    public string Frequency { get; set; }

    [JsonProperty("daysOfWeek", NullValueHandling = NullValueHandling.Ignore)]
    public List<int> DaysOfWeek { get; set; }
}

public class ClubPromoAnnounceConfig
{
    [JsonProperty("announceOnStart")] public bool AnnounceOnStart { get; set; }
    [JsonProperty("announceLeaderChanges")] public bool AnnounceLeaderChanges { get; set; }
    [JsonProperty("announceWarning")] public bool AnnounceWarning { get; set; }
    [JsonProperty("announceOnEnd")] public bool AnnounceOnEnd { get; set; }
}

public class ClubRakeRaceConfig
{
    [JsonProperty("prizePositions")] public int PrizePositions { get; set; }
    [JsonProperty("selfFundingRate")] public decimal SelfFundingRate { get; set; }
}

public class ClubBadBeatConfig
{
    [JsonProperty("rakeContributionRate")]
    public decimal RakeContributionRate { get; set; }

    [JsonProperty("seedAmount")]
    public decimal SeedAmount { get; set; }

    [JsonProperty("payoutStructure")]
    public ClubPromoPayoutStructure PayoutStructure { get; set; }
}

public class ClubHighHandConfig
{
    [JsonProperty("timerDurationMinutes")]
    public int TimerDurationMinutes { get; set; }

    [JsonProperty("minHandRank")]
    public int MinHandRank { get; set; }
}

public class CreateClubPromoResponse
{
    [JsonProperty("promo")]
    public ClubPromoData Promo { get; set; }
}