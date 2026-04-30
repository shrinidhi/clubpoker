using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class ChatMessageItemPrefab : MonoBehaviour
{
    public Text UsernameText;
    public Text MessageText;
    public Text TimeText;
    public RectTransform BubbleRoot;

    public void SetData(
        string username,
        string message,
        string time,
        bool isMine
    )
    {
        if (UsernameText != null)
            UsernameText.text = username;

        if (MessageText != null)
            MessageText.text = message;

        if (TimeText != null)
            TimeText.text = time;

        if (BubbleRoot != null)
        {
            BubbleRoot.anchorMin = isMine
                ? new Vector2(1f, 0.5f)
                : new Vector2(0f, 0.5f);

            BubbleRoot.anchorMax = isMine
                ? new Vector2(1f, 0.5f)
                : new Vector2(0f, 0.5f);

            BubbleRoot.pivot = isMine
                ? new Vector2(1f, 0.5f)
                : new Vector2(0f, 0.5f);
        }
    }
}
}