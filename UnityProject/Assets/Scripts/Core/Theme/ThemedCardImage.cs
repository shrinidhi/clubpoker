using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Theme
{
    /// <summary>
    /// Any Image that shows a card. Holds the card value, not a sprite, so a deck
    /// change repaints it for free. Use for static card rows (preview strips, hand
    /// history); animated flips keep their own scripts and just query ThemeManager.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ThemedCardImage : MonoBehaviour
    {
        [Tooltip("Card value, e.g. AS / A♠. Empty shows the card back.")]
        public string CardValue;

        [Tooltip("Off for preview instances inside the settings popup.")]
        public bool FollowAppliedTheme = true;

        private Image _image;
        private CardDeckSO _previewDeck;

        /// <summary>Non-null pins this image to a deck and ignores applied-theme changes.</summary>
        public CardDeckSO PreviewDeck
        {
            get => _previewDeck;
            set
            {
                _previewDeck = value;
                Repaint();
            }
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (FollowAppliedTheme)
                ThemeManager.Instance.OnThemeApplied += Repaint;

            Repaint();
        }

        private void OnDisable()
        {
            if (FollowAppliedTheme)
                ThemeManager.Instance.OnThemeApplied -= Repaint;
        }

        public void SetCard(string cardValue)
        {
            CardValue = cardValue;
            Repaint();
        }

        public void ShowBack()
        {
            SetCard(string.Empty);
        }

        public void Repaint()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            CardDeckSO deck = _previewDeck
                              ?? (FollowAppliedTheme ? ThemeManager.Instance.CurrentDeck : null);

            if (deck == null)
                return;

            _image.sprite = string.IsNullOrEmpty(CardValue)
                ? deck.CardBackSprite
                : deck.GetFace(CardValue);
        }
    }
}
