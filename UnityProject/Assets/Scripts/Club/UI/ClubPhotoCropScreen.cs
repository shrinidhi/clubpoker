using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Club photo crop screen, reference-game style: the square frame (3×3 grid) is FIXED
/// in the middle and the player moves and zooms the PHOTO under it — drag to pan,
/// pinch / double tap / zoom slider to zoom, rotate 90°. ✓ cuts exactly what's inside
/// the frame.
///
/// Elastic edges: the photo can be pulled past its edges (with resistance) and pinched
/// below "just covers the frame", then springs back on release — so the frame is never
/// left with a gap in what gets cropped.
///
/// Lives on the root of Resources/ClubPhotoCropScreen.prefab, which has its own
/// overlay Canvas; ClubPhotoPicker creates it on first use. Needs a raycast-target
/// Image under the root (Background) — drags and taps anywhere reach this script.
/// </summary>
public class ClubPhotoCropScreen : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Crop")]
    [Tooltip("RawImage showing the photo. Same parent as Frame, anchored middle-center.")]
    public RawImage Photo;
    [Tooltip("The fixed square. Anchored middle-center; its width is the crop size.")]
    public RectTransform Frame;

    [Header("Controls")]
    public Button CloseButton;
    public Button ConfirmButton;
    public Button RotateButton;
    [Tooltip("0 = photo just covers the frame, 1 = MaxZoom × that.")]
    public Slider ZoomSlider;
    [Tooltip("Optional \"100%\" label next to the slider.")]
    public TextMeshProUGUI ZoomText;
    [Tooltip("Optional spinner shown while the picked photo decodes — the screen opens " +
             "straight away instead of after the decode.")]
    public GameObject LoadingIndicator;

    [Header("Behaviour")]
    [Tooltip("How far past \"just covers the frame\" the player can zoom in.")]
    public float MaxZoom = 4f;
    [Tooltip("Square size as a share of the crop area's shorter side (Frame's parent), " +
             "set on every open so it fits any phone. 0 = keep Frame's own size.")]
    [Range(0f, 1f)] public float FrameFill = 0.9f;
    [Tooltip("Zoom a double tap goes to (× \"just covers the frame\").")]
    public float DoubleTapZoom = 2f;
    [Tooltip("How much of a drag / pinch past the edge still moves the photo (0–1). " +
             "Lower = stiffer.")]
    [Range(0f, 1f)] public float Elasticity = 0.35f;
    [Tooltip("Spring-back / double-tap animation length, seconds.")]
    public float AnimDuration = 0.25f;

    // Our own double-tap detection: Unity's clickCount only counts for the mouse — a
    // touch's pointer data is dropped when the finger lifts, so it's always 1 on a phone.
    private const float DoubleTapTime = 0.3f;
    private const float DoubleTapMaxDistance = 80f;   // screen px between the two taps
    private float _lastTapTime = -1f;
    private Vector2 _lastTapPosition;

    private Texture2D _source;
    private Action<Texture2D> _onDone;
    private int _outputMaxSize;

    private int _rotation;      // 0 / 90 / 180 / 270, clockwise
    private float _scale;       // screen units per source pixel (what's shown)
    private float _minScale;
    private Vector2 _offset;    // photo centre relative to frame centre, frame-parent units (shown)

    // Finger-driven values before rubber-banding — what the gesture "asked for".
    private Vector2 _rawOffset;
    private float _rawScale;

    private Canvas _canvas;
    private bool _dragging;
    private bool _pinching;
    private float _lastPinchDistance;
    private Tween _anim;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();

        CloseButton.onClick.AddListener(Cancel);
        ConfirmButton.onClick.AddListener(Confirm);
        if (RotateButton != null) RotateButton.onClick.AddListener(Rotate);

        if (ZoomSlider != null)
        {
            ZoomSlider.minValue = 0f;
            ZoomSlider.maxValue = 1f;
            ZoomSlider.onValueChanged.AddListener(OnZoomSlider);
        }
    }

    private void OnDisable()
    {
        _anim?.Kill();
        _dragging = _pinching = false;
    }

    #region Open / close

    /// True between ShowLoading and Show — still waiting for the decoded photo. False
    /// once the player cancels, so a photo that lands afterwards is thrown away.
    public bool IsWaitingForPhoto { get; private set; }

    /// <summary>
    /// Open straight away, empty, with the spinner — the photo is still decoding.
    /// <paramref name="onCancel"/> runs if ✕ is tapped before it arrives.
    /// </summary>
    public void ShowLoading(Action onCancel)
    {
        _anim?.Kill();
        _source = null;
        _onDone = onCancel != null ? _ => onCancel() : (Action<Texture2D>)null;
        IsWaitingForPhoto = true;

        gameObject.SetActive(true);

        Photo.texture = null;
        Photo.enabled = false;
        SetControlsReady(false);
    }

    private void SetControlsReady(bool ready)
    {
        if (LoadingIndicator != null) LoadingIndicator.SetActive(!ready);
        ConfirmButton.interactable = ready;
        if (RotateButton != null) RotateButton.interactable = ready;
        if (ZoomSlider != null)   ZoomSlider.interactable = ready;
    }

    /// The texture currently shown (preview or final) — lets the loader check the
    /// screen is still on its photo before swapping in the sharp one.
    public Texture2D CurrentSource => _source;

    // False while only a low-res preview is shown: ✓ waits for the real photo.
    private bool _isFinal;

    /// <summary>
    /// Show <paramref name="source"/>. On ✓ <paramref name="onDone"/> gets a new square
    /// texture of at most <paramref name="outputMaxSize"/> px (caller owns it); on ✕ it
    /// gets null. Source textures are never destroyed here.
    ///
    /// <paramref name="isFinal"/> = false → a quick low-res preview: everything works
    /// except ✓, until <see cref="UpgradeSource"/> swaps in the real (readable) photo.
    /// </summary>
    public void Show(Texture2D source, int outputMaxSize, Action<Texture2D> onDone, bool isFinal = true)
    {
        _isFinal = isFinal;
        _source = source;
        _onDone = onDone;
        _outputMaxSize = outputMaxSize;

        // Bilinear sampling at the photo's edge would otherwise wrap to the far side.
        source.wrapMode = TextureWrapMode.Clamp;

        IsWaitingForPhoto = false;
        gameObject.SetActive(true);

        Photo.texture = source;
        Photo.enabled = true;
        Photo.rectTransform.sizeDelta = new Vector2(source.width, source.height);
        SetControlsReady(true);
        ConfirmButton.interactable = isFinal;

        _rotation = 0;
        _dragging = _pinching = false;
        _lastPinchDistance = 0f;

        // Layout must be current before the frame's size is read.
        Canvas.ForceUpdateCanvases();
        FitFrame();

        RecalcMinScale();
        SetState(_minScale, Vector2.zero);
    }

    /// <summary>
    /// Swap the preview for the sharp, readable photo (same picture, more pixels)
    /// without moving anything: scales are converted so the photo stays exactly
    /// where the player has put it — even mid-drag or mid-pinch.
    /// </summary>
    public void UpgradeSource(Texture2D full)
    {
        if (_source == null || full == null) return;

        float ratio = (float)_source.width / full.width;   // preview px per full px

        _scale    *= ratio;
        _rawScale *= ratio;
        _minScale *= ratio;

        // A running spring-back / double-tap animates in the old units — finish it
        // in the new ones instead.
        bool animating = _anim != null && _anim.IsActive();
        _anim?.Kill();

        full.wrapMode = TextureWrapMode.Clamp;
        _source = full;
        _isFinal = true;

        Photo.texture = full;
        Photo.rectTransform.sizeDelta = new Vector2(full.width, full.height);

        if (animating && !_dragging && !_pinching) Settle(animated: true);
        else Apply();

        ConfirmButton.interactable = true;
    }

    /// ✕ — also used by ClubPhotoPicker to close the screen if the photo fails to load.
    public void Cancel() => Finish(null);

    private void Confirm()
    {
        if (_source == null || !_isFinal) return;   // still loading / only the preview

        // Mid-spring-back → crop the settled view, not a half-elastic one.
        Settle(animated: false);
        Finish(CropToTexture());
    }

    private void Finish(Texture2D result)
    {
        Action<Texture2D> done = _onDone;

        _onDone = null;
        _source = null;
        _isFinal = false;
        IsWaitingForPhoto = false;
        Photo.texture = null;
        gameObject.SetActive(false);

        done?.Invoke(result);
    }

    #endregion

    #region Geometry

    private float FrameSize => Frame.rect.width;
    private float MaxScale  => _minScale * MaxZoom;

    // Square = FrameFill × the crop area's shorter side (usually its width), centred.
    // Every child of Frame (shades, border, grid) is anchored to it, so they follow.
    private void FitFrame()
    {
        if (FrameFill <= 0f || !(Frame.parent is RectTransform area)) return;

        float side = Mathf.Min(area.rect.width, area.rect.height) * FrameFill;
        if (side <= 0f) return;

        Frame.sizeDelta = new Vector2(side, side);
    }

    // Source size as displayed: width / height swap at 90° and 270°.
    private Vector2 RotatedSize =>
        _rotation % 180 == 0
            ? new Vector2(_source.width, _source.height)
            : new Vector2(_source.height, _source.width);

    // Smallest scale where the photo still covers the whole frame.
    private void RecalcMinScale()
    {
        Vector2 r = RotatedSize;
        _minScale = FrameSize / Mathf.Min(r.x, r.y);
    }

    // How far the photo centre may sit from the frame centre at a scale and still
    // cover the frame.
    private Vector2 OffsetLimit(float scale)
    {
        Vector2 shown = RotatedSize * scale;
        return new Vector2(Mathf.Max(0f, (shown.x - FrameSize) / 2f),
                           Mathf.Max(0f, (shown.y - FrameSize) / 2f));
    }

    private Vector2 ClampOffset(Vector2 offset, float scale)
    {
        Vector2 lim = OffsetLimit(scale);
        return new Vector2(Mathf.Clamp(offset.x, -lim.x, lim.x),
                           Mathf.Clamp(offset.y, -lim.y, lim.y));
    }

    // Past the limit only Elasticity of the overshoot shows — the rubber-band pull.
    private float Rubber(float value, float limit)
    {
        float over = Mathf.Abs(value) - limit;
        return over <= 0f ? value : Mathf.Sign(value) * (limit + over * Elasticity);
    }

    private Vector2 RubberOffset(Vector2 offset, float scale)
    {
        Vector2 lim = OffsetLimit(scale);
        return new Vector2(Rubber(offset.x, lim.x), Rubber(offset.y, lim.y));
    }

    private float RubberScale(float scale)
    {
        if (scale < _minScale) return _minScale - (_minScale - scale) * Elasticity;
        if (scale > MaxScale)  return MaxScale + (scale - MaxScale) * Elasticity;
        return scale;
    }

    /// Settled state: scale within limits, frame fully covered. Raw = shown.
    private void SetState(float scale, Vector2 offset)
    {
        _scale = Mathf.Clamp(scale, _minScale, MaxScale);
        _offset = ClampOffset(offset, _scale);
        _rawScale = _scale;
        _rawOffset = _offset;
        Apply();
    }

    private void Apply()
    {
        RectTransform photo = Photo.rectTransform;
        photo.anchoredPosition = Frame.anchoredPosition + _offset;
        photo.localScale = new Vector3(_scale, _scale, 1f);
        photo.localEulerAngles = new Vector3(0f, 0f, -_rotation);   // negative = clockwise

        if (ZoomSlider != null)
            ZoomSlider.SetValueWithoutNotify(Mathf.InverseLerp(_minScale, MaxScale, _scale));

        if (ZoomText != null)
            ZoomText.text = Mathf.RoundToInt(Mathf.Clamp(_scale, _minScale, MaxScale) / _minScale * 100f) + "%";
    }

    // Screen point → frame-centred local point (same space as _offset).
    private Vector2 ToFrameLocal(Vector2 screenPoint)
    {
        RectTransform area = (RectTransform)Frame.parent;
        Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, cam, out Vector2 local);
        return local - (Vector2)Frame.localPosition;
    }

    // Offset that keeps the photo point under `focus` in place while scaling.
    private static Vector2 ZoomAbout(Vector2 offset, Vector2 focus, float fromScale, float toScale) =>
        focus - (focus - offset) * (toScale / fromScale);

    #endregion

    #region Spring back / animate

    /// Back inside the limits — animated after a gesture, instantly before cropping.
    private void Settle(bool animated)
    {
        float scale = Mathf.Clamp(_scale, _minScale, MaxScale);
        Vector2 offset = ClampOffset(_offset, scale);

        if (!animated || (Mathf.Approximately(scale, _scale) && offset == _offset))
        {
            _anim?.Kill();
            SetState(scale, offset);
            return;
        }

        AnimateTo(scale, offset);
    }

    private void AnimateTo(float scale, Vector2 offset)
    {
        _anim?.Kill();

        float fromScale = _scale;
        Vector2 fromOffset = _offset;

        _anim = DOVirtual.Float(0f, 1f, AnimDuration, t =>
            {
                _scale = Mathf.Lerp(fromScale, scale, t);
                _offset = Vector2.Lerp(fromOffset, offset, t);
                Apply();
            })
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() => SetState(scale, offset));
    }

    #endregion

    #region Controls

    private void OnZoomSlider(float t)
    {
        if (_source == null) return;
        _anim?.Kill();

        // Slider zooms about the frame centre.
        float scale = Mathf.Lerp(_minScale, MaxScale, t);
        SetState(scale, ZoomAbout(_offset, Vector2.zero, _scale, scale));
    }

    private void Rotate()
    {
        if (_source == null) return;
        _anim?.Kill();
        _rotation = (_rotation + 90) % 360;

        // The covering scale changes with the shape; keep the zoom level, re-centre.
        float zoom = _scale / _minScale;
        RecalcMinScale();
        SetState(_minScale * zoom, Vector2.zero);
    }

    // ── Pan (one finger / mouse) ──────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_source == null) return;

        _anim?.Kill();
        _dragging = true;
        _rawOffset = _offset;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _pinching || Input.touchCount >= 2) return;

        float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
        _rawOffset += eventData.delta / scaleFactor;

        _offset = RubberOffset(_rawOffset, _scale);
        Apply();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        _dragging = false;
        if (!_pinching) Settle(animated: true);
    }

    // ── Double tap ────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_source == null) return;

        float now = Time.unscaledTime;
        bool isDoubleTap = _lastTapTime >= 0f &&
                           now - _lastTapTime <= DoubleTapTime &&
                           Vector2.Distance(eventData.position, _lastTapPosition) <= DoubleTapMaxDistance;

        if (!isDoubleTap)
        {
            _lastTapTime = now;
            _lastTapPosition = eventData.position;
            return;
        }

        _lastTapTime = -1f;   // a third tap starts a new pair

        // Zoomed out → zoom in on the tapped spot; zoomed in → back out.
        bool zoomedIn = _scale > _minScale * 1.05f;
        float target = zoomedIn ? _minScale : Mathf.Min(_minScale * DoubleTapZoom, MaxScale);

        Vector2 focus = ToFrameLocal(eventData.position);
        Vector2 offset = ClampOffset(ZoomAbout(_offset, focus, _scale, target), target);

        AnimateTo(target, offset);
    }

    // ── Pinch (two fingers) ───────────────────────────────────────────────

    private void Update()
    {
        if (_source == null) return;

        if (Input.touchCount == 2)
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);
            float distance = Vector2.Distance(a.position, b.position);
            Vector2 mid = (a.position + b.position) / 2f;

            if (!_pinching)
            {
                // Pinch starts — from whatever is shown (a drag may have been running).
                _anim?.Kill();
                _pinching = true;
                _rawScale = _scale;
                _rawOffset = _offset;
                _lastPinchDistance = distance;
                _lastPinchMid = mid;
                return;
            }

            if (_lastPinchDistance > 0f && distance > 0f)
            {
                float fromScale = _scale;
                _rawScale *= distance / _lastPinchDistance;
                float shownScale = RubberScale(_rawScale);

                // Zoom about the point between the fingers, and follow the fingers
                // as they move together.
                Vector2 focus = ToFrameLocal(mid);
                float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;

                _rawOffset = ZoomAbout(_rawOffset, focus, fromScale, shownScale)
                             + (mid - _lastPinchMid) / scaleFactor;

                _scale = shownScale;
                _offset = RubberOffset(_rawOffset, _scale);
                Apply();
            }

            _lastPinchDistance = distance;
            _lastPinchMid = mid;
        }
        else if (_pinching)
        {
            // Fingers lifted → spring back into the limits.
            _pinching = false;
            _lastPinchDistance = 0f;
            if (!_dragging) Settle(animated: true);
        }
    }

    private Vector2 _lastPinchMid;

    #endregion

    #region Crop

    /// <summary>
    /// The pixels inside the frame, sampled straight from the source (bilinear) into
    /// an N×N texture — rotation and scaling in one pass, no render camera.
    /// </summary>
    private Texture2D CropToTexture()
    {
        Vector2 rotated = RotatedSize;
        float size = FrameSize / _scale;   // frame side in source pixels

        // Frame's bottom-left corner in the displayed (rotated) image, in source pixels.
        float left   = rotated.x / 2f - (_offset.x + FrameSize / 2f) / _scale;
        float bottom = rotated.y / 2f - (_offset.y + FrameSize / 2f) / _scale;

        int n = Mathf.Clamp(Mathf.RoundToInt(size), 1, _outputMaxSize);
        var pixels = new Color[n * n];

        for (int y = 0; y < n; y++)
        {
            float rv = (bottom + (y + 0.5f) * size / n) / rotated.y;
            for (int x = 0; x < n; x++)
            {
                float ru = (left + (x + 0.5f) * size / n) / rotated.x;
                ToSourceUV(ru, rv, out float su, out float sv);
                pixels[y * n + x] = _source.GetPixelBilinear(su, sv);
            }
        }

        var result = new Texture2D(n, n, TextureFormat.RGB24, false);
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    // Displayed (clockwise-rotated) UV → source UV.
    private void ToSourceUV(float ru, float rv, out float su, out float sv)
    {
        switch (_rotation)
        {
            case 90:  su = 1f - rv; sv = ru;      break;
            case 180: su = 1f - ru; sv = 1f - rv; break;
            case 270: su = rv;      sv = 1f - ru; break;
            default:  su = ru;      sv = rv;      break;
        }
    }

    #endregion
}
