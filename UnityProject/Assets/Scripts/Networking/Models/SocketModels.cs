
// All Socket.io event payload models.
// Mirrors the Socket.io v4 event contract exactly — do not change field names
// without coordinating with the backend team.
//
// Event flow reference:
//   CLIENT connects   → server emits socket:authenticated
//   CLIENT emits      → player:join_table
//   SERVER emits      → game:state_update (broadcast)
//   CLIENT emits      → player:reconnect
//   SERVER emits      → game:state_update + game:your_cards + game:player_reconnected
//   SERVER emits      → game:error (on any failure)

using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClubPoker.Networking.Models
{
    // ── Inbound — server → client ─────────────────────────────────────────────

    /// <summary>
    /// socket:authenticated
    /// Emitted privately to the socket immediately after a successful JWT handshake.
    /// </summary>
    public class SocketAuthenticatedPayload
    {
        [JsonProperty("playerId")] public string PlayerId { get; set; }
        [JsonProperty("username")] public string Username { get; set; }

    }

    /// <summary>
    /// game:state_update
    /// Authoritative snapshot of all public game state.
    /// NEVER contains actual hole card strings — cardsDealt is boolean only.
    /// Hole cards are delivered exclusively via game:your_cards.
    /// </summary>
    public class GameStateUpdatePayload
    {
        [JsonProperty("tableId")]              public string            TableId              { get; set; }
        [JsonProperty("gameState")]            public string            GameState            { get; set; }
        [JsonProperty("roundNumber")]          public int               RoundNumber          { get; set; }
        [JsonProperty("pot")]                  public int               Pot                  { get; set; }
        [JsonProperty("sidePots")]             public List<SidePots>     SidePots             { get; set; }
        [JsonProperty("communityCards")]       public List<string>      CommunityCards       { get; set; }
        [JsonProperty("dealerSeat")]           public int               DealerSeat           { get; set; }
        [JsonProperty("currentTurnPlayerId")]  public string            CurrentTurnPlayerId  { get; set; }
        [JsonProperty("players")]              public List<GamePlayer>  Players              { get; set; }
    }

    public class SidePot
    {
        [JsonProperty("amount")]   public int           Amount    { get; set; }
        [JsonProperty("eligible")] public List<string>  Eligible  { get; set; }
    }
   
    public class RequestStatePayload
    {
        public string TableId;
    }

    public class GamePlayer
    {
        [JsonProperty("id")]          public string  Id          { get; set; }
        [JsonProperty("username")]    public string  Username    { get; set; }
        [JsonProperty("chips")]       public int     Chips       { get; set; }
        [JsonProperty("seat")]        public int     Seat        { get; set; }
        [JsonProperty("folded")]      public bool    Folded      { get; set; }
        [JsonProperty("allIn")]       public bool    AllIn       { get; set; }
        [JsonProperty("lastAction")]  public string  LastAction  { get; set; }
        [JsonProperty("cardsDealt")]  public bool    CardsDealt  { get; set; }
    }

    /// <summary>
    /// game:error
    /// Emitted by server on any socket-level game error.
    /// G005 — table full
    /// G007 — table not found
    /// A005 — reconnect token invalid or grace period expired
    /// </summary>
    public class GameErrorPayload
    {
        [JsonProperty("code")]    public string Code    { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    // ── Outbound — client → server ────────────────────────────────────────────

    /// <summary>
    /// player:join_table
    /// Emit immediately after receiving socket:authenticated.
    /// Server assigns a seat and broadcasts game:state_update to all room members.
    /// </summary>
    public class PlayerJoinTablePayload
    {
        [JsonProperty("tableId")]   public string TableId   { get; set; }
        [JsonProperty("playerId")]  public string PlayerId  { get; set; }
    }

    /// <summary>
    /// player:reconnect
    /// Emit after re-establishing socket connection within the 60-second grace period.
    /// Requires a one-time reconnect token from POST /api/reconnect/token.
    /// Token is single-use and expires in 60 seconds.
    /// </summary>
    public class PlayerReconnectPayload
    {
        [JsonProperty("tableId")]         public string TableId         { get; set; }
        [JsonProperty("reconnectToken")]  public string ReconnectToken  { get; set; }
    }

    // ── REST — reconnect token request ────────────────────────────────────────

    /// <summary>
    /// POST /api/reconnect/token request body.
    /// Called immediately on disconnect to pre-generate the reconnect token
    /// while the JWT is still valid.
    /// </summary>
    public class ReconnectTokenRequest
    {
        [JsonProperty("tableId")] public string TableId { get; set; }
    }

    /// <summary>
    /// POST /api/reconnect/token response data.
    /// </summary>
    public class ReconnectTokenResponse
    {
        [JsonProperty("reconnectToken")] public string ReconnectToken { get; set; }
        [JsonProperty("expiresAt")]      public string ExpiresAt      { get; set; }
    }

    // ── Connection state ──────────────────────────────────────────────────────

    /// <summary>
    /// The 4 possible states of the socket connection.
    /// Used by SocketManager state machine (CLUB-487).
    /// </summary>
    /// 


    // ── Your Card ──────────────────────────────────────────────────────
    public class YourCardsPayload
    {
        [JsonProperty("cards")]  public List<string> Cards { get; set; }
        [JsonProperty("variant")]  public string Variant { get; set; }
    }

    public class CommunityCardsPayload
    {
        [JsonProperty("cards")] public List<string> Cards { get; set; }
        [JsonProperty("street")] public string Street { get; set; }
    }


    public class YourTurnPayload
    {
        [JsonProperty("playerId")] public string PlayerId { get; set; }
        [JsonProperty("gameState")] public string GameState { get; set; }
        [JsonProperty("pot")] public int Pot { get; set; }
        [JsonProperty("currentBet")] public int CurrentBet { get; set; }
        [JsonProperty("callAmount")] public int CallAmount { get; set; }
        [JsonProperty("canCheck")] public bool CanCheck { get; set; }
        [JsonProperty("minimumRaise")] public int MinimumRaise { get; set; }
        [JsonProperty("yourChips")] public int YourChips { get; set; }
        [JsonProperty("timeAllowedMs")] public long TimeAllowedMs { get; set; }
        [JsonProperty("validActions")] public List<string> ValidActions { get; set; }
        [JsonProperty("serverTime")] public long ServerTime { get; set; }
    }


    public class TimerTickPayload
    {
        [JsonProperty("remainingMs")] public long RemainingMs { get; set; }
        [JsonProperty("serverTime")] public long ServerTime { get; set; }
        [JsonProperty("playerId")] public string PlayerId { get; set; }
    }

    public class TimerStartPayload
    {
        [JsonProperty("playerId")] public string PlayerId { get; set; }
        [JsonProperty("durationMs")] public long DurationMs { get; set; }
        [JsonProperty("serverTime")] public long ServerTime { get; set; }
    }


    public class PlayerActedPayload
    {
        [JsonProperty("playerId")] public string PlayerId { get; set; }
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("action")] public string Action { get; set; }
        [JsonProperty("amount")] public int Amount { get; set; }
        [JsonProperty("pot")] public int Pot { get; set; }
        [JsonProperty("updatedChips")] public int UpdatedChips { get; set; }
        [JsonProperty("nextPlayerId")] public string NextPlayerId { get; set; }
    }


    public class RoundEndPayload
    {
        [JsonProperty("winner")] public WinnerData winner { get; set; }
        [JsonProperty("potWon")] public int potWon { get; set; }
        [JsonProperty("hand")] public HandData hand { get; set; }
        [JsonProperty("communityCards")] public List<string> communityCards { get; set; }
        [JsonProperty("updatedChipBalances")] public Dictionary<string, int> updatedChipBalances { get; set; }
        [JsonProperty("rake")] public int rake { get; set; }
        [JsonProperty("roundNumber")] public int roundNumber { get; set; }
        [JsonProperty("showdown")] public bool showdown { get; set; }
    }

    
    public class WinnerData
    {
        [JsonProperty("id")] public string id { get; set; }
        [JsonProperty("username")] public string username { get; set; }
        [JsonProperty("holeCards")] public List<string> holeCards { get; set; }
    }

   
    public class HandData
    {
        [JsonProperty("rank")] public int rank { get; set; }
        [JsonProperty("name")] public string name { get; set; }
        [JsonProperty("cards")] public List<string> cards { get; set; }
    }

    public class PotUpdatePayload
    {
        [JsonProperty("pot")] public int pot { get; set; }
        [JsonProperty("sidePots")] public List<SidePots> sidePots { get; set; }
    }

    
    public class SidePots
    {
        [JsonProperty("amount")] public int amount { get; set; }
        [JsonProperty("eligiblePlayerIds")] public List<string> eligiblePlayerIds { get; set; }
    }



    public class DealerMovedPayload
    {
        [JsonProperty("dealerSeat")] public int dealerSeat { get; set; }
        [JsonProperty("smallBlindSeat")] public int smallBlindSeat { get; set; }
        [JsonProperty("bigBlindSeat")] public int bigBlindSeat { get; set; }
        [JsonProperty("preFlopFirstActorSeat")] public int preFlopFirstActorSeat { get; set; }
    }
    public class PlayerJoinedPayload
    {
        [JsonProperty("player")]
        public JoinedPlayerData player { get; set; }

        [JsonProperty("seat")]
        public int seat { get; set; }
    }

    public class JoinedPlayerData
    {
        [JsonProperty("id")]
        public string id { get; set; }

        [JsonProperty("username")]
        public string username { get; set; }

        [JsonProperty("chips")]
        public int chips { get; set; }
    }



    public class PlayerLeftPayload
    {
        [JsonProperty("playerId")] public string playerId { get; set; }
        [JsonProperty("username")] public string username { get; set; }
        [JsonProperty("chipsReturned")] public int chipsReturned { get; set; }
    }

    public class PlayerDisconnectedPayload
    {
        [JsonProperty("playerId")] public string playerId { get; set; }
        [JsonProperty("username")] public string username { get; set; }
        [JsonProperty("gracePeriodSeconds")] public int gracePeriodSeconds { get; set; }
    }

    public class PlayerReconnectedPayload
    {
        [JsonProperty("playerId")] public string playerId { get; set; }
        [JsonProperty("username")] public string username { get; set; }
    }


    public class GamePausedPayload
    {
        [JsonProperty("reason")] public string reason { get; set; }
        [JsonProperty("countdownSeconds")] public int countdownSeconds { get; set; }
    }

 
    public class GameResumedPayload
    {
        [JsonProperty("playerCount")] public int playerCount { get; set; }
    }


   
    public class PlayerSittingOut_CameBackPayload
    {
        [JsonProperty("playerId")] public string playerId { get; set; }
        [JsonProperty("username")] public string username { get; set; }
    }

    public class GameChatPayload
    {
        [JsonProperty("playerId")] public string playerId { get; set; }
        [JsonProperty("username")] public string username { get; set; }
        [JsonProperty("text")] public string text { get; set; }
        [JsonProperty("timestamp")] public string timestamp { get; set; }
    }



    public enum SocketConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }
}