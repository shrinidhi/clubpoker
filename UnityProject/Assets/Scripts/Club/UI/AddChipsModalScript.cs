using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// "Exchange Chips" popup — Creator / Manager spend diamonds to fund the club pool at
/// ClubManager.DiamondsPerStep diamonds → ClubManager.ChipsPerStep chips. The amount must
/// be a whole multiple of DiamondsPerStep and within the available diamond balance.
///
/// One instance, opened from two places: Cashier ▸ Trade (Available Chips) and the club
/// home header. Each caller passes its own refresh through Show(onAdded).
///
/// POST /api/economy/exchange with clubId — see ClubManager.ExchangeDiamondsToPoolAsync.
/// </summary>
public class AddChipsModalScript : MonoBehaviour
{
    [Header("Header")]
    [FormerlySerializedAs("Cancel_Button")]
    public Button Close_Button;                       // X

    [Header("Diamonds")]
    public TextMeshProUGUI DiamondBalance_Text;       // available diamonds, top-left
    [Tooltip("Diamonds to exchange.")]
    public TMP_InputField Amount_InputField;

    [Header("Chips")]
    [Tooltip("Chips to add. Linked to the diamond box both ways: typing in either fills " +
             "the other, once the amount is a whole step (100 diamonds / 1000 chips).")]
    public TMP_InputField Chips_InputField;

    [Header("Info")]
    public TextMeshProUGUI Rate_Text;                 // bulleted notes, set by code

    [Header("Footer")]
    public Button Confirm_Button;

    // -1 while the balance is unknown (loading or failed) — the server decides then.
    private long _available = -1;
    private bool _busy;
    private Action<long> _onAdded;

    // Which box the user typed in last — the one that's reset on a bad amount and that
    // Confirm prices the exchange from.
    private bool _lastEditedChips;

    private void Start()
    {
        Confirm_Button.onClick.AddListener(OnConfirmTap);
        Close_Button.onClick.AddListener(Close);

        Amount_InputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        Amount_InputField.onValueChanged.AddListener(OnDiamondsChanged);
        Amount_InputField.onEndEdit.AddListener(_ => ResetIfInvalid());

        Chips_InputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        Chips_InputField.onValueChanged.AddListener(OnChipsChanged);
        Chips_InputField.onEndEdit.AddListener(_ => ResetIfInvalid());
    }

    /// <param name="onAdded">Called with the chips added once the exchange succeeds.</param>
    public void Show(Action<long> onAdded = null)
    {
        _onAdded = onAdded;
        _available = -1;
        _busy = false;

        _lastEditedChips = false;
        Amount_InputField.SetTextWithoutNotify("");
        Chips_InputField.SetTextWithoutNotify("");
        if (DiamondBalance_Text != null) DiamondBalance_Text.text = "-";
        if (Rate_Text != null) Rate_Text.text = BuildInfoText();
        Confirm_Button.interactable = true;

        transform.SetAsLastSibling();   // above whichever screen opened it
        gameObject.SetActive(true);
        LoadBalance().Forget();
    }

