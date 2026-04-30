using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game { 
public class PlayerCardHandUI : MonoBehaviour
    {
        public static PlayerCardHandUI Instance { get; private set; }

        [Header("My Private Card Images")]
        public List<Image> MyCardImages = new List<Image>();

        [Header("Card Back Sprite")]
        public Sprite CardBackSprite;

        [Header("All Card Face Sprites")]
        public List<CardSpriteData> CardSprites = new List<CardSpriteData>();

        private Dictionary<string, Sprite> _cardLookup =
            new Dictionary<string, Sprite>();

        private void Awake()
        {
            Instance = this;
            PrepareCardLookup();
            HideAllCards();
        }

        private void PrepareCardLookup()
        {
            _cardLookup.Clear();

            foreach (var item in CardSprites)
            {
                if (item == null)
                    continue;

                if (string.IsNullOrEmpty(item.CardName))
                    continue;

                if (item.CardSprite == null)
                    continue;

                if (!_cardLookup.ContainsKey(item.CardName))
                {
                    _cardLookup.Add(
                        item.CardName,
                        item.CardSprite
                    );
                }
            }
        }

        /// <summary>
        /// Server se private cards aaye
        /// Example:
        /// A♠, K♥
        /// </summary>
        public void PlayDealAnimation(List<string> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                Debug.LogError("[PlayerHandUI] Cards NULL");
                return;
            }

            StopAllCoroutines();
            StartCoroutine(DealCardsCoroutine(cards));
        }

        private IEnumerator DealCardsCoroutine(List<string> cards)
        {
            HideAllCards();

            for (int i = 0; i < MyCardImages.Count; i++)
            {
                if (i >= cards.Count)
                    break;

                Image cardImage = MyCardImages[i];

                if (cardImage == null)
                    continue;

                // Step 1 → first show card back
                cardImage.gameObject.SetActive(true);
                cardImage.sprite = CardBackSprite;
                cardImage.transform.localScale = Vector3.zero;

                // pop animation
                float t = 0f;
                while (t < 0.15f)
                {
                    t += Time.deltaTime;
                    float scale = Mathf.Lerp(0f, 1f, t / 0.15f);
                    cardImage.transform.localScale =
                        new Vector3(scale, scale, scale);
                    yield return null;
                }

                cardImage.transform.localScale = Vector3.one;

                yield return new WaitForSeconds(0.15f);

                // Step 2 → flip to real face card
                string cardName = cards[i];

                if (_cardLookup.ContainsKey(cardName))
                {
                    cardImage.sprite = _cardLookup[cardName];
                }
                else
                {
                    Debug.LogWarning(
                        $"[PlayerHandUI] Sprite missing for card: {cardName}"
                    );
                }

                yield return new WaitForSeconds(0.10f);
            }

            Debug.Log(
                $"[PlayerHandUI] Deal animation complete ({cards.Count} cards)"
            );
        }

        public void HideAllCards()
        {
            foreach (var img in MyCardImages)
            {
                if (img == null)
                    continue;

                img.gameObject.SetActive(false);
                img.transform.localScale = Vector3.one;
            }
        }
    }

    [System.Serializable]
    public class CardSpriteData
    {
        public string CardName;
        public Sprite CardSprite;
    }
}