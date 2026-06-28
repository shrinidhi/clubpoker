using System;
using UnityEngine;
using UnityEngine.UI;

public class WaitingPanelPrefab : MonoBehaviour
{
    public Text Playername;
    public Text Time;
    public Text Position;
    public Button RemoveButton;

    public Action<string> OnRemoveAction;

    private string playerId;

    public void SetData(
        string id,
        string username,
        string waitingTime,
        int position,
        bool showRemoveButton)
    {
        playerId = id;

        Playername.text = username;
        Position.text = "#" + position;
        Time.text = waitingTime;

        RemoveButton.gameObject.SetActive(showRemoveButton);

        RemoveButton.onClick.RemoveAllListeners();
        RemoveButton.onClick.AddListener(OnRemoveClicked);
    }

    private void OnRemoveClicked()
    {
        OnRemoveAction?.Invoke(playerId);
    }
}