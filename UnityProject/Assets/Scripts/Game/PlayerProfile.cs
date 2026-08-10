using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class PlayerProfile : MonoBehaviour
    {
        public Text Player_Name;
        public Text Player_Chips;
        public Image Player_Avtar;
        public Text BattingAction_Text;

        [Header("Seat Config")]
        public int seatIndex = 0;

        [Header("State UI")]
        public GameObject DisconnectedPanel;
        // Seconds only ("45s"), and only on my own seat — the server never tells us
        // how long it will hold another player's seat, so theirs stays blank.
        public TextMeshProUGUI DisconnectedCountdownText;
        // The word: "Reconnecting" while they're still in players[], "Disconnected"
        // once the server drops them.
        public TextMeshProUGUI DisconnectedLabelText;
        public GameObject SittingOutPanel;
        // "Reconnecting" (server put them here after a drop) or "Sitting Out"
        // (they chose it). The hand count itself isn't shown.
        public TextMeshProUGUI SittingOutHandsText;
        public CanvasGroup PlayerCanvasGroup;

        private GamePlayer currentPlayer;
        // Set on MY OWN seat while my socket is reconnecting. Driven by the socket
        // state machine, not by a server broadcast — while I'm offline no events
        // reach me, so this is the only signal my own client has.
        private bool localReconnecting;


        [Header("Private Cards UI")]
        public List<Image> PrivateCardImages = new List<Image>();
        public List<Image> PrivateCardHighlightImages = new List<Image>();
        public Sprite CardBackSprite;
        public List<CardSpriteData> CardSprites = new List<CardSpriteData>();

        private Dictionary<string, Sprite> cardLookup = new Dictionary<string, Sprite>();

        public string CurrentPlayerId => currentPlayer != null ? currentPlayer.Id : "";
        private string lastCardKey = "";

        public Image Action_BG;
        public List<Sprite> Action_BG_List;
        public GameObject PlayerThinking;

        public string currentPlayerId;

        public List<Sprite> AvtarImage;
        public GameObject DealerButton;
        public Slider TimerSlider;

        [Header("Blinds")]
        public GameObject BigBling;
        public GameObject SmallBlind;

        [Header("Tooltip")]
        public Button TooltipBtn;
        
        private Coroutine timerRoutine;
        private bool chipTextLockedForWinAnimation = false;
        private Coroutine winChipRoutine;
        private Coroutine winnerCardRoutine;

        public void LockChipTextForWinAnimation()
        {
            chipTextLockedForWinAnimation = true;
        }
        private void Start()
        {
            LoadPlayerData();
        }

        private void Awake()
        {
            PrepareCardLookup();
            //HidePrivateCards();
        }
        private void PrepareCardLookup()
        {
            cardLookup.Clear();
            ClearPrivateCardHighlights();
            foreach (var item in CardSprites)
            {
                if (item == null || string.IsNullOrEmpty(item.CardName) || item.CardSprite == null)
                    continue;

                if (!cardLookup.ContainsKey(item.CardName))
                    cardLookup.Add(item.CardName, item.CardSprite);
            }
        }




        public void ShowWinnerCardsForSeconds(List<string> cards, float duration = 3f)
        {
            if (winnerCardRoutine != null)
                StopCoroutine(winnerCardRoutine);

            winnerCardRoutine = StartCoroutine(
                ShowWinnerCardsRoutine(cards, duration)
            );
        }

        // Flip cards face-up and keep them shown (no reset to the card back).
        // Used for the showdown winner reveal — cards persist until the next hand
        // clears them via HidePrivateCards.
        // Set while a showdown reveal is on screen. state_update keeps calling
        // ShowCardBacks for any player with cardsDealt, and at ROUND_END that lands
        // mid-reveal and resets the cards the coroutine hasn't flipped yet — leaving
        // the winner with one card face-up and one face-down. Cleared when the next
        // hand hides the cards.
        private bool showingRevealedCards;

        /// <summary>
        /// Release the reveal guard so the next hand can stamp card backs again.
        /// Called when the round number changes — the ONLY reliable signal that the
        /// previous hand is finished with.
        /// </summary>
        public void EndCardReveal()
        {
            showingRevealedCards = false;
        }

        public void RevealCardsPersistent(List<string> cards)
        {
            showingRevealedCards = true;

            if (winnerCardRoutine != null)
                StopCoroutine(winnerCardRoutine);

            winnerCardRoutine = StartCoroutine(RevealCardsPersistentRoutine(cards));
        }

        private IEnumerator RevealCardsPersistentRoutine(List<string> cards)
        {
            if (cards == null || cards.Count == 0)
                yield break;

            for (int i = 0; i < PrivateCardImages.Count; i++)
            {
                if (PrivateCardImages[i] == null)
                    continue;

                if (i >= cards.Count)
                {
                    PrivateCardImages[i].gameObject.SetActive(false);
                    continue;
                }

                Image img = PrivateCardImages[i];
                img.gameObject.SetActive(true);
                img.sprite = CardBackSprite;   // start from the back face

                yield return FlipCardToFront(img, cards[i]);
            }
        }

        private IEnumerator ShowWinnerCardsRoutine(List<string> cards, float duration)
        {
            if (cards == null || cards.Count == 0)
                yield break;

            // Flip each card back → front so the reveal is animated, not an instant swap.
            for (int i = 0; i < PrivateCardImages.Count; i++)
            {
                if (PrivateCardImages[i] == null)
                    continue;

                if (i >= cards.Count)
                {
                    PrivateCardImages[i].gameObject.SetActive(false);
                    continue;
                }

                Image img = PrivateCardImages[i];
                img.gameObject.SetActive(true);
                img.sprite = CardBackSprite;   // start from the back face

                yield return FlipCardToFront(img, cards[i]);
            }

            yield return new WaitForSeconds(duration);
            ClearPrivateCardHighlights();
            for (int i = 0; i < PrivateCardImages.Count; i++)
            {
                if (PrivateCardImages[i] == null)
                    continue;

                PrivateCardImages[i].sprite = CardBackSprite;
                PrivateCardImages[i].transform.localScale = Vector3.one;
            }
        }

        // Scale-X flip: squash to 0 showing the back, swap to the face, expand back to 1.
        // Mirrors CardFlipPrefab.FlipAnimation so opponent reveals match the local player.
        private IEnumerator FlipCardToFront(Image img, string card)
        {
            const float flipDuration = 0.15f;
            Transform tr = img.transform;

            float t = 0f;
            while (t < flipDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1f, 0f, t / flipDuration);
                tr.localScale = new Vector3(s, 1f, 1f);
                yield return null;
            }

            string key = ConvertCardKey(card);
            img.sprite = cardLookup.TryGetValue(key, out Sprite sprite)
                ? sprite
                : CardBackSprite;

            t = 0f;
            while (t < flipDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(0f, 1f, t / flipDuration);
                tr.localScale = new Vector3(s, 1f, 1f);
                yield return null;
            }

            tr.localScale = Vector3.one;
        }

        public void ShowPrivateCards(List<string> cards)
        {
            if (cards == null || cards.Count == 0)
                return;

            string newKey = string.Join(",", cards);

          
            if (lastCardKey == newKey)
                return;

            lastCardKey = newKey;
            ClearPrivateCardHighlights();
            StopCoroutine(nameof(ShowPrivateCardsRoutine));
            StartCoroutine(ShowPrivateCardsRoutine(cards));
        }

        private IEnumerator ShowPrivateCardsRoutine(List<string> cards)
        {
            if (cards == null || cards.Count == 0)
                yield break;

            const float showAnimationDuration = 0.10f;
            const float beforeFlipDelay = 0.08f;
            const float nextCardDelay = 0.06f;

            for (int i = 0; i < PrivateCardImages.Count; i++)
            {
                if (PrivateCardImages[i] == null)
                    continue;

                if (i >= cards.Count)
                {
                    PrivateCardImages[i].gameObject.SetActive(false);
                    continue;
                }

                Image img = PrivateCardImages[i];

                img.gameObject.SetActive(true);
                img.sprite = CardBackSprite;
                img.transform.localScale = Vector3.zero;

                float timer = 0f;

                while (timer < showAnimationDuration)
                {
                    timer += Time.deltaTime;

                    float progress = Mathf.Clamp01(
                        timer / showAnimationDuration
                    );

                    float scale = Mathf.Lerp(0f, 1f, progress);

                    img.transform.localScale =
                        new Vector3(scale, scale, scale);

                    yield return null;
                }

                img.transform.localScale = Vector3.one;

                yield return new WaitForSeconds(beforeFlipDelay);

                string key = ConvertCardKey(cards[i]);

                img.sprite = cardLookup.TryGetValue(
                    key,
                    out Sprite sprite
                )
                    ? sprite
                    : CardBackSprite;

                yield return new WaitForSeconds(nextCardDelay);
            }
        }

        public void ShowCardBacks(int count)
        {
            // A showdown reveal is on screen — don't stamp card backs over it.
            if (showingRevealedCards)
                return;

            // A new hand is being dealt — clear any leftover best-hand highlight
            // from the previous round's showdown.
            ClearPrivateCardHighlights();

            for (int i = 0; i < PrivateCardImages.Count; i++)
            {
                if (PrivateCardImages[i] == null)
                    continue;

                bool show = i < count;
                PrivateCardImages[i].gameObject.SetActive(show);

                if (show)
                    PrivateCardImages[i].sprite = CardBackSprite;
            }
        }

        public void HidePrivateCards()
        {
            showingRevealedCards = false;

            lastCardKey = "";
            ClearPrivateCardHighlights();
            foreach (var img in PrivateCardImages)
            {
                if (img != null)
                    img.gameObject.SetActive(false);
            }
        }

       

        private string ConvertCardKey(string serverCard)
        {
            if (string.IsNullOrEmpty(serverCard))
                return serverCard;

            return serverCard
                .Replace("♥", "H")
                .Replace("♦", "D")
                .Replace("♣", "C")
                .Replace("♠", "S")
                .ToUpper();
        }
        private void OnEnable()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateUpdated += LoadPlayerData;
        }

        private void OnDisable()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateUpdated -= LoadPlayerData;
        }
        bool isFirstBind = true;
        private bool tooltipWired = false;

        public void Bind(GamePlayer player)
        {
            currentPlayer = player;

            if (player == null)
            {
                Clear();
                return;
            }

            currentPlayerId = player.Id;
            seatIndex = player.Seat;

            SetupTooltipBtn(player);

            if (Player_Name != null)
                Player_Name.text = player.Username;
            if (Player_Chips != null && !chipTextLockedForWinAnimation)
            {
                Player_Chips.text = player.Chips.ToString();
            }
            if (Player_Chips != null && isFirstBind)
            {
                Player_Chips.text = player.Chips.ToString();
                isFirstBind = false;
            }
            if(player.Chips > 0)
            {
                Only_OneTimeCall = false;
            }

            if(player.Chips == 0)
            {
                Player_Chips.text = "0";
                if (!Only_OneTimeCall)
                {
                    StartCoroutine(No_ChipsStatus_Show());
                }
            }
            SetLocalAvatar(player);
            if (BattingAction_Text != null && !string.IsNullOrEmpty(player.LastAction))
                BattingAction_Text.text = FormatActionText(player.LastAction);

            UpdateActionBG(player.LastAction);

            // My own reconnect wins over the snapshot: the state that told me
            // "you're connected" is by definition older than the drop I'm in.
            if (localReconnecting)
            {
                // Own-seat badge is driven by ShowReconnecting/HideReconnecting,
                // which carry a real countdown. Leave them to it.
            }
            else
            {
                bool sittingOut = player.SittingOut ||
                                  GameStateManager.Instance.IsPlayerSittingOut(player.Id);

                // Sit-out is checked FIRST and wins over the disconnect flag. Once
                // the server marks a dropped player as sitting out, the spec calls
                // that state "Sit Out" — a still-disconnected player stays flagged
                // disconnected too, so testing that first would keep the seat
                // reading "Reconnecting" for all 3 sit-out rounds.
                if (sittingOut)
                    SetSeatStatus(SeatStatus.SittingOut);
                else if (player.Disconnected)
                    SetSeatStatus(SeatStatus.Reconnecting);
                else
                    SetSeatStatus(SeatStatus.None);
            }

            Debug.Log($"[PlayerProfile] Bound prefab -> {player.Username} | Seat: {player.Seat}");
        }
        private void SetupTooltipBtn(GamePlayer player)
        {
            if (TooltipBtn == null) return;

            string localId = AuthManager.Instance != null ? AuthManager.Instance.Session.Id : null;
            bool isLocal = !string.IsNullOrEmpty(localId) && player.Id == localId;

            string variant = GameStateManager.Instance.Variant
                          ?? GameStateManager.Instance.CurrentState?.Variant;
            bool isPLO = variant == "omaha" || variant == "omaha_six"
                      || variant == "plo4"  || variant == "plo6";

            TooltipBtn.gameObject.SetActive(isLocal && isPLO);

            if (isLocal && isPLO && !tooltipWired)
            {
                tooltipWired = true;
                TooltipBtn.onClick.RemoveAllListeners();
                TooltipBtn.onClick.AddListener(() =>
                {
                    if (PokerTableUI.Instance != null)
                        PokerTableUI.Instance.ShowPLOTooltip(variant);
                });
            }
        }

        bool Only_OneTimeCall = false;
        IEnumerator No_ChipsStatus_Show()
        {
                Only_OneTimeCall = true;
               yield return new WaitForSeconds(2f);
             BattingAction_Text.text = "No Chips";
        }
        private void LoadPlayerData()
        {
            if (currentPlayer != null)
            {
                Bind(currentPlayer);
                return;
            }

            if (GameStateManager.Instance == null)
            {
                Debug.LogError("[PlayerProfile] GameStateManager not found");
                return;
            }

            List<GamePlayer> players = GameStateManager.Instance.Players;

            if (players == null || players.Count == 0)
            {
                Debug.LogWarning("[PlayerProfile] No players found");
                return;
            }

            GamePlayer targetPlayer = null;

            foreach (var player in players)
            {
                if (player.Seat == seatIndex)
                {
                    targetPlayer = player;
                    break;
                }
            }

            if (targetPlayer == null)
            {
                Debug.LogWarning($"[PlayerProfile] No player found on seat {seatIndex}");
                Clear();
                return;
            }

            Bind(targetPlayer);
        }

        public void Clear()
        {
            currentPlayer = null;

            if (Player_Name != null)
                Player_Name.text = "";

            if (Player_Chips != null)
                Player_Chips.text = "";

            //if (BattingAction_Text != null)
             //   BattingAction_Text.text = "";

            HideDisconnected();
            HideSittingOut();
        }

        /// <summary>
        /// Wipe the action label. Bind() deliberately won't do this — state_update
        /// reports lastAction:null constantly, and clearing on every null would
        /// erase labels mid-hand. But after a reconnect the label is showing an
        /// action from before the drop, so it has to be cleared explicitly.
        /// </summary>
        public void ClearActionLabel()
        {
            if (BattingAction_Text != null)
                BattingAction_Text.text = "";

            if (currentPlayer != null)
                currentPlayer.LastAction = null;

            UpdateActionBG(null);
        }

        public void UpdateAction(string action)
        {
            if (BattingAction_Text != null)
                BattingAction_Text.text = FormatActionText(action);

            if (currentPlayer != null)
                currentPlayer.LastAction = action;

            UpdateActionBG(action);
        }
        private void UpdateActionBG(string action)
        {
            if (Action_BG == null)
            {
                Debug.LogWarning("[ActionBG] Image missing");
                return;
            }

            if (Action_BG_List == null || Action_BG_List.Count == 0)
            {
                Debug.LogWarning("[ActionBG] Sprite list empty");
             //   Action_BG.gameObject.SetActive(false);
                return;
            }

            if (string.IsNullOrEmpty(action))
            {
                //Action_BG.gameObject.SetActive(false);
                return;
            }

            action = action.ToLower();

            int index = -1;

            switch (action)
            {
                case "fold": index = 0; break;
                case "check": index = 1; break;
                case "call": index = 2; break;
                case "raise": index = 3; break;
                case "all_in": index = 2; break;
            }

            // ❗ SAFE CHECK
            if (index >= 0 && index < Action_BG_List.Count && Action_BG_List[index] != null)
            {
                Action_BG.sprite = Action_BG_List[index];
               // Action_BG.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[ActionBG] Invalid index or missing sprite for action: {action}");
               // Action_BG.gameObject.SetActive(false);
            }
        }

        public void UpdateChips(int chips)
        {
            if (!chipTextLockedForWinAnimation && Player_Chips != null)
            {
                Player_Chips.text = chips.ToString();

                if (currentPlayer != null)
                    currentPlayer.Chips = chips;
            }
                
        }

        // ── Seat status badge ────────────────────────────────────────────────
        //
        // One presentation for every "this player isn't playing right now" state:
        // the seat greys out and SittingOutPanel carries the word. The mid-hand
        // disconnect used to use a second, different-looking widget with an empty
        // timer slot next to it — same meaning, worse look, so it's gone.
        //
        // DisconnectedPanel is now reserved for MY OWN seat, which is the only one
        // with a real countdown to show.

        private enum SeatStatus
        {
            None,
            Reconnecting,   // dropped — mid-hand, or sitting out on the removal clock
            SittingOut,     // chose to sit out; no removal deadline
            Disconnected    // server gave up on them; seat about to be removed
        }

        private SeatStatus seatStatus = SeatStatus.None;

        private void SetSeatDimmed(bool dimmed)
        {
            if (PlayerCanvasGroup == null)
                return;

            PlayerCanvasGroup.alpha = dimmed ? 0.45f : 1f;
            PlayerCanvasGroup.interactable = !dimmed;
            PlayerCanvasGroup.blocksRaycasts = !dimmed;
        }

        private void SetSeatStatus(SeatStatus status)
        {
            seatStatus = status;

            bool inactive = status != SeatStatus.None;

            // My own reconnect dims the seat too, and it outlives a status reset to
            // None — don't let a routine re-bind brighten a seat I'm still cut off on.
            SetSeatDimmed(inactive || localReconnecting);

            if (SittingOutPanel != null)
                SittingOutPanel.SetActive(inactive);

            if (SittingOutHandsText != null)
            {
                // This label always reads "Sitting Out", drop-caused or voluntary.
                // The distinction isn't useful to other players — what matters is
                // that the seat isn't acting. "Reconnecting" with a live countdown
                // stays on the player's OWN seat, via DisconnectedPanel.
                SittingOutHandsText.text = status switch
                {
                    SeatStatus.Reconnecting => "Sitting Out",
                    SeatStatus.SittingOut   => "Sitting Out",
                    SeatStatus.Disconnected => "Disconnected",
                    _                       => ""
                };
            }
        }

        /// <summary>
        /// Another player dropped. No countdown: the server doesn't tell us how long
        /// its grace period is, so any number here would be a guess. Clears when they
        /// reconnect or when they fall out of players[].
        /// </summary>
        public void ShowDisconnected()
        {
            SetSeatStatus(SeatStatus.Reconnecting);
        }

        /// <summary>
        /// The player has fallen out of state_update.players[] — the server gave up
        /// on them. Final state before the seat is destroyed: stop offering hope.
        /// </summary>
        public void MarkDisconnectedAndRemove()
        {
            localReconnecting = false;
            HideOwnReconnectBadge();
            SetSeatStatus(SeatStatus.Disconnected);
        }

        /// <summary>True while this seat is flagged as dropped.</summary>
        public bool IsShowingDisconnected =>
            localReconnecting ||
            seatStatus == SeatStatus.Reconnecting ||
            seatStatus == SeatStatus.Disconnected;

        /// <summary>
        /// My own seat, while my socket is down. Uses DisconnectedPanel because this
        /// is the one case with a real countdown — SocketManager ticks the remaining
        /// seconds and calls this again.
        /// </summary>
        public void ShowReconnecting(int secondsRemaining)
        {
            localReconnecting = true;

            // Grey out the same as any other dropped seat, so my own drop and an
            // opponent's read identically.
            SetSeatDimmed(true);

            if (DisconnectedPanel != null)
                DisconnectedPanel.SetActive(true);

            if (DisconnectedLabelText != null)
                DisconnectedLabelText.text = "Reconnecting";

            if (DisconnectedCountdownText != null)
            {
                DisconnectedCountdownText.text =
                    secondsRemaining > 0 ? $"{secondsRemaining}s" : "";
            }
        }

        public void HideReconnecting()
        {
            localReconnecting = false;
            HideOwnReconnectBadge();

            // Back online, but I may still be sitting out — re-apply the current
            // status so the seat only brightens if it's genuinely back in play.
            SetSeatStatus(seatStatus);
        }

        private void HideOwnReconnectBadge()
        {
            if (DisconnectedPanel != null)
                DisconnectedPanel.SetActive(false);

            if (DisconnectedLabelText != null)
                DisconnectedLabelText.text = "";

            if (DisconnectedCountdownText != null)
                DisconnectedCountdownText.text = "";
        }

        public void HideDisconnected()
        {
            if (localReconnecting)
                return;

            SetSeatStatus(SeatStatus.None);
        }

        // handsRemaining is kept in the signature for callers but no longer changes
        // the label — drop-caused and voluntary sit-out both read "Sitting Out".
        public void ShowSittingOut(int? handsRemaining = null)
        {
            SetSeatStatus(SeatStatus.SittingOut);
        }

        public void HideSittingOut()
        {
            SetSeatStatus(SeatStatus.None);
        }


        public void ShowThinking()
        {
            if (PlayerThinking != null)
                PlayerThinking.SetActive(true);
        }

        public void HideThinking()
        {
            if (PlayerThinking != null)
                PlayerThinking.SetActive(false);

            StopTimer();
        }

        public void StartTimer(float duration)
        {
            if (TimerSlider == null)
                return;

            if (timerRoutine != null)
            {
                StopCoroutine(timerRoutine);
                timerRoutine = null;
            }

            TimerSlider.gameObject.SetActive(true);

            TimerSlider.minValue = 0f;
            TimerSlider.maxValue = 1f;

            TimerSlider.value = 1f;

            timerRoutine = StartCoroutine(TimerRoutine(duration));
        }
        private IEnumerator TimerRoutine(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float normalized = 1f - (elapsed / duration);

                if (TimerSlider != null)
                {
                    TimerSlider.value = Mathf.Clamp01(normalized);
                }

                yield return null;
            }

            StopTimer();
        }

        public void StopTimer()
        {
            if (timerRoutine != null)
            {
                StopCoroutine(timerRoutine);
                timerRoutine = null;
            }

            if (TimerSlider != null)
            {
                TimerSlider.value = 0f;
                TimerSlider.gameObject.SetActive(false);
            }
        }

        private void SetLocalAvatar(GamePlayer player)
        {
            if (Player_Avtar == null)
                return;

            if (AvtarImage == null || AvtarImage.Count == 0)
                return;

            int index = 0;

            if (player != null && !string.IsNullOrEmpty(player.Id))
            {
                index = Mathf.Abs(player.Id.GetHashCode()) % AvtarImage.Count;
            }
            else
            {
                index = Mathf.Abs(seatIndex) % AvtarImage.Count;
            }

            Player_Avtar.sprite = AvtarImage[index];
        }


        public void ShowDealer()
        {
            if (DealerButton != null)
                DealerButton.SetActive(true);
        }

        public void HideDealer()
        {
            if (DealerButton != null)
                DealerButton.SetActive(false);
        }



        private Coroutine chipCountRoutine;

        public int GetCurrentChips()
        {
            if (currentPlayer != null)
                return currentPlayer.Chips;

            if (Player_Chips != null && int.TryParse(Player_Chips.text, out int value))
                return value;

            return 0;
        }

        public void AnimateWinnerChips(int finalChips, float duration = 0.9f)
        {
            if (winChipRoutine != null)
                StopCoroutine(winChipRoutine);

            winChipRoutine = StartCoroutine(AnimateWinnerChipsRoutine(finalChips, duration));
        }

        private IEnumerator AnimateWinnerChipsRoutine(int finalChips, float duration)
        {
            chipTextLockedForWinAnimation = true;

            int startChips = 0;

            if (Player_Chips != null)
                int.TryParse(Player_Chips.text, out startChips);

            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;

                float t = Mathf.Clamp01(timer / duration);
                int value = Mathf.RoundToInt(Mathf.Lerp(startChips, finalChips, t));

                if (Player_Chips != null)
                    Player_Chips.text = value.ToString();

                yield return null;
            }

            if (Player_Chips != null)
                Player_Chips.text = finalChips.ToString();

            if (currentPlayer != null)
                currentPlayer.Chips = finalChips;

            chipTextLockedForWinAnimation = false;
        }

        public void ShowSmallBlind()
        {
            if (SmallBlind != null)
                SmallBlind.SetActive(true);
        }

        public void HideSmallBlind()
        {
            if (SmallBlind != null)
                SmallBlind.SetActive(false);
        }

        public void ShowBigBlind()
        {
            if (BigBling != null)
                BigBling.SetActive(true);
        }

        public void HideBigBlind()
        {
            if (BigBling != null)
                BigBling.SetActive(false);
        }

        public void HighlightPrivateCards(List<string> playerCards, List<string> highlightCards)
        {
            for (int i = 0; i < PrivateCardHighlightImages.Count; i++)
            {
                if (PrivateCardHighlightImages[i] == null)
                    continue;

                bool active =
                    playerCards != null &&
                    highlightCards != null &&
                    i < playerCards.Count &&
                    highlightCards.Contains(playerCards[i]);

                PrivateCardHighlightImages[i].gameObject.SetActive(active);
            }
        }

        public void ClearPrivateCardHighlights()
        {
            foreach (var img in PrivateCardHighlightImages)
            {
                if (img != null)
                    img.gameObject.SetActive(false);
            }
        }


        private string FormatActionText(string action)
        {
            if (string.IsNullOrEmpty(action))
                return "";

            return action.Replace("All_in", "All In")
                         .Replace("all_in", "All In")
                         .Replace("ALL_IN", "All In");
        }

    }
}