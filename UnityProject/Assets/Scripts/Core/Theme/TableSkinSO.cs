using UnityEngine;

namespace ClubPoker.Theme
{
    /// <summary>
    /// One selectable table skin. Every sprite is optional — a skin that leaves a
    /// slot empty keeps whatever the scene already has.
    /// </summary>
    [CreateAssetMenu(fileName = "TableSkin_", menuName = "ClubPoker/Theme/Table Skin")]
    public class TableSkinSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id persisted in PlayerPrefs / sent to backend. Never rename after ship.")]
        public string TableId;
        public string DisplayName;

        [Tooltip("Thumbnail shown in the Table tab grid.")]
        public Sprite PreviewSprite;

        [Tooltip("Big pre-rendered shot of room background + table, drawn in the " +
                 "popup's preview pane. The card strip is overlaid live on top, so " +
                 "this image must not include cards. Falls back to TableSprite.")]
        public Sprite LargePreviewSprite;

        [Header("Sprites")]
        public Sprite RoomBackgroundSprite;
        public Sprite TableSprite;
        public Sprite DealerButtonSprite;
        public Sprite SeatFrameSprite;

        [Header("Tint")]
        public bool OverrideFeltTint;
        public Color FeltTint = Color.white;

        [Header("Unlock")]
        public bool OwnedByDefault = true;
    }
}
