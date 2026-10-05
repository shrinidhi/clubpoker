using System;
using UnityEngine;
using UnityEngine.UI;

public class DayPrefabScript : MonoBehaviour
{
    public Button DaysprefabButton;
    public Text DaysText;

    public Sprite SelectedSprite;
    public Sprite UnselectedSprite;

    public Color SelectedTextColor = new Color(1f, 0.8f, 0.25f);
    public Color UnselectedTextColor = Color.white;

    public int DayIndex { get; private set; }
    public bool IsSelected { get; private set; }

    private Action<int> onClicked;

    private void Awake()
    {
        if (DaysprefabButton != null)
            DaysprefabButton.onClick.AddListener(Click);
    }

    private void OnDestroy()
    {
        if (DaysprefabButton != null)
            DaysprefabButton.onClick.RemoveListener(Click);
    }

    public void Setup(int dayIndex, string label, Action<int> callback)
    {
        DayIndex = dayIndex;
        onClicked = callback;

        if (DaysText != null)
        {
            DaysText.supportRichText = false;
            DaysText.text = label;
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (DaysText != null)
        {
            DaysText.color = selected
                ? SelectedTextColor
                : UnselectedTextColor;
        }

        if (DaysprefabButton != null &&
            DaysprefabButton.image != null)
        {
            Sprite sprite = selected
                ? SelectedSprite
                : UnselectedSprite;

            if (sprite != null)
                DaysprefabButton.image.sprite = sprite;
        }
    }

    public void SetBusy(bool busy)
    {
        if (DaysprefabButton != null)
            DaysprefabButton.interactable = !busy;
    }

    private void Click()
    {
        onClicked?.Invoke(DayIndex);
    }
}