
using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

public class MemeberRestrictAccessScript : MonoBehaviour
{
    [Header("Top Buttons")]
    public Button CloseButton;
    public Button CustomButton;
    public Button LevelSystemButton;

    [Header("Panels")]
    public GameObject CustomPanel;
    public GameObject LevelSystemPanel;

    [Header("Selection Sprites")]
    public Sprite SelectButtonSprite;
    public Sprite UnselectButtonSprite;

    [Header("Custom Range Sliders")]
    public RangeSliderUI MTTAllowbuyinSlider;
    public Text MTTAllowBuyin_Count;

    public RangeSliderUI SNGAllowbuyinSlider;
    public Text SNGAllowbuyin_Count;

    public RangeSliderUI BB_Ante_BlindSlider;
    public Text BB_Ante_Blind_Count;

    [Header("Custom Restriction")]
    public Button RestrictGameType_OnOFFButton;
    public Sprite OnButtonSprite;
    public Sprite OFFButtonSprite;

    [Header("Custom Actions")]
    public Button ResetButton;
    public Button RestrictAllButton;
    public Button SaveButton;

    [Header("Level System Buttons")]
    public Button Micro_OnOff_Button;
    public Button Small_OnOff_Button;
    public Button Mid_OnOff_Button;
    public Button High_OnOff_Button;
    public Button NoseBleeds_OnOff_Button;
    public Button MTTs_OnOff_Button;
    public Button SNGs_OnOff_Button;
    public Button OFXs_OnOff_Button;

    private string clubId;
    private string userId;

    private bool isLevelSystem;
    private bool restrictByGameType;

    private bool restrictMicro;
    private bool restrictSmall;
    private bool restrictMid;
    private bool restrictHigh;
    private bool restrictNoseBleeds;
    private bool restrictMTTs;
    private bool restrictSNGs;
    private bool restrictOFXs;

    private bool isLoading;
    private bool isSaving;

    private void Start()
    {
        if (MTTAllowbuyinSlider != null)
            MTTAllowbuyinSlider.OnRangeChanged.AddListener((min, max) =>
                RefreshRangeLabels());

        if (SNGAllowbuyinSlider != null)
            SNGAllowbuyinSlider.OnRangeChanged.AddListener((min, max) =>
                RefreshRangeLabels());

        if (BB_Ante_BlindSlider != null)
            BB_Ante_BlindSlider.OnRangeChanged.AddListener((min, max) =>
                RefreshRangeLabels());

        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseScreen);

        if (CustomButton != null)
            CustomButton.onClick.AddListener(OpenCustomPanel);

        if (LevelSystemButton != null)
            LevelSystemButton.onClick.AddListener(OpenLevelSystemPanel);

        if (RestrictGameType_OnOFFButton != null)
            RestrictGameType_OnOFFButton.onClick.AddListener(ToggleGameTypeRestriction);

        if (ResetButton != null)
            ResetButton.onClick.AddListener(ResetRestrictions);

        if (RestrictAllButton != null)
            RestrictAllButton.onClick.AddListener(RestrictAll);

        if (SaveButton != null)
            SaveButton.onClick.AddListener(() => SaveRestrictionsAsync().Forget());

