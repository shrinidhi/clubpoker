using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectDatePanel : MonoBehaviour
{
    public Button CloseButton;
    public Button ConfirmButton;
    public Text selectdate;
    public Transform MonthContent;
    public GameObject MonthPrefb;

    [Header("Calendar")]
    public ScrollRect CalendarScrollRect;

    [Header("Settings")]
    public int PreviousMonthCount = 2;
    public int MaxSelectableDays = 7;

    private DateTime? startDate;
    private DateTime? endDate;

    private List<GameObject> spawnedMonths = new List<GameObject>();
    private List<DatePrefab> allDatePrefabs = new List<DatePrefab>();

    private float savedScrollPosition = 1f;
    private bool calendarGenerated;

    private void OnEnable()
    {
        if (!calendarGenerated)
        {
            GenerateCalendar();
            CreateDefaultSelection();
            calendarGenerated = true;
        }
        else
        {
            UpdateDateSelection();

            if (startDate.HasValue)
                StartCoroutine(RestoreScroll(startDate.Value));
        }
    }

    private void Start()
    {
        if (CloseButton != null)
        {
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(ClosePanel);
        }

        if (ConfirmButton != null)
        {
            ConfirmButton.onClick.RemoveAllListeners();
            ConfirmButton.onClick.AddListener(ConfirmDate);
        }
    }

    public void GenerateCalendar()
    {
        ClearCalendar();

        DateTime currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        for (int i = PreviousMonthCount; i >= 0; i--)
        {
            DateTime monthDate = currentMonth.AddMonths(-i);

            GameObject monthObject = Instantiate(MonthPrefb, MonthContent);
            monthObject.transform.localScale = Vector3.one;

            MonthPrefab monthPrefab = monthObject.GetComponent<MonthPrefab>();

            if (monthPrefab != null)
            {
                monthPrefab.GenerateMonth(monthDate.Year, monthDate.Month, OnDateSelected);

                DatePrefab[] dates = monthObject.GetComponentsInChildren<DatePrefab>(true);

                for (int d = 0; d < dates.Length; d++)
                {
                    if (dates[d] != null)
                        allDatePrefabs.Add(dates[d]);
                }
            }

            spawnedMonths.Add(monthObject);
        }

        Canvas.ForceUpdateCanvases();

        if (CalendarScrollRect != null)
            CalendarScrollRect.verticalNormalizedPosition = savedScrollPosition;
    }

    private void CreateDefaultSelection()
    {
        DateTime currentDate = DateTime.Now.Date;

        endDate = currentDate;
        startDate = currentDate.AddDays(-(MaxSelectableDays - 1));

        UpdateDateSelection();
        UpdateDateText();

        StartCoroutine(ScrollToSelectedDateCoroutine(currentDate));
    }

    private void OnDateSelected(DateTime selectedDate)
    {
        selectedDate = selectedDate.Date;

        if (selectedDate > DateTime.Now.Date)
            return;

        if (!startDate.HasValue || !endDate.HasValue)
        {
            startDate = selectedDate;
            endDate = selectedDate;

            UpdateDateSelection();
            UpdateDateText();
            ScrollToSelectedDate(selectedDate);

            return;
        }

        if (startDate.Value.Date == endDate.Value.Date)
        {
            if (selectedDate == startDate.Value.Date)
                return;

            if (selectedDate < startDate.Value.Date)
            {
                int days = (startDate.Value.Date - selectedDate).Days + 1;

                if (days <= MaxSelectableDays)
                {
                    startDate = selectedDate;

                    UpdateDateSelection();
                    UpdateDateText();
                    ScrollToSelectedDate(selectedDate);
                }
                else
                {
                    startDate = selectedDate;
                    endDate = selectedDate;

                    UpdateDateSelection();
                    UpdateDateText();
                    ScrollToSelectedDate(selectedDate);
                }

                return;
            }

            if (selectedDate > endDate.Value.Date)
            {
                int days = (selectedDate - endDate.Value.Date).Days + 1;

                if (days <= MaxSelectableDays)
                {
                    endDate = selectedDate;

                    UpdateDateSelection();
                    UpdateDateText();
                    ScrollToSelectedDate(selectedDate);
                }

                return;
            }
        }

        if (startDate.Value.Date != endDate.Value.Date)
        {
            if (selectedDate < startDate.Value.Date || selectedDate > endDate.Value.Date)
            {
                int daysFromCurrentStart = Math.Abs((selectedDate - startDate.Value.Date).Days) + 1;
                int daysFromCurrentEnd = Math.Abs((selectedDate - endDate.Value.Date).Days) + 1;

                if (daysFromCurrentStart > MaxSelectableDays && daysFromCurrentEnd > MaxSelectableDays)
                {
                    startDate = selectedDate;
                    endDate = selectedDate;

                    UpdateDateSelection();
                    UpdateDateText();
                    ScrollToSelectedDate(selectedDate);

                    return;
                }

                if (selectedDate < startDate.Value.Date)
                {
                    int days = (endDate.Value.Date - selectedDate).Days + 1;

                    if (days <= MaxSelectableDays)
                    {
                        startDate = selectedDate;

                        UpdateDateSelection();
                        UpdateDateText();
                        ScrollToSelectedDate(selectedDate);
                    }
                    else
                    {
                        startDate = selectedDate;
                        endDate = selectedDate;

                        UpdateDateSelection();
                        UpdateDateText();
                        ScrollToSelectedDate(selectedDate);
                    }

                    return;
                }

                if (selectedDate > endDate.Value.Date)
                {
                    int days = (selectedDate - startDate.Value.Date).Days + 1;

                    if (days <= MaxSelectableDays)
                    {
                        endDate = selectedDate;

                        UpdateDateSelection();
                        UpdateDateText();
                        ScrollToSelectedDate(selectedDate);
                    }
                    else
                    {
                        startDate = selectedDate;
                        endDate = selectedDate;

                        UpdateDateSelection();
                        UpdateDateText();
                        ScrollToSelectedDate(selectedDate);
                    }

                    return;
                }
            }

            startDate = selectedDate;
            endDate = selectedDate;

            UpdateDateSelection();
            UpdateDateText();
            ScrollToSelectedDate(selectedDate);
        }
    }

    public void OnDateDoubleClicked(DateTime clickedDate)
    {
        clickedDate = clickedDate.Date;

        if (clickedDate > DateTime.Now.Date)
            return;

        startDate = clickedDate;
        endDate = clickedDate;

        UpdateDateSelection();

        if (selectdate != null)
            selectdate.text = "Start: " + clickedDate.ToString("yyyy.MM.dd") + " - End: " + clickedDate.ToString("yyyy.MM.dd");

        ScrollToSelectedDate(clickedDate);
    }

    public void OnDateSelectedAgain(DateTime selectedDate)
    {
        selectedDate = selectedDate.Date;

        if (selectedDate > DateTime.Now.Date)
            return;

        startDate = selectedDate;
        endDate = selectedDate;

        UpdateDateSelection();
        UpdateDateText();
        ScrollToSelectedDate(selectedDate);
    }

    private void UpdateDateSelection()
    {
        for (int i = 0; i < allDatePrefabs.Count; i++)
        {
            DatePrefab datePrefab = allDatePrefabs[i];

            if (datePrefab == null)
                continue;

            DateTime date = datePrefab.GetDate().Date;
            bool selected = false;

            if (startDate.HasValue && endDate.HasValue)
                selected = date >= startDate.Value.Date && date <= endDate.Value.Date;

            datePrefab.SetSelected(selected);
        }
    }

    private void UpdateDateText()
    {
        if (selectdate == null || !startDate.HasValue || !endDate.HasValue)
            return;

        if (startDate.Value.Date == endDate.Value.Date)
            selectdate.text = "Start: " + startDate.Value.ToString("yyyy.MM.dd");
        else
            selectdate.text = "Start: " + startDate.Value.ToString("yyyy.MM.dd") + " - End: " + endDate.Value.ToString("yyyy.MM.dd");
    }

    private void ScrollToSelectedDate(DateTime selectedDate)
    {
        StartCoroutine(ScrollToSelectedDateCoroutine(selectedDate));
    }

    private IEnumerator ScrollToSelectedDateCoroutine(DateTime selectedDate)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        DatePrefab targetDatePrefab = null;

        for (int i = 0; i < allDatePrefabs.Count; i++)
        {
            if (allDatePrefabs[i] != null && allDatePrefabs[i].GetDate().Date == selectedDate.Date)
            {
                targetDatePrefab = allDatePrefabs[i];
                break;
            }
        }

        if (targetDatePrefab == null || CalendarScrollRect == null)
            yield break;

        RectTransform content = CalendarScrollRect.content;
        RectTransform viewport = CalendarScrollRect.viewport;
        RectTransform target = targetDatePrefab.GetComponent<RectTransform>();

        if (content == null || viewport == null || target == null)
            yield break;

        Canvas.ForceUpdateCanvases();

        Vector3[] targetCorners = new Vector3[4];
        Vector3[] viewportCorners = new Vector3[4];

        target.GetWorldCorners(targetCorners);
        viewport.GetWorldCorners(viewportCorners);

        float targetCenter = (targetCorners[0].y + targetCorners[1].y) * 0.5f;
        float viewportCenter = (viewportCorners[0].y + viewportCorners[1].y) * 0.5f;
        float difference = targetCenter - viewportCenter;

        Vector2 contentPosition = content.anchoredPosition;
        contentPosition.y -= difference;

        float maxY = Mathf.Max(0, content.rect.height - viewport.rect.height);

        contentPosition.y = Mathf.Clamp(contentPosition.y, 0, maxY);
        content.anchoredPosition = contentPosition;

        Canvas.ForceUpdateCanvases();

        savedScrollPosition = CalendarScrollRect.verticalNormalizedPosition;
    }

    private IEnumerator RestoreScroll(DateTime selectedDate)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        ScrollToSelectedDate(selectedDate);
    }

    private void ConfirmDate()
    {
        if (!startDate.HasValue || !endDate.HasValue)
            return;

        int totalDays = (endDate.Value.Date - startDate.Value.Date).Days + 1;

        if (totalDays < 1 || totalDays > MaxSelectableDays)
            return;

        UpdateDateText();
    }

    public DateTime? GetStartDate()
    {
        return startDate;
    }

    public DateTime? GetEndDate()
    {
        return endDate;
    }

    public bool IsDateRangeComplete()
    {
        return startDate.HasValue && endDate.HasValue;
    }

    public void ResetSelection()
    {
        startDate = null;
        endDate = null;
        savedScrollPosition = 1f;
        calendarGenerated = false;

        if (selectdate != null)
            selectdate.text = "";

        for (int i = 0; i < allDatePrefabs.Count; i++)
        {
            if (allDatePrefabs[i] != null)
                allDatePrefabs[i].SetSelected(false);
        }
    }

    private void ClearCalendar()
    {
        allDatePrefabs.Clear();
        spawnedMonths.Clear();

        if (MonthContent == null)
            return;

        for (int i = MonthContent.childCount - 1; i >= 0; i--)
            Destroy(MonthContent.GetChild(i).gameObject);
    }

    private void ClosePanel()
    {
        if (CalendarScrollRect != null)
            savedScrollPosition = CalendarScrollRect.verticalNormalizedPosition;

        gameObject.SetActive(false);
    }
}