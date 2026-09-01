using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Theme
{
    /// <summary>
    /// The Theme Settings popup: three tabs over one shared preview pane.
    /// Selecting a thumbnail only moves ThemeManager's pending selection; Confirm
    /// commits it, X discards it.
    /// </summary>
    public class ThemeSettingsPopup : MonoBehaviour
    {
        private enum Tab { Themes, Table, Cards }

        [Header("Preview pane")]
        [Tooltip("ThemedTableView on the preview art. Must have FollowAppliedTheme = false.")]
        public ThemedTableView PreviewTable;

        [Tooltip("The A-K-Q-J-10 strip under the preview table. FollowAppliedTheme = false.")]
        public List<ThemedCardImage> PreviewCards = new List<ThemedCardImage>();

        [Tooltip("Card values for the strip, in order.")]
        public List<string> PreviewCardValues =
            new List<string> { "AS", "KH", "QC", "JD", "TS" };

        [Header("Tabs")]
        public Button ThemesTabButton;
        public Button TableTabButton;
        public Button CardsTabButton;

        [Tooltip("Shared tab plate sprites. Swapped on the tab Button's own Image. " +
                 "Leave empty to skip sprite swapping.")]
        public Sprite TabActiveSprite;
        public Sprite TabInactiveSprite;

        [Header("Grid")]
        public RectTransform GridContent;
        public ThemeOptionCell OptionCellPrefab;

        [Header("Buttons")]
        public Button ConfirmButton;
        public Button CloseButton;

        private readonly List<ThemeOptionCell> _cells = new List<ThemeOptionCell>();
        private Tab _tab = Tab.Themes;

        private ThemeManager Theme => ThemeManager.Instance;

        private void Awake()
        {
            if (ThemesTabButton != null) ThemesTabButton.onClick.AddListener(() => SwitchTab(Tab.Themes));
            if (TableTabButton  != null) TableTabButton.onClick.AddListener(() => SwitchTab(Tab.Table));
            if (CardsTabButton  != null) CardsTabButton.onClick.AddListener(() => SwitchTab(Tab.Cards));

            if (ConfirmButton != null) ConfirmButton.onClick.AddListener(Confirm);
            if (CloseButton   != null) CloseButton.onClick.AddListener(Close);

            for (int i = 0; i < PreviewCards.Count; i++)
            {
                if (PreviewCards[i] == null)
                    continue;

                PreviewCards[i].FollowAppliedTheme = false;
                PreviewCards[i].CardValue =
                    i < PreviewCardValues.Count ? PreviewCardValues[i] : string.Empty;
            }

            if (PreviewTable != null)
                PreviewTable.FollowAppliedTheme = false;
        }

        private void OnEnable()
        {
            Theme.OnPreviewChanged += RefreshPreview;
            Theme.ResetPending();

            SwitchTab(Tab.Themes);
        }

        private void OnDisable()
        {
            Theme.OnPreviewChanged -= RefreshPreview;
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        private void SwitchTab(Tab tab)
        {
            _tab = tab;

            SetTabSprite(ThemesTabButton, tab == Tab.Themes);
            SetTabSprite(TableTabButton,  tab == Tab.Table);
            SetTabSprite(CardsTabButton,  tab == Tab.Cards);

            BuildGrid();
            RefreshPreview();
        }

        /// <summary>
        /// Swaps the tab plate on the button's own Image. Skipped entirely when the
        /// sprites are unassigned, so an overlay-based design is unaffected.
        /// </summary>
        private void SetTabSprite(Button tab, bool active)
        {
            if (tab == null || TabActiveSprite == null || TabInactiveSprite == null)
                return;

            Image plate = tab.image != null ? tab.image : tab.GetComponent<Image>();

            if (plate != null)
                plate.sprite = active ? TabActiveSprite : TabInactiveSprite;
        }

        // ── Grid ──────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            ClearGrid();

            ThemeCatalogSO catalog = Theme.Catalog;

            if (catalog == null || OptionCellPrefab == null || GridContent == null)
                return;

            switch (_tab)
            {
                case Tab.Themes:
                    foreach (ThemeSO theme in catalog.Themes)
                    {
                        if (theme == null) continue;

                        ThemeSO captured = theme;
                        Spawn(theme.PreviewSprite, !theme.OwnedByDefault,
                              () => Theme.PreviewTheme(captured));
                    }
                    break;

                case Tab.Table:
                    foreach (TableSkinSO table in catalog.Tables)
                    {
                        if (table == null) continue;

                        TableSkinSO captured = table;
                        Spawn(table.PreviewSprite, !table.OwnedByDefault,
                              () => Theme.PreviewTable(captured));
                    }
                    break;

                case Tab.Cards:
                    foreach (CardDeckSO deck in catalog.Decks)
                    {
                        if (deck == null) continue;

                        CardDeckSO captured = deck;
                        Spawn(deck.PreviewSprite, !deck.OwnedByDefault,
                              () => Theme.PreviewDeck(captured));
                    }
                    break;
            }

            RefreshCellStates();
        }

        private void Spawn(Sprite preview, bool locked, System.Action onClick)
        {
            ThemeOptionCell cell = Instantiate(OptionCellPrefab, GridContent);
            cell.Bind(preview, locked, onClick);
            _cells.Add(cell);
        }

        private void ClearGrid()
        {
            foreach (ThemeOptionCell cell in _cells)
                if (cell != null)
                    Destroy(cell.gameObject);

            _cells.Clear();
        }

        /// <summary>
        /// Cell order matches the catalog list order for the active tab, so index
        /// maps straight back to the option it was built from.
        /// </summary>
        private void RefreshCellStates()
        {
            ThemeCatalogSO catalog = Theme.Catalog;

            if (catalog == null)
                return;

            for (int i = 0; i < _cells.Count; i++)
            {
                bool selected = false;
                bool applied = false;

                switch (_tab)
                {
                    case Tab.Themes:
                        if (i < catalog.Themes.Count)
                        {
                            selected = catalog.Themes[i] == Theme.PendingTheme;
                            applied  = catalog.Themes[i] == Theme.CurrentTheme;
                        }
                        break;

                    case Tab.Table:
                        if (i < catalog.Tables.Count)
                        {
                            selected = catalog.Tables[i] == Theme.PendingTable;
                            applied  = catalog.Tables[i] == Theme.CurrentTable;
                        }
                        break;

                    case Tab.Cards:
                        if (i < catalog.Decks.Count)
                        {
                            selected = catalog.Decks[i] == Theme.PendingDeck;
                            applied  = catalog.Decks[i] == Theme.CurrentDeck;
                        }
                        break;
                }

                _cells[i].SetState(selected, applied);
            }
        }

        // ── Preview ───────────────────────────────────────────────────────────

        private void RefreshPreview()
        {
            if (PreviewTable != null)
                PreviewTable.PreviewSkin = Theme.PendingTable;

            foreach (ThemedCardImage card in PreviewCards)
                if (card != null)
                    card.PreviewDeck = Theme.PendingDeck;

            RefreshCellStates();
        }

        // ── Buttons ───────────────────────────────────────────────────────────

        private void Confirm()
        {
            Theme.Apply();
            RefreshCellStates();
            gameObject.SetActive(false);
        }

        private void Close()
        {
            Theme.Discard();
            gameObject.SetActive(false);
        }
    }
}
