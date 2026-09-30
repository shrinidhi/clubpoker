using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Game
{
    public class CommunityCardsUI : MonoBehaviour
    {
        public static CommunityCardsUI Instance;
        [Header("Card Slots (5 Total)")]
        public List<Transform> CardSlots = new List<Transform>();
        [Header("Card Prefab")]
        public GameObject CardPrefab;
        [Header("Animation Settings")]
        public float StaggerDelay = 0.20f;

        private readonly List<GameObject> spawnedCards = new List<GameObject>();
        private readonly List<string> displayedCards = new List<string>();
        private Coroutine boardAnimation;
        private bool runItActive;

        private void Awake() { Instance = this; }

        public void ShowCommunityCards(List<string> newCards, string street)
        {
            if (newCards == null || newCards.Count == 0 || runItActive) return;
            if (boardAnimation != null) StopCoroutine(boardAnimation);
            ClearCardsOnly();
            boardAnimation = StartCoroutine(FlipCardsRoutine(newCards, street));
        }

        private GameObject CreateCard(int index)
        {
            if (CardPrefab == null || index >= CardSlots.Count || CardSlots[index] == null)
            {
                Debug.LogWarning("[CommunityCardsUI] Assign CardPrefab and five valid CardSlots.");
                return null;
            }
            CardSlots[index].gameObject.SetActive(true);
            var card = Instantiate(CardPrefab, CardSlots[index]);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;
            card.SetActive(true);
            spawnedCards.Add(card);
            displayedCards.Add(null);
            return card;
        }

        private IEnumerator FlipCardsRoutine(List<string> newCards, string street)
        {
            for (int i = 0; i < newCards.Count && i < CardSlots.Count; i++)
            {
                var card = CreateCard(i);
                if (card == null) yield break;
                var flip = card.GetComponent<CardFlipPrefab>();
                if (flip != null)
                {
                    flip.SetCardBack();
                    yield return new WaitForSeconds(StaggerDelay);
                    flip.PlayFlip(newCards[i]);
                    displayedCards[i] = newCards[i];
                }
                else Debug.LogWarning("[CommunityCardsUI] CardFlipPrefab missing on CardPrefab");
            }
            if (BestHandCalculator.Instance != null) BestHandCalculator.Instance.Recalculate();
            Debug.Log($"[CommunityCardsUI] {street} cards shown successfully");
        }

        public void BeginRunIt(List<string> fixedCommunity)
        {
            if (boardAnimation != null) StopCoroutine(boardAnimation);
            boardAnimation = null;
            runItActive = true;
            // Existing shared cards stay visible. The first full board fills any
            // missing fixed cards if its earlier animation had not finished.
        }

        // The handler yields this routine so one board cannot interrupt another.
        public IEnumerator RenderRunItBoard(List<string> cards)
        {
            BeginRunIt(null);
            if (cards == null) yield break;
            for (int i = 0; i < cards.Count && i < CardSlots.Count; i++)
            {
                GameObject card = i < spawnedCards.Count ? spawnedCards[i] : CreateCard(i);
                if (card == null) yield break;
                if (displayedCards[i] == cards[i]) continue; // Preserve shared flop.
                var flip = card.GetComponent<CardFlipPrefab>();
                if (flip == null) continue;
                flip.SetCardBack();
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, StaggerDelay));
                flip.PlayFlip(cards[i]);
                displayedCards[i] = cards[i];
            }
            // Existing CardFlipPrefab uses two 0.15s animation phases.
            yield return new WaitForSecondsRealtime(0.35f);
        }

        private void ClearCardsOnly()
        {
            foreach (var card in spawnedCards) if (card != null) Destroy(card);
            spawnedCards.Clear(); displayedCards.Clear();
            foreach (var slot in CardSlots) if (slot != null) slot.gameObject.SetActive(false);
        }

        public void ClearBoard()
        {
            if (boardAnimation != null) StopCoroutine(boardAnimation);
            boardAnimation = null;
            runItActive = false;
            ClearCardsOnly();
        }

        public void HighlightCommunityCards(List<string> highlightCards)
        {
            if (highlightCards == null || highlightCards.Count == 0) return;
            foreach (var cardObj in spawnedCards)
            {
                if (cardObj == null) continue;
                var flip = cardObj.GetComponent<CardFlipPrefab>();
                if (flip != null) flip.SetHighlight(highlightCards.Contains(flip.CurrentCardValue));
            }
        }

        public void ClearHighlights()
        {
            foreach (var cardObj in spawnedCards)
            {
                if (cardObj == null) continue;
                var flip = cardObj.GetComponent<CardFlipPrefab>();
                if (flip != null) flip.SetHighlight(false);
            }
        }
    }
}
