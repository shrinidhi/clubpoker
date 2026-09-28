using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    /// <summary>
    /// What the buy-in popup needs to open. A plain payload so this assembly can
    /// ask for the popup without referencing ClubPoker.UI, which already references
    /// this one.
    /// </summary>
    public struct BuyInPromptRequest
    {
        public string TableId;
        public int MinBuyIn;
        public int MaxBuyIn;
        public int SmallBlind;
        public int BigBlind;
        /// Runs on confirm. Throwing keeps the popup open with the error shown.
        public Func<int, UniTask> OnConfirm;
    }

    /// <summary>
    /// Taking a seat from the table screen, as a spectator tapping "+".
    ///
    /// The seat is not ours to choose. /join takes a buy-in and nothing else — the
    /// server picks the seat — so the seat number the player tapped is only what
    /// started the flow, never where they land. Nothing here pretends otherwise:
    /// the seat comes back in the next game:state_update and the table redraws.
    ///
    /// Same order as the other entry paths (JoinFriendTable), so a mid-hand join
    /// and a failed buy-in behave the same wherever they're started from.
    /// </summary>
    public static class TakeSeatFlow
    {
        /// <summary>
        /// Set by the UI layer (BuyInPromptBinder) at scene start. Null means no
        /// popup is wired into this scene, and a "+" tap can only be refused.
        /// </summary>
        public static Action<BuyInPromptRequest> ShowBuyInPrompt;

        // One claim at a time. Two buy-ins would both succeed and the second seat
        // would be paid for and never taken.
        private static bool _inFlight;

        public static bool IsInFlight => _inFlight;

        /// <summary>Raised when a claim starts and ends, so the "+" buttons can grey out.</summary>
        public static event Action<bool> OnInFlightChanged;

        public static async UniTask ClaimSeatAsync(string tableId)
        {
            if (_inFlight)
                return;

            if (string.IsNullOrEmpty(tableId))
            {
                Debug.LogError("[TakeSeat] No tableId — cannot claim.");
                return;
            }

            SetInFlight(true);

            try
            {
                // A hand already running has no room for a new stack until it ends,
                // and the server won't seat us — so this has to be checked before
                // charging the buy-in.
                //
                // Not over REST though. We are sitting in the table watching its
                // state_update stream: the current gameState is already on screen,
                // fresher than anything /active could tell us, and a round trip
                // here is a round trip the player waits through.
                if (IsHandRunning())
                {
                    ToastEvents.Show(GameMessages.TakeSeatHandInProgress);
                    return;
                }

                // Same reasoning for the limits: whichever screen started this table
                // already fetched them. Only fall back to the network when it didn't
                // — a spectator deep-linked in with no context of its own.
                BuyInLimits limits = LimitsFromContext();

                if (!limits.Valid)
                {
                    TableData table = await AuthManager.Instance.GetTableDetailAsync(tableId);

                    if (table == null)
                    {
                        ToastEvents.Show(GameMessages.TakeSeatFailed(GameMessages.TableUnreachable));
                        return;
                    }

                    limits = new BuyInLimits
                    {
                        Min        = table.MinBuyIn,
                        Max        = table.MaxBuyIn,
                        SmallBlind = table.SmallBlind,
                        BigBlind   = table.BigBlind
                    };
                }

                // One tap, no dialog: buy back in for the stack we left with. The
                // player already chose that amount when they sat down, and being
                // asked again on the way back is a question with an obvious answer.
                int autoAmount = AutoBuyInAmount(limits);

                if (autoAmount > 0)
                {
                    await ConfirmAsync(tableId, autoAmount);
                    return;
                }

                // Never been seated here — a pure observer has no amount to repeat,
                // so this is the one case that still has to ask. The popup owns
                // validation, the loading state and the error text, and stays open
                // on failure so the amount can be corrected.
                if (ShowBuyInPrompt == null)
                {
                    Debug.LogError("[TakeSeat] No buy-in popup bound — put a BuyInPromptBinder in the scene.");
                    ToastEvents.Show(GameMessages.TakeSeatFailed(GameMessages.SomethingWentWrong));
                    return;
                }

                ShowBuyInPrompt(new BuyInPromptRequest
                {
                    TableId    = tableId,
                    MinBuyIn   = limits.Min,
                    MaxBuyIn   = limits.Max,
                    SmallBlind = limits.SmallBlind,
                    BigBlind   = limits.BigBlind,
                    OnConfirm  = amount => ConfirmAsync(tableId, amount)
                });
            }
            catch (Exception e)
            {
                // On the auto path there is no popup to put an error in, so the
                // reason has to reach the player as a toast — "not enough chips"
                // is the one they'll actually hit, and it needs saying.
                Debug.LogError($"[TakeSeat] Claim failed: {e}");

                ToastEvents.Show(GameMessages.TakeSeatFailed(
                    string.IsNullOrEmpty(e.Message) ? GameMessages.SomethingWentWrong : e.Message));
            }
            finally
            {
                // Cleared here, not after the popup closes: from this point the
                // popup is the thing blocking a second claim.
                SetInFlight(false);
            }
        }

        private struct BuyInLimits
        {
            public int Min;
            public int Max;
            public int SmallBlind;
            public int BigBlind;

            /// A max of zero means nobody filled these in — go and fetch them.
            public bool Valid => Max > 0;
        }

        /// <summary>
        /// Limits from whichever screen opened this table, already in memory.
        /// Invalid when the table was entered without them.
        /// </summary>
        private static BuyInLimits LimitsFromContext()
        {
            TableInfo info = TableContext.Info;

            if (info == null)
                return default;

            return new BuyInLimits
            {
                Min        = info.BuyInMin,
                Max        = info.BuyInMax,
                SmallBlind = info.SmallBlind,
                BigBlind   = info.BigBlind
            };
        }

        /// <summary>
        /// Whether a hand is running right now, read from the snapshot we're already
        /// being sent. WAITING and ROUND_END are the two gaps between hands; anything
        /// else (PRE_FLOP through SHOWDOWN) means cards are live.
        ///
        /// No state at all is treated as "running": refusing to seat is recoverable,
        /// charging a buy-in the server then rejects is not.
        /// </summary>
        private static bool IsHandRunning()
        {
            string gameState = GameStateManager.Instance != null
                ? GameStateManager.Instance.CurrentState?.GameState
                : null;

            if (string.IsNullOrEmpty(gameState))
                return true;

            return gameState != "WAITING" && gameState != "ROUND_END";
        }

        /// <summary>
        /// The stack we left the seat with, clamped to what this table accepts —
        /// we may have doubled up past the table maximum, or been ground below its
        /// minimum, and neither is a buy-in the server will take.
        ///
        /// Zero when there is nothing to repeat, which means ask instead.
        /// </summary>
        private static int AutoBuyInAmount(BuyInLimits limits)
        {
            int last = TableJoinHandler.Instance != null
                ? TableJoinHandler.Instance.LastSeatStack
                : 0;

            if (last <= 0)
                return 0;

            if (limits.Min > 0 && last < limits.Min)
                last = limits.Min;

            if (limits.Max > 0 && last > limits.Max)
                last = limits.Max;

            return last;
        }

        /// <summary>
        /// Hands off to the seat conversion the watch &amp; wait queue and the club
        /// flow already use: buy in, take the seat, and re-join in place.
        ///
        /// It must be this and not a plain JoinTable — that path reloads GameTable,
        /// and we are already standing in it. It also sources club seats from club
        /// chips, which a bare buy-in here would not.
        ///
        /// Throws on failure, which the popup shows as its error and the auto path
        /// turns into a toast.
        /// </summary>
        private static UniTask ConfirmAsync(string tableId, int amount)
        {
            if (TableJoinHandler.Instance == null)
                throw new Exception("Table join handler missing");

            return TableJoinHandler.Instance.TakeSeatAsync(tableId, amount);
        }

        private static void SetInFlight(bool value)
        {
            _inFlight = value;
            OnInFlightChanged?.Invoke(value);
        }
    }
}
