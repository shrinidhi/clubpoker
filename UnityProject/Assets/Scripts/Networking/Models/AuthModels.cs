using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClubPoker.Networking.Models
{
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

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("walletChips")]
        public int WalletChips { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("gamesPlayed")]
        public int GamesPlayed { get; set; }

        [JsonProperty("gamesWon")]
        public int GamesWon { get; set; }

        [JsonProperty("totalWinnings")]
        public int TotalWinnings { get; set; }

        [JsonProperty("lastDailyBonus")]
        public DateTime? LastDailyBonus { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
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
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("playerId")] public string PlayerId { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("amount")] public int Amount { get; set; }
        [JsonProperty("tableId")] public string TableId { get; set; }
        [JsonProperty("handId")] public string HandId { get; set; }
        [JsonProperty("balanceBefore")] public int BalanceBefore { get; set; }
        [JsonProperty("balanceAfter")] public int BalanceAfter { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
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

        [JsonProperty("currentPlayers")]
        public int CurrentPlayers { get; set; }

        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("isPrivate")]
        public bool IsPrivate { get; set; }

        [JsonProperty("allowSpectators")]
        public bool AllowSpectators { get; set; }

        [JsonProperty("avgPot")]
        public int AvgPot { get; set; }

        [JsonProperty("handsPerHour")]
        public int HandsPerHour { get; set; }
    }


    #endregion

    #region Create Tables

    public class CreateTableResponse
    {
        [JsonProperty("tableId")]
        public string TableId { get; set; }

        [JsonProperty("table")]
        public TableData Table { get; set; }
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




}



