
using System;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance;

        [Header("Time Bank UI")]
        public Text TimeBankText;

        [Header("Action Buttons")]
        public ActionButtonHandler ActionButtons;

        [Header("Time Bank Button")]
        public TimeBankButtonHandler TimeBankButton;

        public bool _isMyTurn = false;
        public bool IsMyTurn => _isMyTurn;

        private const int WARNING_SECONDS = 5;

        // Server clock synchronization
        private long serverClockOffsetMs;
        private long turnEndsAt;
        private long lastServerRemainingMs;

        // The player for whom game:timer_start was received
        private string turnStartedForPlayerId;
        private bool timerStartReceived;

        private bool timerRunning;
        private bool autoActionSent;
        private int lastWarnedSecond = -1;

        // Own-turn reconnect state
        private long myTurnDeadlineMs;
        private int myTurnRound = -1;
        private string myTurnStreet;
        private bool resumingAfterReconnect;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            if (!timerRunning)
                return;

            UpdateServerAuthoritativeTimer();
        }

        private long GetLocalUnixTimeMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private string GetMyPlayerId()
        {
            var manager = Auth.AuthManager.Instance;

            return manager != null
                ? manager.Session?.Id ?? string.Empty
                : string.Empty;
        }

        // -------------------------------------------------
        // YOUR TURN: action buttons only
        // -------------------------------------------------

        public void StartYourTurn(YourTurnPayload payload)
        {
            if (payload == null)
                return;

            Debug.Log("[TurnManager] StartYourTurn");

            _isMyTurn = true;
            autoActionSent = false;
            lastWarnedSecond = -1;

            if (ActionButtons != null)
            {
                ActionButtons.EnableActions(
                    payload.ValidActions,
                    payload.CanCheck,
                    payload.MinimumRaise,
                    payload.YourChips,
                    payload.CallAmount
                );
            }

            if (TimeBankText != null)
            {
                TimeBankText.text = Loc.Get(
                    "time_bank_seconds",
                    ("seconds", payload.TimeAllowedMs / 1000)
                );
            }

            if (TimeBankButton != null)
                TimeBankButton.OnYourTurnStart(true);

            // Keep the existing reconnect deadline information.
            // The visible timer itself waits for game:timer_start.
            int round = GameStateManager.Instance != null
                ? GameStateManager.Instance.RoundNumber
                : -1;

            long localNow = GetLocalUnixTimeMs();

            if (resumingAfterReconnect &&
                myTurnDeadlineMs > localNow &&
                myTurnRound == round &&
                myTurnStreet == payload.GameState)
            {
                Debug.Log(
                    $"[TurnManager] Existing own-turn deadline retained: " +
                    $"{(myTurnDeadlineMs - localNow) / 1000f:0.#}s"
                );
            }
            else
            {
                myTurnDeadlineMs = 0;
            }

            myTurnRound = round;
            myTurnStreet = payload.GameState;
            resumingAfterReconnect = false;
        }

        // -------------------------------------------------
        // GAME:TIMER_START: authoritative timer for any seat
        // -------------------------------------------------

        public void StartPlayerTimer(
            string playerId,
            long durationMs,
            long receivedTurnEndsAt,
            long serverTime)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogWarning("[TurnManager] timer_start has empty playerId.");
                return;
            }

            if (receivedTurnEndsAt <= 0 || serverTime <= 0)
            {
                Debug.LogWarning(
                    "[TurnManager] timer_start missing turnEndsAt/serverTime."
                );
                return;
            }

            long localNow = GetLocalUnixTimeMs();

            // Server time - local time at the moment this event arrived.
            serverClockOffsetMs = serverTime - localNow;

            turnStartedForPlayerId = playerId;
            turnEndsAt = receivedTurnEndsAt;
            lastServerRemainingMs = Math.Max(
                0,
                turnEndsAt - (localNow + serverClockOffsetMs)
            );

            timerStartReceived = true;
            timerRunning = true;

            autoActionSent = false;
            lastWarnedSecond = -1;

            Debug.Log(
                $"[TimerStart] Player={playerId}, " +
                $"Duration={durationMs}ms, " +
                $"TurnEndsAt={turnEndsAt}, " +
                $"ServerOffset={serverClockOffsetMs}, " +
                $"Remaining={lastServerRemainingMs}ms"
            );

            // Do not show the ring here blindly.
            // It is shown only after state_update confirms this player
            // is the current, non-folded player.
            RefreshTimerIndicatorFromGameState();
        }

        // -------------------------------------------------
        // Called after every game:state_update
        // -------------------------------------------------

        public void SyncCurrentTurnIndicator(
            string currentTurnPlayerId,
            bool currentTurnPlayerHasFolded)
        {
            if (!timerStartReceived ||
                !timerRunning ||
                string.IsNullOrEmpty(turnStartedForPlayerId))
            {
                return;
            }

            bool shouldShow =
                !currentTurnPlayerHasFolded &&
                !string.IsNullOrEmpty(currentTurnPlayerId) &&
                string.Equals(
                    currentTurnPlayerId,
                    turnStartedForPlayerId,
                    StringComparison.OrdinalIgnoreCase
                );

            if (!shouldShow)
            {
                if (PokerTableUI.Instance != null)
                    PokerTableUI.Instance.HideAllThinking();

                return;
            }

            ShowCurrentTimerIndicator();
        }

        private void RefreshTimerIndicatorFromGameState()
        {
            var game = GameStateManager.Instance;

            if (game == null)
                return;

            string currentPlayerId = game.CurrentTurnPlayerId;

            // Pass the actual folded state from your state_update data
            // through SyncCurrentTurnIndicator() after state_update.
            // Do not guess folded state here.
            if (!string.IsNullOrEmpty(currentPlayerId) &&
                string.Equals(
                    currentPlayerId,
                    turnStartedForPlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                ShowCurrentTimerIndicator();
            }
        }

        private void ShowCurrentTimerIndicator()
        {
            if (PokerTableUI.Instance == null)
                return;

            long remainingMs = GetRemainingMs();

            if (remainingMs <= 0)
                return;

            int round = GameStateManager.Instance != null
                ? GameStateManager.Instance.RoundNumber
                : 0;

            float remainingSeconds = remainingMs / 1000f;
            float totalSeconds = Math.Max(
                1,
                lastServerRemainingMs / 1000f
            );

            PokerTableUI.Instance.ShowThinkingAndTimer(
                turnStartedForPlayerId,
                remainingSeconds,
                round,
                totalSeconds,
                force: true
            );
        }

        // -------------------------------------------------
        // Timer countdown
        // -------------------------------------------------

        private long GetRemainingMs()
        {
            if (!timerStartReceived || turnEndsAt <= 0)
                return 0;

            long estimatedServerNow =
                GetLocalUnixTimeMs() + serverClockOffsetMs;

            return Math.Max(0, turnEndsAt - estimatedServerNow);
        }

        private void UpdateServerAuthoritativeTimer()
        {
            long remainingMs = GetRemainingMs();

            int totalSeconds = Mathf.CeilToInt(
                remainingMs / 1000f
            );

            if (_isMyTurn &&
                totalSeconds > 0 &&
                totalSeconds <= WARNING_SECONDS &&
                totalSeconds != lastWarnedSecond)
            {
                lastWarnedSecond = totalSeconds;
                GameAudio.Play(SoundId.TimerWarning);
            }

            if (remainingMs <= 0)
                AutoFoldOnTimeout();
        }

        private void AutoFoldOnTimeout()
        {
            if (autoActionSent)
                return;

            autoActionSent = true;

            if (_isMyTurn)
            {
                string myId = GetMyPlayerId();
                var game = GameStateManager.Instance;

                bool stillMyTurn =
                    game != null &&
                    string.Equals(
                        game.CurrentTurnPlayerId,
                        myId,
                        StringComparison.OrdinalIgnoreCase
                    );

                bool sittingOut =
                    game != null && game.IsPlayerSittingOut(myId);

                if (stillMyTurn && !sittingOut)
                {
                    Debug.Log("[TurnManager] Own timer expired. Auto fold.");

                    if (TableJoinHandler.Instance != null)
                        TableJoinHandler.Instance.Fold();
                }
                else
                {
                    Debug.Log(
                        "[TurnManager] Timer expired, but it is no longer our turn."
                    );
                }
            }

            StopTimerOnly();
        }

        // -------------------------------------------------
        // Clear timer when hand/board changes
        // -------------------------------------------------

        public void ClearTimerForNewHand()
        {
            Debug.Log("[TurnManager] Clearing timer for new hand.");
            ClearServerTimer();
            _isMyTurn = false;
            autoActionSent = false;
            myTurnDeadlineMs = 0;

            if (ActionButtons != null)
                ActionButtons.SetInteractable(false);

            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.HideAllThinking();
        }

        public void ClearTimerForNewBoardCards()
        {
            Debug.Log("[TurnManager] Clearing timer for new board cards.");
            ClearServerTimer();

            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.HideAllThinking();
        }

        private void ClearServerTimer()
        {
            timerStartReceived = false;
            timerRunning = false;
            turnStartedForPlayerId = null;
            turnEndsAt = 0;
            lastServerRemainingMs = 0;
            lastWarnedSecond = -1;
            autoActionSent = false;
        }

        private void StopTimerOnly()
        {
            timerRunning = false;
            timerStartReceived = false;
            turnStartedForPlayerId = null;
            turnEndsAt = 0;
        }

        public void EndTurn()
        {
            Debug.Log("[TurnManager] EndTurn");

            _isMyTurn = false;
            ClearServerTimer();
            myTurnDeadlineMs = 0;

            if (ActionButtons != null)
                ActionButtons.SetInteractable(false);

            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.ResetTurnTimer();
        }

        public void ClearMyTurnState()
        {
            _isMyTurn = false;
            autoActionSent = false;
            resumingAfterReconnect = true;

            if (ActionButtons != null)
                ActionButtons.SetInteractable(false);

            // Do not clear the server timer here.
            // A reconnect must retain the timer_start information.
        }

        public void DisableAllActions()
        {
            if (ActionButtons != null)
                ActionButtons.SetInteractable(false);
        }

        public void EnableAllActions()
        {
            if (ActionButtons != null)
                ActionButtons.SetInteractable(true);
        }
    }
}
