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

            int delay = UnityEngine.Random.Range(600, 1600);
            await UniTask.Delay(delay);

            string action = DecideAction(turn);

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

        public void Disconnect()
        {
            socket?.DisconnectAsync();
            socket = null;
        }
    }
