
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        public float FlipDuration = 0.35f;
        public float StaggerDelay = 0.20f;

        private readonly List<GameObject> spawnedCards = new List<GameObject>();

        private void Awake()
        {
            Instance = this;
        }

        public void ShowCommunityCards(List<string> newCards, string street)
        {
            StartCoroutine(FlipCardsRoutine(newCards, street));
        }

        private IEnumerator FlipCardsRoutine(List<string> newCards, string street)
        {
            if (newCards == null || newCards.Count == 0)
                yield break;

            int existingCount = spawnedCards.Count;

            // Exception:
            // if cards.length > existing board length
            // means server sent full board sync
            if (newCards.Count > existingCount)
            {
                ClearBoard();
                existingCount = 0;
            }

            for (int i = 0; i < newCards.Count; i++)
            {
                int targetIndex = existingCount + i;

                if (targetIndex >= CardSlots.Count)
                    yield break;

                GameObject card =
                    Instantiate(
                        CardPrefab,
                        CardSlots[targetIndex].position,
                        Quaternion.identity,
                        CardSlots[targetIndex]
                    );

                spawnedCards.Add(card);

                CardFlipPrefab flip = card.GetComponent<CardFlipPrefab>();

                if (flip != null)
                {
                    flip.SetCardBack();

                    yield return new WaitForSeconds(StaggerDelay);

                    flip.PlayFlip(newCards[i]);

                  //  if (SoundManager.Instance != null)
                      //  SoundManager.Instance.PlayCardFlip();
                }
                else
                {
                    Debug.LogWarning("CardFlipUI missing on CardPrefab");
                }
            }

            // Best hand recalculate
           if (BestHandCalculator.Instance != null)
            {
              BestHandCalculator.Instance.Recalculate();
            }

            Debug.Log(
                $"[CommunityCardsUI] {street} cards shown successfully"
            );
        }

        public void ClearBoard()
        {
            for (int i = 0; i < spawnedCards.Count; i++)
            {
                if (spawnedCards[i] != null)
                    Destroy(spawnedCards[i]);
            }

            spawnedCards.Clear();
        }
    }
}