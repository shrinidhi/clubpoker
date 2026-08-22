using System;
using UnityEngine;
using UnityEngine.UI;

public class MonthPrefab : MonoBehaviour
{
    public Text MonthYearText;
    public Text[] DayTexts;
    public GameObject DatePrefab;
    public Transform DateContent;

    private readonly string[] DayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public void GenerateMonth(int year, int month, Action<DateTime> onDateSelected)
    {
        DateTime monthDate = new DateTime(year, month, 1);

        if (MonthYearText != null)
            MonthYearText.text = monthDate.ToString("M/yyyy");

        GenerateDayHeaders();
        ClearDates();

        int daysInMonth = DateTime.DaysInMonth(year, month);
        int firstDayIndex = (int)monthDate.DayOfWeek;

        for (int i = 0; i < firstDayIndex; i++)
        {
            GameObject blankObject = Instantiate(DatePrefab, DateContent);
            blankObject.transform.localScale = Vector3.one;

            DatePrefab datePrefab = blankObject.GetComponent<DatePrefab>();

            if (datePrefab != null)
                datePrefab.SetBlank();
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime date = new DateTime(year, month, day);

            GameObject dateObject = Instantiate(DatePrefab, DateContent);
            dateObject.transform.localScale = Vector3.one;

            DatePrefab datePrefab = dateObject.GetComponent<DatePrefab>();

            if (datePrefab != null)
                datePrefab.SetDate(date, onDateSelected);
        }
    }

    private void GenerateDayHeaders()
    {
        if (DayTexts == null || DayTexts.Length < 7)
            return;

        for (int i = 0; i < 7; i++)
        {
            if (DayTexts[i] != null)
                DayTexts[i].text = DayNames[i];
        }
    }

    private void ClearDates()
    {
        if (DateContent == null)
            return;

        for (int i = DateContent.childCount - 1; i >= 0; i--)
            Destroy(DateContent.GetChild(i).gameObject);
    }
}