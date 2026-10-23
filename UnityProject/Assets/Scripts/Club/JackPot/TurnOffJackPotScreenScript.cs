using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;

public class TurnOffJackPotScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Text ClubId;
    public InputField EnterClubIdInputField;
    public Button ConfirmButton;

    public Text ErrorText;

    private BadBeatJackPotScreenScript jackpotScreen;
    private string expectedClubId;
    private bool busy;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (ConfirmButton != null)
            ConfirmButton.onClick.AddListener(ConfirmButtonOnTap);

        if (EnterClubIdInputField != null)
            EnterClubIdInputField.onValueChanged.AddListener(OnClubIdChanged);
    }

    public void Open(BadBeatJackPotScreenScript screen, string clubId)
    {
        if (busy)
            return;

        jackpotScreen = screen;
        expectedClubId = ClubContext.ClubCode;

        // Without a code the player has nothing to type to confirm.
        if (string.IsNullOrEmpty(expectedClubId))
        {
            Debug.LogError("[Jackpot] Club code unavailable — can't open turn-off confirm.");
            return;
        }

        gameObject.SetActive(true);

        if (ClubId != null)
        {
            ClubId.supportRichText = false;
            ClubId.text = $"{Loc.Get("club_code")}: {expectedClubId}";
        }

        if (EnterClubIdInputField != null)
            EnterClubIdInputField.SetTextWithoutNotify("");

        SetError("");
        RefreshControls();
    }

    private bool ClubIdMatches()
    {
        return !string.IsNullOrWhiteSpace(expectedClubId) &&
               EnterClubIdInputField != null &&
               string.Equals(
                   EnterClubIdInputField.text.Trim(),
                   expectedClubId,
                   StringComparison.OrdinalIgnoreCase);
    }

    private void OnClubIdChanged(string value)
    {
        SetError("");
        RefreshControls();
    }

    private void RefreshControls()
    {
        if (CloseButton != null)
            CloseButton.interactable = !busy;

        if (EnterClubIdInputField != null)
            EnterClubIdInputField.interactable = !busy;

        if (ConfirmButton != null)
            ConfirmButton.interactable = !busy && ClubIdMatches();
    }

    private void CloseButtonOnTap()
    {
        if (!busy)
            gameObject.SetActive(false);
    }

    private void ConfirmButtonOnTap()
    {
        if (busy)
            return;

        if (!ClubIdMatches())
        {
            SetError(Loc.Get("jackpot.enter_club_id"));
            return;
        }

        ConfirmAsync().Forget();
    }

    private async UniTask ConfirmAsync()
    {
        busy = true;
        SetError("");
        RefreshControls();

        try
        {
            if (jackpotScreen == null)
                throw new InvalidOperationException(
                    "Jackpot screen is not assigned.");

            await jackpotScreen.DisableJackpotAsync(expectedClubId);

            if (this != null)
                gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            if (this != null && isActiveAndEnabled)
                SetError(Loc.Get("jackpot.disable_failed", ("reason", e.Message)));
        }
        finally
        {
            if (this != null)
            {
                busy = false;
                RefreshControls();
            }
        }
    }

    private void SetError(string message)
    {
        if (ErrorText != null)
        {
            ErrorText.supportRichText = false;
            ErrorText.text = message;
        }

        if (!string.IsNullOrEmpty(message))
            Debug.LogError("[Jackpot] " + message);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (ConfirmButton != null)
            ConfirmButton.onClick.RemoveListener(ConfirmButtonOnTap);

        if (EnterClubIdInputField != null)
            EnterClubIdInputField.onValueChanged.RemoveListener(
                OnClubIdChanged);
    }
}