using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BadBeatJackPotScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Button TopUpButton;
    public Button EnableButton;
    public Button WinnerButton;
    public Button RulesButton;
    public Button QualifyingHandsButton;

    public GameObject TopUpScreen;
    public RectTransform WinnerPanel;
    public RectTransform RulesPanel;
    public RectTransform QualifyingHandsPanel;

    public Sprite SelectButtonSprite;
    public Sprite UnSelectButtonSprite;

    public Sprite EnableButtonSprite;
    public Sprite DisableButtonSprite;

    public TurnOffJackPotScreenScript TurnOffJackPotScreen;

    public Image BadBeatJackPot_Image;
    public Sprite EnableBadJackJotSprite;
    public Sprite DisableBadJackJotSprite;

    public string ClubId;
    public string ClubCode;
    public ClubJackpotData JackpotData { get; private set; }

    private int viewVersion;
    private bool updatingEnabled;
    private bool loading;

    public TextMeshProUGUI CurrentAmount;

    public GameObject TopUpGrid;
    public RectTransform ButtonGrid;

    public JackPotScreenScript JackPotScreenScript;
    public ShowClubTableScreenScript ShowClubTableScreenScript;
    public GameObject BadBeatJackpotScreen;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (TopUpButton != null)
            TopUpButton.onClick.AddListener(TopUpButtonOnTap);

        if (EnableButton != null)
            EnableButton.onClick.AddListener(EnableButtonOnTap);

        if (WinnerButton != null)
            WinnerButton.onClick.AddListener(WinnerButtonOnTap);

        if (RulesButton != null)
            RulesButton.onClick.AddListener(RulesButtonOnTap);

        if (QualifyingHandsButton != null)
            QualifyingHandsButton.onClick.AddListener(
                QualifyingHandsButtonOnTap);
    }

    private void OnEnable()
    {
        StartCoroutine(LoadData());
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    IEnumerator LoadData()
    {
       
        yield return new WaitForSeconds(0.1f);
        ClubCode = ClubContext.ClubDetail.ClubCode;
        ClubId = ClubContext.ClubId;
        WinnerButtonOnTap();

        ClubRole role = ClubContext.ParseRole(ClubContext.SelectedClub.Role);
        bool isCreator = role == ClubRole.Creator;

        if (isCreator)
        {
            TopUpGrid.SetActive(true);

            WinnerPanel.offsetMax = new Vector2(WinnerPanel.offsetMax.x, -213f);
            RulesPanel.offsetMax = new Vector2(RulesPanel.offsetMax.x, -213f);
            QualifyingHandsPanel.offsetMax = new Vector2(QualifyingHandsPanel.offsetMax.x, -213f);
            ButtonGrid.anchoredPosition = new Vector2(ButtonGrid.anchoredPosition.x, -166f);
        }
        else
        {
            TopUpGrid.SetActive(false);
            WinnerPanel.offsetMax = new Vector2(WinnerPanel.offsetMax.x, -108f);
            RulesPanel.offsetMax = new Vector2(RulesPanel.offsetMax.x, -108f);
            QualifyingHandsPanel.offsetMax = new Vector2(QualifyingHandsPanel.offsetMax.x, -108f);
            ButtonGrid.anchoredPosition = new Vector2(ButtonGrid.anchoredPosition.x, -58f);
        }
        Reload();


    }

    public void Reload()
    {
        if (!isActiveAndEnabled || updatingEnabled)
            return;

        JackpotData = null;
        loading = true;
        RefreshControls();

        LoadJackpotAsync(++viewVersion, ClubId).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadJackpotAsync(int version, string clubId)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var response =
                await ClubManager.Instance.GetClubJackpotAsync(clubId);

            if (!IsCurrent(version) || ClubId != clubId)
                return;

            if (response == null || response.Jackpot == null)
                throw new InvalidOperationException(
                    "Jackpot response was empty.");

            JackpotData = response.Jackpot;
            PopulateData(JackpotData);
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
                Debug.LogError("[Jackpot] Load failed: " + e.Message);
        }
        finally
        {
            if (IsCurrent(version))
            {
                loading = false;
                RefreshControls();
            }
        }
    }

    private void PopulateData(ClubJackpotData data)
    {
        if (data == null)
            return;

        SetEnableButtonSprite(EnableButton, data.IsEnabled);

        if (BadBeatJackPot_Image != null)
        {
            BadBeatJackPot_Image.sprite = data.IsEnabled
                ? EnableBadJackJotSprite
                : DisableBadJackJotSprite;
        }
        JackPotScreenScript.BadBeat_JackPotButton.image.sprite = data.IsEnabled
                ? EnableBadJackJotSprite
                : DisableBadJackJotSprite;

        ShowClubTableScreenScript.JackPotButton.image.sprite = data.IsEnabled
                ? EnableBadJackJotSprite
                : DisableBadJackJotSprite;
        if (CurrentAmount != null)
        {
            string BadBeatCUrrentAcount = data.CurrentAmount.ToString( "00,000,000",System.Globalization.CultureInfo.InvariantCulture);

            CurrentAmount.text = BadBeatCUrrentAcount;
            ShowClubTableScreenScript.CurrentAmount.text = BadBeatCUrrentAcount;
            JackPotScreenScript.BadBeatCurrentAmount.text = BadBeatCUrrentAcount;
            CurrentAmount.gameObject.SetActive(data.IsEnabled);
            ShowClubTableScreenScript.CurrentAmount.gameObject.SetActive(data.IsEnabled);
            JackPotScreenScript.BadBeatCurrentAmount.gameObject.SetActive(data.IsEnabled);
        }
    }

    private void CloseButtonOnTap()
    {
        if (!updatingEnabled)
            BadBeatJackpotScreen.SetActive(false);
    }

    private void TopUpButtonOnTap()
    {
        if (!updatingEnabled && TopUpScreen != null)
            TopUpScreen.SetActive(true);
    }

    private void EnableButtonOnTap()
    {
        if (updatingEnabled || loading || JackpotData == null)
            return;

        if (JackpotData.IsEnabled)
        {
            // Require confirmation before disabling the jackpot.
            if (TurnOffJackPotScreen == null)
            {
                Debug.LogError(
                    "[Jackpot] Turn-off confirmation screen is not assigned.");
                return;
            }

            TurnOffJackPotScreen.Open(this, ClubId);
            return;
        }

        if (JackpotData.CurrentAmount <= 0)
        {
            // Open the top-up screen before enabling an empty jackpot.
            if (TopUpScreen == null)
            {
                Debug.LogError(
                    "[Jackpot] Top-up screen is not assigned.");
                return;
            }

            TopUpScreen.SetActive(true);
            return;
        }

        EnableJackpotAsync().Forget();
    }

    private async UniTask EnableJackpotAsync()
    {
        try
        {
            await UpdateEnabledAsync(true, ClubId);
        }
        catch (Exception e)
        {
            if (this != null)
                Debug.LogError(
                    "[Jackpot] Failed to enable jackpot: " + e.Message);
        }
    }

    public async UniTask DisableJackpotAsync(string confirmedClubId)
    {
        if (!isActiveAndEnabled ||
            !string.Equals(
                ClubCode,
                confirmedClubId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected club has changed. Reopen the confirmation.");
        }

        if (loading || JackpotData == null)
            throw new InvalidOperationException(
                "Jackpot data is not available yet.");

        if (updatingEnabled)
            throw new InvalidOperationException(
                "Another jackpot update is in progress.");

        if (!JackpotData.IsEnabled)
            return;

        await UpdateEnabledAsync(false, ClubId);
    }

    private async UniTask UpdateEnabledAsync(
        bool enabled, string clubId)
    {
        if (updatingEnabled)
            throw new InvalidOperationException(
                "Another jackpot update is in progress.");

        updatingEnabled = true;
        int version = viewVersion;
        RefreshControls();

        try
        {
            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var response =
                await ClubManager.Instance.SetClubJackpotEnabledAsync(
                    clubId, enabled);

            if (response == null || response.Jackpot == null)
                throw new InvalidOperationException(
                    "Jackpot response was empty.");

            if (response.Jackpot.IsEnabled != enabled)
                throw new InvalidOperationException(
                    "The server did not return the requested jackpot status.");

            if (!IsCurrent(version) || ClubId != clubId)
                return;

            // Use the server response to update the displayed status.
            JackpotData = response.Jackpot;
            PopulateData(JackpotData);
        }
        finally
        {
            if (this != null)
            {
                updatingEnabled = false;
                RefreshControls();

                // Reload if the screen changed while the request was running.
                if (isActiveAndEnabled && !IsCurrent(version))
                    Reload();
            }
        }
    }

    private void RefreshControls()
    {
        if (EnableButton != null)
        {
            EnableButton.interactable =
                !loading && !updatingEnabled && JackpotData != null;
        }

        if (CloseButton != null)
            CloseButton.interactable = !updatingEnabled;

        if (TopUpButton != null)
            TopUpButton.interactable = !updatingEnabled;
    }

    private void WinnerButtonOnTap()
    {
        if (WinnerPanel != null) WinnerPanel.gameObject.SetActive(true);
        if (RulesPanel != null) RulesPanel.gameObject.SetActive(false);
        if (QualifyingHandsPanel != null)
            QualifyingHandsPanel.gameObject.SetActive(false);

        SetButtonSprite(WinnerButton, true);
        SetButtonSprite(RulesButton, false);
        SetButtonSprite(QualifyingHandsButton, false);
    }

    private void RulesButtonOnTap()
    {
        if (WinnerPanel != null) WinnerPanel.gameObject.SetActive(false);
        if (RulesPanel != null) RulesPanel.gameObject.SetActive(true);
        if (QualifyingHandsPanel != null)
            QualifyingHandsPanel.gameObject.SetActive(false);

        SetButtonSprite(WinnerButton, false);
        SetButtonSprite(RulesButton, true);
        SetButtonSprite(QualifyingHandsButton, false);
    }

    private void QualifyingHandsButtonOnTap()
    {
        if (WinnerPanel != null) WinnerPanel.gameObject.SetActive(false);
        if (RulesPanel != null) RulesPanel.gameObject.SetActive(false);
        if (QualifyingHandsPanel != null)
            QualifyingHandsPanel.gameObject.SetActive(true);

        SetButtonSprite(WinnerButton, false);
        SetButtonSprite(RulesButton, false);
        SetButtonSprite(QualifyingHandsButton, true);
    }

    private void SetButtonSprite(Button button, bool selected)
    {
        ApplySprite(button,selected ? SelectButtonSprite : UnSelectButtonSprite);
    }

    private void SetEnableButtonSprite(Button button, bool enabled)
    {
        ApplySprite(button,enabled ? EnableButtonSprite : DisableButtonSprite);
    }

    private static void ApplySprite(Button button, Sprite sprite)
    {
        if (button == null || button.image == null || sprite == null)
            return;

        button.image.sprite = sprite;

        if (button.transition == Selectable.Transition.SpriteSwap)
        {
            SpriteState state = button.spriteState;
            state.highlightedSprite = sprite;
            state.pressedSprite = sprite;
            state.selectedSprite = sprite;
            state.disabledSprite = sprite;
            button.spriteState = state;
        }
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (TopUpButton != null)
            TopUpButton.onClick.RemoveListener(TopUpButtonOnTap);

        if (EnableButton != null)
            EnableButton.onClick.RemoveListener(EnableButtonOnTap);

        if (WinnerButton != null)
            WinnerButton.onClick.RemoveListener(WinnerButtonOnTap);

        if (RulesButton != null)
            RulesButton.onClick.RemoveListener(RulesButtonOnTap);

        if (QualifyingHandsButton != null)
            QualifyingHandsButton.onClick.RemoveListener(
                QualifyingHandsButtonOnTap);
    }
}