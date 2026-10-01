using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System;

public class ShowClubNotificationManager : MonoBehaviour
{
    public GameObject ClubNotificationScreen;
    public Button OkButton;
    public Text Title_Text;
    public Text Content_Text;

    // Start is called before the first frame update
    void Start()
    {
        if (OkButton != null) OkButton.onClick.AddListener(OkButtonTap);
        if (ClubNotificationScreen != null) ClubNotificationScreen.SetActive(false);
        LoadNotification().Forget();
    }

    void OkButtonTap()
    {
        ClubNotificationScreen.SetActive(false);
    }

    private async UniTaskVoid LoadNotification()
    {
      
        if (string.IsNullOrEmpty(ClubContext.ClubId))
        {
            if (ClubNotificationScreen != null) ClubNotificationScreen.SetActive(false);
            return;
        }
        try
        {
            var data = await ClubManager.Instance.GetNotificationAsync(ClubContext.ClubId);

            if (data == null || data.Notification == null)
            {

                if (ClubNotificationScreen != null) ClubNotificationScreen.SetActive(false);

                return;
            }

            NotificationItem notification = data.Notification;

            if (Title_Text != null) Title_Text.text = notification.Title ?? "";
            if (Content_Text != null) Content_Text.text = notification.Content ?? "";

            if (ClubNotificationScreen != null) ClubNotificationScreen.SetActive(true);

        }
        catch (Exception e)
        {
            if (ClubNotificationScreen != null) ClubNotificationScreen.SetActive(false);
        }
        
    }

}
