using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChipRequestRowScript : MonoBehaviour
{
    [Header("Member Info")]
    public Image Avatar_Image;
    public TextMeshProUGUI Name_Text;
    public TextMeshProUGUI Id_Text;
    public TextMeshProUGUI Timestamp_Text;
    public TextMeshProUGUI Chips_Text;
    public TextMeshProUGUI Note_Text;

    [Header("Action Buttons")]
    public Button Reject_Button;
    public Button Approve_Button;

    private string _requestId;
    private Action<string> _onApprove;
    private Action<string> _onReject;

    public void Setup(ChipRequestItem item,
        Action<string> onApprove,
        Action<string> onReject,
        bool autoRejectMode = false)
    {
        _requestId = item.Id;
        _onApprove = onApprove;
        _onReject  = onReject;

        Name_Text.text  = item.MemberName;
        Id_Text.text    = "ID: " + item.MemberId?.Split('-')[0];
        Chips_Text.text = item.Amount.ToString("N0");

        var localTime = item.CreatedAt.ToLocalTime();
        Timestamp_Text.text = localTime.ToString("dd/MM/yyyy HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);

        if (Note_Text != null)
        {
            bool hasNote = !string.IsNullOrEmpty(item.Note);
            Note_Text.gameObject.SetActive(hasNote);
            if (hasNote) Note_Text.text = item.Note;
        }

        Reject_Button.onClick.RemoveAllListeners();
        Approve_Button.onClick.RemoveAllListeners();
        Reject_Button.onClick.AddListener(OnRejectTap);
        Approve_Button.onClick.AddListener(OnApproveTap);

        Approve_Button.interactable = !autoRejectMode;
    }

    private void OnApproveTap()
    {
        Approve_Button.interactable = false;
        Reject_Button.interactable  = false;
        _onApprove?.Invoke(_requestId);
    }

    public void SetApproveInteractable(bool interactable)
    {
        Approve_Button.interactable = interactable;
    }

    private void OnRejectTap()
    {
        Approve_Button.interactable = false;
        Reject_Button.interactable  = false;
        _onReject?.Invoke(_requestId);
    }
}
