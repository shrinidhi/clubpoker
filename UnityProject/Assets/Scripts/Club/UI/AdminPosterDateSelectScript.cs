using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class AdminPosterDateSelectScript : MonoBehaviour
{
    private bool _initialized = false;
    private Action<DateTime> _onConfirmed;
    [Header("Buttons")]
    public Button CloseButton;
    public Button ResetButton;
    public Button ConfirmButton;
    [Header("Selected Date Text")]
    public Text SelectDateText;
    [Header("Prefabs")]
    public GameObject DatePrefab;
    public GameObject HourPrefab;
    public GameObject MinPrefab;
    [Header("Scroll Rects")]
    public ScrollRect DateScrollRect;
    public ScrollRect HourScrollRect;
    public ScrollRect MinuteScrollRect;
    [Header("Contents")]
    public RectTransform DateContent;
    public RectTransform HourContent;
    public RectTransform MinuteContent;
    [Header("Settings")]
    public int TotalDays = 30;
    public int BlankCount = 2;
    [Header("Snap")]
    public float SnapDuration = 0.15f;
    public float SnapDelay = 0.08f;
    [Header("Selection")]
    public Color NormalColor = new Color(1f, 1f, 1f, 0.7f);
    public Color SelectedColor = new Color(0.35f, 0.65f, 1f, 1f);
    public float SelectedScale = 1.12f;
    public float NormalScale = 1f;
    private readonly List<DateTime> dateValues = new List<DateTime>();
    private readonly List<Text> dateTexts = new List<Text>();
    private readonly List<Text> hourTexts = new List<Text>();
    private readonly List<Text> minuteTexts = new List<Text>();
    private int selectedDateIndex;
    private int selectedHour;
    private int selectedMinute;
    private DateTime selectedDateTime;
    private bool dateNeedSnap;
    private bool hourNeedSnap;
    private bool minuteNeedSnap;
    private bool dateSnapping;
    private bool hourSnapping;
    private bool minuteSnapping;
    private float dateLastMoveTime;
    private float hourLastMoveTime;
    private float minuteLastMoveTime;
    private Coroutine dateSnapCoroutine;
    private Coroutine hourSnapCoroutine;
    private Coroutine minuteSnapCoroutine;
    private void Start()
    {
        SetupButtons();
       // CreateDates();
        //CreateHours();
       // CreateMinutes();
        DateTime now = DateTime.Now;
        selectedDateIndex = 0;
        selectedHour = now.Hour;
        selectedMinute = now.Minute;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(DateContent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(HourContent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(MinuteContent);
        Canvas.ForceUpdateCanvases();
        CenterImmediately(DateScrollRect, dateTexts[selectedDateIndex].rectTransform);
        CenterImmediately(HourScrollRect, hourTexts[selectedHour].rectTransform);
        CenterImmediately(MinuteScrollRect, minuteTexts[selectedMinute].rectTransform);
        UpdateSelection();
       // SetupScrollEvents();
        InitializeIfNeeded();
    }
    private void Update()
    {
        UpdateDateSelection();
        UpdateHourSelection();
        UpdateMinuteSelection();
        UpdateSelectedDateTime();
        CheckSnap();
    }
    private void InitializeIfNeeded()
    {
        if (_initialized)
            return;

        _initialized = true;

        SetupButtons();

        CreateDates();
        CreateHours();
        CreateMinutes();

        Canvas.ForceUpdateCanvases();

        if (DateContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(DateContent);

        if (HourContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(HourContent);

        if (MinuteContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(MinuteContent);

        Canvas.ForceUpdateCanvases();

        SetupScrollEvents();
    }
    private void SetupButtons()
    {
        if (CloseButton != null)
        {
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(Close);
        }
        if (ResetButton != null)
        {
            ResetButton.onClick.RemoveAllListeners();
            ResetButton.onClick.AddListener(ResetSelection);
        }
        if (ConfirmButton != null)
        {
            ConfirmButton.onClick.RemoveAllListeners();
            ConfirmButton.onClick.AddListener(ConfirmSelection);
        }
    }
    private void SetupScrollEvents()
    {
        if (DateScrollRect != null)
        {
            DateScrollRect.onValueChanged.AddListener(delegate { if (!dateSnapping) { dateNeedSnap = true; dateLastMoveTime = Time.unscaledTime; } });
        }
        if (HourScrollRect != null)
        {
            HourScrollRect.onValueChanged.AddListener(delegate { if (!hourSnapping) { hourNeedSnap = true; hourLastMoveTime = Time.unscaledTime; } });
        }
        if (MinuteScrollRect != null)
        {
            MinuteScrollRect.onValueChanged.AddListener(delegate { if (!minuteSnapping) { minuteNeedSnap = true; minuteLastMoveTime = Time.unscaledTime; } });
        }
    }
    private void CreateDates()
    {
        ClearContent(DateContent);
        dateValues.Clear();
        dateTexts.Clear();
        DateTime today = DateTime.Today;
        for (int i = 0; i < BlankCount; i++)
        {
            CreateBlankItem(DatePrefab, DateContent);
        }
        for (int i = 0; i < TotalDays; i++)
        {
            DateTime date = today.AddDays(i);
            dateValues.Add(date);
            GameObject obj = Instantiate(DatePrefab, DateContent);
            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = date.ToString("M/d ddd");
                dateTexts.Add(txt);
            }
        }
        for (int i = 0; i < BlankCount; i++)
        {
            CreateBlankItem(DatePrefab, DateContent);
        }
    }
    private void CreateHours()
    {
        ClearContent(HourContent);
        hourTexts.Clear();
        for (int i = 0; i < BlankCount; i++)
        {
            CreateBlankItem(HourPrefab, HourContent);
        }
        for (int i = 0; i < 24; i++)
        {
            GameObject obj = Instantiate(HourPrefab, HourContent);
            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = i.ToString("00");
                hourTexts.Add(txt);
            }
        }
        for (int i = 0; i < BlankCount; i++)
        {
            CreateBlankItem(HourPrefab, HourContent);
        }
    }
    private void CreateMinutes()
    {
        ClearContent(MinuteContent);
        minuteTexts.Clear();
        for (int i = 0; i < BlankCount; i++)
        {
            CreateBlankItem(MinPrefab, MinuteContent);
        }
        for (int i = 0; i < 60; i++)
        {
            GameObject obj = Instantiate(MinPrefab, MinuteContent);
            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = i.ToString("00");
                minuteTexts.Add(txt);
            }
        }
        for (int i = 0; i < BlankCount; i++)
        {
            CreateBlankItem(MinPrefab, MinuteContent);
        }
    }
    private void CreateBlankItem(GameObject prefab, RectTransform parent)
    {
        GameObject obj = Instantiate(prefab, parent);
        Text txt = obj.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.text = "";
            txt.raycastTarget = false;
        }
    }
    private void UpdateDateSelection()
    {
        if (dateTexts.Count == 0 || DateScrollRect == null)
            return;
        int closest = GetClosestToCenter(dateTexts, DateScrollRect.viewport);
        selectedDateIndex = closest;
        UpdateHighlight(dateTexts, selectedDateIndex);
    }
    private void UpdateHourSelection()
    {
        if (hourTexts.Count == 0 || HourScrollRect == null)
            return;
        int closest = GetClosestToCenter(hourTexts, HourScrollRect.viewport);
        selectedHour = closest;
        UpdateHighlight(hourTexts, selectedHour);
    }
    private void UpdateMinuteSelection()
    {
        if (minuteTexts.Count == 0 || MinuteScrollRect == null)
            return;
        int closest = GetClosestToCenter(minuteTexts, MinuteScrollRect.viewport);
        selectedMinute = closest;
        UpdateHighlight(minuteTexts, selectedMinute);
    }
    private int GetClosestToCenter(List<Text> items, RectTransform viewport)
    {
        if (items.Count == 0 || viewport == null)
            return 0;
        Vector3 viewportCenter = viewport.TransformPoint(viewport.rect.center);
        float minimumDistance = float.MaxValue;
        int closestIndex = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null)
                continue;
            RectTransform itemRect = items[i].rectTransform;
            Vector3 itemCenter = itemRect.TransformPoint(itemRect.rect.center);
            float distance = Mathf.Abs(itemCenter.y - viewportCenter.y);
            if (distance < minimumDistance)
            {
                minimumDistance = distance;
                closestIndex = i;
            }
        }
        return closestIndex;
    }
    private void UpdateHighlight(List<Text> items, int selectedIndex)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null)
                continue;
            bool selected = i == selectedIndex;
            items[i].color = selected ? SelectedColor : NormalColor;
            float scale = selected ? SelectedScale : NormalScale;
            items[i].rectTransform.localScale = Vector3.one * scale;
        }
    }
    private void CheckSnap()
    {
        if (dateNeedSnap && !dateSnapping && Time.unscaledTime - dateLastMoveTime >= SnapDelay)
        {
            dateNeedSnap = false;
            if (dateSnapCoroutine != null)
                StopCoroutine(dateSnapCoroutine);
            int index = GetClosestToCenter(dateTexts, DateScrollRect.viewport);
            dateSnapCoroutine = StartCoroutine(SnapDate(index));
        }
        if (hourNeedSnap && !hourSnapping && Time.unscaledTime - hourLastMoveTime >= SnapDelay)
        {
            hourNeedSnap = false;
            if (hourSnapCoroutine != null)
                StopCoroutine(hourSnapCoroutine);
            int index = GetClosestToCenter(hourTexts, HourScrollRect.viewport);
            hourSnapCoroutine = StartCoroutine(SnapHour(index));
        }
        if (minuteNeedSnap && !minuteSnapping && Time.unscaledTime - minuteLastMoveTime >= SnapDelay)
        {
            minuteNeedSnap = false;
            if (minuteSnapCoroutine != null)
                StopCoroutine(minuteSnapCoroutine);
            int index = GetClosestToCenter(minuteTexts, MinuteScrollRect.viewport);
            minuteSnapCoroutine = StartCoroutine(SnapMinute(index));
        }
    }
    private IEnumerator SnapDate(int index)
    {
        dateSnapping = true;
        DateScrollRect.StopMovement();
        yield return SnapToItem(DateScrollRect, dateTexts[index].rectTransform);
        selectedDateIndex = index;
        dateSnapping = false;
        UpdateSelection();
    }
    private IEnumerator SnapHour(int index)
    {
        hourSnapping = true;
        HourScrollRect.StopMovement();
        yield return SnapToItem(HourScrollRect, hourTexts[index].rectTransform);
        selectedHour = index;
        hourSnapping = false;
        UpdateSelection();
    }
    private IEnumerator SnapMinute(int index)
    {
        minuteSnapping = true;
        MinuteScrollRect.StopMovement();
        yield return SnapToItem(MinuteScrollRect, minuteTexts[index].rectTransform);
        selectedMinute = index;
        minuteSnapping = false;
        UpdateSelection();
    }
    private IEnumerator SnapToItem(ScrollRect scrollRect, RectTransform item)
    {
        if (scrollRect == null || item == null)
            yield break;
        RectTransform content = scrollRect.content;
        Vector2 startPosition = content.anchoredPosition;
        Vector2 targetPosition = GetCenteredPosition(scrollRect, item);
        float timer = 0f;
        while (timer < SnapDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / SnapDuration);
            t = t * t * (3f - 2f * t);
            content.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            yield return null;
        }
        content.anchoredPosition = targetPosition;
        scrollRect.velocity = Vector2.zero;
    }
    private Vector2 GetCenteredPosition(ScrollRect scrollRect, RectTransform item)
    {
        RectTransform viewport = scrollRect.viewport;
        RectTransform content = scrollRect.content;
        Vector3 viewportCenterWorld = viewport.TransformPoint(viewport.rect.center);
        Vector3 itemCenterWorld = item.TransformPoint(item.rect.center);
        Vector3 differenceWorld = viewportCenterWorld - itemCenterWorld;
        Vector3 differenceLocal = content.parent.InverseTransformVector(differenceWorld);
        Vector2 targetPosition = content.anchoredPosition;
        targetPosition.y += differenceLocal.y;
        return targetPosition;
    }
    private void CenterImmediately(ScrollRect scrollRect, RectTransform item)
    {
        if (scrollRect == null || item == null)
            return;
        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.content.anchoredPosition = GetCenteredPosition(scrollRect, item);
        scrollRect.velocity = Vector2.zero;
    }
    private void UpdateSelectedDateTime()
    {
        if (dateValues.Count == 0)
            return;
        selectedDateIndex = Mathf.Clamp(selectedDateIndex, 0, dateValues.Count - 1);
        selectedHour = Mathf.Clamp(selectedHour, 0, 23);
        selectedMinute = Mathf.Clamp(selectedMinute, 0, 59);
        DateTime selectedDate = dateValues[selectedDateIndex];
        selectedDateTime = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, selectedHour, selectedMinute, 0);
        UpdateTopText();
        CheckConfirmButton();
    }
    private void UpdateTopText()
    {
        if (SelectDateText == null)
            return;
        SelectDateText.text = selectedDateTime.ToString("MM/dd ddd HH:mm");
    }
    private void CheckConfirmButton()
    {
        if (ConfirmButton == null)
            return;
        DateTime now = DateTime.Now;
        DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        ConfirmButton.interactable = selectedDateTime >= currentMinute;
    }
    private void ResetSelection()
    {
        InitializeIfNeeded();

        // Safety
        if (dateTexts.Count == 0 ||
            hourTexts.Count == 0 ||
            minuteTexts.Count == 0 ||
            dateValues.Count == 0)
        {
            Debug.LogError(
                "[AdminPosterDateSelectScript] Picker data not initialized."
            );

            return;
        }

        DateTime now = DateTime.Now;

        selectedDateIndex = 0;

        selectedHour = Mathf.Clamp(
            now.Hour,
            0,
            hourTexts.Count - 1
        );

        selectedMinute = Mathf.Clamp(
            now.Minute,
            0,
            minuteTexts.Count - 1
        );

        Canvas.ForceUpdateCanvases();

        if (DateContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(DateContent);

        if (HourContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(HourContent);

        if (MinuteContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(MinuteContent);

        Canvas.ForceUpdateCanvases();

        CenterImmediately(
            DateScrollRect,
            dateTexts[selectedDateIndex].rectTransform
        );

        CenterImmediately(
            HourScrollRect,
            hourTexts[selectedHour].rectTransform
        );

        CenterImmediately(
            MinuteScrollRect,
            minuteTexts[selectedMinute].rectTransform
        );

        UpdateSelection();
    }
    private void UpdateSelection()
    {
        UpdateDateSelection();
        UpdateHourSelection();
        UpdateMinuteSelection();
        UpdateSelectedDateTime();
    }
    private void ConfirmSelection()
    {
        if (ConfirmButton == null || !ConfirmButton.interactable)
            return;

        Debug.Log(
            "Selected Poster Date : " +
            selectedDateTime.ToString("yyyy-MM-dd HH:mm:ss")
        );

        
        _onConfirmed?.Invoke(selectedDateTime);

        Close();
    }

    public void Open(Action<DateTime> onConfirmed)
    {
        _onConfirmed = onConfirmed;

        gameObject.SetActive(true);

        InitializeIfNeeded();

        ResetSelection();
    }
    private void Close()
    {
        gameObject.SetActive(false);
    }
    private void ClearContent(RectTransform content)
    {
        if (content == null)
            return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }
    }
    public DateTime GetSelectedDateTime()
    {
        return selectedDateTime;
    }
}
