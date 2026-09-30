using System;
using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

/// <summary>
/// "Extend Table Duration" sub-popup of Host Privilege. The slider picks the
/// extension in <see cref="stepHours"/> steps; the remaining time counts down
/// live from the table's expiresAt.
///
/// The slider runs in whole steps (wholeNumbers) so it can't land between them —
/// hours = value × stepHours.
/// </summary>
public class ExtendTableDurationPanel : MonoBehaviour
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Slider durationSlider;

    [Tooltip("Shows the picked duration. {0} = hours, e.g. 0.5")]
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private string durationFormat =
        "Extend Table Duration: <color=#F5C542>{0}h</color>";

    [Tooltip("{0} = hh:mm:ss")]
    [SerializeField] private TextMeshProUGUI remainingText;
    [SerializeField] private string remainingFormat = "Remaining Time: {0}";

    [Header("Range")]
    [SerializeField] private float stepHours = 0.5f;
    [SerializeField] private float maxHours  = 12f;

    private const string MsgExtended = "Extension successful";
    private const string ErrExtend   = "Couldn't extend the table. Please try again.";
    private const string UnknownTime = "--:--:--";

    private CancellationTokenSource _tickCts;
    private bool _busy;

    // Whoever opened us — the Host Privilege popup hides itself while this one
    // is up and comes back through here.
    private Action _onClosed;

    private void Awake()
    {
        if (closeButton != null)   closeButton.onClick.AddListener(Hide);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

        if (durationSlider != null)
        {
            durationSlider.wholeNumbers = true;
            durationSlider.minValue     = 1;
            durationSlider.maxValue     = Mathf.Max(1, Mathf.RoundToInt(maxHours / stepHours));
            durationSlider.onValueChanged.AddListener(_ => RenderDuration());
        }
    }

    /// <param name="onClosed">Runs when this popup closes, by X or by a
    /// successful Confirm.</param>
    public void Open(Action onClosed = null)
    {
        _onClosed = onClosed;

        if (durationSlider != null)
            durationSlider.SetValueWithoutNotify(durationSlider.minValue);

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        _busy = false;
        if (confirmButton != null) confirmButton.interactable = true;

        RenderDuration();
        StartTicking();
    }

    private void OnDisable()
    {
        StopTicking();
    }

    private void Hide()
    {
        gameObject.SetActive(false);

        Action cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    private float PickedHours =>
        durationSlider != null ? durationSlider.value * stepHours : stepHours;

    private void RenderDuration()
    {
        if (durationText == null) return;

        string hours = PickedHours.ToString("0.#", CultureInfo.InvariantCulture);
        durationText.text = string.Format(durationFormat, hours);
    }

    // ── Remaining-time countdown ────────────────────────────────────────────

    private void StartTicking()
    {
        StopTicking();
        _tickCts = new CancellationTokenSource();
        TickAsync(_tickCts.Token).Forget();
    }

    private void StopTicking()
    {
        _tickCts?.Cancel();
        _tickCts?.Dispose();
        _tickCts = null;
    }

    private async UniTaskVoid TickAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                RenderRemaining();
                await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
            }
        }
        catch (OperationCanceledException) { /* panel closed */ }
    }

    private void RenderRemaining()
    {
        if (remainingText == null) return;

        // Re-read each tick — a successful extend moves expiresAt under us.
        string expiresAt = TableContext.Info?.ExpiresAt;

        string value = UnknownTime;

        if (DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture,
                              DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                              out DateTime expiresUtc))
        {
            TimeSpan left = expiresUtc - DateTime.UtcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;

            // TotalHours, not Hours — a table can have more than a day left.
            value = $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
        }

        remainingText.text = string.Format(remainingFormat, value);
    }

    // ── Confirm ─────────────────────────────────────────────────────────────

    private void OnConfirm() => ExtendAsync().Forget();

    private async UniTaskVoid ExtendAsync()
    {
        TableInfo info = TableContext.Info;
        if (_busy || info == null || string.IsNullOrEmpty(info.ClubTableRowId))
            return;

        _busy = true;
        if (confirmButton != null) confirmButton.interactable = false;

        int minutes = Mathf.RoundToInt(PickedHours * 60f);

        try
        {
            ExtendTableResponse response = await AuthManager.Instance.ExtendTableAsync(
                info.ClubId, info.ClubTableRowId, minutes);

            if (response == null)
                throw new Exception("Empty extend response");

            if (!string.IsNullOrEmpty(response.ExpiresAt))
                info.ExpiresAt = response.ExpiresAt;

            TableContext.NotifyClubRowChanged();

            ToastEvents.Show(MsgExtended);
            Hide();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ExtendTable] Failed: {e.Message}");
            ToastEvents.Show(ErrExtend);

            _busy = false;
            if (confirmButton != null) confirmButton.interactable = true;
        }
    }
}
