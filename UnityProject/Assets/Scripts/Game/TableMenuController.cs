using ClubPoker.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    /// <summary>
    /// In-game hamburger menu — a side drawer that slides in from the left with a
    /// dimmed backdrop. The hamburger button opens it; each option performs its
    /// action and closes. Tapping the dimmer closes it.
    /// </summary>
    public class TableMenuController : MonoBehaviour
    {
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

        [Header("Slide")]
        [SerializeField] private float slideDuration = 0.25f;

        private float _openX;       // anchoredPosition.x when open
        private float _closedX;     // off-screen to the left
        private bool _isOpen;
        private bool _initialized;


        [SerializeField] private GameObject TopUpPanel;
        [SerializeField] private GameObject HandHistoryPanel;
        [SerializeField] private GameObject RealTimeResultPanel;

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
            // Don't measure the drawer here — it may be inactive (rect not laid out).
            // Positions are captured lazily on the first Open, once it's active.
        }

        private Coroutine _slideRoutine;

        public void Open()
        {
            if (_isOpen || drawerPanel == null) return;
            _isOpen = true;

            // Stand Up only valid while seated (not a spectator, not already standing
            // up). Allowed in every seated state — WAITING/ROUND_END leave now, mid-hand
            // defers to round end. Disable the button otherwise.
            if (standUpButton != null && TableJoinHandler.Instance != null)
            {
                bool canStandUp = !TableJoinHandler.Instance.IsSpectator &&
                                  !TableJoinHandler.Instance.IsStoodUp;
                standUpButton.interactable = canStandUp;
            }

            // Enable the drawer + dimmer (they start disabled in the inspector).
            drawerPanel.gameObject.SetActive(true);
            if (dimmer != null) dimmer.SetActive(true);

            // Capture open/closed positions on first use, now that the drawer is
            // active and its rect is valid. Design-time position = the open spot.
            if (!_initialized)
            {
                _openX = drawerPanel.anchoredPosition.x;
                _closedX = _openX - drawerPanel.rect.width;
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
            // Full leave → back to lobby. Reuses the existing leave confirmation.
            if (LeaveTableHandler.Instance != null)
                LeaveTableHandler.Instance.OpenLeaveDialog();
        }


        void BacktoHomeButtonOnTap()
        {
            if ( !SocketManager.Instance.IsConnected)
                return;

            var payload = new Dictionary<string, object>
           {
            { "tableId", SocketManager.Instance.CurrentTableId }
            };

            SocketManager.Instance.Emit("player:sit_out", payload);
        }


        void TopUpButtonOnTap()
        {
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
    }
}
