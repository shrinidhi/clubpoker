using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Transport;
using UnityEngine;
using ClubPoker.Networking.Models;

public class BotSocketClient
{
    private readonly BotPlayer bot;
    private readonly string tableId;
    private readonly int bigBlind;
    private readonly BotPersonality personality;

    private SocketIO socket;

    public BotSocketClient(BotPlayer bot,  string tableId, int bigBlind = 10)
    {
        this.bot = bot;
        this.tableId = tableId;
        this.bigBlind = bigBlind;

        personality = new BotPersonality
        {
            FoldBias = UnityEngine.Random.Range(0.15f, 0.35f),
            RaiseBias = UnityEngine.Random.Range(0.10f, 0.30f),
            BluffRate = UnityEngine.Random.Range(0.05f, 0.20f)
        };
    }

    public async UniTask Connect()
    {
        string wsUrl = ClubPoker.Core.ConfigManager.Instance.Config.webSocketUrl;

        socket = new SocketIO( wsUrl,   new SocketIOOptions
            {
                Auth = new Dictionary<string, string>
                {
                    { "token", bot.Token }
                },

                Transport = TransportProtocol.WebSocket,
                Reconnection = false
            });

        socket.OnConnected += async (sender, e) =>
        {
            Debug.Log(
                $"🤖 Socket connected: {bot.Username}"
            );

            await socket.EmitAsync( "player:join_table", new
                {
                    tableId = tableId,
                    playerId = bot.PlayerId
                });
        };

        socket.On("game:your_turn", response =>
        {
            string json =
                response.GetValue().ToString();

            HandleTurn(json).Forget();
        });

        socket.On("game:error", response =>
        {
            Debug.LogError(
                $"🤖 Bot error {bot.Username}: " +
                $"{response.GetValue()}"
            );
        });

        await socket.ConnectAsync();
    }

    private async UniTaskVoid HandleTurn(string json)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            YourTurnPayload turn =
                JsonConvert.DeserializeObject<YourTurnPayload>(json);

            if (turn == null ||
                turn.ValidActions == null ||
                turn.ValidActions.Count == 0)
            {
                return;
            }

            Debug.Log(
                $"🤖 {bot.Username} turn | " +
                $"actions={string.Join(",", turn.ValidActions)} " +
                $"canCheck={turn.CanCheck} " +
                $"callAmount={turn.CallAmount} " +
                $"yourChips={turn.YourChips} " +
                $"gameState={turn.GameState}"
            );

            if (!turn.CanCheck && turn.CallAmount == 0 && turn.ValidActions.Contains("call"))
            {
                turn.CanCheck = true;
            }

            int availableChips = GetSafeChips(turn);

            if (availableChips <= 0)
            {
                await SendZeroChipFold(turn);
                return;
            }

            BotDecision decision = DecideSmartAction(turn);

            if (decision == null || string.IsNullOrEmpty(decision.Type))
            {
                decision = GetSafeFallback(turn);
            }


            if (decision != null && decision.Type == "all_in")
            {
                decision = GetSafeFallback(turn);
            }


            if (decision == null || string.IsNullOrEmpty(decision.Type))
            {
                Debug.LogWarning(
                    $"🤖 {bot.Username}: " +
                    "No safe action available."
                );

                return;
            }


            int delay =GetThinkingDelay(decision.Type);

            Debug.Log(
                $"🤖 {bot.Username} thinking " +
                $"{delay / 1000f:0.0}s before " +
                $"{decision.Type}"
            );

            await UniTask.Delay(delay);

            // Bot may have been stopped (Disconnect nulls the socket) while
            // "thinking" — the table is gone, so silently drop the action.
            if (socket == null)
                return;

            availableChips =  GetSafeChips(turn);

            if (availableChips <= 0)
            {
                await SendZeroChipFold(turn);
                return;
            }


            // Cap a call to whatever chips remain — this is what makes it an
            // all-in call rather than the (possibly larger) full call amount.
            if (decision.Type == "call")
            {
                decision.Amount =
                    Mathf.Min(decision.Amount, availableChips);
            }

