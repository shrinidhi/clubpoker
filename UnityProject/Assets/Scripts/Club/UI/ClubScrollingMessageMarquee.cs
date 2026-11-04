using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// LED-strip style ticker for Admin ▸ Scrolling Message. Text enters from the right edge
/// of Viewport, scrolls left until fully off-screen once, then hides. Driven by
/// ClubScrollBanner, which keeps it on screen across scenes and decides what plays next.
public class ClubScrollingMessageMarquee : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform Viewport;      // masked container defining visible width
    public RectTransform TextRect;      // child holding Label, moves along X
    public TextMeshProUGUI Label;

    [Header("Go button")]
    public Button GoButton;             // static, sits right of the strip — outside Viewport's mask

    [Header("Motion")]
    public float PixelsPerSecond = 120f;

    /// Raised with the attached tableId when Go is tapped.
    public event Action<string> OnGoTapped;

    /// Raised once the text has scrolled fully off-screen.
    public event Action OnFinished;

    private string _pendingMessage;
    private string _tableId;
    private int _runToken;

    private void Awake()
    {
        if (GoButton != null) GoButton.onClick.AddListener(() => OnGoTapped?.Invoke(_tableId));
    }

    private void OnDisable()
    {
        _runToken++; // cancels any in-flight loop
    }

    public void SetMessage(string clubName, string message, string tableId = null)
    {
        message = message?.Trim() ?? "";
        bool show = !string.IsNullOrEmpty(message);

        if (Viewport != null) Viewport.gameObject.SetActive(show);
        if (!show)
        {
            if (GoButton != null) GoButton.gameObject.SetActive(false);
            return;
        }

        _tableId = tableId;
        if (GoButton != null) GoButton.gameObject.SetActive(!string.IsNullOrEmpty(tableId));

        string display = string.IsNullOrEmpty(clubName) ? message : $"{clubName}: {message}";

        _pendingMessage = display;
        if (Label != null) Label.text = display;

        RunLoop(++_runToken).Forget();
    }

    private async UniTaskVoid RunLoop(int token)
    {
        if (Label == null || TextRect == null || Viewport == null) return;

        var cts = this.GetCancellationTokenOnDestroy();

        // Let TMP compute preferred width for the new text before measuring it.
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, cts);
        if (token != _runToken || Label.text != _pendingMessage) return;

        float viewportWidth = Viewport.rect.width;
        float textWidth = Label.preferredWidth;

        float startX = viewportWidth;
        float endX = -textWidth;
        float distance = startX - endX;
        float duration = distance / Mathf.Max(1f, PixelsPerSecond);

        // One pass only: scroll fully off-screen, then hide the strip. No repeat.
        SetX(startX);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (token != _runToken || !isActiveAndEnabled) return;
            elapsed += Time.unscaledDeltaTime;
            SetX(Mathf.Lerp(startX, endX, elapsed / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, cts);
        }
        SetX(endX);

        if (token != _runToken) return;

        if (Viewport != null) Viewport.gameObject.SetActive(false);
        if (GoButton != null) GoButton.gameObject.SetActive(false);

        OnFinished?.Invoke();
    }

    private void SetX(float x)
    {
        Vector2 pos = TextRect.anchoredPosition;
        pos.x = x;
        TextRect.anchoredPosition = pos;
    }
}
