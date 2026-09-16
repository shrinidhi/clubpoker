using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;

/// <summary>
/// "Request Chips" popup — members who can't fund the pool ask the club for chips.
/// Any whole amount above 0. The request lands in the admin's Cashier ▸ Chips Request tab.
/// POST /api/clubs/{clubId}/chips/request { amount }.
/// </summary>
public class RequestChipsModalScript : MonoBehaviour
{
    [Header("Close")]
    public Button Close_Button;
    [Header("Input")]
    public TMP_InputField Amount_InputField;

    [Header("Text")]
    public TextMeshProUGUI CurrentChips_Text;   // optional — member's own club chips
    
    [Header("Footer")]
    public Button Confirm_Button;
    

    private bool _busy;

    private void Start()
    {
        Confirm_Button.onClick.AddListener(OnConfirmTap);
        Close_Button.onClick.AddListener(Close);

        Amount_InputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        Amount_InputField.onValueChanged.AddListener(_ => Refresh());
    }

    public void Show()
    {
        _busy = false;
        Amount_InputField.text = "";
        if (CurrentChips_Text != null) CurrentChips_Text.text = ClubWallet.Chips.ToString("N0");

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        Refresh();
    }

    private long ParseAmount() =>
        long.TryParse(Amount_InputField.text, out long v) && v > 0 ? v : 0;

    private void Refresh()
    {
        Confirm_Button.interactable = !_busy && ParseAmount() > 0;
    }

    private void OnConfirmTap()
    {
        long amount = ParseAmount();
        if (_busy || amount <= 0) return;
        SendRequest(amount).Forget();
    }

    private async UniTaskVoid SendRequest(long amount)
    {
        _busy = true;
        Refresh();
        try
        {
            var res = await ClubManager.Instance.RequestChipsAsync(ClubContext.ClubId, amount);

            // Clubs with auto-reject on answer straight away.
            bool rejected = string.Equals(res?.Status, "REJECTED", StringComparison.OrdinalIgnoreCase);
            ShowToast(rejected ? "Chip request was rejected" : "Chip request sent");
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[RequestChipsModalScript] request error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to send request" : e.Message);
        }
        finally
        {
            _busy = false;
            if (gameObject.activeInHierarchy) Refresh();
        }
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    private static void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
