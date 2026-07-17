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

        // Create all bots in parallel — the server auto-starts a hand a few seconds
        // after 2 players are seated, so sequential joins leave late bots out of the
        // first hand (invisible until it ends). All bots must be seated before that.
        // Starts are staggered 120ms apart: fully simultaneous registers race in the
        // backend DB (500 S003), and the web simulator's ~140ms spacing never does.
        var tasks = new List<UniTask>(botsToCreate);
        for (int i = 0; i < botsToCreate; i++)
        {
            tasks.Add(CreateBotSafe(tableId, amount, i));
            await UniTask.Delay(120);
        }

        await UniTask.WhenAll(tasks);

        Debug.Log("✅ All bots ready");
    }

    // One failed bot must not abort the rest (or the caller's table start).
    private async UniTask CreateBotSafe(string tableId, int amount, int index)
    {
        const int maxAttempts = 2;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await CreateBot(tableId, amount, index);
                return;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BotRunner] Bot {index} attempt {attempt}/{maxAttempts} failed: {e.Message}");

                if (attempt < maxAttempts)
                    await UniTask.Delay(400);
            }
        }
    }

    private async UniTask CreateBot(string tableId, int amount, int index)
        {
            long suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000;
            string username = $"BOT_{suffix}{index}{UnityEngine.Random.Range(10, 99)}";
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

            var joinResult = await BotApiClient.Post<JoinTableResponse>(
                $"/api/lobby/tables/{tableId}/join",
                new { buyInAmount = amount },
                bot.Token
            );
            
            Debug.Log($"🤖 Bot REST join OK: seat={joinResult?.seat} tableId={tableId}");

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
