using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ClubPoker.Networking.Models;


    public class UnityBotRunner : MonoBehaviour
    {
        public static UnityBotRunner Instance { get; private set; }

        [SerializeField] private int botCount = 1;
        [SerializeField] private int buyInAmount = 1000;

        private readonly List<BotPlayer> bots = new();
        private bool isRunning;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

    public async UniTask StartBots(string tableId, int maxPlayers, int minBuyIn = 0)
    {
        if (isRunning) return;

        isRunning = true;

        int amount = minBuyIn > 0 ? minBuyIn : buyInAmount;
        int botsToCreate = Mathf.Max(0, maxPlayers - 1);

        Debug.Log($"[BotRunner] MaxPlayers={maxPlayers}, BotsToCreate={botsToCreate}, BuyIn={amount}");

        for (int i = 0; i < botsToCreate; i++)
        {
            await CreateBot(tableId, amount);
            await UniTask.Delay(300);
        }

        Debug.Log("✅ All bots ready");
    }

    private async UniTask CreateBot(string tableId, int amount)
        {
            long suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000;
            string username = $"BOT_{suffix}{UnityEngine.Random.Range(10, 99)}";
            string email = username.ToLower() + "@bot.dev";
            string password = "Test1234!";

            var login = await BotApiClient.Post<LoginResponse>(
                "/api/auth/register",
                new { username, email, password }
            );

            var bot = new BotPlayer
            {
                Username = login.Player.Username,
                PlayerId = login.Player.Id,
                Token = login.Tokens.AccessToken
            };

            bots.Add(bot);

            await BotApiClient.Post<BuyInResponse>(
                "/api/economy/buyin",
                new { tableId, amount },
                bot.Token
            );

            await BotApiClient.Post<JoinTableResponse>(
                $"/api/lobby/tables/{tableId}/join",
                new { buyInAmount = amount },
                bot.Token
            );

            bot.Socket = new BotSocketClient(bot, tableId);
            await bot.Socket.Connect();

            Debug.Log($"🤖 Bot joined: {bot.Username}");
        }

        public void StopBots()
        {
            foreach (var bot in bots)
                bot.Socket?.Disconnect();

            bots.Clear();
            isRunning = false;
        }
    }

    [Serializable]
    public class BotPlayer
    {
        public string Username;
        public string PlayerId;
        public string Token;
        public BotSocketClient Socket;
    }
