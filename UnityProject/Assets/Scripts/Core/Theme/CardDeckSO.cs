using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Theme
{
    /// <summary>
    /// One selectable card skin: 52 faces + a back. Replaces the per-prefab
    /// inspector sprite lists so a deck can be swapped at runtime in one place.
    /// </summary>
    [CreateAssetMenu(fileName = "CardDeck_", menuName = "ClubPoker/Theme/Card Deck")]
    public class CardDeckSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id persisted in PlayerPrefs / sent to backend. Never rename after ship.")]
        public string DeckId;
        public string DisplayName;

        [Tooltip("Thumbnail shown in the Cards tab grid.")]
        public Sprite PreviewSprite;

        [Header("Sprites")]
        public Sprite CardBackSprite;

        [Tooltip("52 faces. CardName accepts 'AS', 'A♠' or 'As' — normalized on load.")]
        public List<CardFaceEntry> Faces = new List<CardFaceEntry>();

        [Header("Unlock")]
        public bool OwnedByDefault = true;

        private Dictionary<string, Sprite> _lookup;

        public Sprite GetFace(string cardValue)
        {
            BuildLookup();

            string key = CardKey.Normalize(cardValue);

            if (string.IsNullOrEmpty(key))
                return CardBackSprite;

            return _lookup.TryGetValue(key, out Sprite sprite) ? sprite : CardBackSprite;
        }

        public bool HasFace(string cardValue)
        {
            BuildLookup();
            return _lookup.ContainsKey(CardKey.Normalize(cardValue));
        }

        private void BuildLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, Sprite>(64);

            foreach (CardFaceEntry entry in Faces)
            {
                if (entry == null || entry.CardSprite == null)
                    continue;

                string key = CardKey.Normalize(entry.CardName);

                if (string.IsNullOrEmpty(key))
                    continue;

                _lookup[key] = entry.CardSprite;
            }
        }

        /// <summary>Editor-time edits invalidate the cache.</summary>
        private void OnValidate()
        {
            _lookup = null;
        }
    }

    [System.Serializable]
    public class CardFaceEntry
    {
        public string CardName;
        public Sprite CardSprite;
    }
}
