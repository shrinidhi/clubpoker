using System;
using System.Collections.Generic;
using Newtonsoft.Json;


// ── Club Member ───────────────────────────────────────────────────────────

public class ClubMember
{
    [JsonProperty("userId")]   public string Id       { get; set; }
    [JsonProperty("username")] public string Username { get; set; }
    [JsonProperty("alias")]    public string Nickname { get; set; }
    [JsonProperty("avatar")]   public string Avatar   { get; set; }
    [JsonProperty("role")]     public string Role     { get; set; }
    [JsonProperty("chips")]    public long   Chips    { get; set; }
    [JsonProperty("totalWinnings")] public long TotalWinnings { get; set; }
    [JsonProperty("agentCredit")]   public long AgentCredit   { get; set; }
}

public class ClubMembersData
{
    [JsonProperty("members")] public List<ClubMember> Members { get; set; }
    [JsonProperty("total")]   public int              Total   { get; set; }
    [JsonProperty("page")]    public int              Page    { get; set; }
    [JsonProperty("limit")]   public int              Limit   { get; set; }
}

public class ClubMembersResponse
{
    [JsonProperty("data")] public ClubMembersData Data { get; set; }
}

// ── Send Chips ─────────────────────────────────────────────────

public class SendChipsRequest
{
    [JsonProperty("memberIds")] public List<string> MemberIds { get; set; }
    [JsonProperty("amount")]    public long         Amount    { get; set; }
}

public class SendChipsResult
{
    [JsonProperty("memberId")]   public string MemberId   { get; set; }
    [JsonProperty("success")]    public bool   Success    { get; set; }
    [JsonProperty("newBalance")] public long   NewBalance { get; set; }
    [JsonProperty("error")]      public string Error      { get; set; }
}

public class SendChipsResponse
{
    [JsonProperty("results")] public List<SendChipsResult> Results { get; set; }
}

// ── Claim Chips  ────────────────────────────────────────────────

public class ClaimChipsRequest
{
    [JsonProperty("memberIds")] public List<string> MemberIds { get; set; }
    [JsonProperty("amount")]    public long         Amount    { get; set; }
    [JsonProperty("claimAll")]  public bool         ClaimAll  { get; set; }
}

public class ClaimChipsResult
{
    [JsonProperty("memberId")]   public string MemberId   { get; set; }
    [JsonProperty("success")]    public bool   Success    { get; set; }
    [JsonProperty("newBalance")] public long   NewBalance { get; set; }
    [JsonProperty("error")]      public string Error      { get; set; }
}

public class ClaimChipsResponse
{
    [JsonProperty("results")] public List<ClaimChipsResult> Results { get; set; }
}

// ── Chip Records  ───────────────────────────────────────────────

public class ChipRecord
{
    [JsonProperty("id")]                public string   Id               { get; set; }
    [JsonProperty("type")]              public string   Type             { get; set; }
    [JsonProperty("amount")]            public long     Amount           { get; set; }
    [JsonProperty("memberId")]          public string   MemberId         { get; set; }
    [JsonProperty("memberUsername")]    public string   MemberName       { get; set; }
    [JsonProperty("memberAvatar")]      public string   MemberAvatar     { get; set; }
    [JsonProperty("operatorId")]        public string   OperatorId       { get; set; }
    [JsonProperty("operatorUsername")]  public string   OperatorName     { get; set; }
    [JsonProperty("operatorAvatar")]    public string   OperatorAvatar   { get; set; }
    [JsonProperty("balanceBefore")]     public long     BalanceBefore    { get; set; }
    [JsonProperty("balanceAfter")]      public long     BalanceAfter     { get; set; }
    [JsonProperty("note")]              public string   Note             { get; set; }
    [JsonProperty("createdAt")]         public DateTime Timestamp        { get; set; }
}

