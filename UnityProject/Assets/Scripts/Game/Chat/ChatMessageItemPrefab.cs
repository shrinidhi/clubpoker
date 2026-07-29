using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class ChatMessageItemPrefab : MonoBehaviour
    {
        public Text UsernameText;
        public Text MessageText;
        public Text TimeText;

        public void SetData(
            string username,
            string message,
            string time)
        {
            if (UsernameText != null)
            {
                UsernameText.text =
                    string.IsNullOrEmpty(username)
                        ? "Unknown"
                        : username;
            }

            if (MessageText != null)
            {
                MessageText.text =
                    message ?? "";
            }

            if (TimeText != null)
            {
                TimeText.text =
                    time ?? "";
            }
        }
    }
}