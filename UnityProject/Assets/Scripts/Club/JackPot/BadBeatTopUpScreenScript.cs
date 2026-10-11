using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class BadBeatTopUpScreenScript : MonoBehaviour
{
    public Button RecordButton;
    public GameObject RecordScreen;
    public Button CloseButton;
    public Button TopUpButton;
    public Button WithdrawButton;

    public Text WithdrawText;
    public Text TopUpText;

    public GameObject TopUpPanel;
    public GameObject WithdrawPanel;

    public Sprite SelectButtonSprite;
    public Sprite UnSelectButtonSprite;

    public Text ChipsPoolText;
    public Text WithdrawChipText;

    public InputField TopUpInputField;
    public InputField WithdrawInputField;

    public Button TopupConfirm_Button;
    public Button WithdrawConfirm_Button;

    public BadBeatJackPotScreenScript JackpotScreen;

    public string ClubId;

    private bool busy;
    private bool balancesLoaded;
    private decimal jackpotAmount;
    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (TopUpButton != null)
            TopUpButton.onClick.AddListener(TopUpButtonOnTap);

        if (WithdrawButton != null)
            WithdrawButton.onClick.AddListener(WithdrawButtonOnTap);

        if (TopupConfirm_Button != null)
            TopupConfirm_Button.onClick.AddListener(TopupConfirmButtonOnTap);

        if (WithdrawConfirm_Button != null)
            WithdrawConfirm_Button.onClick.AddListener(
                WithdrawConfirmButtonOnTap);

        RecordButton.onClick.AddListener(RecordButtonOnTap);
    }

    private void OnEnable()
    {
        ++viewVersion;
        ClubId = ClubContext.ClubId;

        TopUpButtonOnTap();
        SetMessage("");

        // Wait for the current request to finish before allowing another request.
        if (!busy)
            Reload();
    }

    private void OnDisable()
    {
        ++viewVersion;
        balancesLoaded = false;
    }

    void RecordButtonOnTap()
    {
        RecordScreen.SetActive(true);
    }

    public void Reload()
    {
        if (!isActiveAndEnabled || busy)
            return;

        balancesLoaded = false;

        SetText(ChipsPoolText, "—");
        SetText(WithdrawChipText, "—");

        LoadBalancesAsync(++viewVersion, ClubId).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadBalancesAsync(int version, string clubId)
    {
        SetBusy(true);

        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            await RefreshBalancesAsync(version, clubId);
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
                ShowError("Balance load failed: " + e.Message);
        }
        finally
        {
            if (this != null)
                SetBusy(false);
        }
    }

    private async UniTask RefreshBalancesAsync(int version, string clubId)
    {
        if (string.IsNullOrWhiteSpace(clubId))
            throw new InvalidOperationException("Club ID is missing.");

        if (ClubManager.Instance == null)
            throw new InvalidOperationException("ClubManager is missing.");

        var results = await UniTask.WhenAll(
            ClubManager.Instance.GetClubJackpotAsync(clubId),
            ClubManager.Instance.GetClubDetailAsync(clubId)
        );

        if (!IsCurrent(version))
            return;

        if (results.Item1 == null || results.Item1.Jackpot == null)
            throw new InvalidOperationException(
                "Jackpot response was empty.");

        if (results.Item2 == null || results.Item2.Club == null)
            throw new InvalidOperationException(
                "Club response was empty.");

        jackpotAmount = results.Item1.Jackpot.CurrentAmount;

        // Display the latest club chip pool available for top-ups.
        SetText(
            ChipsPoolText,
            results.Item2.Club.ChipPool.ToString(
                "#,0.##", CultureInfo.InvariantCulture));

        // Display the latest jackpot balance available for withdrawals.
        SetText(
            WithdrawChipText,
            jackpotAmount.ToString(
                "#,0.##", CultureInfo.InvariantCulture));

        balancesLoaded = true;
    }

    private void TopupConfirmButtonOnTap()
    {
        if (busy || !balancesLoaded)
            return;

        if (!TryReadAmount(TopUpInputField, out decimal amount))
            return;

        SubmitAsync(true, amount, ClubId).Forget();
    }

    private void WithdrawConfirmButtonOnTap()
    {
        if (busy || !balancesLoaded)
            return;

        if (!TryReadAmount(WithdrawInputField, out decimal amount))
            return;

        if (amount > jackpotAmount)
        {
            ShowError("The withdrawal amount cannot exceed the jackpot balance.");
            return;
        }

        SubmitAsync(false, amount, ClubId).Forget();
    }

    private bool TryReadAmount(InputField input, out decimal amount)
    {
        amount = 0;

        string value = input != null
            ? input.text.Trim()
            : "";

        if (!decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amount) ||
            amount <= 0)
        {
            ShowError("Enter a valid amount greater than zero.");
            return false;
        }

        return true;
    }

    private async UniTask SubmitAsync(
        bool isTopUp, decimal amount, string clubId)
    {
        SetBusy(true);
        SetMessage("");

        bool submitted = false;

        try
        {
            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            if (isTopUp)
            {
                await ClubManager.Instance.TopUpClubJackpotAsync(
                    clubId, amount);
            }
            else
            {
                await ClubManager.Instance.WithdrawClubJackpotAsync(
                    clubId, amount);
            }

            submitted = true;

            if (this == null)
                return;

            // Refresh the jackpot balance on the parent screen.
            if (JackpotScreen != null &&
                JackpotScreen.isActiveAndEnabled)
            {
                JackpotScreen.Reload();
            }

            if (!isActiveAndEnabled || ClubId != clubId)
                return;

            if (isTopUp && TopUpInputField != null)
                TopUpInputField.text = "";

            if (!isTopUp && WithdrawInputField != null)
                WithdrawInputField.text = "";

            balancesLoaded = false;

            await RefreshBalancesAsync(viewVersion, clubId);

            if (this != null && isActiveAndEnabled && ClubId == clubId)
            {
                SetMessage(isTopUp
                    ? "Top-up successful."
                    : "Withdraw successful.");
            }
        }
        catch (Exception e)
        {
            if (this != null && isActiveAndEnabled && ClubId == clubId)
            {
                ShowError(submitted
                    ? "Transaction successful, balance refresh failed. " +
                      "Reopen the panel to refresh the balance. " + e.Message
                    : "Request failed: " + e.Message);
            }


        }
        finally
        {
            if (this != null)
                SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;

        if (CloseButton != null)
            CloseButton.interactable = !value;

        if (TopUpButton != null)
            TopUpButton.interactable = !value;

        if (WithdrawButton != null)
            WithdrawButton.interactable = !value;

        if (TopUpInputField != null)
            TopUpInputField.interactable = !value;

        if (WithdrawInputField != null)
            WithdrawInputField.interactable = !value;

        if (TopupConfirm_Button != null)
            TopupConfirm_Button.interactable = !value && balancesLoaded;

        if (WithdrawConfirm_Button != null)
            WithdrawConfirm_Button.interactable = !value && balancesLoaded;
    }

    private void CloseButtonOnTap()
    {
        if (!busy)
            gameObject.SetActive(false);
    }

    private void TopUpButtonOnTap()
    {
        if (TopUpPanel != null)
            TopUpPanel.SetActive(true);

        if (WithdrawPanel != null)
            WithdrawPanel.SetActive(false);

        SetButtonSprite(TopUpButton, SelectButtonSprite);
        SetButtonSprite(WithdrawButton, UnSelectButtonSprite);

        if (WithdrawText != null)
            WithdrawText.color = new Color32(46, 46, 46, 255);

        if (TopUpText != null)
            TopUpText.color = new Color32(65, 26, 3, 255);
    }

    private void WithdrawButtonOnTap()
    {
        if (WithdrawPanel != null)
            WithdrawPanel.SetActive(true);

        if (TopUpPanel != null)
            TopUpPanel.SetActive(false);

        SetButtonSprite(TopUpButton, UnSelectButtonSprite);
        SetButtonSprite(WithdrawButton, SelectButtonSprite);

        if (WithdrawText != null)
            WithdrawText.color = new Color32(65, 26, 3, 255);

        if (TopUpText != null)
            TopUpText.color = new Color32(46, 46, 46, 255);
    }

    private static void SetButtonSprite(Button button, Sprite sprite)
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

    private static void SetText(Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private void SetMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            InformationPrefabScript.Instance.ShowMessage(message);
        }
    }

    private void ShowError(string message)
    {
        SetMessage(message);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (TopUpButton != null)
            TopUpButton.onClick.RemoveListener(TopUpButtonOnTap);

        if (WithdrawButton != null)
            WithdrawButton.onClick.RemoveListener(WithdrawButtonOnTap);

        if (TopupConfirm_Button != null)
            TopupConfirm_Button.onClick.RemoveListener(
                TopupConfirmButtonOnTap);

        if (WithdrawConfirm_Button != null)
            WithdrawConfirm_Button.onClick.RemoveListener(
                WithdrawConfirmButtonOnTap);
    }
}