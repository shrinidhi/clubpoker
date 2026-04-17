

using UnityEngine;

namespace ClubPoker.UI
{
    public class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect          _lastSafeArea;
        private Canvas        _canvas;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas        = GetComponentInParent<Canvas>();
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;

            if (safeArea == _lastSafeArea) return;
            _lastSafeArea = safeArea;

            // Convert safe area to anchor values using actual screen size
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            Debug.Log($"[SafeAreaPanel] Applied — " +
                      $"Screen: {Screen.width}x{Screen.height}, " +
                      $"SafeArea: {safeArea}, " +
                      $"AnchorMin: {anchorMin}, AnchorMax: {anchorMax}");
        }
    }
}