using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        public Text DisconnectedCountdownText;
        public GameObject SittingOutPanel;
        public CanvasGroup PlayerCanvasGroup;

        private GamePlayer currentPlayer;
        private Coroutine disconnectRoutine;


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
        private Coroutine timerRoutine;
        private bool chipTextLockedForWinAnimation = false;
        private Coroutine winChipRoutine;
        private Coroutine winnerCardRoutine;

        public GameObject BigBling;
        public GameObject SmallBlind;
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

        private IEnumerator ShowWinnerCardsRoutine(List<string> cards, float duration)
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

                string key = ConvertCardKey(cards[i]);

                PrivateCardImages[i].gameObject.SetActive(true);

                PrivateCardImages[i].sprite =
                    cardLookup.TryGetValue(key, out Sprite sprite)
                    ? sprite
                    : CardBackSprite;
            }

            yield return new WaitForSeconds(duration);
            ClearPrivateCardHighlights();
            for (int i = 0; i < PrivateCardImages.Count; i++)
            {
                if (PrivateCardImages[i] == null)
                    continue;

                PrivateCardImages[i].sprite = CardBackSprite;
            }
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

                float t = 0f;
                while (t < 0.15f)
                {
                    t += Time.deltaTime;
                    float s = Mathf.Lerp(0f, 1f, t / 0.15f);
                    img.transform.localScale = new Vector3(s, s, s);
                    yield return null;
                }

                img.transform.localScale = Vector3.one;

                yield return new WaitForSeconds(0.15f);

                string key = ConvertCardKey(cards[i]);

                img.sprite = cardLookup.TryGetValue(key, out Sprite sprite)
                    ? sprite
                    : CardBackSprite;

                yield return new WaitForSeconds(0.10f);
            }
        }

        public void ShowCardBacks(int count)
        {
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
        bool isFirstBind =true;
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

            if (Player_Name != null)
                Player_Name.text = player.Username;
            if (Player_Chips != null && !chipTextLockedForWinAnimation)
            {
              //  Player_Chips.text = player.Chips.ToString();
               // Debug.Log("WinnerCoin : " + player.Chips);
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
                BattingAction_Text.text = player.LastAction;

            UpdateActionBG(player.LastAction);

            HideDisconnected();
            HideSittingOut();

           

            Debug.Log($"[PlayerProfile] Bound prefab -> {player.Username} | Seat: {player.Seat}");
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

        public void UpdateAction(string action)
        {
            if (BattingAction_Text != null)
                BattingAction_Text.text = string.IsNullOrEmpty(action) ? "" : action;

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

        public void ShowDisconnected(int seconds)
        {
            if (DisconnectedPanel != null)
                DisconnectedPanel.SetActive(true);

            if (disconnectRoutine != null)
                StopCoroutine(disconnectRoutine);

            disconnectRoutine = StartCoroutine(DisconnectedCountdown(seconds));
        }

        public void HideDisconnected()
        {
            if (disconnectRoutine != null)
            {
                StopCoroutine(disconnectRoutine);
                disconnectRoutine = null;
            }

            if (DisconnectedPanel != null)
                DisconnectedPanel.SetActive(false);

            if (DisconnectedCountdownText != null)
                DisconnectedCountdownText.text = "";
        }

        private IEnumerator DisconnectedCountdown(int seconds)
        {
            int remaining = seconds;

            while (remaining >= 0)
            {
                if (DisconnectedCountdownText != null)
                    DisconnectedCountdownText.text = remaining + "s";

                yield return new WaitForSeconds(1f);
                remaining--;
            }
        }

        public void ShowSittingOut()
        {
            if (PlayerCanvasGroup != null)
            {
                PlayerCanvasGroup.alpha = 0.45f;
                PlayerCanvasGroup.interactable = false;
                PlayerCanvasGroup.blocksRaycasts = false;
            }

            if (SittingOutPanel != null)
                SittingOutPanel.SetActive(true);
        }

        public void HideSittingOut()
        {
            if (PlayerCanvasGroup != null)
            {
                PlayerCanvasGroup.alpha = 1f;
                PlayerCanvasGroup.interactable = true;
                PlayerCanvasGroup.blocksRaycasts = true;
            }

            if (SittingOutPanel != null)
                SittingOutPanel.SetActive(false);
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

    }
}