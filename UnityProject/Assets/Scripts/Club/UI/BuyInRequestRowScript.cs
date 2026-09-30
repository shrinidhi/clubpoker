using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Networking.Models;

/// <summary>
/// One row of the Authorized List: who wants to sit down and for how much, with
/// Reject / Approve. Both buttons lock on tap so a slow call can't be sent
/// twice; the panel removes the row on success or calls
/// <see cref="SetButtonsInteractable"/> to hand it back on failure.
/// </summary>
public class BuyInRequestRowScript : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Image           avatarImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI idText;

    [Header("Request")]
    [SerializeField] private TextMeshProUGUI amountText;

    [Header("Actions")]
    [SerializeField] private Button rejectButton;
    [SerializeField] private Button approveButton;

    [Header("Avatar lookup")]
    [Tooltip("Same avatar set the seats use. Left empty, the prefab's image stays.")]
    [SerializeField] private InGameAvtarSO avatarSet;

    public string RequestId { get; private set; }

    private Action<BuyInRequestRowScript> _onApprove;
    private Action<BuyInRequestRowScript> _onReject;

    private void Awake()
    {
        if (rejectButton != null)  rejectButton.onClick.AddListener(OnRejectTap);
        if (approveButton != null) approveButton.onClick.AddListener(OnApproveTap);
    }

    public void Setup(BuyInRequestItem item,
                      Action<BuyInRequestRowScript> onApprove,
                      Action<BuyInRequestRowScript> onReject)
    {
        RequestId  = item.Id;
        _onApprove = onApprove;
        _onReject  = onReject;

        if (nameText != null)   nameText.text   = item.Username;
        if (idText != null)     idText.text     = "ID: " + ShortId(item.PlayerId);
        if (amountText != null) amountText.text = item.Amount.ToString("N0");

        SetAvatar(item.Avatar);
        SetButtonsInteractable(true);
    }

    public void SetButtonsInteractable(bool interactable)
    {
        if (rejectButton != null)  rejectButton.interactable  = interactable;
        if (approveButton != null) approveButton.interactable = interactable;
    }

    private void OnApproveTap()
    {
        SetButtonsInteractable(false);
        _onApprove?.Invoke(this);
    }

    private void OnRejectTap()
    {
        SetButtonsInteractable(false);
        _onReject?.Invoke(this);
    }

    private void SetAvatar(string avatarName)
    {
        if (avatarImage == null || avatarSet == null || string.IsNullOrEmpty(avatarName))
            return;

        foreach (IngameAvtarData data in avatarSet.AvtarBadges)
        {
            if (data.AvtarName == avatarName)
            {
                avatarImage.sprite = data.AvtarImage;
                return;
            }
        }
    }

    // Player ids are UUIDs — the first block is what the rest of the UI shows.
    private static string ShortId(string id) =>
        string.IsNullOrEmpty(id) ? "-" : id.Split('-')[0];
}
