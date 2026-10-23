using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Core;

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

    // "?" explanation — from the language files (club.fee_allocation_tip).
    private static string TipMessage => Loc.Get("club.fee_allocation_tip");

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
            AlertPopup.Show(Loc.Get("fee_allocation"), TipMessage, showCancel: false, onConfirm: null);
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

            ShowToast(Loc.Get("club.fee_allocation_updated"));
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminFeeAllocationPopupScript] save error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? Loc.Get("save_failed") : e.Message);
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
