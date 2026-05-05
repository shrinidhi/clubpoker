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
    private SocketIO socket;

    public BotSocketClient(BotPlayer bot, string tableId)
    {
        this.bot = bot;
        this.tableId = tableId;
    }

    public async UniTask Connect()
    {
        socket = new SocketIO("http://143.110.247.128:3050", new SocketIOOptions
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
            Debug.Log($"🤖 Socket connected: {bot.Username}");

            await socket.EmitAsync("player:join_table", new
            {
                tableId = tableId,
                playerId = bot.PlayerId
            });
        };

        socket.On("game:your_turn", response =>
        {
            string json = response.GetValue().ToString();
            HandleTurn(json).Forget();
        });

        socket.On("game:error", response =>
        {
            Debug.LogError($"🤖 Bot error {bot.Username}: {response.GetValue()}");
        });

        await socket.ConnectAsync();
    }

    private async UniTaskVoid HandleTurn(string json)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var turn = JsonConvert.DeserializeObject<YourTurnPayload>(json);

            if (turn == null || turn.ValidActions == null || turn.ValidActions.Count == 0)
                return;

            string turnPlayerId = string.IsNullOrEmpty(turn.PlayerId)
                ? bot.PlayerId
                : turn.PlayerId;


          

            string action = DecideAction(turn);
            int delay = GetThinkingDelay(action);

            Debug.Log($"🤖 {bot.Username} thinking {delay / 1000f:0.0}s before {action}");
            //GameEvents.OnPlayerThinking?.Invoke(turnPlayerId);
            await UniTask.Delay(delay);

            var payload = new Dictionary<string, object>
        {
            { "tableId", tableId },
            { "type", action }
        };

            if (action == "raise")
            {
                int amount = Mathf.Max(turn.MinimumRaise, turn.CallAmount * 2);
                payload.Add("amount", amount);
            }

            await socket.EmitAsync("player:action", payload);

            Debug.Log($"🤖 {bot.Username} → {action}");
           

        }
        catch (Exception e)
        {
            Debug.LogError($"🤖 Bot turn failed: {e.Message}");
        }
    }

    private string DecideAction(YourTurnPayload turn)
    {
        if (turn.CanCheck && turn.ValidActions.Contains("check"))
            return "check";

        if (turn.ValidActions.Contains("call"))
            return "call";

        if (turn.ValidActions.Contains("fold"))
            return "fold";

        return turn.ValidActions[0];
    }

    private int GetThinkingDelay(string action)
    {
        switch (action)
        {
            case "check":
                return UnityEngine.Random.Range(3000, 5000);

            case "call":
                return UnityEngine.Random.Range(4000, 8000);

            case "raise":
                return UnityEngine.Random.Range(5000, 8000);

            case "fold":
                return UnityEngine.Random.Range(3000, 5000);

            case "all_in":
                return UnityEngine.Random.Range(3000, 5000);

            default:
                return UnityEngine.Random.Range(2000,5000);
        }
    }

    public void Disconnect()
    {
        socket?.DisconnectAsync();
        socket = null;
    }
}