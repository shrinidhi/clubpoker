using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class ShowMobilePushNotiManager : MonoBehaviour
{
    public GameObject PushScreen;

    public TextMeshProUGUI Title_Text;
    public TextMeshProUGUI Content_Text;

    public Button CloseButton;
    public Button OkButton;
    public TextMeshProUGUI OkButtonText;

    public ShowClubTableScreenScript ShowClubTableScreenScript;

    private string _tableId;
    private bool _loading;

    private void Start()
    {
        if (CloseButton != null) CloseButton.onClick.AddListener(Close);
        if (OkButton != null) OkButton.onClick.AddListener(OnOkButtonTap);

        if (PushScreen != null) PushScreen.SetActive(false);

        LoadPendingPush().Forget();
    }

    public void RefreshPendingPush()
    {
        LoadPendingPush().Forget();
    }

    private async UniTaskVoid LoadPendingPush()
    {
        if (_loading) return;
        if (string.IsNullOrEmpty(ClubContext.ClubId))
        {
            if (PushScreen != null) PushScreen.SetActive(false);
            return;
        }

        _loading = true;

        try
        {
            var data = await ClubManager.Instance.GetPendingPushAsync(ClubContext.ClubId);

            if (data == null || data.Push == null)
            {
                _tableId = null;

                if (PushScreen != null) PushScreen.SetActive(false);

                return;
            }

            PendingPushItem push = data.Push;

            if (Title_Text != null) Title_Text.text = push.Title ?? "";
            if (Content_Text != null) Content_Text.text = push.Content ?? "";

            _tableId = push.TableId;

            bool hasTable = !string.IsNullOrEmpty(_tableId);

            if (OkButtonText != null) OkButtonText.text = hasTable ? "Go to table" : "OK";

            if (PushScreen != null) PushScreen.SetActive(true);

            Debug.Log(
                "Pending Push | Title : " + push.Title +
                " | TableId : " + (_tableId ?? "null") +
                " | SentAt : " + push.SentAt
            );
        }
        catch (Exception e)
        {
            Debug.LogError("[ShowMobilePushNotiManager] Pending push error : " + e.Message);

            _tableId = null;

            if (PushScreen != null) PushScreen.SetActive(false);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnOkButtonTap()
    {
        if (string.IsNullOrEmpty(_tableId))
        {
            Close();
            return;
        }

        string tableId = _tableId;

        Close();

        if (ShowClubTableScreenScript != null)
        {
            ShowClubTableScreenScript.JoinTableById(tableId);
        }
        else
        {
            Debug.LogError("[ShowMobilePushNotiManager] ShowClubTableScreenScript not assigned");
        }
    }

    private void Close()
    {
        if (PushScreen != null) PushScreen.SetActive(false);
    }
}