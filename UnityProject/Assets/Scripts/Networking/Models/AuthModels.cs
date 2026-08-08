using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClubPoker.Networking.Models
{
    public class LobbyVariantResponse
    {
        [JsonProperty("lobbyvariants")]
        public List<LobbyVariantData> LobbyVariants { get; set; }
    }

    public class LobbyVariantData
    {
        [JsonProperty("variantKey")]
        public string VariantKey { get; set; }

        [JsonProperty("variantName")]
        public string VariantName { get; set; }

        [JsonProperty("isLocked")]
        public bool IsLocked { get; set; }
    }
    public class ClubTableVariantResponse
    {
        [JsonProperty("clubTablevariants")]
        public List<ClubTableVariantData> ClubTableVariants { get; set; }
    }
    public class ClubTableVariantData
    {
        [JsonProperty("variantKey")]
        public string VariantKey { get; set; }

        [JsonProperty("variantName")]
        public string VariantName { get; set; }

        [JsonProperty("isLocked")]
        public bool IsLocked { get; set; }
    }






    #region Request Models

    public class LoginRequest
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }

    public class RefreshTokenRequest
    {
        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }

    #endregion

    #region Response Models


    public class LoginResponse
    {
        [JsonProperty("player")]
        public PlayerData Player { get; set; }

        [JsonProperty("tokens")]
        public TokenPair Tokens { get; set; }
    }
    public class TokenPair
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }

    public class RefreshTokenResponse
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }

    #endregion

    #region Player Model



    public class PlayerData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("walletChips")]
        public int WalletChips { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("isGuest")]
        public bool IsGuest { get; set; }

        [JsonProperty("lastDailyBonus")]
        public DateTime? LastDailyBonus { get; set; }
    }

    public class PlayerProfileResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("data")]
        public PlayerFullProfileData Data { get; set; }
    }

    public class PlayerFullProfileData
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("chipBalance")]
        public int ChipBalance { get; set; }

        [JsonProperty("diamondBalance")]
        public int DiamondBalance { get; set; }

        [JsonProperty("totalWinnings")]
        public int TotalWinnings { get; set; }

        [JsonProperty("totalHands")]
        public int TotalHands { get; set; }

        [JsonProperty("gamesPlayed")]
        public int GamesPlayed { get; set; }

        [JsonProperty("gamesWon")]
        public int GamesWon { get; set; }

        [JsonProperty("winRate")]
        public float WinRate { get; set; }

        [JsonProperty("favouriteVariant")]
        public string FavouriteVariant { get; set; }

        [JsonProperty("registeredAt")]
        public DateTime RegisteredAt { get; set; }

        [JsonProperty("lastLoginAt")]
        public DateTime LastLoginAt { get; set; }

        [JsonProperty("isBanned")]
        public bool IsBanned { get; set; }

        [JsonProperty("selfExcludedAt")]
        public string SelfExcludedAt { get; set; }
    }

    public class UpdateProfileResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("data")]
        public UpdateProfileData Data { get; set; }
    }

    public class UpdateProfileData
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }
    }

    #endregion

    #region Register & Logout Requests

    public class RegisterRequest
    {
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("password")] public string Password { get; set; }
    }

    public class LogoutRequest
    {
        [JsonProperty("refreshToken")] public string RefreshToken { get; set; }
    }

    #endregion

    #region Guest Models

    public class GuestTokenData
    {
        [JsonProperty("accessToken")] public string AccessToken { get; set; }
        [JsonProperty("expiresIn")] public int ExpiresIn { get; set; } // seconds, 7200 = 2h
    }

    public class GuestPlayerData
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("avatar")] public string Avatar { get; set; }
        [JsonProperty("walletChips")] public int WalletChips { get; set; }
        [JsonProperty("isGuest")] public bool IsGuest { get; set; }
        [JsonProperty("lastDailyBonus")] public DateTime? LastDailyBonus { get; set; }
    }

    public class GuestResponseData
    {
        [JsonProperty("player")] public GuestPlayerData Player { get; set; }
        [JsonProperty("tokens")] public GuestTokenData Tokens { get; set; }
        [JsonProperty("guestId")] public string GuestId { get; set; }
        [JsonProperty("guestChips")] public int GuestChips { get; set; }
        [JsonProperty("temporary")] public bool Temporary { get; set; }
    }

    #endregion




    #region Avatar Models

    public class AvatarListResponse
    {
        [JsonProperty("avatars")]
        public List<AvatarData> Avatars { get; set; }
    }

    public class AvatarData
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("unlockType")]
        public string UnlockType { get; set; }

        [JsonProperty("unlockRequirement")]
        public int UnlockRequirement { get; set; }

        [JsonProperty("unlocked")]
        public bool Unlocked { get; set; }
    }

    #endregion


    #region Profile Update

    public class UpdateProfileRequest
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }
    }

    #endregion

    #region HUD Chips Data
    public class ChipsData
    {
        [JsonProperty("walletChips")]
        public int WalletChips { get; set; }

        [JsonProperty("lockedInTables")]
        public int LockedInTables { get; set; }

        [JsonProperty("availableChips")]
        public int AvailableChips { get; set; }
    }
    #endregion


    #region  Buy In Data

    public class BuyInResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("data")]
        public BuyInData Data { get; set; }

    }
    public class BuyInData
    {
        [JsonProperty("tableChips")]
        public int TableChips { get; set; }

        [JsonProperty("walletChips")]
        public int WalletChips { get; set; }

        [JsonProperty("transaction")]
        public TransactionData Transaction { get; set; }
    }

    public class TransactionData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }

    #endregion



    #region Lobby Tables

    public class TablesData
    {
        [JsonProperty("items")]
        public List<TableData> Items { get; set; }

        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }
    }

    public class Pagination
    {
        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("hasMore")]
        public bool HasMore { get; set; }
    }

    public class TableData
    {
        [JsonProperty("id")]
        public string TableId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("variantDisplay")]
        public string VariantDisplay { get; set; }

        [JsonProperty("bigBlind")]
        public int BigBlind { get; set; }

        // Backend sends no smallBlind; derive as bigBlind / 2.
        [JsonIgnore]
        public int SmallBlind => BigBlind / 2;

        [JsonProperty("minBuyIn")]
        public int MinBuyIn { get; set; }

        [JsonProperty("maxBuyIn")]
        public int MaxBuyIn { get; set; }

        [JsonProperty("currentPlayers")]
        public int CurrentPlayers { get; set; }

        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("persistent")]
        public bool IsPersistent { get; set; }

        [JsonProperty("isPrivate")]
        public bool IsPrivate { get; set; }

        [JsonProperty("allowSpectators")]
        public bool AllowSpectators { get; set; }

        [JsonProperty("avgPot")]
        public int AvgPot { get; set; }

        [JsonProperty("handsPerHour")]
        public int HandsPerHour { get; set; }

        // Populated client-side from GET /api/lobby/tables/{id}/active.
        [JsonIgnore]
        public bool HandInProgress { get; set; }

        [JsonIgnore]
        public string GameState { get; set; }
    }

    public class TableActiveData
    {
        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("gameState")]
        public string GameState { get; set; }

        [JsonProperty("handInProgress")]
        public bool HandInProgress { get; set; }
    }

    public class SpectateData
    {
        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("spectatorToken")]
        public string SpectatorToken { get; set; }

        [JsonProperty("currentState")]
        public SpectateState CurrentState { get; set; }
    }

    public class SpectateState
    {
        [JsonProperty("gameState")]
        public string GameState { get; set; }

        [JsonProperty("communityCards")]
        public List<string> CommunityCards { get; set; }

        [JsonProperty("pot")]
        public int Pot { get; set; }

        [JsonProperty("players")]
        public List<SpectatePlayer> Players { get; set; }
    }

    public class SpectatePlayer
    {
        [JsonProperty("seat")]
        public int Seat { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("chips")]
        public int Chips { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("cardsDealt")]
        public bool CardsDealt { get; set; }
    }


    #endregion

    #region Create Tables

    public class CreateTableResponse
    {
        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("shareCode")]
        public string ShareCode { get; set; }
    }


    public class CreateTableRequest
    {
        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonProperty("smallBlind")]
        public int SmallBlind { get; set; }

        [JsonProperty("bigBlind")]
        public int BigBlind { get; set; }

        [JsonProperty("minBuyIn")]
        public int MinBuyIn { get; set; }

        [JsonProperty("maxBuyIn")]
        public int MaxBuyIn { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("clubId")]
        public string ClubId { get; set; }
    }

    #endregion


    #region  Quick Join
    public class QuickJoinRequest
    {
        [JsonProperty("variant")]
        public string Variant { get; set; }
    }

    public class QuickJoinResponse
    {
        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("table")]
        public TableData Table { get; set; }
    }

    #endregion



    #region Leaderboard Models

    public class LeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank;
        [JsonProperty("playerId")] public string PlayerId;
        [JsonProperty("username")] public string Username;
        [JsonProperty("avatar")] public string Avatar;
        [JsonProperty("totalWinnings")] public int TotalWinnings;
        [JsonProperty("gamesWon")] public int GamesWon;
        [JsonProperty("winRate")] public float WinRate;
        [JsonProperty("isCurrentPlayer")] public bool IsCurrentPlayer;
    }

    public class GlobalLeaderboardData
    {
        [JsonProperty("items")] public List<LeaderboardEntry> Items;
        [JsonProperty("currentPlayerRank")] public int? CurrentPlayerRank;
        [JsonProperty("pagination")] public Pagination Pagination;
    }

    public class WeeklyLeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank;
        [JsonProperty("username")] public string Username;
        [JsonProperty("avatar")] public string Avatar;
        [JsonProperty("weeklyWinnings")] public int WeeklyWinnings;
        [JsonProperty("handsPlayed")] public int HandsPlayed;
        [JsonProperty("isCurrentPlayer")] public bool IsCurrentPlayer;
    }

    public class WeeklyLeaderboardData
    {
        [JsonProperty("weekStart")] public string WeekStart;
        [JsonProperty("weekEnd")] public string WeekEnd;
        [JsonProperty("resetsIn")] public string ResetsIn;
        [JsonProperty("items")] public List<WeeklyLeaderboardEntry> Items;
        [JsonProperty("currentPlayerRank")] public int? CurrentPlayerRank;
        [JsonProperty("pagination")] public Pagination Pagination;
    }

    #endregion


    #region Transactions

    public class TransactionHistoryData
    {
        [JsonProperty("items")]
        public List<TransactionData> Items { get; set; }

        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }
    }

    #endregion


    #region Daily Bonus Models

    public class DailyBonusData
    {
        [JsonProperty("bonusAmount")]
        public int BonusAmount { get; set; }

        [JsonProperty("newBalance")]
        public int NewBalance { get; set; }

        [JsonProperty("nextBonusAt")]
        public string NextBonusAt { get; set; }
    }

    public class DailyBonusResult
    {
        public bool Success;
        public int ChipsGranted;
        public int NewBalance;
        public System.DateTime NextBonusTime;

        public string ErrorCode;
        public string ErrorMessage;
    }
    #endregion


    public class JoinTableApiResponse
    {
        public string status;
        public JoinTableResponse data;
    }
    public class JoinTableResponse
    {
        public string tableId;
        public int seat;
        public string shareCode;
        public string variant;
        public int bigBlind;
        public int minBuyIn;
        public int maxBuyIn;
        public int maxPlayers;
    }




    #region Club Models

    public class CreateClubRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("badge")]
        public string Badge { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class CreateClubApiResponse
    {
        [JsonProperty("club")]
        public ClubData Club { get; set; }
    }

    public class ClubData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("clubCode")]
        public string ClubCode { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("ownerId")]
        public string OwnerId { get; set; }

        [JsonProperty("chipPool")]
        public int ChipPool { get; set; }

        [JsonProperty("welcomeMessage")]
        public string WelcomeMessage { get; set; }

        [JsonProperty("badge")]
        public string Badge { get; set; }

        [JsonProperty("logoUrl")]
        public string LogoUrl { get; set; }

        [JsonProperty("badBeatEnabled")]
        public bool BadBeatEnabled { get; set; }

        [JsonProperty("highHandEnabled")]
        public bool HighHandEnabled { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; }

        [JsonProperty("memberCount")]
        public int MemberCount { get; set; }
    }

    #endregion


    #region Club List Models

    public class ClubListApiResponse
    {
        [JsonProperty("clubs")]
        public List<ClubListData> Clubs { get; set; }
    }

    public class ClubListData
    {
        [JsonProperty("clubId")]
        public string ClubId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("clubCode")]
        public string ClubCode { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("chips")]
        public int Chips { get; set; }

        [JsonProperty("memberCount")]
        public int MemberCount { get; set; }

        [JsonProperty("badge")]
        public string Badge { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("joinedAt")]
        public string JoinedAt { get; set; }
    }

    #endregion



    #region Club Table Models

    public class CreateClubTableRequest
    {
        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("smallBlind")]
        public int SmallBlind { get; set; }

        [JsonProperty("bigBlind")]
        public int BigBlind { get; set; }

        [JsonProperty("maxSeats")]
        public int MaxSeats { get; set; }

        [JsonProperty("buyInMin")]
        public int BuyInMin { get; set; }

        [JsonProperty("buyInMax")]
        public int BuyInMax { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("ante")]
        public int Ante { get; set; }

        [JsonProperty("actionTimeSecs")]
        public int ActionTimeSecs { get; set; }

        [JsonProperty("durationMinutes")]
        public int? DurationMinutes { get; set; }

        [JsonProperty("straddleEnabled")]
        public bool StraddleEnabled { get; set; }

        [JsonProperty("voluntaryStraddle")]
        public bool VoluntaryStraddle { get; set; }

        [JsonProperty("runItTwice")]
        public bool RunItTwice { get; set; }

        [JsonProperty("bombPot")]
        public bool BombPot { get; set; }
    }

    // POST /api/clubs/{id}/tables
    public class CreateClubTableApiResponse
    {
        [JsonProperty("table")]
        public ClubTableData Table { get; set; }
    }

    // GET /api/clubs/{id}/tables
    public class ClubTablesApiResponse
    {
        [JsonProperty("tables")]
        public List<ClubTableData> Tables { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class ClubTableData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("clubId")]
        public string ClubId { get; set; }

        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("createdById")]
        public string CreatedById { get; set; }

        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("smallBlind")]
        public int SmallBlind { get; set; }

        [JsonProperty("bigBlind")]
        public int BigBlind { get; set; }

        [JsonProperty("ante")]
        public int Ante { get; set; }

        [JsonProperty("buyInMin")]
        public int BuyInMin { get; set; }

        [JsonProperty("buyInMax")]
        public int BuyInMax { get; set; }

        [JsonProperty("maxSeats")]
        public int MaxSeats { get; set; }

        [JsonProperty("actionTimeSecs")]
        public int ActionTimeSecs { get; set; }

        [JsonProperty("playerCount")]
        public int PlayerCount { get; set; }

        [JsonProperty("averagePot")]
        public int AveragePot { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; }

        [JsonProperty("live")]
        public bool Live { get; set; }

        [JsonProperty("durationMinutes")]
        public int DurationMinutes { get; set; }

        [JsonProperty("extensionCredits")]
        public int ExtensionCredits { get; set; }

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; }


    }

    #endregion



    #region Club Search Models

    public class ClubSearchApiResponse
    {
        [JsonProperty("club")]
        public ClubSearchData Club { get; set; }
    }

    public class ClubSearchData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("clubCode")]
        public string ClubCode { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("badge")]
        public string Badge { get; set; }

        [JsonProperty("memberCount")]
        public int MemberCount { get; set; }

        [JsonProperty("joinStatus")]
        public string JoinStatus { get; set; }
    }

    #endregion



    public class SaveClubTableTemplateRequest
    {
        [JsonProperty("slot")]
        public int Slot { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("smallBlind")]
        public int SmallBlind { get; set; }

        [JsonProperty("bigBlind")]
        public int BigBlind { get; set; }

        [JsonProperty("ante")]
        public int Ante { get; set; }

        [JsonProperty("buyInMin")]
        public int BuyInMin { get; set; }

        [JsonProperty("buyInMax")]
        public int BuyInMax { get; set; }

        [JsonProperty("maxSeats")]
        public int MaxSeats { get; set; }

        [JsonProperty("actionTimeSecs")]
        public int ActionTimeSecs { get; set; }

        [JsonProperty("bombPot")]
        public bool BombPot { get; set; }

        [JsonProperty("straddleEnabled")]
        public bool StraddleEnabled { get; set; }

        [JsonProperty("runItTwice")]
        public bool RunItTwice { get; set; }

        [JsonProperty("voluntaryStraddle")]
        public bool VoluntaryStraddle { get; set; }
    }

    public class SaveClubTableTemplateApiResponse
    {
        [JsonProperty("template")]
        public ClubTableTemplateData Template { get; set; }
    }

    public class ClubTableTemplatesApiResponse
    {
        [JsonProperty("templates")]
        public List<ClubTableTemplateData> Templates { get; set; }
    }

    public class ClubTableTemplateData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("clubId")]
        public string ClubId { get; set; }

        [JsonProperty("slot")]
        public int Slot { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("smallBlind")]
        public int SmallBlind { get; set; }

        [JsonProperty("bigBlind")]
        public int BigBlind { get; set; }

        [JsonProperty("ante")]
        public int Ante { get; set; }

        [JsonProperty("buyInMin")]
        public int BuyInMin { get; set; }

        [JsonProperty("buyInMax")]
        public int BuyInMax { get; set; }

        [JsonProperty("maxSeats")]
        public int MaxSeats { get; set; }

        [JsonProperty("actionTimeSecs")]
        public int ActionTimeSecs { get; set; }

        [JsonProperty("bombPot")]
        public bool BombPot { get; set; }

        [JsonProperty("straddleEnabled")]
        public bool StraddleEnabled { get; set; }

        [JsonProperty("runItTwice")]
        public bool RunItTwice { get; set; }
    }


    public class BulkCreateClubTablesRequest
    {
        [JsonProperty("items")]
        public List<BulkCreateClubTableItem> Items { get; set; }
    }

    public class BulkCreateClubTableItem
    {
        [JsonProperty("slot")]
        public int Slot { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("addDatePrefix")]
        public bool AddDatePrefix { get; set; }
    }

    public class BulkCreateClubTablesApiResponse
    {
        [JsonProperty("created")]
        public int Created { get; set; }

        [JsonProperty("tables")]
        public List<ClubTableData> Tables { get; set; }
    }



    public class DeleteClubTableApiResponse
    {
        [JsonProperty("disbanded")]
        public bool Disbanded { get; set; }

        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }



    #region Club Applications

    public class ClubApplicationsApiResponse
    {
        [JsonProperty("applications")]
        public List<ClubApplicationData> Applications { get; set; }
    }

    public class ClubApplicationData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("clubId")]
        public string ClubId { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("applicant")]
        public ClubApplicantData Applicant { get; set; }
    }

    public class ClubApplicantData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }
    }

    #endregion


    #region Club Members

    public class ClubMembersApiResponse
    {
        [JsonProperty("members")]
        public List<ClubMemberData> Members { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }
    }

    public class ClubMemberData
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("remark")]
        public string Remark { get; set; }

        [JsonProperty("gamesPlayed")]
        public int GamesPlayed { get; set; }

        [JsonProperty("gamesWon")]
        public int GamesWon { get; set; }

        [JsonProperty("totalFee")]
        public int TotalFee { get; set; }

        [JsonProperty("bb100")]
        public float BB100 { get; set; }

        [JsonProperty("lastLoginAt")]
        public string LastLoginAt { get; set; }

        [JsonProperty("chips")]
        public int Chips { get; set; }

        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("joinedAt")]
        public string JoinedAt { get; set; }

        [JsonProperty("totalWinnings")]
        public int TotalWinnings { get; set; }

        [JsonProperty("handsPlayed")]
        public int HandsPlayed { get; set; }

        [JsonProperty("isTableManager")]
        public bool IsTableManager { get; set; }

        [JsonProperty("chatBanned")]
        public bool ChatBanned { get; set; }

        [JsonProperty("suspended")]
        public bool Suspended { get; set; }

        [JsonProperty("agentCredit")]
        public int AgentCredit { get; set; }
    }

    #endregion

    #region Agent Models

    public class AgentDataApiResponse
    {
        [JsonProperty("agentUserId")]
        public string AgentUserId { get; set; }

        [JsonProperty("downlineCount")]
        public int DownlineCount { get; set; }

        [JsonProperty("downlineChips")]
        public int DownlineChips { get; set; }

        [JsonProperty("agentCredit")]
        public int AgentCredit { get; set; }

        [JsonProperty("stats")]
        public AgentStatsData Stats { get; set; }
    }

    public class AgentStatsData
    {
        [JsonProperty("thisWeek")]
        public AgentStatItem ThisWeek { get; set; }

        [JsonProperty("lastWeek")]
        public AgentStatItem LastWeek { get; set; }

        [JsonProperty("total")]
        public AgentStatItem Total { get; set; }
    }

    public class AgentStatItem
    {
        [JsonProperty("winnings")]
        public int Winnings { get; set; }

        [JsonProperty("fee")]
        public int Fee { get; set; }

        [JsonProperty("hands")]
        public int Hands { get; set; }
    }

    #endregion


    public class MemberDetailResponse
    {
        [JsonProperty("member")]
        public ClubMemberData Member { get; set; }
    }


    public class DeleteClubMemberResponse
    {
        [JsonProperty("removed")]
        public bool Removed { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("chipsRecalled")]
        public int ChipsRecalled { get; set; }

        [JsonProperty("shouldKickSocket")]
        public bool ShouldKickSocket { get; set; }
    }


    public class ClubOnlineApiResponse
    {
        [JsonProperty("online")]
        public List<string> Online { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }




    public class ExtendTableResponse
    {
        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; }

        [JsonProperty("addedMinutes")]
        public int AddedMinutes { get; set; }
    }




    public class PlayerStatsData
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("variantFilter")]
        public string VariantFilter { get; set; }

        [JsonProperty("totalHands")]
        public int TotalHands { get; set; }

        [JsonProperty("totalWinnings")]
        public int TotalWinnings { get; set; }

        [JsonProperty("totalFee")]
        public int TotalFee { get; set; }

        [JsonProperty("bbPer100")]
        public float BBPer100 { get; set; }

        [JsonProperty("preflop")]
        public PreflopStats Preflop { get; set; }

        [JsonProperty("postflop")]
        public PostflopStats Postflop { get; set; }

        [JsonProperty("allIn")]
        public AllInSummaryStats AllIn { get; set; }
    }

    public class PreflopStats
    {
        [JsonProperty("vpip")]
        public float VPIP { get; set; }

        [JsonProperty("pfr")]
        public float PFR { get; set; }

        [JsonProperty("stealPct")]
        public float StealPct { get; set; }

        [JsonProperty("threeBetPct")]
        public float ThreeBetPct { get; set; }

        [JsonProperty("foldTo3BetPct")]
        public float FoldTo3BetPct { get; set; }
    }

    public class PostflopStats
    {
        [JsonProperty("wtsd")]
        public float WTSD { get; set; }

        [JsonProperty("wsdWinPct")]
        public float WSDWinPct { get; set; }

        [JsonProperty("cBetPct")]
        public float CBetPct { get; set; }

        [JsonProperty("foldToCBetPct")]
        public float FoldToCBetPct { get; set; }

        [JsonProperty("checkRaisePct")]
        public float CheckRaisePct { get; set; }
    }

    public class AllInSummaryStats
    {
        [JsonProperty("wins")]
        public int Wins { get; set; }

        [JsonProperty("losses")]
        public int Losses { get; set; }
    }



    public class AllInStatsData
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("variantFilter")]
        public string VariantFilter { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("wins")]
        public int Wins { get; set; }

        [JsonProperty("losses")]
        public int Losses { get; set; }

        [JsonProperty("chops")]
        public int Chops { get; set; }

        [JsonProperty("winPct")]
        public float WinPct { get; set; }

        [JsonProperty("allInWinnings")]
        public int AllInWinnings { get; set; }

        [JsonProperty("equityRealisation")]
        public float? EquityRealisation { get; set; }

        [JsonProperty("records")]
        public List<AllInRecordData> Records { get; set; }
    }

    public class AllInRecordData
    {
    }



    public class KickPlayerResponse
    {
        [JsonProperty("kicked")]
        public bool Kicked { get; set; }

        [JsonProperty("socketCount")]
        public int SocketCount { get; set; }

        [JsonProperty("playerId")]
        public string PlayerId { get; set; }
    }


    public class KickListResponse
    {
        [JsonProperty("kickList")]
        public List<KickedPlayerData> KickList;

        [JsonProperty("count")]
        public int Count;
    }

    public class KickedPlayerData
    {
        [JsonProperty("playerId")]
        public string PlayerId;

        [JsonProperty("username")]
        public string Username;

        [JsonProperty("kickedAt")]
        public string KickedAt;
    }

    public class WaitingListResponse
    {
        [JsonProperty("waitingList")]
        public List<WaitingPlayerData> WaitingList { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class WaitingPlayerData
    {
        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("playerId")]
        public string PlayerId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("waitingSince")]
        public string WaitingSince { get; set; }
    }

    public class JoinWaitingListResponse
    {
        [JsonProperty("joined")]
        public bool Joined { get; set; }

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("waiting")]
        public int Waiting { get; set; }
    }

    public class LeaveWaitingListResponse
    {
        [JsonProperty("left")]
        public bool Left { get; set; }

        [JsonProperty("waiting")]
        public int Waiting { get; set; }
    }


    #region Join Friend Table

    public class JoinByCodeRequest
    {
        [JsonProperty("shareCode")]
        public string ShareCode { get; set; }
    }

    public class JoinByCodeResponse
    {
        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("bigBlind")]
        public int BigBlind { get; set; }

        [JsonProperty("minBuyIn")]
        public int MinBuyIn { get; set; }

        [JsonProperty("maxBuyIn")]
        public int MaxBuyIn { get; set; }

        [JsonProperty("seatsLeft")]
        public int SeatsLeft { get; set; }

        [JsonIgnore]
        public int SmallBlind => BigBlind / 2;
    }

    #endregion



    public class CareerOverviewData
    {
        [JsonProperty("period")] public string Period { get; set; }
        [JsonProperty("variant")] public string Variant { get; set; }
        [JsonProperty("clubName")] public string ClubName { get; set; }
        [JsonProperty("winnings")] public int Winnings { get; set; }
        [JsonProperty("sessions")] public List<CareerSessionData> Sessions { get; set; }
    }

    public class CareerSessionData
    {
        [JsonProperty("sessionId")] public string SessionId { get; set; }
        [JsonProperty("tableId")] public string TableId { get; set; }
        [JsonProperty("clubId")] public string ClubId { get; set; }
        [JsonProperty("clubName")] public string ClubName { get; set; }
        [JsonProperty("clubBadge")] public string ClubBadge { get; set; }
        [JsonProperty("tableName")] public string TableName { get; set; }
        [JsonProperty("variant")] public string Variant { get; set; }
        [JsonProperty("blindsLabel")] public string BlindsLabel { get; set; }
        [JsonProperty("date")] public string Date { get; set; }
        [JsonProperty("durationLabel")] public string DurationLabel { get; set; }
        [JsonProperty("games")] public int Games { get; set; }
        [JsonProperty("hands")] public int Hands { get; set; }
        [JsonProperty("buyIn")] public int BuyIn { get; set; }
        [JsonProperty("winnings")] public int Winnings { get; set; }
    }



    public class CareerHandHistoryData
    {
        [JsonProperty("items")]
        public List<CareerHandHistoryItem> Items { get; set; }

        [JsonProperty("pagination")]
        public CareerHandHistoryPagination Pagination { get; set; }
    }

    public class CareerHandHistoryItem
    {
        [JsonProperty("handId")]
        public string HandId { get; set; }

        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("tableName")]
        public string TableName { get; set; }

        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("playedAt")]
        public string PlayedAt { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("potSize")]
        public int PotSize { get; set; }

        [JsonProperty("yourCards")]
        public List<string> YourCards { get; set; }

        [JsonProperty("winningHand")]
        public string WinningHand { get; set; }

        [JsonProperty("rake")]
        public int Rake { get; set; }

        [JsonProperty("chipsWon")]
        public int ChipsWon { get; set; }

        [JsonProperty("chipsLost")]
        public int ChipsLost { get; set; }

        [JsonProperty("netResult")]
        public int NetResult { get; set; }

        [JsonProperty("isFavorite")]
        public bool IsFavorite { get; set; }
    }

    public class CareerHandHistoryPagination
    {
        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("hasMore")]
        public bool HasMore { get; set; }
    }



    public class CareerHandDetailData
    {
        [JsonProperty("round")]
        public int Round { get; set; }

        [JsonProperty("handId")]
        public string HandId { get; set; }

        [JsonProperty("variant")]
        public string Variant { get; set; }

        [JsonProperty("blindsLabel")]
        public string BlindsLabel { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("winner")]
        public CareerHandWinner Winner { get; set; }

        [JsonProperty("potWon")]
        public int PotWon { get; set; }

        [JsonProperty("handName")]
        public string HandName { get; set; }

        [JsonProperty("communityCards")]
        public List<string> CommunityCards { get; set; }

        [JsonProperty("showdownCards")]
        public List<CareerShowdownPlayer> ShowdownCards { get; set; }

        [JsonProperty("players")]
        public List<CareerHandPlayer> Players { get; set; }

        [JsonProperty("actions")]
        public List<CareerHandAction> Actions { get; set; }

        [JsonProperty("positions")]
        public CareerHandPositions Positions { get; set; }
    }

    public class CareerHandWinner
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }
    }

    public class CareerShowdownPlayer
    {
        [JsonProperty("playerId")]
        public string PlayerId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("holeCards")]
        public List<string> HoleCards { get; set; }

        [JsonProperty("handName")]
        public string HandName { get; set; }

        [JsonProperty("bestHandCards")]
        public List<string> BestHandCards { get; set; }
    }

    public class CareerHandPlayer
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("buyIn")]
        public int BuyIn { get; set; }

        [JsonProperty("chips")]
        public int Chips { get; set; }

        [JsonProperty("holeCards")]
        public List<string> HoleCards { get; set; }

        [JsonProperty("handName")]
        public string HandName { get; set; }

        [JsonProperty("bestHandCards")]
        public List<string> BestHandCards { get; set; }
    }

    public class CareerHandAction
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("playerId")]
        public string PlayerId { get; set; }

        [JsonProperty("amount")]
        public float Amount { get; set; }

        [JsonProperty("chipsAfter")]
        public int ChipsAfter { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("street")]
        public string Street { get; set; }

        [JsonProperty("cards")]
        public List<string> Cards { get; set; }
    }

    public class CareerHandPositions
    {
        [JsonProperty("dealerIdx")]
        public int DealerIdx { get; set; }

        [JsonProperty("sbIdx")]
        public int SbIdx { get; set; }

        [JsonProperty("bbIdx")]
        public int BbIdx { get; set; }
    }

    public class DiamondData
    {
        [JsonProperty("balance")]
        public int Balance { get; set; }

        [JsonProperty("lockedDiamonds")]
        public int LockedDiamonds { get; set; }

        [JsonProperty("available")]
        public int Available { get; set; }
    }


    public class ShopPackagesData
    {
        [JsonProperty("packages")]
        public List<ShopPackageData> Packages { get; set; }
    }

    public class ShopPackageData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("diamonds")]
        public int Diamonds { get; set; }

        [JsonProperty("bonusDiamonds")]
        public int BonusDiamonds { get; set; }

        [JsonProperty("totalDiamonds")]
        public int TotalDiamonds { get; set; }

        [JsonProperty("price")]
        public float Price { get; set; }

        [JsonProperty("priceLabel")]
        public string PriceLabel { get; set; }

        [JsonProperty("extraLabel")]
        public string ExtraLabel { get; set; }
    }

    public class DiamondPurchaseValidateRequest
    {
        [JsonProperty("packageId")]
        public string PackageId { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("receipt")]
        public string Receipt { get; set; }

        [JsonProperty("receiptId")]
        public string ReceiptId { get; set; }
    }

    public class DiamondPurchaseValidateData
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }

        [JsonProperty("packageId")]
        public string PackageId { get; set; }

        [JsonProperty("diamondsGranted")]
        public int DiamondsGranted { get; set; }

        [JsonProperty("bonusDiamonds")]
        public int BonusDiamonds { get; set; }

        [JsonProperty("totalDiamonds")]
        public int TotalDiamonds { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }
    }


    public class FakeDiamondReceipt
    {
        [JsonProperty("packageId")]
        public string PackageId { get; set; }

        [JsonProperty("receiptId")]
        public string ReceiptId { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }


    [Serializable]
    public class GoldData
    {
        public int Gold;
        public int Daimond;
    }

    public class GoldExchangeRequest
    {
        [JsonProperty("diamonds")]
        public int Diamonds { get; set; }

        [JsonProperty("chips")]
        public int Chips { get; set; }
    }

    public class GoldExchangeData
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("diamondsSpent")]
        public int DiamondsSpent { get; set; }

        [JsonProperty("chipsReceived")]
        public int ChipsReceived { get; set; }

        [JsonProperty("newDiamondBalance")]
        public int NewDiamondBalance { get; set; }

        [JsonProperty("newWalletChips")]
        public int NewWalletChips { get; set; }
    }

}



