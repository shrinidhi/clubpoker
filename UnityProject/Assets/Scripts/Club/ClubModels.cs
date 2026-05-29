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

