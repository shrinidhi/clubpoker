using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DayGridScript : MonoBehaviour
{
    public GameObject DaysPrefab;
    public Transform Content;

    private readonly List<DayPrefabScript> rows = new List<DayPrefabScript>();
    private readonly HashSet<int> selectedDays = new HashSet<int>();

    private bool busy;

    private void Start()
    {
        EnsureRows();
    }

    private void EnsureRows()
    {
        if (rows.Count > 0)
            return;

        if (DaysPrefab == null || Content == null)
        {
            Debug.LogError("[PromoDays] Assign DaysPrefab and Content.");
            return;
        }

        if (DaysPrefab.GetComponent<DayPrefabScript>() == null)
        {
            Debug.LogError("[PromoDays] DayPrefabScript is missing.");
            return;
        }

        string[] labels = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };

        for (int i = 0; i < labels.Length; i++)
        {
            GameObject instance = Instantiate(DaysPrefab, Content);
            instance.SetActive(true);

            DayPrefabScript row = instance.GetComponent<DayPrefabScript>();

            row.Setup(i, labels[i], ToggleDay);
            row.SetSelected(selectedDays.Contains(i));
            row.SetBusy(busy);

            rows.Add(row);
        }

        if (Content is RectTransform rect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void ToggleDay(int day)
    {
        if (busy)
            return;

        if (!selectedDays.Add(day))
            selectedDays.Remove(day);

        foreach (var row in rows)
            row.SetSelected(selectedDays.Contains(row.DayIndex));
    }

    public List<int> GetSelectedDays()
    {
        var result = new List<int>(selectedDays);
        result.Sort();
        return result;
    }

    public void SetSelectedDays(IEnumerable<int> days)
    {
        selectedDays.Clear();

        if (days != null)
        {
            foreach (int day in days)
            {
                if (day >= 0 && day <= 6)
                    selectedDays.Add(day);
            }
        }

        EnsureRows();

        foreach (var row in rows)
            row.SetSelected(selectedDays.Contains(row.DayIndex));
    }

    public void SetBusy(bool value)
    {
        busy = value;

        foreach (var row in rows)
        {
            if (row != null)
                row.SetBusy(value);
        }
    }
}