        if (Micro_OnOff_Button != null)
            Micro_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("MICRO"));

        if (Small_OnOff_Button != null)
            Small_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("SMALL"));

        if (Mid_OnOff_Button != null)
            Mid_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("MID"));

        if (High_OnOff_Button != null)
            High_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("HIGH"));

        if (NoseBleeds_OnOff_Button != null)
            NoseBleeds_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("NOSEBLEEDS"));

        if (MTTs_OnOff_Button != null)
            MTTs_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("MTT"));

        if (SNGs_OnOff_Button != null)
            SNGs_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("SNG"));

        if (OFXs_OnOff_Button != null)
            OFXs_OnOff_Button.onClick.AddListener(() =>
                ToggleLevelRestriction("OFX"));

        // Custom panel default selection.
        isLevelSystem = false;
        ShowSelectedPanel();

        RefreshAllUI();
    }

    private void OnEnable()
    {
        // Screen open hone par GET call.
        if (!string.IsNullOrEmpty(clubId) &&
            !string.IsNullOrEmpty(userId))
        {
            LoadRestrictionsAsync().Forget();
        }
    }

    public void Open(string selectedClubId, string selectedUserId)
    {
        clubId = selectedClubId;
        userId = selectedUserId;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        else
        {
            LoadRestrictionsAsync().Forget();
        }
    }

    private void CloseScreen()
    {
        gameObject.SetActive(false);
    }

    // --------------------------------------------------
    // GET RESTRICTION
    // --------------------------------------------------

    private async UniTask LoadRestrictionsAsync()
    {
        if (isLoading)
            return;

        if (string.IsNullOrEmpty(clubId) ||
            string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[Restriction] ClubId or UserId missing");
            return;
        }

        isLoading = true;

        SetButtonsInteractable(false);

        try
        {
            MemberRestrictionData data =
                await AuthManager.Instance.GetMemberRestrictionAsync(
                    clubId,
                    userId
                );

            if (data == null)
            {
                Debug.LogError("[Restriction] Unable to load restriction");
                return;
            }

            ApplyRestrictionData(data);

            Debug.Log("[Restriction] GET successful");
        }
        catch (Exception e)
        {
            Debug.LogError("[Restriction] GET error: " + e.Message);
        }
        finally
        {
            isLoading = false;
            SetButtonsInteractable(true);
        }
    }

    private void ApplyRestrictionData(MemberRestrictionData data)
    {
        if (data == null)
            return;

        isLevelSystem = data.UseSystemLevel;

        if (MTTAllowbuyinSlider != null)
            MTTAllowbuyinSlider.SetRange(
                data.MttBuyInMin,
                data.MttBuyInMax
            );

        if (SNGAllowbuyinSlider != null)
            SNGAllowbuyinSlider.SetRange(
                data.SngBuyInMin,
                data.SngBuyInMax
            );

        if (BB_Ante_BlindSlider != null)
            BB_Ante_BlindSlider.SetRange(
                data.BbMin,
                data.BbMax
            );

        restrictByGameType = data.RestrictByGameType;

        restrictMicro = data.RestrictMicro;
        restrictSmall = data.RestrictSmall;
        restrictMid = data.RestrictMid;
        restrictHigh = data.RestrictHigh;
        restrictNoseBleeds = data.RestrictNoseBleeds;

        restrictMTTs = data.RestrictMTTs;
        restrictSNGs = data.RestrictSNGs;
       restrictOFXs = data.RestrictOFXs;

        ShowSelectedPanel();
        RefreshAllUI();
    }
    // --------------------------------------------------
    // CUSTOM / LEVEL SYSTEM PANEL
    // --------------------------------------------------

    private void OpenCustomPanel()
    {
        isLevelSystem = false;
        ShowSelectedPanel();
    }

    private void OpenLevelSystemPanel()
    {
        isLevelSystem = true;
        ShowSelectedPanel();
    }

    private void ShowSelectedPanel()
    {
        if (CustomPanel != null)
            CustomPanel.SetActive(!isLevelSystem);

        if (LevelSystemPanel != null)
            LevelSystemPanel.SetActive(isLevelSystem);

        SetSelectionSprite(CustomButton, !isLevelSystem);
        SetSelectionSprite(LevelSystemButton, isLevelSystem);
    }

    private void SetSelectionSprite(Button button, bool selected)
    {
        if (button == null || button.image == null)
            return;

        button.image.sprite = selected
            ? SelectButtonSprite
            : UnselectButtonSprite;
    }

    // --------------------------------------------------
    // CUSTOM RESTRICTION TOGGLES
    // --------------------------------------------------

    private void ToggleGameTypeRestriction()
    {
        restrictByGameType = !restrictByGameType;
        RefreshAllUI();
    }

    // --------------------------------------------------
    // LEVEL SYSTEM TOGGLES
    // --------------------------------------------------

    private void ToggleLevelRestriction(string type)
    {
        switch (type)
        {
            case "MICRO":
                restrictMicro = !restrictMicro;
                break;

            case "SMALL":
                restrictSmall = !restrictSmall;
                break;

            case "MID":
                restrictMid = !restrictMid;
                break;

            case "HIGH":
                restrictHigh = !restrictHigh;
                break;

            case "NOSEBLEEDS":
                restrictNoseBleeds = !restrictNoseBleeds;
                break;

            case "MTT":
                restrictMTTs = !restrictMTTs;
                break;

            case "SNG":
                restrictSNGs = !restrictSNGs;
                break;

            case "OFX":
                restrictOFXs = !restrictOFXs;
                break;
        }

        RefreshAllUI();
    }

    // --------------------------------------------------
    // RESET
    // --------------------------------------------------

    private void ResetRestrictions()
    {
        // Full MTT range: 0 - 1M
        if (MTTAllowbuyinSlider != null)
            MTTAllowbuyinSlider.SetRange(0, 1000000);

        // Full SNG range: 0 - 1M
        if (SNGAllowbuyinSlider != null)
            SNGAllowbuyinSlider.SetRange(0, 1000000);

        // Full BB range: 0 - 100K
        if (BB_Ante_BlindSlider != null)
            BB_Ante_BlindSlider.SetRange(0, 100000);

        restrictByGameType = false;

        RefreshAllUI();

        Debug.Log("[Restriction] Reset to full ranges");
    }

    // --------------------------------------------------
    // RESTRICT ALL
    // --------------------------------------------------

    private void RestrictAll()
    {
        // Dono handles 0 par aa jayenge.
        if (MTTAllowbuyinSlider != null)
            MTTAllowbuyinSlider.SetRange(0, 0);

        if (SNGAllowbuyinSlider != null)
            SNGAllowbuyinSlider.SetRange(0, 0);

        if (BB_Ante_BlindSlider != null)
            BB_Ante_BlindSlider.SetRange(0, 0);

        RefreshAllUI();

        Debug.Log("[Restriction] All ranges restricted to 0");
    }

    // --------------------------------------------------
    // REFRESH UI
    // --------------------------------------------------

    private void RefreshAllUI()
    {
        RefreshRangeLabels();

        SetToggleSprite(
            RestrictGameType_OnOFFButton,
            restrictByGameType
        );

        SetToggleSprite(Micro_OnOff_Button, restrictMicro);
        SetToggleSprite(Small_OnOff_Button, restrictSmall);
        SetToggleSprite(Mid_OnOff_Button, restrictMid);
        SetToggleSprite(High_OnOff_Button, restrictHigh);
        SetToggleSprite(NoseBleeds_OnOff_Button, restrictNoseBleeds);
        SetToggleSprite(MTTs_OnOff_Button, restrictMTTs);
        SetToggleSprite(SNGs_OnOff_Button, restrictSNGs);
        SetToggleSprite(OFXs_OnOff_Button, restrictOFXs);
    }

    private void RefreshRangeLabels()
    {
        if (MTTAllowbuyinSlider != null &&
            MTTAllowBuyin_Count != null)
        {
            MTTAllowBuyin_Count.text =
                FormatRange(
                    MTTAllowbuyinSlider.LeftValue,
                    MTTAllowbuyinSlider.RightValue
                );
        }

        if (SNGAllowbuyinSlider != null &&
            SNGAllowbuyin_Count != null)
        {
            SNGAllowbuyin_Count.text =
                FormatRange(
                    SNGAllowbuyinSlider.LeftValue,
                    SNGAllowbuyinSlider.RightValue
                );
        }

        if (BB_Ante_BlindSlider != null &&
            BB_Ante_Blind_Count != null)
        {
            BB_Ante_Blind_Count.text =
                FormatRange(
                    BB_Ante_BlindSlider.LeftValue,
                    BB_Ante_BlindSlider.RightValue
                );
        }
    }

    private string FormatRange(int min, int max)
    {
        return RangeSliderUI.FormatValue(min) +
               " - " +
               RangeSliderUI.FormatValue(max);
    }

    private void SetToggleSprite(Button button, bool isOn)
    {
        if (button == null || button.image == null)
            return;

        button.image.sprite = isOn
            ? OnButtonSprite
            : OFFButtonSprite;
    }

    // --------------------------------------------------
    // SAVE - PUT API
    // --------------------------------------------------

    private async UniTask SaveRestrictionsAsync()
    {
        if (isSaving || isLoading)
            return;

        if (string.IsNullOrEmpty(clubId) ||
            string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[Restriction] ClubId or UserId missing");
            return;
        }

        isSaving = true;

        if (SaveButton != null)
            SaveButton.interactable = false;

        try
        {
            object requestBody;

            if (!isLevelSystem)
            {
                // CUSTOM MODE
                requestBody = new
                {
                    useSystemLevel = false,

                    mttBuyInMin = MTTAllowbuyinSlider.LeftValue,
                    mttBuyInMax = MTTAllowbuyinSlider.RightValue,

                    sngBuyInMin = SNGAllowbuyinSlider.LeftValue,
                    sngBuyInMax = SNGAllowbuyinSlider.RightValue,

                    bbMin = BB_Ante_BlindSlider.LeftValue,
                    bbMax = BB_Ante_BlindSlider.RightValue,

                    restrictByGameType = restrictByGameType
                };
            }
            else
            {
                // LEVEL SYSTEM MODE
                requestBody = new
                {
                    useSystemLevel = true,

                    restrictMicro = restrictMicro,
                    restrictSmall = restrictSmall,
                    restrictMid = restrictMid,
                    restrictHigh = restrictHigh,
                    restrictNoseBleeds = restrictNoseBleeds,

                    restrictMTTs = restrictMTTs,
                    restrictSNGs = restrictSNGs,
                    restrictOFXs = restrictOFXs
                };
            }

            MemberRestrictionData response =
                await AuthManager.Instance.UpdateMemberRestrictionAsync(
                    clubId,
                    userId,
                    requestBody
                );

            if (response != null)
            {
                // Server response ke according UI refresh.
                ApplyRestrictionData(response);
                gameObject.SetActive(false);
                InformationPrefabScript.Instance.ShowMessage("Restriction successful");
                Debug.Log("[Restriction] PUT successful");
            }
            else
            {
                Debug.LogError("[Restriction] PUT failed");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Restriction] Save error: " + e.Message);
        }
        finally
        {
            isSaving = false;

            if (SaveButton != null)
                SaveButton.interactable = true;
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (SaveButton != null)
            SaveButton.interactable = interactable;

        if (ResetButton != null)
            ResetButton.interactable = interactable;

        if (RestrictAllButton != null)
            RestrictAllButton.interactable = interactable;
    }
}