public class ChipRecordsData
{
    [JsonProperty("records")] public List<ChipRecord> Records { get; set; }
    [JsonProperty("total")]   public int              Total   { get; set; }
    [JsonProperty("page")]    public int              Page    { get; set; }
    [JsonProperty("limit")]   public int              Limit   { get; set; }
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

public class ChipRequestResponse
{
    [JsonProperty("requestId")] public string RequestId { get; set; }
    [JsonProperty("status")]    public string Status    { get; set; }
}

public class ChipRequestItem
{
    [JsonProperty("id")]          public string   Id          { get; set; }
    [JsonProperty("clubId")]      public string   ClubId      { get; set; }
    [JsonProperty("requesterId")] public string   MemberId    { get; set; }
    [JsonProperty("username")]    public string   MemberName  { get; set; }
    [JsonProperty("avatar")]      public string   Avatar      { get; set; }
    [JsonProperty("amount")]      public long     Amount      { get; set; }
    [JsonProperty("status")]      public string   Status      { get; set; }
    [JsonProperty("note")]        public string   Note        { get; set; }
    [JsonProperty("createdAt")]   public DateTime CreatedAt   { get; set; }
}

public class ChipRequestsData
{
    [JsonProperty("requests")] public List<ChipRequestItem> Requests { get; set; }
    [JsonProperty("total")]    public int                   Total    { get; set; }
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
    [JsonProperty("clubId")]       public string ClubId       { get; set; }
    [JsonProperty("poolChips")]    public long   PoolChips    { get; set; }
    [JsonProperty("membersChips")] public long   MembersChips { get; set; }
    [JsonProperty("agentsCredit")] public long   AgentsCredit { get; set; }
    [JsonProperty("walletChips")]  public long   WalletChips  { get; set; }
}

// ── Add Chips to Club Pool ────────────────────────────────────────────────

public class AddChipsRequest
{
    [JsonProperty("amount")] public long Amount { get; set; }
}

public class AddChipsResponse
{
    [JsonProperty("added")]        public bool Added       { get; set; }
    [JsonProperty("amount")]       public long Amount      { get; set; }
    [JsonProperty("newPoolTotal")] public long NewPoolTotal { get; set; }
}

// ── Chips Summary ─────────────────────────────────────────────────────────

public class ChipsSummaryData
{
    [JsonProperty("chipPool")]      public long PoolChips    { get; set; }
    [JsonProperty("membersChips")]  public long MembersChips { get; set; }
    [JsonProperty("agentsCredit")]  public long AgentsCredit { get; set; }
    [JsonProperty("autoReject")]    public bool AutoReject   { get; set; }
    [JsonProperty("pendingCount")]  public int  PendingCount { get; set; }
}

public class ChipsSummaryResponse
{
    [JsonProperty("data")] public ChipsSummaryData Data { get; set; }
}

// ── Socket: chips:request_received (admin inbox push) ────────────────────

public class ChipRequestReceivedEvent
{
    [JsonProperty("requestId")]  public string RequestId  { get; set; }
    [JsonProperty("memberId")]   public string MemberId   { get; set; }
    [JsonProperty("memberName")] public string MemberName { get; set; }
    [JsonProperty("amount")]     public long   Amount     { get; set; }
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
    [JsonProperty("clubId")]
    public string ClubId { get; set; }
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
    [JsonProperty("messageId")] public string MessageId { get; set; }
    [JsonProperty("title")]     public string Title     { get; set; }
    [JsonProperty("body")]      public string Body      { get; set; }
    [JsonProperty("createdAt")] public string CreatedAt { get; set; }
    [JsonProperty("isRead")]    public bool   IsRead    { get; set; }
}

// ── Club Data (stats + game list) ────────────────────────────────────────────

public class ClubDataResponse
{
    [JsonProperty("games")]   public List<ClubGameData> Games   { get; set; }
    [JsonProperty("summary")] public ClubDataSummary    Summary { get; set; }
}

public class ClubDataSummary
{
    [JsonProperty("totalGames")]     public int  TotalGames     { get; set; }
    [JsonProperty("playerWinnings")] public long PlayerWinnings { get; set; }
    [JsonProperty("totalFee")]       public long TotalFee       { get; set; }
    [JsonProperty("insuranceEV")]    public long InsuranceEV    { get; set; }
}

// NOTE: field names guessed from the UI — verify against a populated `games[]` item.
public class ClubGameData
{
    [JsonProperty("gameId")]     public string GameId     { get; set; }
    [JsonProperty("createdAt")]  public string CreatedAt  { get; set; }
    [JsonProperty("creatorId")]  public string CreatorId  { get; set; }   // user id
    [JsonProperty("tableName")]  public string TableName  { get; set; }
    [JsonProperty("avatar")]     public string Avatar     { get; set; }
    [JsonProperty("variant")]    public string Variant    { get; set; }
    [JsonProperty("rake")]       public double Rake       { get; set; }   // e.g. 4.5 (%)
    [JsonProperty("smallBlind")] public long   SmallBlind { get; set; }
    [JsonProperty("bigBlind")]   public long   BigBlind   { get; set; }
    [JsonProperty("fee")]        public long   Fee        { get; set; }
}

// ── Club Data Export ─────────────────────────────────────────────────────────

public class ExportDataRequest
{
    [JsonProperty("email")] public string       Email { get; set; }
    [JsonProperty("types")] public List<string> Types { get; set; }
    [JsonProperty("from")]  public string        From { get; set; }
    [JsonProperty("to")]    public string        To   { get; set; }
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
    [JsonProperty("today")]    public long Today    { get; set; }
    [JsonProperty("thisWeek")] public long ThisWeek { get; set; }
    [JsonProperty("lastWeek")] public long LastWeek { get; set; }
    [JsonProperty("overall")]  public long Overall  { get; set; }
}

public class AdminStatsData
{
    [JsonProperty("fee")]            public AdminStatValue Fee            { get; set; }
    [JsonProperty("games")]          public AdminStatValue Games          { get; set; }
    [JsonProperty("playerWinnings")] public AdminStatValue PlayerWinnings { get; set; }
    [JsonProperty("insuranceEV")]    public AdminStatValue InsuranceEV    { get; set; }
}

// ── Admin: Club Level ──────────────────────────────────────────────────────────
// GET  /api/clubs/{clubId}/level/config  → ClubLevelConfigResponse
// POST /api/clubs/{clubId}/level/upgrade { level }

public class ClubLevelItem
{
    [JsonProperty("level")]       public int    Level       { get; set; }
    [JsonProperty("maxAgents")]   public int    MaxAgents   { get; set; }
    [JsonProperty("maxMembers")]  public int    MaxMembers  { get; set; }
    [JsonProperty("diamondCost")] public long   DiamondCost { get; set; }
    [JsonProperty("label")]       public string Label       { get; set; }
    [JsonProperty("superAgent")]  public bool   SuperAgent  { get; set; }   // absent → false
}

public class ClubLevelConfigResponse
{
    [JsonProperty("config")]    public List<ClubLevelItem> Config    { get; set; }
    [JsonProperty("clubLevel")] public int                 ClubLevel { get; set; }
    [JsonProperty("expiresAt")] public string              ExpiresAt { get; set; }   // nullable
}

// ── Admin: Club detail (GET/PUT /api/clubs/{clubId}) ──────────────────────────
// Source of truth for feeAllocPercent + scrollMessage. PUT takes a partial body,
// e.g. {"feeAllocPercent":25} or {"scrollMessage":"..."}.

public class ClubDetailData
{
    [JsonProperty("id")]                     public string ClubId                { get; set; }
    [JsonProperty("clubCode")]               public string ClubCode              { get; set; }
    [JsonProperty("name")]                   public string Name                  { get; set; }
    [JsonProperty("ownerId")]                public string OwnerId               { get; set; }
    [JsonProperty("chipPool")]               public long   ChipPool              { get; set; }
    [JsonProperty("welcomeMessage")]         public string WelcomeMessage        { get; set; }
    [JsonProperty("badge")]                  public string Badge                 { get; set; }
    [JsonProperty("logoUrl")]                public string LogoUrl               { get; set; }
    [JsonProperty("badBeatEnabled")]         public bool   BadBeatEnabled        { get; set; }
    [JsonProperty("highHandEnabled")]        public bool   HighHandEnabled       { get; set; }
    [JsonProperty("autoRejectChipRequests")] public bool   AutoRejectChipRequests{ get; set; }
    [JsonProperty("clubLevel")]              public int    ClubLevel             { get; set; }
    [JsonProperty("clubLevelExpiresAt")]     public string ClubLevelExpiresAt    { get; set; }
    [JsonProperty("createdAt")]              public string CreatedAt             { get; set; }
    [JsonProperty("updatedAt")]              public string UpdatedAt             { get; set; }
    [JsonProperty("feeAllocPercent")]        public int    FeeAllocPercent       { get; set; }
    [JsonProperty("scrollMessage")]          public string ScrollMessage         { get; set; }
    [JsonProperty("memberCount")]            public int    MemberCount           { get; set; }
    [JsonProperty("activeTableCount")]       public int    ActiveTableCount      { get; set; }
    [JsonProperty("myRole")]                 public string MyRole                { get; set; }
    [JsonProperty("description")]            public string Description           { get; set; }

}

public class ClubDetailResponse
{
    [JsonProperty("club")] public ClubDetailData Club { get; set; }
}

// ── Admin: Mobile Push ─────────────────────────────────────────────────────────
// GET  /api/player/diamonds        → DiamondsData (balance to display / gate on)
// POST /api/clubs/{clubId}/push    { title, content } → PushResponse (cost comes back here)

public class DiamondsData
{
    [JsonProperty("balance")]        public long Balance        { get; set; }
    [JsonProperty("lockedDiamonds")] public long LockedDiamonds { get; set; }
    [JsonProperty("available")]      public long Available      { get; set; }
}

public class PushResponse
{
    [JsonProperty("sent")]        public bool   Sent        { get; set; }
    [JsonProperty("title")]       public string Title       { get; set; }
    [JsonProperty("content")]     public string Content     { get; set; }
    [JsonProperty("diamondCost")] public long   DiamondCost { get; set; }
}

// ── Admin: Club Posters ────────────────────────────────────────────────────────
// GET    /api/clubs/{clubId}/posters              → PostersResponse
// POST   /api/clubs/{clubId}/posters  { url(base64 data-uri), filename, fileSize } → PosterResponse
// DELETE /api/clubs/{clubId}/posters/{posterId}   → { deleted: true }

public class PosterData
{
    [JsonProperty("id")]        public string Id        { get; set; }
    [JsonProperty("clubId")]    public string ClubId    { get; set; }
    [JsonProperty("url")]       public string Url        { get; set; }   // base64 data URI
    [JsonProperty("filename")]  public string Filename  { get; set; }
    [JsonProperty("fileSize")]  public long   FileSize  { get; set; }
    [JsonProperty("isActive")]  public bool   IsActive  { get; set; }
    [JsonProperty("order")]     public int    Order      { get; set; }
    [JsonProperty("expiresAt")] public string ExpiresAt { get; set; }   // nullable
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
    [JsonProperty("memberLeave")]    public bool MemberLeave    { get; set; }
    [JsonProperty("chipsRequest")]   public bool ChipsRequest   { get; set; }

    // Present only on the PUT response.
    [JsonProperty("id")]        public string Id        { get; set; }
    [JsonProperty("clubId")]    public string ClubId    { get; set; }
    [JsonProperty("updatedAt")] public string UpdatedAt { get; set; }
}