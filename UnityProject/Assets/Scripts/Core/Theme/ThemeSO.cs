using UnityEngine;

namespace ClubPoker.Theme
{
    /// <summary>
    /// A theme is a preset pairing of one table skin + one card deck.
    /// Picking a theme in the Themes tab sets both; the Table and Cards tabs can
    /// then override either half independently (which makes the selection custom).
    /// </summary>
    [CreateAssetMenu(fileName = "Theme_", menuName = "ClubPoker/Theme/Theme")]
    public class ThemeSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id persisted in PlayerPrefs / sent to backend. Never rename after ship.")]
        public string ThemeId;
        public string DisplayName;

        [Tooltip("Thumbnail shown in the Themes tab grid.")]
        public Sprite PreviewSprite;

        [Header("Preset")]
        public TableSkinSO Table;
        public CardDeckSO Deck;

        [Header("Unlock")]
        public bool OwnedByDefault = true;
    }
}
