using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    /// <summary>
    /// Solid black band over the top unsafe area (notch / cutout / status bar strip)
    /// on every screen. Created once at startup and kept across scene loads, so no
    /// scene needs wiring. Height follows Screen.safeArea — zero on devices without
    /// a top inset.
    /// </summary>
    public class TopInsetBlackBar : MonoBehaviour
    {
        private RectTransform _bar;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var root = new GameObject("TopInsetBlackBar");
            DontDestroyOnLoad(root);

            // Above every scene canvas and popup.
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(root.transform, false);

            var image = barGo.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;   // never eats touches

            var bar = barGo.GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;

            root.AddComponent<TopInsetBlackBar>()._bar = bar;
        }

        private void Start() => Apply();

        // Safe area can change on rotation or when the cutout mode changes.
        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea ||
                Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
                Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            // Overlay canvas units == screen pixels, so the inset maps 1:1.
            float topInset = Mathf.Max(0f, Screen.height - _lastSafeArea.yMax);
            _bar.sizeDelta = new Vector2(0f, topInset);
            _bar.gameObject.SetActive(topInset > 0f);
        }
    }
}
