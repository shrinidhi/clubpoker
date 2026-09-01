using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Theme
{
    /// <summary>
    /// Single registry of everything selectable in the Theme Settings popup.
    /// Lives at Assets/Resources/Theme/ThemeCatalog.asset so ThemeManager can load
    /// it without scene wiring.
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeCatalog", menuName = "ClubPoker/Theme/Theme Catalog")]
    public class ThemeCatalogSO : ScriptableObject
    {
        [Header("Themes tab")]
        public List<ThemeSO> Themes = new List<ThemeSO>();

        [Header("Table tab")]
        public List<TableSkinSO> Tables = new List<TableSkinSO>();

        [Header("Cards tab")]
        public List<CardDeckSO> Decks = new List<CardDeckSO>();

        [Header("Fallback used when nothing is saved yet")]
        public ThemeSO DefaultTheme;

        public ThemeSO FindTheme(string id)
        {
            foreach (ThemeSO t in Themes)
                if (t != null && t.ThemeId == id) return t;
            return null;
        }

        public TableSkinSO FindTable(string id)
        {
            foreach (TableSkinSO t in Tables)
                if (t != null && t.TableId == id) return t;
            return null;
        }

        public CardDeckSO FindDeck(string id)
        {
            foreach (CardDeckSO d in Decks)
                if (d != null && d.DeckId == id) return d;
            return null;
        }

        /// <summary>
        /// Pulls the table/deck referenced by a theme into the flat tab lists, so a
        /// theme preset never points at an option the Table/Cards tabs cannot show.
        /// </summary>
        private void OnValidate()
        {
            foreach (ThemeSO theme in Themes)
            {
                if (theme == null) continue;

                if (theme.Table != null && !Tables.Contains(theme.Table))
                    Tables.Add(theme.Table);

                if (theme.Deck != null && !Decks.Contains(theme.Deck))
                    Decks.Add(theme.Deck);
            }
        }
    }
}
