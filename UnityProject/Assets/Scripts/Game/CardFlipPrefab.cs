using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using ClubPoker.Theme;

namespace ClubPoker.Game
{
    public class CardFlipPrefab : MonoBehaviour
    {
        public Image CardFrontImage;
        public Sprite CardBackSprite;
        public List<CardSpriteData> CardSprites = new List<CardSpriteData>();

        private Dictionary<string, Sprite> _lookup = new Dictionary<string, Sprite>();
        private string currentCard;
        private Coroutine flipCoroutine;
        [Header("Highlight")]
        public Image HighlightImage;

        public string CurrentCardValue { get; private set; }
        private void Awake()
        {
            PrepareLookup();
        }

        private void OnEnable()
        {
            ThemeManager.Instance.OnThemeApplied += Repaint;
        }

        private void OnDisable()
        {
            ThemeManager.Instance.OnThemeApplied -= Repaint;
        }

        /// <summary>
        /// Deck changed mid-hand — restamp whatever this card already shows, with no
        /// flip animation. CurrentCardValue is empty while the card is face-down.
        /// </summary>
        private void Repaint()
        {
            if (CardFrontImage == null)
                return;

            if (string.IsNullOrEmpty(CurrentCardValue))
            {
                CardFrontImage.sprite = ResolveBack();
                return;
            }

            Sprite sprite = ResolveFace(CurrentCardValue);

            if (sprite != null)
                CardFrontImage.sprite = sprite;
        }

        private void PrepareLookup()
        {
            _lookup.Clear();

            foreach (var item in CardSprites)
            {
                if (item == null || string.IsNullOrEmpty(item.CardName) || item.CardSprite == null)
                    continue;

                if (!_lookup.ContainsKey(item.CardName.ToUpper()))
                    _lookup.Add(item.CardName.ToUpper(), item.CardSprite);
            }
        }
        public void SetHighlight(bool active)
        {
            if (HighlightImage != null)
                HighlightImage.gameObject.SetActive(active);
        }
        /// <summary>
        /// Applied deck wins; the inspector list stays as the fallback for prefabs
        /// not migrated to the theme catalog yet.
        /// </summary>
        private Sprite ResolveBack()
        {
            return ThemeManager.Instance.GetCardBack() ?? CardBackSprite;
        }

        private Sprite ResolveFace(string cardValue)
        {
            CardDeckSO deck = ThemeManager.Instance.CurrentDeck;

            if (deck != null && deck.HasFace(cardValue))
                return deck.GetFace(cardValue);

            return _lookup.TryGetValue(ConvertCardKey(cardValue), out Sprite sprite)
                ? sprite
                : null;
        }

        public void SetCardBack()
        {
            if (CardFrontImage != null)
                CardFrontImage.sprite = ResolveBack();

            CurrentCardValue = "";
            SetHighlight(false);

            transform.localScale = Vector3.one;
        }

        public void PlayFlip(string cardValue)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[CardFlip] Cannot flip because card or parent is inactive.");
                return;
            }

            currentCard = cardValue;

            if (flipCoroutine != null)
                StopCoroutine(flipCoroutine);

            flipCoroutine = StartCoroutine(FlipAnimation());
        }

        private IEnumerator FlipAnimation()
        {
            float duration = 0.15f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float scaleX = Mathf.Lerp(1f, 0f, timer / duration);
                transform.localScale = new Vector3(scaleX, 1f, 1f);
                yield return null;
            }

            SetCardFront(currentCard);

            timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float scaleX = Mathf.Lerp(0f, 1f, timer / duration);
                transform.localScale = new Vector3(scaleX, 1f, 1f);
                yield return null;
            }

            transform.localScale = Vector3.one;
        }

        private void SetCardFront(string cardValue)
        {
            CurrentCardValue = cardValue;

            Sprite sprite = ResolveFace(cardValue);

            if (sprite != null)
                CardFrontImage.sprite = sprite;
            else
            {
                Debug.LogWarning($"[CardFlip] Sprite missing: {ConvertCardKey(cardValue)}");
                CardFrontImage.sprite = ResolveBack();
            }
        }

        private string ConvertCardKey(string serverCard)
        {
            if (string.IsNullOrEmpty(serverCard))
                return "";

            return serverCard
                .Replace("♥", "H")
                .Replace("♦", "D")
                .Replace("♣", "C")
                .Replace("♠", "S")
                .ToUpper();
        }
    }
}