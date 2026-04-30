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

        [Header("Seat Positions (0 to 8)")]
        public List<Transform> seatPositions =
            new List<Transform>();

        private readonly List<GameObject> spawnedSidePots =
            new List<GameObject>();


        [Header("Player Count UI")]
        public Text playerCountText;

        [Header("Seat Player Panels")]
        public List<GameObject> playerSeatPanels =
            new List<GameObject>();

        [Header("Empty Seat Available Indicators")]
        public List<GameObject> emptySeatIndicators =
            new List<GameObject>();

        [Header("Join / Leave Animation")]
        public float joinLeaveAnimationDuration = 0.25f;


        [Header("Disconnected UI")]
        public List<GameObject> disconnectedIndicators =
    new List<GameObject>();

        public List<Text> disconnectedCountdownTexts =
            new List<Text>();

        private Dictionary<int, Coroutine> disconnectCountdowns =
            new Dictionary<int, Coroutine>();



        [Header("Pause Overlay")]
        public GameObject pauseOverlay;
        public Text pauseReasonText;
        public Text pauseCountdownText;

        private Coroutine pauseCountdownRoutine;


        [Header("Sitting Out UI")]
        public List<CanvasGroup> playerPanelCanvasGroups =
    new List<CanvasGroup>();

        public List<GameObject> sittingOutLabels =
            new List<GameObject>();


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        //------------------------------------------------------
        // ROUND END
        //------------------------------------------------------

        public void AnimatePotToWinner(
            string playerId,
            int potAmount
        )
        {
            Debug.Log(
                $"[PokerTableUI] Pot → Winner | " +
                $"Player: {playerId}, Amount: {potAmount}"
            );
        }

        public void AnimateSplitPotToWinners(
            Dictionary<string, int> winners,
            int totalPot
        )
        {
            Debug.Log(
                $"[PokerTableUI] Split Pot | Total: {totalPot}"
            );

            foreach (var winner in winners)
            {
                Debug.Log(
                    $"Winner: {winner.Key} → {winner.Value}"
                );
            }
        }

        public void RevealPlayerCards(
            string playerId,
            List<string> holeCards
        )
        {
            Debug.Log(
                $"[PokerTableUI] Reveal Cards | {playerId} → " +
                string.Join(", ", holeCards)
            );
        }

        public void ShowHandRank(
            string playerId,
            string handRank
        )
        {
            Debug.Log(
                $"[PokerTableUI] Hand Rank | {playerId} → {handRank}"
            );
        }

        public void UpdateAllPlayerChips(
            Dictionary<string, int> balances
        )
        {
            Debug.Log("[PokerTableUI] Updating player chips");

            foreach (var item in balances)
            {
                Debug.Log(
                    $"Player: {item.Key} → Chips: {item.Value}"
                );
            }
        }

        //------------------------------------------------------
        // POT UPDATE
        //------------------------------------------------------

        public void AnimateChipsToPot()
        {
            Debug.Log(
                "[PokerTableUI] Chip animation → Player → Pot"
            );
        }

        public void UpdateMainPot(int potAmount)
        {
            if (mainPotText != null)
            {
                mainPotText.text = $"Pot: {potAmount}";
            }

            Debug.Log(
                $"[PokerTableUI] Main Pot Updated → {potAmount}"
            );
        }

        public void ShowSidePots(
            List<SidePots> sidePots
        )
        {
            HideSidePots();

            if (sidePots == null || sidePots.Count == 0)
                return;

            for (int i = 0; i < sidePots.Count; i++)
            {
                GameObject obj = Instantiate(
                    sidePotLabelPrefab,
                    sidePotContainer
                );

                Text txt = obj.GetComponent<Text>();

                if (txt != null)
                {
                    txt.text =
                        $"Side Pot {i + 1}: {sidePots[i].amount}";
                }

                spawnedSidePots.Add(obj);
            }

            Debug.Log(
                $"[PokerTableUI] Side Pots Shown → {sidePots.Count}"
            );
        }

        public void HideSidePots()
        {
            foreach (var item in spawnedSidePots)
            {
                if (item != null)
                    Destroy(item);
            }

            spawnedSidePots.Clear();

            Debug.Log("[PokerTableUI] Side Pots Hidden");
        }

        public void ShowRake(int rake)
        {
            if (rakePanel != null)
                rakePanel.SetActive(true);

            if (rakeText != null)
                rakeText.text = $"Rake: {rake}";

            Debug.Log(
                $"[PokerTableUI] Rake Shown → {rake}"
            );
        }

        public void HideRake()
        {
            if (rakePanel != null)
                rakePanel.SetActive(false);

            Debug.Log("[PokerTableUI] Rake Hidden");
        }

        //------------------------------------------------------
        // DEALER MOVED
        //------------------------------------------------------

        public void MoveDealerButton(int dealerSeat)
        {
            if (dealerButtonToken == null)
            {
                Debug.LogWarning(
                    "[PokerTableUI] Dealer Button Token missing"
                );
                return;
            }

            if (dealerSeat < 0 || dealerSeat >= seatPositions.Count)
            {
                Debug.LogError(
                    $"[PokerTableUI] Invalid dealer seat: {dealerSeat}"
                );
                return;
            }

            dealerButtonToken.position =
                seatPositions[dealerSeat].position;

            Debug.Log(
                $"[PokerTableUI] Dealer Button moved → Seat {dealerSeat}"
            );
        }

        public void UpdateBlindIndicators(
            int smallBlindSeat,
            int bigBlindSeat
        )
        {
            if (smallBlindSeat >= 0 &&
                smallBlindSeat < seatPositions.Count &&
                smallBlindIndicator != null)
            {
                smallBlindIndicator.position =
                    seatPositions[smallBlindSeat].position;
            }

            if (bigBlindSeat >= 0 &&
                bigBlindSeat < seatPositions.Count &&
                bigBlindIndicator != null)
            {
                bigBlindIndicator.position =
                    seatPositions[bigBlindSeat].position;
            }

            Debug.Log(
                $"[PokerTableUI] Blinds Updated → " +
                $"SB: {smallBlindSeat}, BB: {bigBlindSeat}"
            );
        }

        public void HandlePreFlopFirstActor(
            int firstActorSeat
        )
        {
            Debug.Log(
                $"[PokerTableUI] PreFlop First Actor → Seat {firstActorSeat}"
            );

            // Optional:
            // highlight first acting player here
        }



        // ------------------------------------------------------
        // PLAYER JOIN ANIMATION
        // ------------------------------------------------------

        public void ShowPlayerJoinAnimation(int seat)
        {
            if (seat < 0 || seat >= playerSeatPanels.Count)
            {
                Debug.LogError(
                    $"[PokerTableUI] Invalid Join Seat: {seat}"
                );
                return;
            }

            GameObject seatPanel = playerSeatPanels[seat];

            if (seatPanel == null)
            {
                Debug.LogWarning(
                    $"[PokerTableUI] Seat Panel NULL at seat {seat}"
                );
                return;
            }

            seatPanel.SetActive(true);

            StopCoroutine("AnimateJoin");
            StartCoroutine(AnimateJoin(seatPanel));

            Debug.Log(
                $"[PokerTableUI] Player Join Animation → Seat {seat}"
            );
        }

        private IEnumerator AnimateJoin(GameObject target)
        {
            float timer = 0f;

            target.transform.localScale = Vector3.zero;

            while (timer < joinLeaveAnimationDuration)
            {
                timer += Time.deltaTime;

                float t = timer / joinLeaveAnimationDuration;

                target.transform.localScale =
                    Vector3.Lerp(
                        Vector3.zero,
                        Vector3.one,
                        t
                    );

                yield return null;
            }

            target.transform.localScale = Vector3.one;
        }


        // ------------------------------------------------------
        // PLAYER LEAVE ANIMATION
        // ------------------------------------------------------

        public void ShowPlayerLeaveAnimation(int seat)
        {
            if (seat < 0 || seat >= playerSeatPanels.Count)
            {
                Debug.LogError(
                    $"[PokerTableUI] Invalid Leave Seat: {seat}"
                );
                return;
            }

            GameObject seatPanel = playerSeatPanels[seat];

            if (seatPanel == null)
            {
                Debug.LogWarning(
                    $"[PokerTableUI] Seat Panel NULL at seat {seat}"
                );
                return;
            }

            StopCoroutine("AnimateLeave");
            StartCoroutine(AnimateLeave(seatPanel, seat));

            Debug.Log(
                $"[PokerTableUI] Player Leave Animation → Seat {seat}"
            );
        }

        private IEnumerator AnimateLeave(
            GameObject target,
            int seat
        )
        {
            float timer = 0f;

            Vector3 startScale = target.transform.localScale;

            while (timer < joinLeaveAnimationDuration)
            {
                timer += Time.deltaTime;

                float t = timer / joinLeaveAnimationDuration;

                target.transform.localScale =
                    Vector3.Lerp(
                        startScale,
                        Vector3.zero,
                        t
                    );

                yield return null;
            }

            target.transform.localScale = Vector3.one;
            target.SetActive(false);

            RefreshSeatAvailability();

            Debug.Log(
                $"[PokerTableUI] Player removed from Seat {seat}"
            );
        }


        // ------------------------------------------------------
        // PLAYER COUNT UPDATE
        // ------------------------------------------------------

        public void UpdatePlayerCount()
        {
            int totalPlayers = 0;

            for (int i = 0; i < playerSeatPanels.Count; i++)
            {
                if (playerSeatPanels[i] != null &&
                    playerSeatPanels[i].activeSelf)
                {
                    totalPlayers++;
                }
            }

            if (playerCountText != null)
            {
                playerCountText.text =
                    $"Players: {totalPlayers}";
            }

            Debug.Log(
                $"[PokerTableUI] Player Count Updated → {totalPlayers}"
            );
        }


        // ------------------------------------------------------
        // EMPTY SEAT AVAILABLE INDICATOR
        // ------------------------------------------------------

        public void RefreshSeatAvailability()
        {
            for (int i = 0; i < emptySeatIndicators.Count; i++)
            {
                bool seatOccupied = false;

                if (i < playerSeatPanels.Count &&
                    playerSeatPanels[i] != null)
                {
                    seatOccupied =
                        playerSeatPanels[i].activeSelf;
                }

                if (emptySeatIndicators[i] != null)
                {
                    emptySeatIndicators[i].SetActive(
                        !seatOccupied
                    );
                }
            }

            Debug.Log(
                "[PokerTableUI] Seat Availability Refreshed"
            );
        }





        public void ShowDisconnectedIndicator(
    int seat,
    int gracePeriodSeconds
)
        {
            if (seat < 0 || seat >= disconnectedIndicators.Count)
            {
                Debug.LogError(
                    $"[PokerTableUI] Invalid disconnected seat: {seat}"
                );
                return;
            }

            if (disconnectedIndicators[seat] != null)
                disconnectedIndicators[seat].SetActive(true);

            if (disconnectCountdowns.ContainsKey(seat) &&
                disconnectCountdowns[seat] != null)
            {
                StopCoroutine(disconnectCountdowns[seat]);
            }

            disconnectCountdowns[seat] = StartCoroutine(
                DisconnectedCountdownRoutine(
                    seat,
                    gracePeriodSeconds
                )
            );

            Debug.Log(
                $"[PokerTableUI] Disconnected Indicator ON → Seat {seat}"
            );
        }

        private IEnumerator DisconnectedCountdownRoutine(
            int seat,
            int seconds
        )
        {
            int remaining = seconds;

            while (remaining >= 0)
            {
                if (seat < disconnectedCountdownTexts.Count &&
                    disconnectedCountdownTexts[seat] != null)
                {
                    disconnectedCountdownTexts[seat].text =
                        remaining + "s";
                }

                yield return new WaitForSeconds(1f);
                remaining--;
            }

            Debug.Log(
                $"[PokerTableUI] Grace countdown finished → Seat {seat}"
            );
        }

        public void HideDisconnectedIndicator(int seat)
        {
            if (seat < 0 || seat >= disconnectedIndicators.Count)
            {
                Debug.LogError(
                    $"[PokerTableUI] Invalid reconnect seat: {seat}"
                );
                return;
            }

            if (disconnectCountdowns.ContainsKey(seat) &&
                disconnectCountdowns[seat] != null)
            {
                StopCoroutine(disconnectCountdowns[seat]);
                disconnectCountdowns[seat] = null;
            }

            if (disconnectedIndicators[seat] != null)
                disconnectedIndicators[seat].SetActive(false);

            if (seat < disconnectedCountdownTexts.Count &&
                disconnectedCountdownTexts[seat] != null)
            {
                disconnectedCountdownTexts[seat].text = "";
            }

            Debug.Log(
                $"[PokerTableUI] Disconnected Indicator OFF → Seat {seat}"
            );
        }




        public void ShowPauseOverlay(
    string reason,
    int countdownSeconds
)
        {
            if (pauseOverlay != null)
                pauseOverlay.SetActive(true);

            // Localized readable text
            string readableReason = GetReadableReason(reason);

            if (pauseReasonText != null)
                pauseReasonText.text = readableReason;

            if (pauseCountdownRoutine != null)
                StopCoroutine(pauseCountdownRoutine);

            pauseCountdownRoutine = StartCoroutine(
                PauseCountdown(countdownSeconds)
            );

            Debug.Log(
                $"[PokerTableUI] Pause Overlay ON → {readableReason}"
            );
        }

        public void HidePauseOverlay()
        {
            if (pauseOverlay != null)
                pauseOverlay.SetActive(false);

            if (pauseCountdownRoutine != null)
                StopCoroutine(pauseCountdownRoutine);

            Debug.Log("[PokerTableUI] Pause Overlay OFF");
        }

        private IEnumerator PauseCountdown(int seconds)
        {
            int remaining = seconds;

            while (remaining >= 0)
            {
                if (pauseCountdownText != null)
                {
                    pauseCountdownText.text =
                        $"Resuming in {remaining}s";
                }

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



        public void ShowSittingOutState(int seat)
        {
            if (seat < 0)
                return;

            if (seat < playerPanelCanvasGroups.Count &&
                playerPanelCanvasGroups[seat] != null)
            {
                playerPanelCanvasGroups[seat].alpha = 0.45f;
                playerPanelCanvasGroups[seat].interactable = false;
                playerPanelCanvasGroups[seat].blocksRaycasts = false;
            }

            if (seat < sittingOutLabels.Count &&
                sittingOutLabels[seat] != null)
            {
                sittingOutLabels[seat].SetActive(true);
            }

            Debug.Log($"[PokerTableUI] Sitting Out ON → Seat {seat}");
        }

        public void HideSittingOutState(int seat)
        {
            if (seat < 0)
                return;

            if (seat < playerPanelCanvasGroups.Count &&
                playerPanelCanvasGroups[seat] != null)
            {
                playerPanelCanvasGroups[seat].alpha = 1f;
                playerPanelCanvasGroups[seat].interactable = true;
                playerPanelCanvasGroups[seat].blocksRaycasts = true;
            }

            if (seat < sittingOutLabels.Count &&
                sittingOutLabels[seat] != null)
            {
                sittingOutLabels[seat].SetActive(false);
            }

            Debug.Log($"[PokerTableUI] Sitting Out OFF → Seat {seat}");
        }

    }
}