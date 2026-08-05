using System;
using UnityEngine;
using UnityEngine.UI;

public class MationPrefab : MonoBehaviour
{
    public Button MantionButton;
    public Text MantionName;

    private string username;
    private Action<string> callback;

    public void SetData(string playerName, Action<string> onClick)
    {
        username = playerName;
        callback = onClick;

        if (MantionName != null)
            MantionName.text = "@" + playerName;

        if (MantionButton != null)
        {
            MantionButton.onClick.RemoveAllListeners();
            MantionButton.onClick.AddListener(MantionButtonOnTap);
        }
    }

    private void MantionButtonOnTap()
    {
        callback?.Invoke(username);
    }

    private void OnDestroy()
    {
        if (MantionButton != null)
            MantionButton.onClick.RemoveListener(MantionButtonOnTap);
    }
}