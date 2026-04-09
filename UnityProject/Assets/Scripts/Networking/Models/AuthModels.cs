using System;
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
}