            if (decision == null || string.IsNullOrEmpty(decision.Type))
            {
                Debug.LogWarning(
                    $"🤖 {bot.Username}: " +
                    "Action cancelled because no safe action exists."
                );

                return;
            }

            Dictionary<string, object> payload =  new Dictionary<string, object>
                {
                    { "tableId", tableId },
                    { "type", decision.Type }
                };

            if (decision.Type == "raise")
            {
                payload.Add(
                    "amount",
                    decision.Amount
                );
            }

            await socket.EmitAsync(
                "player:action",
                payload
            );

            Debug.Log(
                $"🤖 {bot.Username} → " +
                $"{decision.Type} " +
                $"amount={decision.Amount}"
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"🤖 Bot turn failed: {e.Message}\n" +
                $"{e.StackTrace}"
            );
        }
    }

  
    private async UniTask SendZeroChipFold(
        YourTurnPayload turn)
    {
        if (socket == null)
            return;

        if (turn.ValidActions != null &&
            turn.ValidActions.Contains("fold"))
        {
            Debug.Log(
                $"🤖 {bot.Username} has 0 chips. " +
                "Sending direct fold."
            );

            Dictionary<string, object> foldPayload =
                new Dictionary<string, object>
                {
                    { "tableId", tableId },
                    { "type", "fold" }
                };

            await socket.EmitAsync(
                "player:action",
                foldPayload
            );

            Debug.Log(
                $"🤖 {bot.Username} → fold " +
                "(0 chips)"
            );

            return;
        }


        if (turn.CanCheck &&
            turn.ValidActions != null &&
            turn.ValidActions.Contains("check"))
        {
            Debug.LogWarning(
                $"🤖 {bot.Username} has 0 chips, " +
                "but fold is not valid. Sending check."
            );

            Dictionary<string, object> checkPayload =
                new Dictionary<string, object>
                {
                    { "tableId", tableId },
                    { "type", "check" }
                };

            await socket.EmitAsync(
                "player:action",
                checkPayload
            );

            return;
        }

        Debug.LogWarning(
            $"🤖 {bot.Username} has 0 chips, " +
            "but fold/check is not available. " +
            $"ValidActions=" +
            $"{string.Join(",", turn.ValidActions)}"
        );
    }

    private BotDecision DecideSmartAction(
        YourTurnPayload turn)
    {
        int callAmount = Mathf.Max(0, turn.CallAmount);

        bool canCheck = turn.CanCheck;

        int minimumRaise =  Mathf.Max(0, turn.MinimumRaise);

        int yourChips =GetSafeChips(turn);

        int pot = GetSafePot(turn);

        string gameState =
            string.IsNullOrEmpty(turn.GameState)
                ? "PRE_FLOP"
                : turn.GameState;

        List<string> validActions =
            turn.ValidActions ??
            new List<string>();

        bool isPreFlop = gameState == "PRE_FLOP";

        bool isRiver = gameState == "RIVER";

        float potOdds =
            pot > 0
                ? (float)callAmount /
                  (pot + callAmount)
                : 0f;

        bool goodPotOdds =  potOdds < 0.25f;

        bool okPotOdds =  potOdds < 0.40f;

        bool shortStack = yourChips <= bigBlind * 5;


        bool canCall =
            validActions.Contains("call") &&
            yourChips > 0;

        // When call amount exceeds remaining stack, calling puts the bot
        // all-in for what it has left rather than the full call amount.
        int cappedCallAmount =
            Mathf.Min(callAmount, yourChips);

        bool canRaise =
            validActions.Contains("raise") &&
            yourChips > callAmount &&
            minimumRaise > 0;

        bool canFold =
            validActions.Contains("fold");

        if (yourChips <= 0)
        {
            if (canFold)
                return Decision("fold", 0);

            if (canCheck &&
                validActions.Contains("check"))
            {
                return Decision("check", 0);
            }

            return null;
        }

        // -----------------------------
        // Short stack
        // -----------------------------
        if (shortStack)
        {
            if (canCheck)
                return Decision("check", 0);

            if (goodPotOdds && canCall)
            {
                return Decision(
                    "call",
                    cappedCallAmount
                );
            }

            if (canFold &&
                UnityEngine.Random.value <
                personality.FoldBias * 1.5f)
            {
                return Decision("fold", 0);
            }

            if (canCall)
            {
                return Decision(
                    "call",
                    cappedCallAmount
                );
            }

            return canFold
                ? Decision("fold", 0)
                : GetSafeNonAllInDecision(turn);
        }

        // -----------------------------
        // Pre-flop
        // -----------------------------
        if (isPreFlop)
        {
            if (canCheck)
            {
                if (canRaise &&
                    UnityEngine.Random.value <
                    personality.RaiseBias * 0.6f)
                {
                    int raiseAmt =
                        minimumRaise +
                        UnityEngine.Random.Range(
                            0,
                            bigBlind * 2
                        );

                    raiseAmt =
                        ClampRaiseAmount(
                            raiseAmt,
                            minimumRaise,
                            yourChips
                        );

                    if (raiseAmt > 0)
                    {
                        return Decision(
                            "raise",
                            raiseAmt
                        );
                    }
                }

                return Decision("check", 0);
            }

            float betToBB =
                bigBlind > 0
                    ? (float)callAmount /
                      bigBlind
                    : 0f;

            float foldAdj =
                personality.FoldBias +
                (betToBB > 3f ? 0.15f : 0f);

            if (canFold &&
                UnityEngine.Random.value < foldAdj)
            {
                return Decision("fold", 0);
            }

            if (canRaise &&
                UnityEngine.Random.value <
                personality.RaiseBias * 0.4f)
            {
                int raiseAmt =
                    callAmount * 3 +
                    UnityEngine.Random.Range(
                        0,
                        bigBlind * 2
                    );

                raiseAmt =
                    ClampRaiseAmount(
                        raiseAmt,
                        minimumRaise,
                        yourChips
                    );

                if (raiseAmt > 0)
                {
                    return Decision(
                        "raise",
                        raiseAmt
                    );
                }
            }

            if (canCall)
            {
                return Decision(
                    "call",
                    cappedCallAmount
                );
            }

            return canFold
                ? Decision("fold", 0)
                : GetSafeNonAllInDecision(turn);
        }

        // -----------------------------
        // Post-flop: can check
        // -----------------------------
        if (canCheck)
        {
            float betChance =
                personality.RaiseBias *
                (isRiver ? 1.2f : 0.8f) +
                (isRiver
                    ? personality.BluffRate
                    : 0f);

            if (canRaise &&
                UnityEngine.Random.value <
                betChance)
            {
                float betFraction =
                    0.5f +
                    UnityEngine.Random.value *
                    0.5f;

                int betAmt =
                    Mathf.FloorToInt(
                        pot * betFraction
                    );

                betAmt =
                    ClampRaiseAmount(
                        betAmt,
                        minimumRaise,
                        yourChips
                    );

                if (betAmt > 0)
                {
                    return Decision(
                        "raise",
                        betAmt
                    );
                }
            }

            return Decision("check", 0);
        }

        // -----------------------------
        // Post-flop: facing bet
        // -----------------------------
        float foldProb =
            personality.FoldBias;

        if (goodPotOdds)
            foldProb *= 0.3f;
        else if (okPotOdds)
            foldProb *= 0.6f;
        else
            foldProb *= 1.3f;

        if (isRiver)
        {
            foldProb *=
                UnityEngine.Random.value <
                personality.BluffRate
                    ? 0.4f
                    : 1.1f;
        }

        if (canFold &&
            UnityEngine.Random.value < foldProb)
        {
            return Decision("fold", 0);
        }

        float reraiseChance =
            personality.RaiseBias *
            0.3f *
            (isRiver ? 1.4f : 1.0f);

        if (canRaise &&
            UnityEngine.Random.value <
            reraiseChance)
        {
            int raiseAmt =
                Mathf.FloorToInt(
                    pot *
                    (0.75f +
                     UnityEngine.Random.value *
                     0.5f)
                );

            raiseAmt =
                ClampRaiseAmount(
                    raiseAmt,
                    minimumRaise,
                    yourChips
                );

            if (raiseAmt > 0)
            {
                return Decision(
                    "raise",
                    raiseAmt
                );
            }
        }

        if (canCall)
        {
            return Decision(
                "call",
                cappedCallAmount
            );
        }

        return canFold
            ? Decision("fold", 0)
            : GetSafeNonAllInDecision(turn);
    }

    private BotDecision GetSafeNonAllInDecision(
        YourTurnPayload turn)
    {
        if (turn == null ||
            turn.ValidActions == null)
        {
            return null;
        }

        int yourChips =
            GetSafeChips(turn);

        if (yourChips <= 0)
        {
            if (turn.ValidActions.Contains("fold"))
                return Decision("fold", 0);

            if (turn.CanCheck &&
                turn.ValidActions.Contains("check"))
            {
                return Decision("check", 0);
            }

            return null;
        }

        if (turn.CanCheck &&
            turn.ValidActions.Contains("check"))
        {
            return Decision("check", 0);
        }

        if (turn.ValidActions.Contains("call") &&
            turn.CallAmount >= 0 &&
            turn.CallAmount < yourChips)
        {
            return Decision(
                "call",
                turn.CallAmount
            );
        }

        if (turn.ValidActions.Contains("fold"))
        {
            return Decision("fold", 0);
        }

        return null;
    }

    private BotDecision GetSafeFallback(
        YourTurnPayload turn)
    {
        if (turn == null ||
            turn.ValidActions == null)
        {
            return null;
        }

        int yourChips =
            GetSafeChips(turn);

        if (yourChips <= 0)
        {
            if (turn.ValidActions.Contains("fold"))
                return Decision("fold", 0);

            if (turn.CanCheck &&
                turn.ValidActions.Contains("check"))
            {
                return Decision("check", 0);
            }

            return null;
        }

        if (turn.CanCheck &&
            turn.ValidActions.Contains("check"))
        {
            return Decision("check", 0);
        }

        if (turn.ValidActions.Contains("call") &&
            turn.CallAmount >= 0 &&
            turn.CallAmount < yourChips)
        {
            return Decision(
                "call",
                turn.CallAmount
            );
        }

        if (turn.ValidActions.Contains("fold"))
        {
            return Decision("fold", 0);
        }

        return null;
    }

    private BotDecision Decision(
        string type,
        int amount)
    {
        return new BotDecision
        {
            Type = type,
            Amount = amount
        };
    }

    private int ClampRaiseAmount(
        int amount,
        int minimumRaise,
        int yourChips)
    {
        if (yourChips <= 0)
            return 0;

        int maxSafeRaise =
            Mathf.FloorToInt(
                yourChips * 0.70f
            );

        if (maxSafeRaise < minimumRaise)
            return 0;

        amount =
            Mathf.Max(
                amount,
                minimumRaise
            );

        amount =
            Mathf.Min(
                amount,
                maxSafeRaise
            );

        return amount;
    }

    private int GetSafeChips(
        YourTurnPayload turn)
    {
        if (turn == null)
            return 0;

        return Mathf.Max(
            0,
            turn.YourChips
        );
    }

    private int GetSafePot(
        YourTurnPayload turn)
    {
        if (turn == null)
            return 0;

        if (turn.Pot > 0)
            return turn.Pot;

        return 0;
    }

    private int GetThinkingDelay(
        string action)
    {
        switch (action)
        {
            case "check":
                return UnityEngine.Random.Range(
                    1000,
                    3000
                );

            case "call":
                return UnityEngine.Random.Range(
                    2000,
                    5000
                );

            case "raise":
                return UnityEngine.Random.Range(
                    3000,
                    6000
                );

            case "fold":
                return UnityEngine.Random.Range(
                    1000,
                    3000
                );

            default:
                return UnityEngine.Random.Range(
                    1000,
                    3000
                );
        }
    }

    public void Disconnect()
    {
        socket?.DisconnectAsync();
        socket = null;
    }

    private class BotDecision
    {
        public string Type;
        public int Amount;
    }

    [Serializable]
    private class BotPersonality
    {
        public float FoldBias;
        public float RaiseBias;
        public float BluffRate;
    }
}