using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class PromoSelectDateTimeScript : MonoBehaviour
{
    public Button CloseButton;
    public Button ClearButton;
    public Button ConfirmButton;

    public Text SelectDateTime;

    public GameObject DatePrefab;
    public GameObject HourPrefab;
    public GameObject MinPrefab;
    public GameObject AMPM_Prefab;

    public Transform DateContent;
    public Transform HourContent;
    public Transform MinContent;
    public Transform AMPM_Content;

    public ScrollRect DateScrollRect;
    public ScrollRect HourScrollRect;
    public ScrollRect MinScrollRect;
    public ScrollRect AMPM_ScrollRect;

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

    private class PickerColumn
    {
        public GameObject Prefab;
        public Transform Content;
        public ScrollRect Scroll;
        public readonly List<Text> Texts = new List<Text>();

        public int Index;
        public bool NeedSnap;
        public bool Snapping;
        public float LastMoveTime;
        public UnityAction<Vector2> Listener;
    }

    private PickerColumn[] columns;
    private readonly List<DateTime> dateValues = new List<DateTime>();
    private Action<DateTime> onConfirmed;
    private DateTime selectedDateTime;
    private bool ready;

    private void Awake()
    {
        columns = new[]
        {
            CreateColumn(DatePrefab, DateContent, DateScrollRect),
            CreateColumn(HourPrefab, HourContent, HourScrollRect),
            CreateColumn(MinPrefab, MinContent, MinScrollRect),
            CreateColumn(AMPM_Prefab, AMPM_Content, AMPM_ScrollRect)
        };

        if (CloseButton != null)
            CloseButton.onClick.AddListener(Close);

        if (ClearButton != null)
            ClearButton.onClick.AddListener(ResetSelection);

        if (ConfirmButton != null)
            ConfirmButton.onClick.AddListener(ConfirmSelection);
    }

    private PickerColumn CreateColumn(
        GameObject prefab, Transform content, ScrollRect scroll)
    {
        var column = new PickerColumn
        {
            Prefab = prefab,
            Content = content,
            Scroll = scroll
        };

        column.Listener = position =>
        {
            if (!ready || column.Snapping) return;

            column.NeedSnap = true;
            column.LastMoveTime = Time.unscaledTime;
        };

        if (scroll != null)
            scroll.onValueChanged.AddListener(column.Listener);

        return column;
    }

    private void OnEnable()
    {
        ResetSelection();
    }

    private void OnDisable()
    {
        ready = false;
        StopAllCoroutines();

        if (columns == null) return;

        foreach (var column in columns)
        {
            column.Snapping = false;
            column.NeedSnap = false;

            if (column.Scroll != null)
                column.Scroll.StopMovement();
        }
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(Close);

        if (ClearButton != null)
            ClearButton.onClick.RemoveListener(ResetSelection);

        if (ConfirmButton != null)
            ConfirmButton.onClick.RemoveListener(ConfirmSelection);

        if (columns == null) return;

        foreach (var column in columns)
        {
            if (column.Scroll != null)
                column.Scroll.onValueChanged.RemoveListener(column.Listener);
        }
    }

    public void Open(Action<DateTime> confirmedCallback)
    {
        onConfirmed = confirmedCallback;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        else
            ResetSelection();
    }

    private void ResetSelection()
    {
        if (!isActiveAndEnabled) return;

        ready = false;
        StopAllCoroutines();

        if (ConfirmButton != null)
            ConfirmButton.interactable = false;

        foreach (var column in columns)
        {
            column.Snapping = false;
            column.NeedSnap = false;

            if (column.Scroll != null)
                column.Scroll.StopMovement();
        }

        if (!ValidateReferences()) return;

        StartCoroutine(BuildPicker());
    }

    private bool ValidateReferences()
    {
        foreach (var column in columns)
        {
            if (column.Prefab == null ||
                !(column.Content is RectTransform) ||
                column.Scroll == null ||
                column.Scroll.viewport == null ||
                column.Prefab.GetComponentInChildren<Text>(true) == null)
            {
                Debug.LogError(
                    "[PromoDateTime] Assign all prefabs, contents and scroll viewports. " +
                    "Each prefab needs a Text component.");

                return false;
            }

            column.Scroll.content = (RectTransform)column.Content;
        }

        return true;
    }

    private IEnumerator BuildPicker()
    {
        dateValues.Clear();
        var dates = new List<string>();
        var hours = new List<string>();
        var minutes = new List<string>();

        DateTime today = DateTime.Today;

        for (int i = 0; i < Mathf.Max(1, TotalDays); i++)
        {
            DateTime date = today.AddDays(i);
            dateValues.Add(date);
            dates.Add(date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        }

        for (int hour = 1; hour <= 12; hour++)
            hours.Add(hour.ToString("00"));

        for (int minute = 0; minute < 60; minute++)
            minutes.Add(minute.ToString("00"));

        BuildColumn(columns[0], dates);
        BuildColumn(columns[1], hours);
        BuildColumn(columns[2], minutes);
        BuildColumn(columns[3], new List<string> { "AM", "PM" });

        // Allow old rows to be destroyed and new layouts to initialize.
        yield return null;

        Canvas.ForceUpdateCanvases();

        foreach (var column in columns)
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                (RectTransform)column.Content);

        Canvas.ForceUpdateCanvases();

        DateTime now = DateTime.Now;
        int hour12 = now.Hour % 12;
        if (hour12 == 0) hour12 = 12;

        columns[0].Index = 0;
        columns[1].Index = hour12 - 1;
        columns[2].Index = now.Minute;
        columns[3].Index = now.Hour >= 12 ? 1 : 0;

        foreach (var column in columns)
        {
            column.Scroll.StopMovement();
            column.Scroll.content.anchoredPosition =
                GetCenteredPosition(column, column.Index);
        }

        ready = true;
        UpdateSelection();
    }

    private void BuildColumn(PickerColumn column, List<string> values)
    {
        for (int i = column.Content.childCount - 1; i >= 0; i--)
        {
            GameObject child = column.Content.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        column.Texts.Clear();

        int blanks = Mathf.Max(0, BlankCount);

        for (int i = 0; i < blanks; i++)
            CreateItem(column, "", false);

        foreach (string value in values)
            CreateItem(column, value, true);

        for (int i = 0; i < blanks; i++)
            CreateItem(column, "", false);
    }

    private void CreateItem(PickerColumn column, string value, bool selectable)
    {
        GameObject instance = Instantiate(column.Prefab, column.Content);
        instance.SetActive(true);

        Text text = instance.GetComponentInChildren<Text>(true);
        text.text = value;
        text.supportRichText = false;

        if (selectable)
            column.Texts.Add(text);
        else
            text.raycastTarget = false;
    }

    private void Update()
    {
        if (!ready) return;

        UpdateSelection();

        foreach (var column in columns)
        {
            if (!column.NeedSnap || column.Snapping)
                continue;

            // Wait until dragging and scroll inertia have stopped.
            if (Input.GetMouseButton(0) || Input.touchCount > 0)
                continue;

            if (column.Scroll.velocity.sqrMagnitude > 25f)
                continue;

            if (Time.unscaledTime - column.LastMoveTime < SnapDelay)
                continue;

            column.NeedSnap = false;
            StartCoroutine(SnapColumn(column));
        }
    }

    private void UpdateSelection()
    {
        foreach (var column in columns)
        {
            if (!column.Snapping)
                column.Index = GetClosestToCenter(column);

            for (int i = 0; i < column.Texts.Count; i++)
            {
                bool selected = i == column.Index;
                Text text = column.Texts[i];

                text.color = selected ? SelectedColor : NormalColor;
                text.rectTransform.localScale = Vector3.one *
                    (selected ? SelectedScale : NormalScale);
            }
        }

        DateTime date = dateValues[columns[0].Index];

        int hour12 = columns[1].Index + 1;
        int hour24 = hour12 % 12;

        if (columns[3].Index == 1)
            hour24 += 12;

        selectedDateTime = new DateTime(
            date.Year, date.Month, date.Day,
            hour24, columns[2].Index, 0,
            DateTimeKind.Local);

        if (SelectDateTime != null)
        {
            SelectDateTime.text = selectedDateTime.ToString(
                "dd/MM/yyyy hh:mm tt",
                CultureInfo.InvariantCulture);
        }

        if (ConfirmButton != null)
        {
            DateTime now = DateTime.Now;
            DateTime currentMinute = new DateTime(
                now.Year, now.Month, now.Day,
                now.Hour, now.Minute, 0,
                DateTimeKind.Local);

            // Same rule as the admin picker: current minute is allowed.
            ConfirmButton.interactable = selectedDateTime >= currentMinute;
        }
    }

    private int GetClosestToCenter(PickerColumn column)
    {
        RectTransform viewport = column.Scroll.viewport;

        Vector3 center = viewport.InverseTransformPoint(
            viewport.TransformPoint(viewport.rect.center));

        float closestDistance = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < column.Texts.Count; i++)
        {
            RectTransform item = column.Texts[i].rectTransform;

            Vector3 itemCenter = viewport.InverseTransformPoint(
                item.TransformPoint(item.rect.center));

            float distance = Mathf.Abs(itemCenter.y - center.y);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private Vector2 GetCenteredPosition(PickerColumn column, int index)
    {
        RectTransform viewport = column.Scroll.viewport;
        RectTransform content = column.Scroll.content;
        RectTransform item = column.Texts[index].rectTransform;

        Vector3 viewportCenter = viewport.TransformPoint(viewport.rect.center);
        Vector3 itemCenter = item.TransformPoint(item.rect.center);

        Vector3 difference = content.parent.InverseTransformVector(
            viewportCenter - itemCenter);

        Vector2 target = content.anchoredPosition;
        target.y += difference.y;

        return target;
    }

    private IEnumerator SnapColumn(PickerColumn column)
    {
        column.Snapping = true;
        column.Scroll.StopMovement();
        column.Index = GetClosestToCenter(column);

        RectTransform content = column.Scroll.content;
        Vector2 start = content.anchoredPosition;
        Vector2 target = GetCenteredPosition(column, column.Index);

        float duration = Mathf.Max(0f, SnapDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            content.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        content.anchoredPosition = target;
        column.Scroll.StopMovement();

        column.Snapping = false;
        column.NeedSnap = false;

        UpdateSelection();
    }

    private void ConfirmSelection()
    {
        if (!ready) return;

        // Finish any pending snaps before confirming.
        StopAllCoroutines();

        foreach (var column in columns)
        {
            column.Snapping = false;
            column.NeedSnap = false;
            column.Scroll.StopMovement();
            column.Index = GetClosestToCenter(column);

            column.Scroll.content.anchoredPosition =
                GetCenteredPosition(column, column.Index);
        }

        UpdateSelection();

        if (ConfirmButton == null || !ConfirmButton.interactable)
            return;

        DateTime confirmedDate = selectedDateTime;
        Action<DateTime> callback = onConfirmed;

        onConfirmed = null;
        Close();
        callback?.Invoke(confirmedDate);
    }

    private void Close()
    {
        onConfirmed = null;
        gameObject.SetActive(false);
    }

    public DateTime GetSelectedDateTime()
    {
        return selectedDateTime;
    }
}