using ClubPoker.Networking.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance;

        [Header("Timer UI")]
        public Text TimerText;

        [Header("Time Bank UI")]
        public Text TimeBankText;

        [Header("Action Buttons")]
        public ActionButtonHandler ActionButtons;

        [Header("Time Bank Button")]
        public TimeBankButtonHandler TimeBankButton;

        private bool _isMyTurn = false;

        // Server authoritative timer sync
        private long serverClockOffsetMs = 0;
        private long lastServerRemainingMs = 0;
        private long lastServerTimestampMs = 0;
        private bool timerRunning = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            EndTurn();
        }

        private void Update()
        {
            if (!timerRunning || !_isMyTurn)
                return;

            UpdateServerAuthoritativeTimer();
        }

        public void StartYourTurn(YourTurnPayload payload)
        {
            if (payload == null)
                return;

            Debug.Log("[TurnManager] StartYourTurn");

            _isMyTurn = true;

            if (ActionButtons != null)
            {
                ActionButtons.EnableActions(
                    payload.ValidActions,
                    payload.CanCheck
                );
            }

            if (TimeBankText != null)
            {
                TimeBankText.text =
                    "Time Bank : " + (payload.TimeAllowedMs / 1000) + " sec";
            }

            if (TimeBankButton != null)
            {
                TimeBankButton.OnYourTurnStart(true);
            }

            // Initial fallback value before first timer_tick
            lastServerRemainingMs = payload.TimeAllowedMs;
            lastServerTimestampMs = GetLocalUnixTimeMs();
            timerRunning = true;
        }

        public void ApplyTimerTick(long remainingMs, long serverTime)
        {
            long localNow = GetLocalUnixTimeMs();

            // Calculate clock offset
            serverClockOffsetMs = serverTime - localNow;

            // Save authoritative values
            lastServerRemainingMs = remainingMs;
            lastServerTimestampMs = serverTime;

            timerRunning = true;

            Debug.Log(
                $"[TimerSync] remaining={remainingMs} " +
                $"serverTime={serverTime} " +
                $"offset={serverClockOffsetMs}"
            );
        }

        private void UpdateServerAuthoritativeTimer()
        {
            long localNow = GetLocalUnixTimeMs();

            // Convert local → estimated server time
            long estimatedServerNow = localNow + serverClockOffsetMs;

            // Time passed since last server tick
            long elapsed = estimatedServerNow - lastServerTimestampMs;

            // True remaining time from server perspective
            long correctedRemainingMs =
                Mathf.Max(0, (int)(lastServerRemainingMs - elapsed));

            int totalSeconds = Mathf.CeilToInt(correctedRemainingMs / 1000f);

            int min = totalSeconds / 60;
            int sec = totalSeconds % 60;

            if (TimerText != null)
            {
                TimerText.text =
                    min.ToString("00") + ":" + sec.ToString("00");
            }

            if (correctedRemainingMs <= 0)
            {
                EndTurn();
            }
        }

        public void EndTurn()
        {
            Debug.Log("[TurnManager] EndTurn");

            _isMyTurn = false;
            timerRunning = false;

            if (ActionButtons != null)
            {
                ActionButtons.SetInteractable(false);
            }

            if (TimerText != null)
            {
                TimerText.text = "00:00";
            }
        }

        private long GetLocalUnixTimeMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }


        public void StartPlayerTimer(
    string playerId,
    long durationMs,
    long serverTime
)
        {
            Debug.Log(
                $"[TimerStart] Player={playerId} " +
                $"Duration={durationMs}ms " +
                $"ServerTime={serverTime}"
            );

            // Previous timer clear
            timerRunning = false;

            // Optional:
            // previous player timer ring hide here

            // Calculate clock offset using server time
            long localNow = GetLocalUnixTimeMs();
            serverClockOffsetMs = serverTime - localNow;

            // Save authoritative timer values
            lastServerRemainingMs = durationMs;
            lastServerTimestampMs = serverTime;

            timerRunning = true;

            // Check if this is MY turn
            string myPlayerId = GetMyPlayerId();

            if (playerId == myPlayerId)
            {
                _isMyTurn = true;

                if (ActionButtons != null)
                {
                    ActionButtons.SetInteractable(true);
                }

                if (TimeBankButton != null)
                {
                    TimeBankButton.OnYourTurnStart(true);
                }
            }
            else
            {
                _isMyTurn = false;

                if (ActionButtons != null)
                {
                    ActionButtons.SetInteractable(false);
                }
            }

            // Timer ring show on correct player panel
            ShowTimerRing(playerId);

            Debug.Log("[TimerStart] Timer started successfully");
        }

        private void ShowTimerRing(string playerId)
        {
            Debug.Log($"[TimerRing] Show timer ring for Player: {playerId}");
        }

        private string GetMyPlayerId()
        {
            var mgr = Auth.AuthManager.Instance;

            return mgr != null
                ? mgr.Session.Id ?? string.Empty
                : string.Empty;
        }




        public void DisableAllActions()
        {
            Debug.Log("[TurnManager] Actions Disabled");
            ActionButtons.SetInteractable(false);

        }

        public void EnableAllActions()
        {
            Debug.Log("[TurnManager] Actions Enabled");
            ActionButtons.SetInteractable(true);

        }
    }
}