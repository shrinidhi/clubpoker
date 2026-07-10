using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Fee Allocation. One whole-percent slider ("Losers Only Share") saved on Confirm
/// via PUT /api/clubs/{clubId} {"feeAllocPercent":N}. The (?) opens the long explanation in
/// the shared AlertPopup.
/// Backend is LIVE. NOTE: the prototype's "Weighted Contribute" toggle has no API field yet,
/// so it is not wired.
/// </summary>
public class AdminFeeAllocationPopupScript : MonoBehaviour
{
    [Header("Header")]
    public Button Close_Button;
    public Button Tip_Button;                  // question mark → explanation

    [TextArea(6, 20)] public string TipMessage =
        "Fee Allocation explanation\n\n" +
        "If player A, B, C put 40 chips, 40 chips, 20 chips into the pot, the total pot is " +
        "100 chips. A is the winner, B and C are the losers. The whole pot (player A, B, C) " +
        "got into the pot divide the total pot to 40%, 40%, 20%, fee is 5% of the pot. So the " +
        "total fee is 5 chips.\n\n" +
        "1. If you set fee allocation as 0%, the fee will be distributed between players " +
        "according to how many chips they put into the pot, so the fee counted to each player is:\n" +
        "Player A: 5×40% = 2\nPlayer B: 5×40% = 2\nPlayer C: 5×20% = 1\n\n" +
        "2. If you set fee allocation as 60%, 60% of the total fee will only be distributed " +
        "between losers, the rest 40% of the total fee will be distributed between winners and " +
        "losers according to how many chips they put into the pot.";

    [Header("Slider")]
    public Slider Percent_Slider;              // whole numbers, 0..100
    public TextMeshProUGUI Percent_Text;       // e.g. "80%"

    [Header("Submit")]
    public Button Confirm_Button;

    [Header("Shared")]
    public AlertPopup AlertPopup;
    public AdminPanelScript AdminPanel;        // to refresh the row's inline "0%"

    private int _loadedPercent;

    private void Start()
    {
        if (Close_Button   != null) Close_Button.onClick.AddListener(Close);
        if (Tip_Button     != null) Tip_Button.onClick.AddListener(OnTipTap);
        if (Confirm_Button != null) Confirm_Button.onClick.AddListener(OnConfirmTap);

        if (Percent_Slider != null)
        {
            Percent_Slider.minValue = 0;
            Percent_Slider.maxValue = 100;
            Percent_Slider.wholeNumbers = true;
            Percent_Slider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    // Club detail is cached on scene entry (ClubViewController.LoadClubDetail).
    private void OnEnable()
    {
        _loadedPercent = ClubContext.FeeAllocPercent;

        if (Percent_Slider != null)
            Percent_Slider.SetValueWithoutNotify(_loadedPercent);
        RefreshPercentText(_loadedPercent);

        SetInteractable(true);
    }

    private void OnSliderChanged(float value)
    {
        RefreshPercentText(Mathf.RoundToInt(value));
    }

    private void RefreshPercentText(int percent)
    {
        if (Percent_Text != null) Percent_Text.text = percent + "%";
    }

    private void OnTipTap()
    {
        if (AlertPopup != null)
            AlertPopup.Show("Fee Allocation", TipMessage, showCancel: false, onConfirm: null);
    }

    private void OnConfirmTap()
    {
        int percent = Percent_Slider != null ? Mathf.RoundToInt(Percent_Slider.value) : 0;

        if (percent == _loadedPercent)
        {
            Close();
            return;
        }

        Save(percent).Forget();
    }

    private async UniTaskVoid Save(int percent)
    {
        SetInteractable(false);
        try
        {
            var res = await ClubManager.Instance
                .UpdateFeeAllocPercentAsync(ClubContext.ClubId, percent);

            int saved = res?.Club?.FeeAllocPercent ?? percent;
            _loadedPercent = saved;

            // Keep the cached detail in step with the server.
            if (res?.Club != null) ClubContext.SetClubDetail(res.Club);
            else if (ClubContext.ClubDetail != null) ClubContext.ClubDetail.FeeAllocPercent = saved;

            if (AdminPanel != null) AdminPanel.SetFeeAllocationValue(saved);

            ShowToast("Fee allocation updated");
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminFeeAllocationPopupScript] save error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to save" : e.Message);
        }
        finally
        {
            SetInteractable(true);
        }
    }

    private void SetInteractable(bool on)
    {
        if (Percent_Slider != null) Percent_Slider.interactable = on;
        if (Confirm_Button != null) Confirm_Button.interactable = on;
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    // Same toast used across the club screens (Data/Export).
    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
