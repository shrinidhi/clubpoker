using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using DG.Tweening;

namespace ClubPoker.UI
{
    /// <summary>
    /// Main-menu hamburger menu — a side drawer that slides in from the RIGHT
    /// (the hamburger sits top-right) with a dimmed backdrop. Same pattern as
    /// the in-game TableMenuController, mirrored: closed = open + width.
    ///
    /// Meant to live in a prefab shared by the home and lobby screens, so both
    /// menus stay identical. hamburgerButton is per scene (it sits in each
    /// screen's own top bar) — assign it on the instance.
    /// </summary>
    public class MainMenuDrawerController : MonoBehaviour
    {
        /// <summary>A row that opens a web page. Empty URL = not ready yet:
        /// the row shows faded and can't be tapped.</summary>
        [Serializable]
        public class LinkRow
        {
            public Button button;
            public string url;
        }

        [Header("Toggle")]
        [SerializeField] private Button hamburgerButton;
        [SerializeField] private RectTransform drawerPanel; // the sliding drawer (right-anchored)
        [SerializeField] private GameObject dimmer;         // full-screen backdrop
        [SerializeField] private Button dimmerButton;       // closes on tap-outside

        [Header("Sound")]
        [Tooltip("The whole Sound row — tapping anywhere on it toggles.")]
        [SerializeField] private Button soundRowButton;
        [SerializeField] private Button soundToggleButton;
        [SerializeField] private Image  soundToggleImage;
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;

        [Header("Links (empty URL = disabled)")]
        [SerializeField] private LinkRow terms;
        [SerializeField] private LinkRow privacyPolicy;
        [SerializeField] private LinkRow fairGaming;
        [SerializeField] private LinkRow contactUs;

        [Header("Not built yet")]
        [Tooltip("Rows shown but disabled until their screen exists " +
                 "(Account setting, Language, Notice).")]
        [SerializeField] private Button[] comingSoonButtons;

        [Header("Options")]
        [SerializeField] private Button logoutButton;

        [Header("Footer")]
        [SerializeField] private TextMeshProUGUI versionText;

        [Header("Slide")]
        [SerializeField] private float slideDuration = 0.25f;

        [Header("Row animation")]
        [Tooltip("Parent of the menu rows (ButtonGrid). Each active child slides in " +
                 "from the right, one after another. The footer isn't in here, so " +
                 "the version text stays still.")]
        [SerializeField] private RectTransform rowsContainer;
        [SerializeField] private float rowSlideOffset = 200f;  // px to the right it starts from
        [SerializeField] private float rowDuration    = 0.3f;
        [SerializeField] private float rowStagger     = 0.05f; // delay between rows

        private const float DISABLED_ROW_ALPHA = 0.5f;

        private float _openX;    // anchoredPosition.x when open (design-time spot)
        private float _closedX;  // off-screen to the right
        private bool _isOpen;
        private bool _initialized;
        private Tween _slideTween;
        private readonly List<Tween> _rowTweens = new List<Tween>();

        // Put this controller on an ALWAYS-ACTIVE object (not the drawer itself),
        // so Start runs even though the drawer/dimmer start disabled in the inspector.
        private void Start()
        {
            if (hamburgerButton != null) hamburgerButton.onClick.AddListener(Open);
            if (dimmerButton != null)    dimmerButton.onClick.AddListener(Close);
            if (logoutButton != null)    logoutButton.onClick.AddListener(OnLogout);

            if (soundRowButton != null)    soundRowButton.onClick.AddListener(SoundSettings.Toggle);
            if (soundToggleButton != null) soundToggleButton.onClick.AddListener(SoundSettings.Toggle);
            SoundSettings.OnChanged += RefreshSound;
            RefreshSound(SoundSettings.IsOn);

            BindLink(terms);
            BindLink(privacyPolicy);
            BindLink(fairGaming);
            BindLink(contactUs);

            if (comingSoonButtons != null)
                foreach (Button b in comingSoonButtons)
                    SetRowEnabled(b, false);

            if (versionText != null)
                versionText.text = $"Version:{Application.version}";
        }

