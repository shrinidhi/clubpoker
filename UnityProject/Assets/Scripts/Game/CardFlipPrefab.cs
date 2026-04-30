using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class CardFlipPrefab : MonoBehaviour
    {
        public Image CardFrontImage;
        public Sprite CardBackSprite;

        private string currentCard;

        public void SetCardBack()
        {
            if (CardFrontImage != null)
            {
                CardFrontImage.sprite = CardBackSprite;
            }

            transform.localScale = Vector3.one;
        }

        public void PlayFlip(string cardValue)
        {
            currentCard = cardValue;
            StartCoroutine(FlipAnimation());
        }

        private IEnumerator FlipAnimation()
        {
            float duration = 0.15f;
            float timer = 0f;

            // shrink
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float scaleX = Mathf.Lerp(1f, 0f, timer / duration);
                transform.localScale = new Vector3(scaleX, 1f, 1f);
                yield return null;
            }

            SetCardFront(currentCard);

            timer = 0f;

            // expand
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
            Debug.Log($"Flip Complete → {cardValue}");
        }
    }
}
