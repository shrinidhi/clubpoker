using ClubPoker.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    /// <summary>
    /// In-game hamburger menu — a side drawer that slides in from the left with a
    /// dimmed backdrop. The hamburger button opens it; each option performs its
    /// action and closes. Tapping the dimmer closes it.
    ///
    /// The option list is not the same everywhere: a club table offers the full
    /// set, a lobby table only a subset. Which rows show is driven by
    /// <see cref="TableContext.Origin"/> — see <see cref="ApplyContext"/>.
    /// </summary>
    public class TableMenuController : MonoBehaviour
    {
        /// <summary>One menu row and the contexts it belongs to. Row is the whole
        /// line (icon + label + button), so hiding it collapses the layout instead
        /// of leaving a gap.</summary>
        [Serializable]
        public class MenuItem
        {
            public string     Id;              // label only, for readability in the inspector
            public GameObject Row;
            public bool       ShowInLobby = true;
            public bool       ShowInClub  = true;
        }

        /// <summary>A titled block of rows. The header hides itself when every row
        /// in the block is hidden, so a lobby table shows no empty sections.</summary>
        [Serializable]
        public class MenuGroup
        {
            public string         Name;
            public List<MenuItem> Items = new List<MenuItem>();
        }

        [Header("Toggle")]
        [SerializeField] private Button hamburgerButton;
        [SerializeField] private RectTransform drawerPanel; // the sliding drawer (left-anchored)
        [SerializeField] private GameObject dimmer;         // full-screen backdrop
        [SerializeField] private Button dimmerButton;
        // closes on tap-outside

        [Header("Options")]
        [SerializeField] private Button standUpButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button BacktoHomeButton;
        [SerializeField] private Button TopUpButton;
        [SerializeField] private Button HandHistoryButton;
        [SerializeField] private Button RealTimeButton;

        [Header("Options — club only")]
        [SerializeField] private Button withdrawButton;
        [SerializeField] private Button autoRebuyButton;
        [SerializeField] private Button sitOutButton;

        [Header("Options — table")]
        [SerializeField] private Button themeButton;
        [SerializeField] private Button tableSettingsButton;

        [Header("Visibility")]
        // Grouped rows, ticked per context in the inspector. Leave empty to fall
        // back to the built-in matrix below, which drives the buttons directly.
        [SerializeField] private List<MenuGroup> groups = new List<MenuGroup>();

        [Header("Slide")]
        [SerializeField] private float slideDuration = 0.25f;

        private float _openX;       // anchoredPosition.x when open
        private float _closedX;     // off-screen to the left
        private bool _isOpen;
        private bool _initialized;

        [Header("Panels to open")]
        [SerializeField] private GameObject WithdrawPanel;
        [SerializeField] private GameObject AutoRebuyPanel;
        [SerializeField] private GameObject ThemePanel;
        [SerializeField] private GameObject TableSettingsPanel;
        [SerializeField] private GameObject TopUpPanel;
        [SerializeField] private GameObject HandHistoryPanel;
        [SerializeField] private GameObject RealTimeResultPanel;

        [Header("Club seat")]
        [Tooltip("Opened on arrival at a club table, where the player lands as an " +
                 "observer and buys the seat here rather than on the club screen.")]
        [SerializeField] private ClubBuyInPanel clubBuyInPanel;


        // Put this controller on an ALWAYS-ACTIVE object (not the drawer itself),
        // so Start runs even though the drawer/dimmer start disabled in the inspector.
        private void Start()
        {
            if (hamburgerButton != null) hamburgerButton.onClick.AddListener(Open);
            if (dimmerButton != null)    dimmerButton.onClick.AddListener(Close);

            if (standUpButton != null) standUpButton.onClick.AddListener(OnStandUp);
            if (exitButton != null)    exitButton.onClick.AddListener(OnExit);
            BacktoHomeButton.onClick.AddListener(BacktoHomeButtonOnTap);
            TopUpButton.onClick.AddListener(TopUpButtonOnTap);
            HandHistoryButton.onClick.AddListener(HandHistoryButtonOnTap);
            RealTimeButton.onClick.AddListener(RealTimeButtonOnTap);

            if (withdrawButton != null)      withdrawButton.onClick.AddListener(WithdrawButtonOnTap);
            if (autoRebuyButton != null)     autoRebuyButton.onClick.AddListener(AutoRebuyButtonOnTap);
            if (sitOutButton != null)        sitOutButton.onClick.AddListener(SitOutButtonOnTap);
            if (themeButton != null)         themeButton.onClick.AddListener(ThemeButtonOnTap);
            if (tableSettingsButton != null) tableSettingsButton.onClick.AddListener(TableSettingsButtonOnTap);

            // A cold-start reconnect drops us straight into the table with no entry
            // screen having run — pull the origin back off disk before the first Open.
            TableContext.Restore();

            OpenClubBuyInIfOwed();
            // Don't measure the drawer here — it may be inactive (rect not laid out).
            // Positions are captured lazily on the first Open, once it's active.
        }

        /// <summary>
        /// A club table is entered with the buy-in still owed — the popup that takes
        /// the seat lives in this scene, so it is opened here on arrival. Confirming
        /// it seats the player and the hand starts as normal; dismissing it leaves
        /// them watching, and the drawer's Top Up row is the way back in.
        ///
        /// Confirm is also where the engine table gets created, for a club table row
        /// nobody has sat at yet — see ClubSeatFlow.
        /// </summary>
        private void OpenClubBuyInIfOwed()
        {
            if (!TableContext.PendingClubBuyIn)
                return;

            ResolveClubBuyInPanel();

            if (clubBuyInPanel == null)
            {
                Debug.LogError("[TableMenu] No ClubBuyInPanel in the scene — " +
                               "club buy-in can't be shown.");
                return;
            }

            // No seat and no state yet: the overlay and the round status line are both
            // describing a game that hasn't been joined, and the status line still
            // carries whatever text the scene was saved with. The first state_update
            // after seating renders them for real.
            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.HideGameStatus();

            var table = TableContext.Info;

            clubBuyInPanel.Open(
                TableContext.TableId,
                table?.BuyInMin ?? 0,
                table?.BuyInMax ?? 0,
                async amount =>
                {
                    // First player to buy in is the one who creates the real table.
                    string tableId = await ClubSeatFlow.EnsureTableAsync();

                    if (string.IsNullOrEmpty(tableId))
                        throw new System.Exception("Table unavailable");

                    // Buys in and seats in place — no scene reload either way, whether
                    // we were observing or arrived at a table that didn't exist yet.
                    // Throws on failure so the popup stays open with the error.
                    await TableJoinHandler.Instance.TakeSeatAsync(tableId, amount);

                    ClubSeatFlow.End();

                    if (PokerTableUI.Instance != null)
                    {
                        // The table screen opened before this table existed, so its
                        // size fetch found nothing and RenderFullTable would discard
                        // every state update. Re-read it now that there's a table.
                        PokerTableUI.Instance.RefreshTableSize();

                        // The socket join and first state_update are still in flight —
                        // show "waiting for players" rather than a blank felt.
                        // RenderFullTable corrects it the moment state lands.
                        if (GameStateManager.Instance == null ||
                            GameStateManager.Instance.CurrentState == null)
                        {
                            PokerTableUI.Instance.SetWaitingForPlayers(true);
                        }
                    }
                },
                TableContext.ClubId,
                // There's no "+ seat" control on our table to re-open this with, so
                // closing it means "not sitting down here" — back to the club. A
                // spectator queued for a seat is the exception: they have a table to
                // stay and watch, so dismissing just closes.
                mustBuyInOrLeave: TableJoinHandler.Instance == null ||
                                  !TableJoinHandler.Instance.IsSpectator);
        }

        private Coroutine _slideRoutine;

        public void Open()
        {
            if (_isOpen || drawerPanel == null) return;
            _isOpen = true;

            // Enable the drawer + dimmer (they start disabled in the inspector).
            drawerPanel.gameObject.SetActive(true);
            if (dimmer != null) dimmer.SetActive(true);

            // Two separate axes, applied in order: context decides which rows exist
            // at all, runtime state decides whether an existing row is tappable.
            ApplyContext();
            ApplyRuntimeState();

            RebuildDrawerLayout();

            // Capture the open position once — the design-time spot, before we ever
            // move the drawer. The closed position is re-derived every open, since a
            // horizontal fitter can give the drawer a different width per context.
            if (!_initialized)
            {
                _openX = drawerPanel.anchoredPosition.x;
                _initialized = true;
            }

            _closedX = _openX - drawerPanel.rect.width;

            // Snap off-screen, then slide in.
            drawerPanel.anchoredPosition = new Vector2(_closedX, drawerPanel.anchoredPosition.y);
            StartSlide(_openX, hideDimmerAtEnd: false);
        }

        /// <summary>
        /// Force the drawer's layout to settle *this* frame, after rows have been
        /// shown/hidden.
        ///
        /// Hiding a row only marks the layout dirty; the groups and fitters would
        /// otherwise run at the end of the frame, so the first open would show a gap
        /// where the hidden row was and measure a stale rect.width for the slide.
        /// On the very first open the drawer has never been active, so no layout pass
        /// has ever run on it and the design-time sizes are still in place — one
        /// deferred rebuild is not enough.
        ///
        /// Deepest-first, because a parent group sizes itself from children that must
        /// already be correct; GetComponentsInChildren is depth-first pre-order, so
        /// walking it backwards goes bottom-up.
        /// </summary>
        private void RebuildDrawerLayout()
        {
            if (drawerPanel == null) return;

            // Flush pending transform changes from the SetActive calls first —
            // rebuilding before this reads pre-activation rects.
            Canvas.ForceUpdateCanvases();

            LayoutGroup[] childGroups = drawerPanel.GetComponentsInChildren<LayoutGroup>(false);
            for (int i = childGroups.Length - 1; i >= 0; i--)
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    (RectTransform)childGroups[i].transform);

            LayoutRebuilder.ForceRebuildLayoutImmediate(drawerPanel);
        }

        #region Context visibility

        /// <summary>
        /// Show/hide rows for the context the table was entered from. Uses the
        /// inspector groups when they're wired; otherwise falls back to driving the
        /// known buttons directly, so the matrix holds even before the prefab work
        /// lands.
        /// </summary>
        private void ApplyContext()
        {
            // Club-only rows hinge entirely on this. It is Lobby unless the club
            // table screen ran TableContext.EnterFromClub — so entering the table
            // any other way (lobby join, quick join) hides them.
            bool club = TableContext.IsClub;

            // Half-filled groups (a Size bumped in the inspector but no rows yet)
            // count as unwired — otherwise they'd silently disable the fallback and
            // nothing would hide at all.
            if (HasWiredGroups())
            {
                foreach (MenuGroup group in groups)
                {
                    if (group == null) continue;

                    int visible = 0;

                    if (group.Items != null)
                    {
                        foreach (MenuItem item in group.Items)
                        {
                            if (item == null || item.Row == null) continue;

                            bool show = club ? item.ShowInClub : item.ShowInLobby;
                            item.Row.SetActive(show);
                            if (show) visible++;
                        }
                    }

                    // An all-hidden block would otherwise leave a stray title.
                    // if (group.Header != null) group.Header.SetActive(visible > 0);
                }
            }
            else
            {
                // Buy-in: Top Up everywhere; Withdraw and Auto Rebuy/Withdraw are
                // club-only, since lobby stacks settle straight back to the wallet.
                SetRowActive(TopUpButton,     true);
                SetRowActive(withdrawButton,  club);
                SetRowActive(autoRebuyButton, club);

                // Seat: Stand Up everywhere; Sit Out club-only.
                SetRowActive(standUpButton, true);
                SetRowActive(sitOutButton,  club);

                // Table + info: same in both contexts.
                SetRowActive(themeButton,         true);
                SetRowActive(tableSettingsButton, true);
                SetRowActive(HandHistoryButton,   true);
                SetRowActive(RealTimeButton,      true);

                // Exit: one Back row whose label follows the origin, plus Exit.
                SetRowActive(BacktoHomeButton, true);
                SetRowActive(exitButton,       true);
            }

            // Back leads to the club or to home depending on where we came from —
            // one row, two labels, so the drawer doesn't need two prefabs.
            SetLabel(BacktoHomeButton, TableExitRouter.BackLabel);
        }

        /// <summary>Enable/disable rows for live game state — spectating, already
        /// stood up, socket down. Separate from context so nothing shifts layout.</summary>
        private void ApplyRuntimeState()
        {
            var join = TableJoinHandler.Instance;
            if (join == null) return;

            bool seated = !join.IsSpectator && !join.IsStoodUp;

            // Stand Up only valid while seated. Allowed in every seated state —
            // WAITING/ROUND_END leave now, mid-hand defers to round end.
            if (standUpButton != null) standUpButton.interactable = seated;

            // Sitting out, topping up and withdrawing all need a live seat — except
            // that a club observer uses the Top Up row to buy the seat itself.
            if (sitOutButton != null)         sitOutButton.interactable = seated;
            if (TopUpButton != null)          TopUpButton.interactable = seated || CanBuyClubSeat;
            if (withdrawButton != null)       withdrawButton.interactable = seated;
            if (BacktoHomeButton != null)     BacktoHomeButton.interactable = seated;
        }

        /// <summary>
        /// Find the buy-in popup when the inspector reference is empty. It starts
        /// inactive in the scene, so the search has to include inactive objects —
        /// and doing this means the popup works whether or not anyone remembered to
        /// drag it into the field.
        /// </summary>
        private void ResolveClubBuyInPanel()
        {
            if (clubBuyInPanel != null)
                return;

            ClubBuyInPanel[] found = FindObjectsOfType<ClubBuyInPanel>(true);

            if (found != null && found.Length > 0)
                clubBuyInPanel = found[0];
        }

        /// <summary>At a club table with no seat yet — either watching a live table
        /// or sitting on one that hasn't been created. Both are states the player
        /// lands in by tapping a club table, and both are fixed by buying in.</summary>
        private bool CanBuyClubSeat =>
            TableContext.IsClub &&
            (string.IsNullOrEmpty(TableContext.TableId) ||
             (TableJoinHandler.Instance != null && TableJoinHandler.Instance.IsSpectator));

        /// <summary>True once at least one group row points at a real object.</summary>
        private bool HasWiredGroups()
        {
            if (groups == null) return false;

            foreach (MenuGroup group in groups)
            {
                if (group?.Items == null) continue;

                foreach (MenuItem item in group.Items)
                    if (item != null && item.Row != null) return true;
            }

            return false;
        }

        // Fallback path only — toggles the button object itself. Prefabs whose row
        // wraps extra decoration around the button should be wired into `groups`
        // instead, where the row object is named explicitly.
        private static void SetRowActive(Button button, bool active)
        {
            if (button == null) return;
            button.gameObject.SetActive(active);
        }

        private static void SetLabel(Button button, string text)
        {
            if (button == null) return;

            var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) { tmp.text = text; return; }

            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = text;
        }

        #endregion

        public void Close()
        {
            if (!_isOpen)
            {
                if (dimmer != null) dimmer.SetActive(false);
                return;
            }
            _isOpen = false;

            if (drawerPanel == null) return;
            StartSlide(_closedX, hideDimmerAtEnd: true);
        }

        private void StartSlide(float targetX, bool hideDimmerAtEnd)
        {
            if (_slideRoutine != null) StopCoroutine(_slideRoutine);
            _slideRoutine = StartCoroutine(SlideTo(targetX, hideDimmerAtEnd));
        }

        private IEnumerator SlideTo(float targetX, bool hideDimmerAtEnd)
        {
            float startX = drawerPanel.anchoredPosition.x;
            float t = 0f;

            while (t < slideDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / slideDuration);
                drawerPanel.anchoredPosition =
                    new Vector2(Mathf.Lerp(startX, targetX, p), drawerPanel.anchoredPosition.y);
                yield return null;
            }

            drawerPanel.anchoredPosition = new Vector2(targetX, drawerPanel.anchoredPosition.y);

            // On close: hide the dimmer and disable the drawer (back to default state).
            if (hideDimmerAtEnd)
            {
                if (dimmer != null) dimmer.SetActive(false);
                drawerPanel.gameObject.SetActive(false);
            }
        }

        private void OnStandUp()
        {
            Close();
            //do show the confirm dialog (chips + mid-hand note). On confirm
            // it stands up → spectator (between hands now; mid-hand after the round).
            if (LeaveTableHandler.Instance != null)
                LeaveTableHandler.Instance.OpenStandUpDialog();
        }

        private void OnExit()
        {
            Close();
            // Full leave → back to wherever we came from. Reuses the existing
            // leave confirmation, which routes via TableExitRouter on confirm.
            if (LeaveTableHandler.Instance != null)
                LeaveTableHandler.Instance.OpenLeaveDialog();
        }


        // Back = keep the seat, leave the screen. Sits out first so the table
        // doesn't stall on our timer, then routes to the club or the main menu.
        void BacktoHomeButtonOnTap()
        {
            EmitSitOut();
            Close();
            TableExitRouter.GoBack();
        }

        // Sit Out = stay on the table screen, just skip hands.
        void SitOutButtonOnTap()
        {
            EmitSitOut();
            Close();
        }

        private void EmitSitOut()
        {
            if (SocketManager.Instance == null || !SocketManager.Instance.IsConnected)
                return;

            var payload = new Dictionary<string, object>
           {
            { "tableId", SocketManager.Instance.CurrentTableId }
            };

            SocketManager.Instance.Emit("player:sit_out", payload);

            // Apply locally right away: auto-fold flag, fold if it's my turn,
            // hide action buttons — don't wait for the server broadcast.
            if (TableJoinHandler.Instance != null)
                TableJoinHandler.Instance.NotifySitOutRequested();
        }


        void TopUpButtonOnTap()
        {
            // Observing a club table with no seat yet: Top Up has nothing to top up,
            // so the same row is the way back into the buy-in popup.
            if (CanBuyClubSeat)
            {
                TableContext.BeginClubBuyIn();
                OpenClubBuyInIfOwed();
                Close();
                return;
            }

            TopUpPanel.SetActive(true);
        }

        void HandHistoryButtonOnTap()
        {
            HandHistoryPanel.SetActive(true);
        }

        void RealTimeButtonOnTap()
        {
            RealTimeResultPanel.SetActive(true);
        }

        void WithdrawButtonOnTap()
        {
            if (WithdrawPanel != null) WithdrawPanel.SetActive(true);
            Close();
        }

        void AutoRebuyButtonOnTap()
        {
            if (AutoRebuyPanel != null) AutoRebuyPanel.SetActive(true);
            Close();
        }

        void ThemeButtonOnTap()
        {
            if (ThemePanel != null) ThemePanel.SetActive(true);
            Close();
        }

        void TableSettingsButtonOnTap()
        {
            if (TableSettingsPanel != null) TableSettingsPanel.SetActive(true);
            Close();
        }
    }
}
