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

        private void Awake()
        {
            Instance = this;
        }

        public void ShowCommunityCards(List<string> newCards, string street)
        {
            if (newCards == null || newCards.Count == 0)
                return;

            StopAllCoroutines();
            StartCoroutine(FlipCardsRoutine(newCards, street));
        }

        private IEnumerator FlipCardsRoutine(List<string> newCards, string street)
        {
            ClearBoard();

            for (int i = 0; i < newCards.Count; i++)
            {
                if (i >= CardSlots.Count)
                    yield break;

                
                CardSlots[i].gameObject.SetActive(true);

                GameObject card = Instantiate(CardPrefab, CardSlots[i]);
                card.transform.localPosition = Vector3.zero;
                card.transform.localRotation = Quaternion.identity;
                card.transform.localScale = Vector3.one;
                card.SetActive(true);

                spawnedCards.Add(card);

                CardFlipPrefab flip = card.GetComponent<CardFlipPrefab>();

                if (flip != null)
                {
                    flip.SetCardBack();

                    yield return new WaitForSeconds(StaggerDelay);

                    flip.PlayFlip(newCards[i]);
                }
                else
                {
                    Debug.LogWarning("[CommunityCardsUI] CardFlipPrefab missing on CardPrefab");
                }
            }

            if (BestHandCalculator.Instance != null)
                BestHandCalculator.Instance.Recalculate();

            Debug.Log($"[CommunityCardsUI] {street} cards shown successfully");
        }

        public void ClearBoard()
        {
            foreach (GameObject card in spawnedCards)
            {
                if (card != null)
                    Destroy(card);
            }

            spawnedCards.Clear();

            for (int i = 0; i < CardSlots.Count; i++)
            {
                CardSlots[i].gameObject.SetActive(false);
            }
        }
    }
}