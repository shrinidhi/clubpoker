using System;
using UnityEngine;

namespace ClubPoker.Theme
{
    /// <summary>
    /// Owns the player's applied table skin + card deck and broadcasts changes.
    /// Views never read PlayerPrefs or the catalog directly — they read
    /// <see cref="CurrentTable"/> / <see cref="CurrentDeck"/> and subscribe to
    /// <see cref="OnThemeApplied"/>.
    ///
    /// Selection is two-stage so the popup's preview can update live while the
    /// table underneath stays untouched until Confirm:
    ///   Preview*(x)  → changes the pending selection only
    ///   Apply()      → commits pending, persists, fires OnThemeApplied
    ///   Discard()    → drops pending (popup closed with X)
    /// </summary>
    public class ThemeManager
    {
        private const string CATALOG_RESOURCE_PATH = "Theme/ThemeCatalog";

        private const string KEY_THEME = "theme.selected.theme";
        private const string KEY_TABLE = "theme.selected.table";
        private const string KEY_DECK  = "theme.selected.deck";

        private static ThemeManager _instance;

        public static ThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ThemeManager();
                    _instance.Initialize();
                }

                return _instance;
            }
        }

        /// <summary>Fires after Apply() commits, and once after first load.</summary>
        public event Action OnThemeApplied;

        /// <summary>Fires on every pending change — popup preview listens to this.</summary>
        public event Action OnPreviewChanged;

        public ThemeCatalogSO Catalog { get; private set; }

        public ThemeSO CurrentTheme { get; private set; }
        public TableSkinSO CurrentTable { get; private set; }
        public CardDeckSO CurrentDeck { get; private set; }

        public ThemeSO PendingTheme { get; private set; }
        public TableSkinSO PendingTable { get; private set; }
        public CardDeckSO PendingDeck { get; private set; }

        public bool HasUnappliedChanges =>
            PendingTable != CurrentTable || PendingDeck != CurrentDeck;

        // ── Bootstrap ─────────────────────────────────────────────────────────

        private void Initialize()
        {
            Catalog = Resources.Load<ThemeCatalogSO>(CATALOG_RESOURCE_PATH);

            if (Catalog == null)
            {
                Debug.LogError(
                    $"[ThemeManager] No ThemeCatalog at Resources/{CATALOG_RESOURCE_PATH}. " +
                    "Themes disabled; scene sprites stay as authored.");
                return;
            }

            LoadSaved();
            ResetPending();

            OnThemeApplied?.Invoke();
        }

        private void LoadSaved()
        {
            ThemeSO theme = Catalog.FindTheme(PlayerPrefs.GetString(KEY_THEME, ""))
                            ?? Catalog.DefaultTheme;

            CurrentTable = Catalog.FindTable(PlayerPrefs.GetString(KEY_TABLE, ""))
                           ?? theme?.Table;

            CurrentDeck = Catalog.FindDeck(PlayerPrefs.GetString(KEY_DECK, ""))
                          ?? theme?.Deck;

            // The pair is the source of truth, not the stored theme id: a player who
            // last saved a custom table/deck mix must load as custom (no preset
            // ticked), not as whichever theme the id or the default pointed at.
            CurrentTheme = MatchTheme(CurrentTable, CurrentDeck);
        }

        // ── Pending selection (popup) ─────────────────────────────────────────

        public void ResetPending()
        {
            PendingTheme = CurrentTheme;
            PendingTable = CurrentTable;
            PendingDeck  = CurrentDeck;

            OnPreviewChanged?.Invoke();
        }

        /// <summary>Themes tab: a preset sets both halves at once.</summary>
        public void PreviewTheme(ThemeSO theme)
        {
            if (theme == null)
                return;

            PendingTheme = theme;

            if (theme.Table != null) PendingTable = theme.Table;
            if (theme.Deck  != null) PendingDeck  = theme.Deck;

            OnPreviewChanged?.Invoke();
        }

        /// <summary>Table tab: overrides the table half only.</summary>
        public void PreviewTable(TableSkinSO table)
        {
            if (table == null)
                return;

            PendingTable = table;
            PendingTheme = MatchTheme(PendingTable, PendingDeck);

            OnPreviewChanged?.Invoke();
        }

        /// <summary>Cards tab: overrides the deck half only.</summary>
        public void PreviewDeck(CardDeckSO deck)
        {
            if (deck == null)
                return;

            PendingDeck = deck;
            PendingTheme = MatchTheme(PendingTable, PendingDeck);

            OnPreviewChanged?.Invoke();
        }

        /// <summary>Null when the pending pair is a custom mix, not a shipped preset.</summary>
        private ThemeSO MatchTheme(TableSkinSO table, CardDeckSO deck)
        {
            if (Catalog == null)
                return null;

            foreach (ThemeSO theme in Catalog.Themes)
                if (theme != null && theme.Table == table && theme.Deck == deck)
                    return theme;

            return null;
        }

        // ── Commit ────────────────────────────────────────────────────────────

        public void Apply()
        {
            CurrentTheme = PendingTheme;
            CurrentTable = PendingTable;
            CurrentDeck  = PendingDeck;

            Save();

            OnThemeApplied?.Invoke();
        }

        public void Discard()
        {
            ResetPending();
        }

        private void Save()
        {
            PlayerPrefs.SetString(KEY_THEME, CurrentTheme != null ? CurrentTheme.ThemeId : "");
            PlayerPrefs.SetString(KEY_TABLE, CurrentTable != null ? CurrentTable.TableId : "");
            PlayerPrefs.SetString(KEY_DECK,  CurrentDeck  != null ? CurrentDeck.DeckId   : "");
            PlayerPrefs.Save();
        }

        // ── Convenience for card views ────────────────────────────────────────

        public Sprite GetCardFace(string cardValue)
        {
            return CurrentDeck != null ? CurrentDeck.GetFace(cardValue) : null;
        }

        public Sprite GetCardBack()
        {
            return CurrentDeck != null ? CurrentDeck.CardBackSprite : null;
        }
    }
}
