using System;
using TMPro;
using UnityEngine;

namespace ClubPoker.UI
{
    /// <summary>
    /// Lifts a popup just enough to keep the input field being typed in above the
    /// on-screen keyboard, and eases it back when the keyboard goes.
    ///
    /// Put on the popup's own RectTransform (the box that should move). The lift is
    /// always measured from the popup's resting spot and eased, so a keyboard that
    /// reports its height over a few frames, or focus hopping between two fields,
    /// can't make it jump. It never lifts the popup's top past the top of the
    /// screen — on a popup that already fills the screen it moves little or not at all.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyboardAvoider : MonoBehaviour
    {
        [Tooltip("Gap kept between the field and the keyboard, in canvas units.")]
        [SerializeField] private float fieldMargin = 40f;
        [Tooltip("Gap kept between the popup's top and the top of the screen, in canvas units.")]
        [SerializeField] private float topMargin = 20f;
        [Tooltip("Seconds the popup takes to ease to its new spot.")]
        [SerializeField] private float smoothTime = 0.12f;

        // Focus drops for a frame or two when it moves to another field; the lift
        // is held this long before the popup is allowed back down.
        private const float RELEASE_DELAY = 0.2f;

        // Lift changes smaller than this (canvas units) are ignored.
        private const float DEAD_ZONE = 4f;

        private RectTransform _rect;
        private Canvas _canvas;
        private TMP_InputField[] _inputs;
        private readonly Vector3[] _corners = new Vector3[4];

        private float _homeY;
        private float _lift;        // current, canvas units
        private float _target;
        private float _velocity;
        private float _lastNeeded;  // unscaled time a lift was last called for

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _inputs = GetComponentsInChildren<TMP_InputField>(true);
        }

        private void OnEnable()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null) _canvas = _canvas.rootCanvas;

            _homeY = _rect.anchoredPosition.y;
            _lift = _target = _velocity = 0f;
        }

        private void OnDisable()
        {
            // Closed while lifted: next time it opens from its resting spot.
            if (_lift != 0f) SetLift(0f);
        }

        private void LateUpdate()
        {
            if (_canvas == null) return;

            float wanted = WantedLift();

            if (wanted > 0f)
            {
                _lastNeeded = Time.unscaledTime;
                if (Mathf.Abs(wanted - _target) > DEAD_ZONE) _target = wanted;
            }
            else if (Time.unscaledTime - _lastNeeded > RELEASE_DELAY)
            {
                _target = 0f;
            }

            if (Mathf.Approximately(_lift, _target)) return;

            float next = Mathf.SmoothDamp(_lift, _target, ref _velocity, smoothTime,
                                          Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(next - _target) < 0.5f) next = _target;

            SetLift(next);
        }

        private void SetLift(float lift)
        {
            _lift = lift;
            _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, _homeY + lift);
        }

        /// <summary>Lift the focused field needs right now, in canvas units; 0 = none.</summary>
        private float WantedLift()
        {
            TMP_InputField focused = null;
            foreach (TMP_InputField input in _inputs)
            {
                if (input != null && input.isFocused) { focused = input; break; }
            }

            if (focused == null) return 0f;

            float keyboardTop = KeyboardOverlap.Pixels();   // screen px from the bottom
            if (keyboardTop <= 0f) return 0f;

            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            float liftPx = _lift * scale;

            // Where things sit with the popup at rest, in screen pixels.
            float fieldBottom = ScreenY(focused.GetComponent<RectTransform>(), cam, top: false) - liftPx;
            float popupTop    = ScreenY(_rect, cam, top: true) - liftPx;

            float needPx = keyboardTop + fieldMargin * scale - fieldBottom;
            float roomPx = Screen.safeArea.yMax - topMargin * scale - popupTop;

            return Mathf.Max(0f, Mathf.Min(needPx, roomPx)) / scale;
        }

        private float ScreenY(RectTransform rect, Camera cam, bool top)
        {
            rect.GetWorldCorners(_corners);   // 0 = bottom-left, 1 = top-left
            return RectTransformUtility.WorldToScreenPoint(cam, _corners[top ? 1 : 0]).y;
        }
    }

    /// <summary>How much of the screen the on-screen keyboard covers.</summary>
    public static class KeyboardOverlap
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _decorView;
        private static AndroidJavaObject _contentView;
        private static AndroidJavaObject _visibleFrame;
        private static bool _failed;
#endif

        /// <summary>Height covered, in screen pixels from the bottom; 0 when closed.</summary>
        public static float Pixels()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // TouchScreenKeyboard.area is empty on Android while the input field hides
            // the native text box, so ask the window what is still visible instead.
            if (!_failed)
            {
                try
                {
                    if (_decorView == null)
                    {
                        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                        {
                            _decorView = window.Call<AndroidJavaObject>("getDecorView");
                        }

                        // android.R.id.content: the app's drawable area.
                        _contentView = _decorView.Call<AndroidJavaObject>("findViewById", 16908290);
                        _visibleFrame = new AndroidJavaObject("android.graphics.Rect");
                    }

                    _decorView.Call("getWindowVisibleDisplayFrame", _visibleFrame);

                    int contentHeight = _contentView.Call<int>("getHeight");
                    if (contentHeight <= 0) return 0f;

                    int hidden = Mathf.Max(0,
                        ScreenTop(_contentView) + contentHeight - _visibleFrame.Get<int>("bottom"));

                    // A navigation bar also hides a strip; only a keyboard hides this much.
                    bool open = TouchScreenKeyboard.visible || hidden > contentHeight * 0.15f;
                    if (!open) return 0f;

                    return Mathf.Clamp((float)hidden * Screen.height / contentHeight, 0f, Screen.height);
                }
                catch (Exception e)
                {
                    _failed = true;
                    Debug.LogWarning("[KeyboardOverlap] Native keyboard measurement failed: " + e.Message);
                }
            }

            return TouchScreenKeyboard.visible
                ? Mathf.Clamp(TouchScreenKeyboard.area.height, 0f, Screen.height)
                : 0f;
#elif UNITY_IOS && !UNITY_EDITOR
            return TouchScreenKeyboard.visible
                ? Mathf.Clamp(TouchScreenKeyboard.area.height, 0f, Screen.height)
                : 0f;
#else
            return 0f;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int ScreenTop(AndroidJavaObject view)
        {
            IntPtr positions = AndroidJNI.NewIntArray(2);
            try
            {
                IntPtr method = AndroidJNIHelper.GetMethodID(
                    view.GetRawClass(), "getLocationOnScreen", "([I)V");
                AndroidJNI.CallVoidMethod(view.GetRawObject(), method,
                    new[] { new jvalue { l = positions } });
                return AndroidJNI.FromIntArray(positions)[1];
            }
            finally
            {
                AndroidJNI.DeleteLocalRef(positions);
            }
        }
#endif
    }
}
