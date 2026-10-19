using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardTempletePrefab : MonoBehaviour
{
    public Text Type_Text;
    public Text Leaderboard_Text;
    public Text PrizePool_Text;

    public Button EditButton;
    public Button DeleteButton;
    public Toggle SelectToggle;

    public ClubLeaderboardData Data { get; private set; }

    private Action<LeaderboardTempletePrefab, bool> onSelectionChanged;

    private void Awake()
    {
        if (SelectToggle != null)
        {
            SelectToggle.onValueChanged.AddListener(
                SelectToggleOnChanged);
        }

        // Wire these when template editing and deletion are implemented.
        if (EditButton != null)
            EditButton.interactable = false;

        if (DeleteButton != null)
            DeleteButton.interactable = false;
    }

    public void SetData(
        ClubLeaderboardData data,
        ToggleGroup group,
        Action<LeaderboardTempletePrefab, bool> selectionCallback)
    {
        Data = data;
        onSelectionChanged = selectionCallback;

        SetText(Type_Text, data != null ? data.Type : null);
        SetText(Leaderboard_Text, data != null ? data.Name : null);

        decimal totalPrizePool = 0;

        if (data != null && data.Prizes != null)
        {
            foreach (var prize in data.Prizes)
            {
                if (prize != null)
                    totalPrizePool += prize.Amount;
            }
        }

        SetText(PrizePool_Text,"Prize Pool: "+ totalPrizePool.ToString("#,0.##",CultureInfo.InvariantCulture));

        if (SelectToggle != null)
        {
            // Reset before joining the group.
            SelectToggle.group = null;
            SelectToggle.SetIsOnWithoutNotify(false);
            SelectToggle.group = group;
            SelectToggle.interactable = data != null;
        }
    }

    public void SetSelected(bool selected, bool notify = true)
    {
        if (SelectToggle == null)
            return;

        if (notify)
            SelectToggle.isOn = selected;
        else
            SelectToggle.SetIsOnWithoutNotify(selected);
    }

    private void SelectToggleOnChanged(bool selected)
    {
        onSelectionChanged?.Invoke(this, selected);
    }

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void OnDestroy()
    {
        if (SelectToggle != null)
        {
            SelectToggle.onValueChanged.RemoveListener(
                SelectToggleOnChanged);
        }
    }
}