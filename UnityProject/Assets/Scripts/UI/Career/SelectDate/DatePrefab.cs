using System;
using UnityEngine;
using UnityEngine.UI;

public class DatePrefab : MonoBehaviour
{
    public Button DateButton;
    public Text DateText;
    public Sprite SelectDate;

    private DateTime date;
    private bool isBlank;
    private Sprite normalSprite;
    private Action<DateTime> onDateSelected;

    private static DatePrefab lastClickedPrefab;
    private static bool doubleSelected;

    public void SetDate(DateTime selectedDate, Action<DateTime> onDateSelected)
    {
        date = selectedDate.Date;
        isBlank = false;
        this.onDateSelected = onDateSelected;

        if (DateText != null)
        {
            DateText.text = date.Day.ToString();
            DateText.gameObject.SetActive(true);
            DateText.color = date.Date == DateTime.Now.Date ? Color.yellow : Color.white;
        }

        if (DateButton != null)
        {
            DateButton.gameObject.SetActive(true);
            DateButton.interactable = date.Date <= DateTime.Now.Date;
            normalSprite = DateButton.image.sprite;

            DateButton.onClick.RemoveAllListeners();
            DateButton.onClick.AddListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        SelectDatePanel panel = FindObjectOfType<SelectDatePanel>();

        if (panel == null)
            return;

        if (lastClickedPrefab == this)
        {
            if (!doubleSelected)
            {
                doubleSelected = true;
                panel.OnDateDoubleClicked(date);
            }
            else
            {
                doubleSelected = false;
                panel.OnDateSelectedAgain(date);
            }

            return;
        }

        lastClickedPrefab = this;
        doubleSelected = false;

        if (onDateSelected != null)
            onDateSelected.Invoke(date);
    }

    public void SetSelected(bool selected)
    {
        if (isBlank)
            return;

        if (DateButton != null && DateButton.image != null)
        {
            DateButton.image.sprite = selected && SelectDate != null ? SelectDate : normalSprite;
            DateButton.image.color = selected && SelectDate != null ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }

        if (DateText != null)
            DateText.color = date.Date == DateTime.Now.Date ? Color.yellow : Color.white;
    }

    public void SetBlank()
    {
        isBlank = true;

        if (DateText != null)
        {
            DateText.text = "";
            DateText.gameObject.SetActive(false);
        }

        if (DateButton != null)
        {
            DateButton.interactable = false;

            if (DateButton.image != null)
                DateButton.image.color = new Color32(255, 255, 255, 0);
        }
    }

    public DateTime GetDate()
    {
        return date;
    }
}