using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Core;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class PokerTableUI : MonoBehaviour
    {
        public static PokerTableUI Instance { get; private set; }
        public Text Hand_Name;
        public GameObject HandNameTextBG;
        public TextMeshProUGUI Variant_Name;
        [Header("Main Pot UI")]
        public TextMeshProUGUI mainPotText;
        public GameObject mainPotBG;

        [Header("Rake UI")]
        public Text rakeText;
        public GameObject rakePanel;

        [Header("Side Pot UI")]
        public Transform sidePotContainer;
        public GameObject sidePotLabelPrefab;

        [Header("Dealer Button UI")]
        public RectTransform dealerButtonToken;

        [Header("Blind Indicators")]
        public RectTransform smallBlindIndicator;
        public RectTransform bigBlindIndicator;

        [Header("Player Count UI")]
        public Text playerCountText;

        [Header("Player Seat Prefab")]
        public PlayerProfile playerSeatPrefab;

        [Header("2 Player Slots")]
        public List<Transform> slots2Player = new List<Transform>();

        [Header("3 Player Slots")]
        public List<Transform> slots3Player = new List<Transform>();

        [Header("4 Player Slots")]
        public List<Transform> slots4Player = new List<Transform>();

        [Header("5 Player Slots")]
        public List<Transform> slots5Player = new List<Transform>();

        [Header("6 Player Slots")]
        public List<Transform> slots6Player = new List<Transform>();

        [Header("7 Player Slots")]
        public List<Transform> slots7Player = new List<Transform>();

        [Header("8 Player Slots")]
        public List<Transform> slots8Player = new List<Transform>();

        [Header("9 Player Slots")]
        public List<Transform> slots9Player = new List<Transform>();

        [Header("Join / Leave Animation")]
        public float joinLeaveAnimationDuration = 0.25f;

        [Header("Pause Overlay")]
        public GameObject pauseOverlay;
        public Text pauseReasonText;
        public Text pauseCountdownText;

        [Header("Waiting for Players Overlay")]
        public GameObject waitingForPlayersOverlay;

        [Header("Spectator")]
        public GameObject spectatorLabel;   // "Spectator" badge shown while watching

        private Coroutine pauseCountdownRoutine;

        private readonly List<GameObject> spawnedSidePots = new List<GameObject>();
        private readonly List<PlayerProfile> spawnedSeats = new List<PlayerProfile>();
        private readonly Dictionary<int, PlayerProfile> seatViews = new Dictionary<int, PlayerProfile>();
        private List<Transform> currentSlots = new List<Transform>();

        // game:state_update omits maxPlayers; fetched from the table detail on
        // entry so the seat layout uses the real table size (absolute seats fit).
        private int _tableMaxPlayers;
        private int _lastRenderedMaxPlayers = -1;

        private List<string> pendingMyCards;
        private bool tableRendered;

        [Header("Game Status Text")]
        public TextMeshProUGUI gameStatusText;

        [Header("Winner UI")]
        public GameObject winnerPanel;
        public TextMeshProUGUI winnerText;

        [Header("PLOTooltip")]
        public GameObject PLOTooltipPanel;
        public TextMeshProUGUI PLOTooltipTitle;
        public TextMeshProUGUI PLOTooltipText;

        private string activeThinkingPlayerId;
        private string currentTimerPlayerId;
        private int currentTimerRound = -1;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
        private void OnEnable()
        {
           // GameEvents.OnPlayerThinking += ShowPlayerThinking;
        }

        private void Start()
        {
            // Reflect current role on entry (Watch & Wait sets it before this scene loads).
            if (TableJoinHandler.Instance != null)
                SetSpectatorMode(TableJoinHandler.Instance.IsSpectator);

            InitTable().Forget();
        }

        // game:state_update omits maxPlayers, so pull the real table size from the
        // detail endpoint FIRST (so the seat layout is right from the first render),
        // then request a full state — the join-confirmation state_update is consumed
        // for scene loading in TableJoinHandler, so we re-request to render everyone.
        private async UniTaskVoid InitTable()
        {
            await FetchTableMaxPlayers();

            await UniTask.Delay(500);

            if (StateSyncHandler.Instance != null)
                StateSyncHandler.Instance.RequestState();
        }

        private async UniTask FetchTableMaxPlayers()
        {
            string tableId = SocketManager.Instance != null
                ? SocketManager.Instance.CurrentTableId
                : null;

            if (string.IsNullOrEmpty(tableId))
                return;

            var detail = await Auth.AuthManager.Instance.GetTableDetailAsync(tableId);
            if (detail != null && detail.MaxPlayers > 0)
                _tableMaxPlayers = detail.MaxPlayers;
        }

        private void OnDisable()
        {
          //  GameEvents.OnPlayerThinking -= ShowPlayerThinking;
        }
        // ------------------------------------------------------
        // FULL TABLE RENDER
        // ------------------------------------------------------
        public void SetGameStatus(string text)
        {
            if (gameStatusText != null)
                gameStatusText.text = text;
        }

        // Toggle the "Spectator" badge when watching vs seated.
        public void SetSpectatorMode(bool isSpectator)
        {
            if (spectatorLabel != null)
                spectatorLabel.SetActive(isSpectator);
        }

        public void ShowGameOver()
        {
            if (winnerPanel == null || winnerText == null) return;
            winnerText.text = $"<color=#FF4444>GAME OVER</color>";
            winnerPanel.SetActive(true);

            if (SocketManager.Instance != null)
                SocketManager.Instance.Disconnect();

            if (UnityBotRunner.Instance != null)
                UnityBotRunner.Instance.StopBots();
        }

        public void ShowWinner(string username, int potWon, string handName = null)
        {
            if (winnerPanel == null || winnerText == null) return;

            string hand = !string.IsNullOrEmpty(handName) ? $"  <color=#AAAAAA>({handName})</color>" : "";
            winnerText.text = $"<color=#8CCCF9>WINNER</color>  <color=#FFD700>{username}</color>  <color=#FFFFFF>{potWon}</color>{hand}";
            winnerPanel.SetActive(true);

            StartCoroutine(HideWinnerAfterDelay(3f));
        }

        private IEnumerator HideWinnerAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (winnerPanel != null)
                winnerPanel.SetActive(false);
        }
        public void RenderFullTable(GameStateUpdatePayload state)
        {
            if (state == null || state.Players == null)
                return;

            // Don't render with a guessed (count-based) layout before the real table
            // size is known — otherwise a wrong-size layout flashes, then rebuilds when
            // maxPlayers (from the detail fetch) arrives. Wait for the real size.
            if (state.MaxPlayer <= 0 && _tableMaxPlayers <= 0)
                return;

            int maxPlayers = GetMaxPlayersFromState(state);

            // If the layout size changed (e.g. the real maxPlayers arrived after a
            // count-based first render), clear and rebuild so seats re-parent to the
            // new slot-set instead of overlapping the old positions.
            if (maxPlayers != _lastRenderedMaxPlayers)
            {
                ClearSeatPrefabs();
                _lastRenderedMaxPlayers = maxPlayers;
            }

            currentSlots = GetSlotsByMaxPlayers(maxPlayers);

            string myPlayerId = Auth.AuthManager.Instance.Session.Id;
            int holeCount = HoleCardCount(state.Variant);

            foreach (var player in state.Players)
            {
                int seat = player.Seat;

                if (seat < 0 || seat >= currentSlots.Count)
                    continue;

                PlayerProfile view;

                if (seatViews.TryGetValue(seat, out view))
                {
                    view.Bind(player); // update only
                }
                else
                {
                    view = Instantiate(playerSeatPrefab, currentSlots[seat]);

                    view.transform.localPosition = Vector3.zero;
                    view.transform.localRotation = Quaternion.identity;
                    view.transform.localScale = Vector3.one;

                    view.Bind(player);

                    spawnedSeats.Add(view);
                    seatViews[seat] = view;
                }

                // Show card backs for OTHER players that have been dealt in — driven
                // by cardsDealt so spectators (no your_cards) still see opponents'
                // holdings. The local player's own cards come via game:your_cards.
                if (player.Id != myPlayerId && player.CardsDealt && holeCount > 0)
                    view.ShowCardBacks(holeCount);
            }

            // Remove views for seats no longer present (player left or changed seat)
            // so departed players don't linger and the same name can't show twice.
            var incomingSeats = new HashSet<int>();
            foreach (var player in state.Players)
                incomingSeats.Add(player.Seat);

            var staleSeats = new List<int>();
            foreach (var seat in seatViews.Keys)
                if (!incomingSeats.Contains(seat))
                    staleSeats.Add(seat);

            foreach (var seat in staleSeats)
            {
                if (seatViews[seat] != null)
                {
                    spawnedSeats.Remove(seatViews[seat]);
                    Destroy(seatViews[seat].gameObject);
                }
                seatViews.Remove(seat);
            }

            tableRendered = true;

            // Show the "waiting for another player" overlay until enough players are
            // seated to start a hand. Count-based, not gameState — you can be alone at
            // ROUND_END too. Hide the round status while waiting so stale "Round N:STATE"
            // doesn't show before the game starts.
            bool waitingForPlayers = state.Players.Count < 2;

            if (waitingForPlayersOverlay != null)
                waitingForPlayersOverlay.SetActive(waitingForPlayers);

            if (gameStatusText != null)
                gameStatusText.gameObject.SetActive(!waitingForPlayers);

            // Keep the spectator badge in sync with the current role every render —
            // covers join, spectator→seat conversion, stand-up, and game start.
            if (TableJoinHandler.Instance != null)
                SetSpectatorMode(TableJoinHandler.Instance.IsSpectator);

            UpdatePlayerCountUI(state.Players.Count, maxPlayers);

            if (pendingMyCards != null && pendingMyCards.Count > 0)
            {
                ShowMyPrivateCards(pendingMyCards);
            }


            Variant_Name.text = VariantUtils.ToDisplayName(state.Variant);
        }

      

        private int HoleCardCount(string variant)
        {
            switch (variant)
            {
                case "texas_holdem": return 2;
                case "omaha":
                case "plo4":         return 4;
                case "plo6":
                case "omaha_six":    return 6;
                default:             return 2;
            }
        }

        private int GetMaxPlayersFromState(GameStateUpdatePayload state)
        {
            if (state.MaxPlayer > 0)
                return state.MaxPlayer;

            // Real table size from the detail fetch — keeps absolute seats in range.
            if (_tableMaxPlayers > 0)
                return _tableMaxPlayers;

            int count = state.Players != null ? state.Players.Count : 4;

            if (count <= 2) return 2;
            if (count == 3) return 3;
            if (count == 4) return 4;
            if (count == 5) return 5;
            if (count <= 6) return 6;
            if (count <= 7) return 7;
            if (count <= 8) return 8;

            return 9;
        }

        private List<Transform> GetSlotsByMaxPlayers(int maxPlayers)
        {
            switch (maxPlayers)
            {
                case 2: return slots2Player;
                case 3: return slots3Player;
                case 4: return slots4Player;
                case 5: return slots5Player;
                case 6: return slots6Player;
                case 7: return slots7Player;
                case 8: return slots8Player;
                case 9: return slots9Player;
                default:
                    Debug.LogWarning($"[PokerTableUI] Unsupported maxPlayers {maxPlayers}, fallback 4");
                    return slots4Player;
            }
        }

        private void ClearSeatPrefabs()
        {
            foreach (var seat in spawnedSeats)
            {
                if (seat != null)
                    Destroy(seat.gameObject);
            }

            spawnedSeats.Clear();
            seatViews.Clear();
        }

        private void UpdatePlayerCountUI(int current, int max)
        {
            if (playerCountText != null)
                playerCountText.text = $"Players: {current}/{max}";
        }

      
        public void ShowPlayerJoinAnimation(int seat)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view) && view != null)
            {
                view.gameObject.SetActive(true);
                StopCoroutine(nameof(AnimateJoin));
                StartCoroutine(AnimateJoin(view.gameObject));
                Debug.Log($"[PokerTableUI] Player Join Animation -> Seat {seat}");
            }
        }

        private IEnumerator AnimateJoin(GameObject target)
        {
            if (target == null)
                yield break;

            float timer = 0f;
            target.transform.localScale = Vector3.zero;

            while (timer < joinLeaveAnimationDuration)
            {
                timer += Time.deltaTime;
                float t = timer / joinLeaveAnimationDuration;
                target.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }

            target.transform.localScale = Vector3.one;
        }

        public void ShowPlayerLeaveAnimation(int seat)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view) && view != null)
            {
                StartCoroutine(AnimateLeave(view.gameObject, seat));
                Debug.Log($"[PokerTableUI] Player Leave Animation -> Seat {seat}");
            }
        }

        private IEnumerator AnimateLeave(GameObject target, int seat)
        {
            if (target == null)
                yield break;

            float timer = 0f;
            Vector3 startScale = target.transform.localScale;

            while (timer < joinLeaveAnimationDuration)
            {
                timer += Time.deltaTime;
                float t = timer / joinLeaveAnimationDuration;
                target.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            seatViews.Remove(seat);
            spawnedSeats.RemoveAll(x => x == null || x.gameObject == target);

            Destroy(target);

            UpdatePlayerCount();
            RefreshSeatAvailability();

            Debug.Log($"[PokerTableUI] Player removed from Seat {seat}");
        }

        public void UpdatePlayerCount()
        {
            if (playerCountText != null)
                playerCountText.text = $"Players: {seatViews.Count}";

            Debug.Log($"[PokerTableUI] Player Count Updated -> {seatViews.Count}");
        }

        public void RefreshSeatAvailability()
        {
            Debug.Log("[PokerTableUI] Seat availability refreshed by prefab system");
        }

        // ------------------------------------------------------
        // PLAYER PREFAB STATE UPDATE
        // ------------------------------------------------------

        public void UpdateSeatAction(int seat, string action)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view))
                view.UpdateAction(action);
        }

        public void UpdateSeatChips(int seat, int chips)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view))
                view.UpdateChips(chips);
        }

        public void ShowDisconnectedIndicator(int seat, int gracePeriodSeconds)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view))
                view.ShowDisconnected(gracePeriodSeconds);
        }

        public void HideDisconnectedIndicator(int seat)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view))
                view.HideDisconnected();
        }

        public void ShowSittingOutState(int seat)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view))
                view.ShowSittingOut();
        }

        public void HideSittingOutState(int seat)
        {
            if (seatViews.TryGetValue(seat, out PlayerProfile view))
                view.HideSittingOut();
        }

        // ------------------------------------------------------
        // POT UPDATE
        // ------------------------------------------------------

        public void AnimateChipsToPot()
        {
            Debug.Log("[PokerTableUI] Chip animation -> Player -> Pot");
        }

        public void UpdateMainPot(int potAmount)
        {
            bool hasPot = potAmount > 0;
            if (mainPotBG != null) mainPotBG.SetActive(hasPot);
            if (mainPotText != null)
                mainPotText.text = hasPot ? $"<color=#8CCCF9>POT</color> <color=#FFFFFF>{potAmount}</color>" : "";

            Debug.Log($"[PokerTableUI] Main Pot Updated -> {potAmount}");
        }

        public void ShowSidePots(List<SidePots> sidePots)
        {
            HideSidePots();

            if (sidePots == null || sidePots.Count == 0)
                return;

            for (int i = 0; i < sidePots.Count; i++)
            {
                GameObject obj = Instantiate(sidePotLabelPrefab, sidePotContainer);
                Text txt = obj.GetComponent<Text>();

                if (txt != null)
                    txt.text = $"Side Pot {i + 1}: {sidePots[i].amount}";

                spawnedSidePots.Add(obj);
            }

            Debug.Log($"[PokerTableUI] Side Pots Shown -> {sidePots.Count}");
        }

        public void HideSidePots()
        {
            foreach (var item in spawnedSidePots)
            {
                if (item != null)
                    Destroy(item);
            }

            spawnedSidePots.Clear();
        }

        public void ShowRake(int rake)
        {
            if (rakePanel != null)
                rakePanel.SetActive(true);

            if (rakeText != null)
                rakeText.text = $"Rake: {rake}";
        }

        public void HideRake()
        {
            if (rakePanel != null)
                rakePanel.SetActive(false);
        }

        // ------------------------------------------------------
        // DEALER / BLINDS
        // ------------------------------------------------------

        public void MoveDealerButton(int dealerSeat)
        {
            if (dealerButtonToken == null)
                return;

            Transform slot = GetSlotTransform(dealerSeat);

            if (slot == null)
            {
                Debug.LogWarning($"[PokerTableUI] Dealer slot missing: {dealerSeat}");
                return;
            }

            dealerButtonToken.position = slot.position;
            Debug.Log($"[PokerTableUI] Dealer Button moved -> Seat {dealerSeat}");
        }
        private int _lastSmallBlindSeat = -1;
        private int _lastBigBlindSeat = -1;

        public void UpdateBlindIndicators(int smallBlindSeat, int bigBlindSeat)
        {
            _lastSmallBlindSeat = smallBlindSeat;
            _lastBigBlindSeat = bigBlindSeat;

            ReapplyBlindIndicators();
        }

        public void ReapplyBlindIndicators()
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                profile.HideSmallBlind();
                profile.HideBigBlind();

                if (profile.seatIndex == _lastSmallBlindSeat)
                    profile.ShowSmallBlind();

                if (profile.seatIndex == _lastBigBlindSeat)
                    profile.ShowBigBlind();
            }

            Debug.Log($"[PokerTableUI] Blinds Updated -> SB: {_lastSmallBlindSeat}, BB: {_lastBigBlindSeat}");
        }

        public void HandlePreFlopFirstActor(int firstActorSeat)
        {
            Debug.Log($"[PokerTableUI] PreFlop First Actor -> Seat {firstActorSeat}");
        }

        private Transform GetSlotTransform(int seat)
        {
            if (currentSlots != null && seat >= 0 && seat < currentSlots.Count)
                return currentSlots[seat];

            return null;
        }

        // ------------------------------------------------------
        // ROUND END
        // ------------------------------------------------------

        public void AnimatePotToWinner(string playerId, int potAmount)
        {
            Debug.Log($"[PokerTableUI] Pot -> Winner | Player: {playerId}, Amount: {potAmount}");
           
        }

        public void AnimateSplitPotToWinners(Dictionary<string, int> winners, int totalPot)
        {
            Debug.Log($"[PokerTableUI] Split Pot | Total: {totalPot}");
        }

       

        public void ShowHandRank(string playerId, string handRank)
        {
            Debug.Log($"[PokerTableUI] Hand Rank | {playerId} -> {handRank}");
        }

        public void UpdateAllPlayerChips(Dictionary<string, int> balances)
        {
            if (balances == null)
                return;

            Debug.Log("[PokerTableUI] Updating player chips");

            foreach (var item in balances)
            {
                Debug.Log($"Player: {item.Key} -> Chips: {item.Value}");
            }
        }

        // ------------------------------------------------------
        // PAUSE
        // ------------------------------------------------------

        public void ShowPauseOverlay(string reason, int countdownSeconds)
        {
            if (pauseOverlay != null)
                pauseOverlay.SetActive(true);

            if (pauseReasonText != null)
                pauseReasonText.text = GetReadableReason(reason);

            if (pauseCountdownRoutine != null)
                StopCoroutine(pauseCountdownRoutine);

            pauseCountdownRoutine = StartCoroutine(PauseCountdown(countdownSeconds));
        }

        public void HidePauseOverlay()
        {
            if (pauseOverlay != null)
                pauseOverlay.SetActive(false);

            if (pauseCountdownRoutine != null)
            {
                StopCoroutine(pauseCountdownRoutine);
                pauseCountdownRoutine = null;
            }
        }

        private IEnumerator PauseCountdown(int seconds)
        {
            int remaining = seconds;

            while (remaining >= 0)
            {
                if (pauseCountdownText != null)
                    pauseCountdownText.text = $"Resuming in {remaining}s";

                yield return new WaitForSeconds(1f);
                remaining--;
            }
        }

        private string GetReadableReason(string reason)
        {
            switch (reason)
            {
                case "waiting_for_players":
                    return "Waiting for players...";
                case "min_players":
                    return "Minimum players required";
                case "chip_conservation":
                    return "Paused for chip conservation";
                case "admin":
                    return "Game paused by admin";
                default:
                    return "Game paused";
            }
        }



        private bool hasShownCards = false;

        public void ShowMyPrivateCards(List<string> cards)
        {
            if (cards == null || cards.Count == 0)
                return;

            pendingMyCards = new List<string>(cards);

           
            if (!tableRendered || seatViews.Count == 0)
            {
                Debug.Log("[PokerTableUI] Cards saved, waiting for player seats render");
                return;
            }

            string myPlayerId = Auth.AuthManager.Instance.Session.Id;

            foreach (var pair in seatViews)
            {
                PlayerProfile view = pair.Value;

                if (view == null)
                    continue;

                if (view.CurrentPlayerId == myPlayerId)
                    view.ShowPrivateCards(cards); 
                else
                    view.ShowCardBacks(cards.Count);
            }
        }


        public void ResetCardsForNewRound()
        {
            // Do NOT hide cards at round-end. The finished hand stays on screen:
            //   - local player  → own cards face-up
            //   - winner (if opponent) → flipped face-up
            //   - losing opponents → card backs (face-down, still shown)
            // The next hand refreshes every seat: opponents get fresh card backs
            // (ShowCardBacks when cardsDealt) and the local player gets new cards
            // via game:your_cards, which clears the previous winner reveal.
            pendingMyCards = null;

            // Clear last round's best-hand highlight. This runs before the current
            // round's HighlightWinnerCardsDelayed (0.6s delay), so the new highlight
            // still shows; it's the previous round's highlight being cleared here.
            foreach (var pair in seatViews)
            {
                if (pair.Value != null)
                    pair.Value.ClearPrivateCardHighlights();
            }
        }


        public void ShowPlayerThinking(string playerId)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                if (profile.CurrentPlayerId == playerId)
                    profile.ShowThinking();
                else
                    profile.HideThinking();
            }
        }

        public void HideAllThinking()
        {
            foreach (var seat in seatViews)
            {
                if (seat.Value != null)
                    seat.Value.HideThinking();
            }
        }



        public void UpdateDealerButton(int dealerSeat)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                
                if (profile.seatIndex == dealerSeat)
                    profile.ShowDealer();
                else
                    profile.HideDealer();
            }
        }


        public void ShowThinkingAndTimer(string playerId, float durationSeconds, int roundNumber)
        {

            if (currentTimerPlayerId == playerId && currentTimerRound == roundNumber)
                return;

            currentTimerPlayerId = playerId;
            currentTimerRound = roundNumber;


            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                if (profile.CurrentPlayerId == playerId)
                {
                    profile.ShowThinking();
                    profile.StartTimer(durationSeconds);
                }
                else
                {
                    profile.HideThinking();
                    profile.StopTimer();
                }
            }
        }

        public void HideAllThinkingAndTimers()
        {
            foreach (var seat in seatViews)
            {
                if (seat.Value != null)
                {
                    seat.Value.HideThinking();
                    seat.Value.StopTimer();
                }
            }
        }

        public void ResetTurnTimer()
        {
           
            currentTimerPlayerId = "";
            currentTimerRound = -1;
            HideAllThinkingAndTimers();
        }


        public void ClearAllPlayerActions()
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;
                if (mainPotText != null) mainPotText.text = "";
                if (mainPotBG != null) mainPotBG.SetActive(false);
                profile.UpdateAction("");
            }
        }


        public void PlayCoinToPot(string playerId, int amount)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                if (profile.CurrentPlayerId == playerId)
                {
                    RectTransform from = profile.transform as RectTransform;

                    if (CoinTransactionAnimation.Instance != null)
                        CoinTransactionAnimation.Instance.PlayToPot(from, amount);

                    return;
                }
            }
        }

        public void PlayPotToWinner(string winnerPlayerId)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                if (profile.CurrentPlayerId == winnerPlayerId)
                {
                    RectTransform winner = profile.transform as RectTransform;

                    if (CoinTransactionAnimation.Instance != null)
                        CoinTransactionAnimation.Instance.MovePotToWinner(winner);
                    
                    return;
                }
            }
        }


        public void AnimateWinnerChips(string winnerPlayerId, int finalChips)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                if (profile.CurrentPlayerId == winnerPlayerId)
                {
                  //  profile.AnimateChipsTo(finalChips, 0.8f);
                    return;
                }
            }
        }

        public IEnumerator PlayPotToWinnerAndUpdateChips(string winnerPlayerId, int finalChips)
        {
            PlayPotToWinner(winnerPlayerId);

            if (CoinTransactionAnimation.Instance != null)
            {
                yield return new WaitForSeconds(
                    CoinTransactionAnimation.Instance.moveToWinnerDuration + 0.9f
                );
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }

            AnimateWinnerChips(winnerPlayerId, finalChips);
        }

        public void LockWinnerChipText(string winnerId)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile != null && profile.CurrentPlayerId == winnerId)
                {
                    profile.LockChipTextForWinAnimation();
                    return;
                }
            }
        }

        public void AnimateWinnerChipText(string winnerId, int finalChips)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile != null && profile.CurrentPlayerId == winnerId)
                {
                    profile.AnimateWinnerChips(finalChips, 0.9f);
                    return;
                }
            }
        }

        // Reveal ONLY the winner's hole cards at showdown.
        // Losing opponents keep their cards hidden. The local player's cards are
        // always visible, so if the local player is the winner we skip the reveal —
        // ShowWinnerCardsForSeconds resets to the card back after its duration, which
        // would wrongly hide our own hand.
        public void ShowWinnerShowdownCards(List<ShowdownCardData> showdownCards, string winnerId)
        {
            if (showdownCards == null || string.IsNullOrEmpty(winnerId))
                return;

            string myPlayerId = Auth.AuthManager.Instance.Session.Id;
            if (winnerId == myPlayerId)
                return;

            ShowdownCardData winnerData = showdownCards.Find(
                d => d != null && d.playerId == winnerId);

            if (winnerData == null || winnerData.holeCards == null || winnerData.holeCards.Count == 0)
                return;

            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;
                if (profile == null)
                    continue;

                if (profile.CurrentPlayerId == winnerId)
                {
                    profile.RevealCardsPersistent(winnerData.holeCards);
                    break;
                }
            }
        }

        public void ShowAllShowdownCards(List<ShowdownCardData> showdownCards)
        {
            if (showdownCards == null || showdownCards.Count == 0)
                return;

            foreach (var data in showdownCards)
            {
                if (data == null || data.holeCards == null || data.holeCards.Count == 0)
                    continue;

                foreach (var seat in seatViews)
                {
                    PlayerProfile profile = seat.Value;

                    if (profile == null)
                        continue;

                    if (profile.CurrentPlayerId == data.playerId)
                    {
                        profile.ShowWinnerCardsForSeconds(data.holeCards, 3f);
                        break;
                    }
                }
            }
        }


        public void HighlightWinnerCards(
    string winnerPlayerId,
    List<string> highlightCards,
    List<ShowdownCardData> showdownCards)
        {
            if (highlightCards == null || highlightCards.Count == 0)
                return;

            if (showdownCards == null)
                return;

            ShowdownCardData winnerData = showdownCards.Find(
                x => x.playerId == winnerPlayerId
            );

            if (winnerData == null)
                return;

            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;

                if (profile == null)
                    continue;

                if (profile.CurrentPlayerId == winnerPlayerId)
                {
                    profile.HighlightPrivateCards(
                        winnerData.holeCards,
                        highlightCards
                    );

                    break;
                }
            }
        }





        public void HighlightLocalPlayerBestCards(string localPlayerId, List<string> holeCards, List<string> bestHoleCards)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;
                if (profile == null) continue;

                if (profile.CurrentPlayerId == localPlayerId)
                {
                    profile.HighlightPrivateCards(holeCards, bestHoleCards);
                    break;
                }
            }
        }

        public void ClearLocalPlayerHighlight(string localPlayerId)
        {
            foreach (var seat in seatViews)
            {
                PlayerProfile profile = seat.Value;
                if (profile == null) continue;

                if (profile.CurrentPlayerId == localPlayerId)
                {
                    profile.ClearPrivateCardHighlights();
                    break;
                }
            }
        }

        private Coroutine handNameRoutine;

        public void ShowHandName(string handName)
        {
            if (handNameRoutine != null)
                StopCoroutine(handNameRoutine);

            handNameRoutine = StartCoroutine(ShowHandNameForSeconds(handName));
        }

        private IEnumerator ShowHandNameForSeconds(string handName)
        {
            
            Hand_Name.text = handName;
            HandNameTextBG.gameObject.SetActive(true);

            yield return new WaitForSeconds(2f);

            HandNameTextBG.gameObject.SetActive(false);
        }

        public void ShowPLOTooltip(string variant)
        {
            if (PLOTooltipPanel == null) return;

            if (PLOTooltipTitle != null)
                PLOTooltipTitle.text = $"{VariantUtils.ToDisplayName(variant)} Rules";

            if (PLOTooltipText != null)
            {
                PLOTooltipText.text = (variant == "omaha_six" || variant == "plo6")
                    ? "You have 6 hole cards.\nUse exactly 2 of them + 3 community cards to make your best 5-card hand."
                    : "You have 4 hole cards.\nUse exactly 2 of them + 3 community cards to make your best 5-card hand.";
            }

            PLOTooltipPanel.SetActive(true);
        }

        public void HidePLOTooltip()
        {
            if (PLOTooltipPanel != null)
                PLOTooltipPanel.SetActive(false);
        }

    }
}