        public void Open()
        {
            if (_isOpen || drawerPanel == null) return;
            _isOpen = true;

            drawerPanel.gameObject.SetActive(true);
            if (dimmer != null) dimmer.SetActive(true);

            // Capture open/closed positions on first use, once the drawer is active
            // and its rect is valid. Design-time position = the open spot; closed is
            // one full width further RIGHT (mirror of the in-game left drawer).
            if (!_initialized)
            {
                _openX = drawerPanel.anchoredPosition.x;
                _closedX = _openX + drawerPanel.rect.width;
                _initialized = true;
            }

            // Snap off-screen, then slide in.
            drawerPanel.anchoredPosition = new Vector2(_closedX, drawerPanel.anchoredPosition.y);
            StartSlide(_openX, hideDimmerAtEnd: false);

            AnimateRows();
        }

        /// <summary>Rows follow the drawer in, staggered top to bottom.</summary>
        private void AnimateRows()
        {
            if (rowsContainer == null) return;

            // Snap any rows still mid-slide home first, so "home" is never read
            // from a half-slid row.
            CompleteRowTweens();

            // The rows sit in a VerticalLayoutGroup. Activating the drawer queues a
            // layout pass for the end of this frame, and that pass puts every row
            // back on its slot — wiping the offset set below, so the slide ran from
            // home to home and nothing moved. Run the queued pass now instead, so
            // nothing is left to snap the rows back afterwards.
            Canvas.ForceUpdateCanvases();

            int index = 0;
            foreach (Transform child in rowsContainer)
            {
                if (!child.gameObject.activeSelf || !(child is RectTransform row))
                    continue;

                float homeX = row.anchoredPosition.x;

                // Explicit start: a delayed tween otherwise reads its start value
                // when the delay ends, not now.
                _rowTweens.Add(row
                    .DOAnchorPosX(homeX, rowDuration)
                    .From(new Vector2(homeX + rowSlideOffset, row.anchoredPosition.y))
                    .SetDelay(index * rowStagger)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true));

                index++;
            }
        }

        private void CompleteRowTweens()
        {
            foreach (Tween t in _rowTweens)
                if (t != null && t.IsActive()) t.Complete();

            _rowTweens.Clear();
        }

        public void Close()
        {
            if (!_isOpen)
            {
                if (dimmer != null) dimmer.SetActive(false);
                return;
            }
            _isOpen = false;

            if (drawerPanel == null) return;

            // Rows ride out with the drawer — no reverse stagger on close.
            CompleteRowTweens();
            StartSlide(_closedX, hideDimmerAtEnd: true);
        }

        private void StartSlide(float targetX, bool hideDimmerAtEnd)
        {
            _slideTween?.Kill();

            _slideTween = drawerPanel
                .DOAnchorPosX(targetX, slideDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true); // unscaled time, keeps working if timescale changes

            if (hideDimmerAtEnd)
            {
                _slideTween.OnComplete(() =>
                {
                    if (dimmer != null) dimmer.SetActive(false);
                    drawerPanel.gameObject.SetActive(false);
                });
            }
        }

        private void OnDestroy()
        {
            _slideTween?.Kill();
            foreach (Tween t in _rowTweens) t?.Kill();
            SoundSettings.OnChanged -= RefreshSound;
        }

        #region Rows

        private void RefreshSound(bool on)
        {
            if (soundToggleImage == null) return;

            Sprite sprite = on ? soundOnSprite : soundOffSprite;
            if (sprite != null) soundToggleImage.sprite = sprite;
        }

        private static void BindLink(LinkRow row)
        {
            if (row == null || row.button == null) return;

            bool ready = !string.IsNullOrWhiteSpace(row.url);
            SetRowEnabled(row.button, ready);

            if (ready)
                row.button.onClick.AddListener(() => Application.OpenURL(row.url.Trim()));
        }

        // Fade the whole row (icon, label, arrow) — Button's own disabled tint
        // only reaches its background, so a dead row would still look live.
        private static void SetRowEnabled(Button button, bool enabled)
        {
            if (button == null) return;

            button.interactable = enabled;

            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();

            group.alpha = enabled ? 1f : DISABLED_ROW_ALPHA;
        }

        #endregion

        private void OnLogout()
        {
            // Guard against double-tap while the request is in flight.
            if (logoutButton != null) logoutButton.interactable = false;

            // LogoutAsync does the full sequence: server call, token wipe,
            // cache clear, session reset, navigate to LoginScene.
            AuthManager.Instance.LogoutAsync().Forget();
        }
    }
}