    // Bulleted notes under the input. <indent> gives a hanging indent, so a wrapped
    // line starts under the text, not under the bullet. Rich Text must be on.
    private static string BuildInfoText()
    {
        string[] lines =
        {
            $"{ClubManager.DiamondsPerStep} Diamond = {ClubManager.ChipsPerStep} Chips",
            $"Please enter an integral multiple of {ClubManager.DiamondsPerStep}.",
            "Please contact Club Poker customer support if you think diamond convert to chip ratio is not correct.",
        };

        var sb = new System.Text.StringBuilder();
        foreach (string line in lines)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("•<indent=1em>").Append(line).Append("</indent>");
        }
        return sb.ToString();
    }

    private async UniTaskVoid LoadBalance()
    {
        try
        {
            var d = await ClubManager.Instance.GetDiamondsAsync();
            _available = d?.Available ?? 0;
            if (DiamondBalance_Text != null) DiamondBalance_Text.text = _available.ToString("N0");
            SyncOtherBox();   // anything typed before the balance landed
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddChipsModalScript] diamond balance error: {e.Message}");
            _available = -1;
            if (DiamondBalance_Text != null) DiamondBalance_Text.text = "-";
        }
    }

    private static long Parse(TMP_InputField field) =>
        long.TryParse(field.text, out long v) && v > 0 ? v : 0;

    private static bool IsValidDiamonds(long diamonds) =>
        diamonds > 0 && diamonds % ClubManager.DiamondsPerStep == 0;

    private static bool IsValidChips(long chips) =>
        chips > 0 && chips % ClubManager.ChipsPerStep == 0;

    /// True while the balance is unknown — the server decides then.
    private bool Affordable(long diamonds) => _available < 0 || diamonds <= _available;

    private void OnDiamondsChanged(string _)
    {
        _lastEditedChips = false;
        SyncOtherBox();
    }

    private void OnChipsChanged(string _)
    {
        _lastEditedChips = true;
        SyncOtherBox();
    }

    // The two boxes mirror each other, but the other box only fills in for a whole step
    // the player can afford — 150, or 100 against a balance of 4, leaves it blank.
    // SetTextWithoutNotify so filling one box doesn't fire the other's handler back.
    private void SyncOtherBox()
    {
        if (_lastEditedChips)
        {
            long chips = Parse(Chips_InputField);
            long cost  = chips / ClubManager.ChipsPerStep * ClubManager.DiamondsPerStep;
            Amount_InputField.SetTextWithoutNotify(IsValidChips(chips) && Affordable(cost)
                ? cost.ToString()
                : "");
        }
        else
        {
            long diamonds = Parse(Amount_InputField);
            Chips_InputField.SetTextWithoutNotify(IsValidDiamonds(diamonds) && Affordable(diamonds)
                ? (diamonds / ClubManager.DiamondsPerStep * ClubManager.ChipsPerStep).ToString()
                : "");
        }
    }

    // Keyboard closed on an amount that can't be exchanged — not a whole step (150
    // diamonds / 1000 chips) or more than the balance: both boxes are emptied, which
    // shows the "0" placeholder. Silent, as in the reference. Checked on end edit, not
    // per keystroke, so typing 1000 against a balance of 400 isn't wiped at the first "1".
    private void ResetIfInvalid()
    {
        var typedIn = _lastEditedChips ? Chips_InputField : Amount_InputField;

        long amount = Parse(typedIn);
        if (amount <= 0) return;   // empty / 0 — nothing to reset

        bool wholeStep = _lastEditedChips ? IsValidChips(amount) : IsValidDiamonds(amount);
        long diamonds  = _lastEditedChips
            ? amount / ClubManager.ChipsPerStep * ClubManager.DiamondsPerStep
            : amount;
        if (wholeStep && Affordable(diamonds)) return;

        Amount_InputField.SetTextWithoutNotify("");
        Chips_InputField.SetTextWithoutNotify("");
    }

    // Confirm stays tappable. Empty, 0 or not a whole step → "Invalid number" (the rule
    // is in the notes above). The cost comes from the box typed in, so a valid chips
    // amount is priced even when the diamond box was left blank.
    private void OnConfirmTap()
    {
        if (_busy) return;

        long diamonds;
        if (_lastEditedChips)
        {
            long chips = Parse(Chips_InputField);
            diamonds = IsValidChips(chips) ? chips / ClubManager.ChipsPerStep * ClubManager.DiamondsPerStep : 0;
        }
        else
        {
            long typed = Parse(Amount_InputField);
            diamonds = IsValidDiamonds(typed) ? typed : 0;
        }

        if (diamonds <= 0)
        {
            ShowToast("Invalid number");
            return;
        }
        if (!Affordable(diamonds))
        {
            ShowToast("Not enough diamonds");
            return;
        }

        Exchange(diamonds).Forget();
    }

    private async UniTaskVoid Exchange(long diamonds)
    {
        _busy = true;
        Confirm_Button.interactable = false;
        try
        {
            var res = await ClubManager.Instance.ExchangeDiamondsToPoolAsync(ClubContext.ClubId, diamonds);
            if (res == null || !res.Success)
            {
                ShowToast("Failed to add chips");
                return;
            }

            // Fires OnPoolChipsChanged → cashier stats bar and club home header follow.
            ClubContext.UpdatePoolChips(res.ClubChipPool, ClubContext.MembersChips, ClubContext.AgentsCredit);

            ShowToast($"Added {res.ChipsReceived:N0} chips to club pool");
            var cb = _onAdded;
            Close();
            cb?.Invoke(res.ChipsReceived);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddChipsModalScript] exchange error: {e.Message}");
            ShowToast(ResolveError(e));
        }
        finally
        {
            _busy = false;
            Confirm_Button.interactable = true;
        }
    }

    private static string ResolveError(Exception e)
    {
        string m = e.Message ?? "";
        if (m.IndexOf("diamond", StringComparison.OrdinalIgnoreCase) >= 0 ||
            m.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Not enough diamonds";
        return string.IsNullOrEmpty(m) ? "Failed to add chips" : m;
    }

    private void Close()
    {
        _onAdded = null;
        gameObject.SetActive(false);
    }

    private static void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
