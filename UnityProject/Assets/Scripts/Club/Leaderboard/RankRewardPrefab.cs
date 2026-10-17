using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class RankRewardPrefab : MonoBehaviour
{
    public Text RankText;
    public InputField Prize_InputField;
    public Button DeleteButton;

    public int Position { get; private set; }

    private Action<RankRewardPrefab> onDelete;
    private Action onPrizeChanged;

    private void Awake()
    {
        if (DeleteButton != null)
            DeleteButton.onClick.AddListener(DeleteButtonOnTap);

        if (Prize_InputField != null)
        {
            Prize_InputField.contentType =
                InputField.ContentType.DecimalNumber;

            Prize_InputField.onValueChanged.AddListener(
                PrizeInputOnChanged);
        }
    }

    public void SetData(
        int position,
        decimal amount,
        Action<RankRewardPrefab> deleteCallback,
        Action prizeChangedCallback)
    {
        onDelete = deleteCallback;
        onPrizeChanged = prizeChangedCallback;

        SetPosition(position);

        if (Prize_InputField != null)
        {
            Prize_InputField.SetTextWithoutNotify(
                amount.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture));
        }
    }

    public void SetPosition(int position)
    {
        Position = position;

        if (RankText != null)
            RankText.text = GetOrdinal(position);
    }

    public bool TryGetAmount(out decimal amount)
    {
        amount = 0;

        return Prize_InputField != null &&
               decimal.TryParse(
                   Prize_InputField.text.Trim(),
                   NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture,
                   out amount) &&
               amount >= 0;
    }

    public void SetBusy(bool busy)
    {
        if (Prize_InputField != null)
            Prize_InputField.interactable = !busy;

        if (DeleteButton != null)
            DeleteButton.interactable = !busy;
    }

    private void DeleteButtonOnTap()
    {
        onDelete?.Invoke(this);
    }

    private void PrizeInputOnChanged(string value)
    {
        onPrizeChanged?.Invoke();
    }

    private static string GetOrdinal(int number)
    {
        int lastTwo = number % 100;

        if (lastTwo >= 11 && lastTwo <= 13)
            return number + "th";

        switch (number % 10)
        {
            case 1: return number + "st";
            case 2: return number + "nd";
            case 3: return number + "rd";
            default: return number + "th";
        }
    }

    private void OnDestroy()
    {
        if (DeleteButton != null)
            DeleteButton.onClick.RemoveListener(DeleteButtonOnTap);

        if (Prize_InputField != null)
            Prize_InputField.onValueChanged.RemoveListener(
                PrizeInputOnChanged);
    }
}