using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using DG.Tweening;

namespace ClubPoker.UI
{
    /// <summary>
    /// Main-menu hamburger menu — a side drawer that slides in from the RIGHT
    /// (the hamburger sits top-right) with a dimmed backdrop. Same pattern as
    /// the in-game TableMenuController, mirrored: closed = open + width.
    /// </summary>
    public class MainMenuDrawerController : MonoBehaviour
    {
        [Header("Toggle")]
        [SerializeField] private Button hamburgerButton;
        [SerializeField] private RectTransform drawerPanel; // the sliding drawer (right-anchored)
        [SerializeField] private GameObject dimmer;         // full-screen backdrop
        [SerializeField] private Button dimmerButton;       // closes on tap-outside

        [Header("Options")]
        [SerializeField] private Button logoutButton;

        [Header("Slide")]
        [SerializeField] private float slideDuration = 0.25f;

        private float _openX;    // anchoredPosition.x when open (design-time spot)
        private float _closedX;  // off-screen to the right
        private bool _isOpen;
        private bool _initialized;
        private Tween _slideTween;

        // Put this controller on an ALWAYS-ACTIVE object (not the drawer itself),
        // so Start runs even though the drawer/dimmer start disabled in the inspector.
        private void Start()
        {
            if (hamburgerButton != null) hamburgerButton.onClick.AddListener(Open);
            if (dimmerButton != null)    dimmerButton.onClick.AddListener(Close);
            if (logoutButton != null)    logoutButton.onClick.AddListener(OnLogout);
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
        }

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
