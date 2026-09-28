using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class AdminMobilePushPopupScript : MonoBehaviour
{
    private const int TitleMax = 20;
    private const int ContentMax = 150;

    public Button Close_Button;

    public TMP_InputField Title_Input;
    public TMP_InputField Content_Input;

    public TextMeshProUGUI Preview_Title_Text;
    public TextMeshProUGUI Preview_Content_Text;

    public string Preview_Title_Placeholder = "Title";
    public string Preview_Content_Placeholder = "Content";

    public TextMeshProUGUI Balance_Text;
    public TextMeshProUGUI Cost_Text;
    public TextMeshProUGUI Quota_Text;

    public int CostEstimate = 0;

    public Button Confirm_Button;

    private long _available;

    private int _quotaLimit;
    private int _quotaRemaining;

    private bool _quotaLoaded;
    private bool _quotaRefreshing;

    private DateTime _resetAtUtc;
    private bool _hasResetTime;

    private Coroutine _timerCoroutine;

    private void Start()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(Close);
        if (Confirm_Button != null) Confirm_Button.onClick.AddListener(OnConfirmTap);

        if (Title_Input != null)
        {
            Title_Input.characterLimit = TitleMax;
            Title_Input.onValueChanged.AddListener(_ => RefreshPreview());
        }

        if (Content_Input != null)
        {
            Content_Input.characterLimit = ContentMax;
            Content_Input.onValueChanged.AddListener(_ => RefreshPreview());
        }
    }

    private void OnEnable()
    {
        if (Title_Input != null) Title_Input.text = "";
        if (Content_Input != null) Content_Input.text = "";
        if (Cost_Text != null) Cost_Text.text = CostEstimate > 0 ? CostEstimate.ToString() : "-";

        _quotaLoaded = false;
        _quotaLimit = 0;
        _quotaRemaining = 0;
        _hasResetTime = false;

        if (Quota_Text != null) Quota_Text.text = "Current available sending times : -/- , reset after\n-- : -- : --";

        RefreshPreview();

        LoadBalance().Forget();
        LoadQuota().Forget();

        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(QuotaTimer());
    }

    private void OnDisable()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
    }

    private void RefreshPreview()
    {
        if (Preview_Title_Text != null)
        {
            string t = Title_Input != null ? Title_Input.text : "";
            Preview_Title_Text.text = string.IsNullOrEmpty(t) ? Preview_Title_Placeholder : t;
        }

        if (Preview_Content_Text != null)
        {
            string c = Content_Input != null ? Content_Input.text : "";
            Preview_Content_Text.text = string.IsNullOrEmpty(c) ? Preview_Content_Placeholder : c;
        }
    }

    private async UniTaskVoid LoadBalance()
    {
        try
        {
            var d = await ClubManager.Instance.GetDiamondsAsync();

            _available = d?.Available ?? 0;

            if (Balance_Text != null) Balance_Text.text = _available.ToString("N0");
        }
        catch (Exception e)
        {
            Debug.LogError("[AdminMobilePushPopupScript] balance load error: " + e.Message);

            if (Balance_Text != null) Balance_Text.text = "-";
        }
    }

    private async UniTaskVoid LoadQuota()
    {
        if (_quotaRefreshing) return;

        _quotaRefreshing = true;

        try
        {
            var quota = await ClubManager.Instance.GetPushQuotaAsync(ClubContext.ClubId);

            if (quota == null) return;

            _quotaLimit = quota.Limit;
            _quotaRemaining = quota.Remaining;
            _quotaLoaded = true;

            SetResetTime(quota.ResetAt);
            RefreshQuotaText();

            Debug.Log("Push Quota : " + _quotaRemaining + "/" + _quotaLimit + " Reset : " + quota.ResetAt);
        }
        catch (Exception e)
        {
            Debug.LogError("[AdminMobilePushPopupScript] quota load error: " + e.Message);

            if (Quota_Text != null) Quota_Text.text = "Current available sending times : -/- , reset after\n-- : -- : --";
        }
        finally
        {
            _quotaRefreshing = false;
        }
    }

    private void SetResetTime(string resetAt)
    {
        _hasResetTime = false;

        if (string.IsNullOrEmpty(resetAt)) return;

        DateTimeOffset value;

        if (DateTimeOffset.TryParse(
            resetAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value))
        {
            _resetAtUtc = value.UtcDateTime;
            _hasResetTime = true;
        }
        else
        {
            Debug.LogWarning("Could not parse resetAt : " + resetAt);
        }
    }

    private IEnumerator QuotaTimer()
    {
        while (true)
        {
            RefreshQuotaText();

            if (_hasResetTime && DateTime.UtcNow >= _resetAtUtc)
            {
                _hasResetTime = false;
                LoadQuota().Forget();
            }

            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private void RefreshQuotaText()
    {
        if (Quota_Text == null) return;

        string timer = "-- : -- : --";

        if (_hasResetTime)
        {
            TimeSpan remaining = _resetAtUtc - DateTime.UtcNow;

            if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;

            int hours = Mathf.Max(0, (int)remaining.TotalHours);
            int minutes = remaining.Minutes;
            int seconds = remaining.Seconds;

            timer = hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        string quota = _quotaLoaded ? _quotaRemaining + "/" + _quotaLimit : "-/-";

        Quota_Text.text = "Current available sending times : " + quota + " , reset after\n" + timer;
    }

    private void OnConfirmTap()
    {
        string title = Title_Input != null ? Title_Input.text.Trim() : "";
        string content = Content_Input != null ? Content_Input.text.Trim() : "";

        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
        {
            ShowToast("Enter title and content");
            return;
        }

        if (string.IsNullOrEmpty(content))
        {
            ShowToast("The content cannot be empty");
            return;
        }

        if (_quotaLoaded && _quotaRemaining <= 0)
        {
            ShowToast("Push notification limit reached");
            return;
        }

        if (CostEstimate > 0 && _available < CostEstimate)
        {
            ShowToast("Not enough diamonds");
            return;
        }

        Send().Forget();
    }

    private async UniTaskVoid Send()
    {
        string title = Title_Input != null ? Title_Input.text.Trim() : "";
        string content = Content_Input != null ? Content_Input.text.Trim() : "";

        if (Confirm_Button != null) Confirm_Button.interactable = false;

        try
        {
            var res = await ClubManager.Instance.SendPushAsync(ClubContext.ClubId, title, content, null);

            if (res == null) return;

            _available = Math.Max(0, _available - res.DiamondCost);

            if (Balance_Text != null) Balance_Text.text = _available.ToString("N0");

            if (Cost_Text != null) Cost_Text.text = res.DiamondCost.ToString();

            _quotaRemaining = res.RemainingQuota;
            _quotaLoaded = true;

            SetResetTime(res.ResetAt);
            RefreshQuotaText();

            Debug.Log("Push Sent | Remaining : " + res.RemainingQuota + "/" + _quotaLimit + " | Reset : " + res.ResetAt);

            ShowToast("Push Notification Sent");

            await UniTask.Delay(500);

            LoadQuota().Forget();

            Close();
        }
        catch (Exception e)
        {
            Debug.LogError("[AdminMobilePushPopupScript] send error: " + e.Message);
            ShowToast(ResolveError(e));
        }
        finally
        {
            if (Confirm_Button != null) Confirm_Button.interactable = true;
        }
    }

    private static string ResolveError(Exception e)
    {
        string m = e.Message ?? "";

        if (m.IndexOf("diamond", StringComparison.OrdinalIgnoreCase) >= 0 ||
            m.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Not enough diamonds";

        if (m.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
            m.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Push notification limit reached";

        return string.IsNullOrEmpty(m) ? "Failed to send push" : m;
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null) InformationPrefabScript.Instance.ShowMessage(message);
    }
}