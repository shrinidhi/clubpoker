using ClubPoker.Game;
using UnityEngine;
using UnityEngine.UI;

public class DefaultMsgPrefab : MonoBehaviour
{
    public Button SendButton;
    public Text DefaultMsg;

    private void Start()
    {
        if (SendButton != null)
            SendButton.onClick.AddListener(SendDefaultMessage);
    }

    private void OnDestroy()
    {
        if (SendButton != null)
            SendButton.onClick.RemoveListener(SendDefaultMessage);
    }

    private void SendDefaultMessage()
    {
        if (ChatHandler.Instance == null)
            return;

        ChatHandler.Instance.SendDefaultMessage(DefaultMsg.text);
    }
}