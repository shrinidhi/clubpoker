using UnityEngine;
using TMPro;
using DG.Tweening;
using ClubPoker.Networking;

namespace ClubPoker.UI
{
    public class OfflineBanner : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private RectTransform bannerRect;

        #endregion

        #region Constants

        private const string DEFAULT_MESSAGE = "No Internet Connection!!";
        private const float ANIMATION_DURATION = 0.3f;
        private const float HIDDEN_Y = 100f;
        private const float VISIBLE_Y = 0f;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Start hidden above screen
            bannerRect.anchoredPosition = new Vector2(0f, HIDDEN_Y);
            gameObject.SetActive(false);

            // Subscribe to network events
            if (NetworkMonitor.Instance != null)
            {
                NetworkMonitor.Instance.OnWentOffline += Show;
                NetworkMonitor.Instance.OnCameOnline += Hide;
            }
        }

        private void OnDestroy()
        {
            if (NetworkMonitor.Instance != null)
            {
                NetworkMonitor.Instance.OnWentOffline -= Show;
                NetworkMonitor.Instance.OnCameOnline -= Hide;
            }
        }

        #endregion

        #region Public Methods

        public void Show()
        {
            gameObject.SetActive(true);

            if (messageText != null)
                messageText.text = DEFAULT_MESSAGE;

            // Slide down from top
            bannerRect.DOAnchorPosY(VISIBLE_Y, ANIMATION_DURATION)
                .SetEase(Ease.OutBack);

            Debug.Log("[OfflineBanner] Showing offline banner");
        }

        public void Hide()
        {
            // Slide up out of screen
            bannerRect.DOAnchorPosY(HIDDEN_Y, ANIMATION_DURATION)
                .SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));

            Debug.Log("[OfflineBanner] Hiding offline banner");
        }

        #endregion
    }
}