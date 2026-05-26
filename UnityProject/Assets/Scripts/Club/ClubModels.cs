using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClubPoker.Club
{
    // ── Club Member ───────────────────────────────────────────────────────────

    public class ClubMember
    {
        [JsonProperty("id")]       public string Id       { get; set; }
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("nickname")] public string Nickname { get; set; }
        [JsonProperty("avatar")]   public string Avatar   { get; set; }
        [JsonProperty("role")]     public string Role     { get; set; }
        [JsonProperty("chips")]    public long   Chips    { get; set; }
    }

    public class ClubMembersResponse
    {
        [JsonProperty("items")]      public List<ClubMember> Items      { get; set; }
        [JsonProperty("totalCount")] public int              TotalCount { get; set; }
    }

    // ── Send Chips (CLUB-678) ─────────────────────────────────────────────────

    public class SendChipsRequest
    {
        [JsonProperty("memberIds")] public List<string> MemberIds { get; set; }
        [JsonProperty("amount")]    public long         Amount    { get; set; }
    }

    public class SendChipsResponse
    {
        [JsonProperty("success")]      public bool Success      { get; set; }
        [JsonProperty("poolChips")]    public long PoolChips    { get; set; }
        [JsonProperty("membersChips")] public long MembersChips { get; set; }
    }

    // ── Claim Chips (CLUB-679) ────────────────────────────────────────────────

    public class ClaimChipsRequest
    {
        [JsonProperty("memberId")] public string MemberId { get; set; }
        [JsonProperty("amount")]   public long   Amount   { get; set; }
        [JsonProperty("claimAll")] public bool   ClaimAll { get; set; }
    }

    public class ClaimChipsResponse
    {
        [JsonProperty("success")]      public bool Success      { get; set; }
        [JsonProperty("poolChips")]    public long PoolChips    { get; set; }
        [JsonProperty("membersChips")] public long MembersChips { get; set; }
    }

    // ── Chip Records (CLUB-680) ───────────────────────────────────────────────

    public class ChipRecord
    {
        [JsonProperty("id")]           public string   Id           { get; set; }
        [JsonProperty("type")]         public string   Type         { get; set; } // SEND | CLAIM_BACK | REQUEST
        [JsonProperty("amount")]       public long     Amount       { get; set; }
        [JsonProperty("operatorId")]   public string   OperatorId   { get; set; }
        [JsonProperty("operatorName")] public string   OperatorName { get; set; }
        [JsonProperty("memberId")]     public string   MemberId     { get; set; }
        [JsonProperty("memberName")]   public string   MemberName   { get; set; }
        [JsonProperty("timestamp")]    public DateTime Timestamp    { get; set; }
    }

    public class ChipRecordsResponse
    {
        [JsonProperty("items")]      public List<ChipRecord> Items      { get; set; }
        [JsonProperty("pagination")] public ChipPagination   Pagination { get; set; }
    }

    public class ChipPagination
    {
        [JsonProperty("page")]    public int  Page    { get; set; }
        [JsonProperty("limit")]   public int  Limit   { get; set; }
        [JsonProperty("total")]   public int  Total   { get; set; }
        [JsonProperty("hasMore")] public bool HasMore { get; set; }
    }

    // ── Chip Request (CLUB-681) ───────────────────────────────────────────────

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
        [JsonProperty("id")]         public string   Id         { get; set; }
        [JsonProperty("memberId")]   public string   MemberId   { get; set; }
        [JsonProperty("memberName")] public string   MemberName { get; set; }
        [JsonProperty("avatar")]     public string   Avatar     { get; set; }
        [JsonProperty("amount")]     public long     Amount     { get; set; }
        [JsonProperty("status")]     public string   Status     { get; set; } // PENDING|APPROVED|REJECTED
        [JsonProperty("createdAt")]  public DateTime CreatedAt  { get; set; }
    }

    public class ChipRequestsResponse
    {
        [JsonProperty("items")] public List<ChipRequestItem> Items { get; set; }
    }

    public class ApproveRejectRequest
    {
        [JsonProperty("action")] public string Action { get; set; } // "approve" | "reject"
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

    // ── Socket: chips:request_received (admin inbox push) ────────────────────

    public class ChipRequestReceivedEvent
    {
        [JsonProperty("requestId")]  public string RequestId  { get; set; }
        [JsonProperty("memberId")]   public string MemberId   { get; set; }
        [JsonProperty("memberName")] public string MemberName { get; set; }
        [JsonProperty("amount")]     public long   Amount     { get; set; }
    }
}
