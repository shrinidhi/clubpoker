// 📁 CREATE AT: Assets/Scripts/UI/SafeAreaPanel.cs
// 📋 ACTION:    New file — right-click UI/ → Create → C# Script → SafeAreaPanel
// 🔗 NAMESPACE: ClubPoker.UI
// ⚙️ ASMDEF:    ClubPoker.UI

using UnityEngine;

namespace ClubPoker.UI
{
    public class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea == _lastSafeArea) return;
            _lastSafeArea = safeArea;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}