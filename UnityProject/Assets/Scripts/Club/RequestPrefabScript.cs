using System;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class RequestPrefabScript : MonoBehaviour
{
    public Text PlayerName;
    public Text Player_Id;
    public Text Msg_Text;

    public Button Accept_Button;
    public Button Reject_Button;

    private ClubApplicationData applicationData;
    private Action<ClubApplicationData> onAccept;
    private Action<ClubApplicationData> onReject;

    public void Setup(
        ClubApplicationData data,
        Action<ClubApplicationData> acceptCallback,
        Action<ClubApplicationData> rejectCallback)
    {
        applicationData = data;
        onAccept = acceptCallback;
        onReject = rejectCallback;

        if (data.Applicant != null)
        {
            PlayerName.text = data.Applicant.Username;
            Player_Id.text = data.Applicant.Id;
        }
        else
        {
            PlayerName.text = "Unknown";
            Player_Id.text = data.UserId;
        }

        Msg_Text.text = data.Message;

        Accept_Button.onClick.RemoveAllListeners();
        Accept_Button.onClick.AddListener(OnAcceptClick);

        Reject_Button.onClick.RemoveAllListeners();
        Reject_Button.onClick.AddListener(OnRejectClick);
    }

    private void OnAcceptClick()
    {
        onAccept?.Invoke(applicationData);
    }

    private void OnRejectClick()
    {
        onReject?.Invoke(applicationData);
    }
}