using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class PokerTableUI : MonoBehaviour
    {
        public static PokerTableUI Instance { get; private set; }

        [Header("Main Pot UI")]
        public Text mainPotText;

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

        private Coroutine pauseCountdownRoutine;

        private readonly List<GameObject> spawnedSidePots = new List<GameObject>();
        private readonly List<PlayerProfile> spawnedSeats = new List<PlayerProfile>();
        private readonly Dictionary<int, PlayerProfile> seatViews = new Dictionary<int, PlayerProfile>();
        private List<Transform> currentSlots = new List<Transform>();

        private List<string> pendingMyCards;
        private bool tableRendered;

        [Header("Game Status Text")]
        public Text gameStatusText;
        private string activeThinkingPlayerId = "";
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
        public void RenderFullTable(GameStateUpdatePayload state)
        {
            if (state == null || state.Players == null)
                return;

            int maxPlayers = GetMaxPlayersFromState(state);
            currentSlots = GetSlotsByMaxPlayers(maxPlayers);

            foreach (var player in state.Players)
            {
                int seat = player.Seat;

                if (seat < 0 || seat >= currentSlots.Count)
                    continue;

                if (seatViews.TryGetValue(seat, out PlayerProfile view))
                {
                    view.Bind(player); // update only

                  //  ApplyThinkingState(view);
                }
                else
                {
                    PlayerProfile newView = Instantiate(playerSeatPrefab, currentSlots[seat]);

                    newView.transform.localPosition = Vector3.zero;
                    newView.transform.localRotation = Quaternion.identity;
                    newView.transform.localScale = Vector3.one;

                    newView.Bind(player);

                    //ApplyThinkingState(newView);
                    spawnedSeats.Add(newView);
                    seatViews[seat] = newView;



                }
            }

            tableRendered = true;

            UpdatePlayerCountUI(state.Players.Count, maxPlayers);

            if (pendingMyCards != null && pendingMyCards.Count > 0)
            {
                ShowMyPrivateCards(pendingMyCards);
            }
        }

        private void ApplyThinkingState(PlayerProfile view)
        {
            if (view == null)
                return;

            if (!string.IsNullOrEmpty(activeThinkingPlayerId) &&
                view.CurrentPlayerId == activeThinkingPlayerId)
            {
               // view.ShowThinking();
            }
            else
            {
              //  view.HideThinking();
            }
        }

        private int GetMaxPlayersFromState(GameStateUpdatePayload state)
        {
            if (state.MaxPlayer > 0)
                return state.MaxPlayer;

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
            if (mainPotText != null)
                mainPotText.text = $"Pot: {potAmount}";

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

        public void UpdateBlindIndicators(int smallBlindSeat, int bigBlindSeat)
        {
            Transform sb = GetSlotTransform(smallBlindSeat);
            Transform bb = GetSlotTransform(bigBlindSeat);

            if (sb != null && smallBlindIndicator != null)
                smallBlindIndicator.position = sb.position;

            if (bb != null && bigBlindIndicator != null)
                bigBlindIndicator.position = bb.position;

            Debug.Log($"[PokerTableUI] Blinds Updated -> SB: {smallBlindSeat}, BB: {bigBlindSeat}");
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

        public void RevealPlayerCards(string playerId, List<string> holeCards)
        {
            Debug.Log($"[PokerTableUI] Reveal Cards | {playerId} -> {string.Join(", ", holeCards)}");
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
            pendingMyCards = null;

            foreach (var pair in seatViews)
            {
                if (pair.Value != null)
                    pair.Value.HidePrivateCards();
            }
        }


        public void ShowPlayerThinking(string playerId)
        {
            activeThinkingPlayerId = playerId;

            foreach (var item in seatViews)
            {
                PlayerProfile view = item.Value;

                if (view == null)
                    continue;

                if (view.CurrentPlayerId == playerId)
                    view.ShowThinking();
                else
                    view.HideThinking();
            }
        }

        public void HideAllThinking()
        {
            activeThinkingPlayerId = "";

            foreach (var item in seatViews)
            {
                if (item.Value != null)
                    item.Value.HideThinking();
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
    }
}