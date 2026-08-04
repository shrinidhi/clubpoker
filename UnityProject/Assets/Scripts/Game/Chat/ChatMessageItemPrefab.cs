using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class ChatMessageItemPrefab : MonoBehaviour
    {
        public Text UsernameText;
        public Text MessageText;
        public Text TimeText;

        [Header("Message Text Size")]
        public float MaxTextWidth = 650f;

        [Header("Bubble")]
        public RectTransform Msg_BG;
        public bool IsOwnMessage = true;

        public RectTransform Root;
        public void SetData(string username, string message, string time , bool isown)
        {
            IsOwnMessage = isown;
            if (UsernameText != null)
                UsernameText.text = string.IsNullOrEmpty(username) ? "Unknown" : username;

            if (TimeText != null)
                TimeText.text = time ?? "";

            if (MessageText != null)
            {
                MessageText.text = message ?? "";
                SetMessageTextSize();
            }
        }

        private void SetMessageTextSize()
        {
            if (MessageText == null || Msg_BG == null)
                return;

            MessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            MessageText.verticalOverflow = VerticalWrapMode.Overflow;

            Canvas.ForceUpdateCanvases();

            TextGenerationSettings settings =
                MessageText.GetGenerationSettings(
                    new Vector2(10000f, 10000f)
                );

            float preferredWidth =
                MessageText.cachedTextGeneratorForLayout
                    .GetPreferredWidth(
                        MessageText.text,
                        settings
                    ) / MessageText.pixelsPerUnit;

            float finalWidth =
                Mathf.Min(
                    preferredWidth,
                    MaxTextWidth
                );

            MessageText.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                finalWidth
            );

            Canvas.ForceUpdateCanvases();

            float finalHeight =
                MessageText.preferredHeight;

            MessageText.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                finalHeight
            );

            float bubbleWidth =
                finalWidth + 210f;

            float bubbleHeight =
                finalHeight + 100f;

            SetBubblePositionAndSize(
                bubbleWidth,
                bubbleHeight
            );

            Debug.Log(
                $"Text: {finalWidth} x {finalHeight}"
            );

            Debug.Log(
                $"Bubble: {bubbleWidth} x {bubbleHeight}"
            );
        }

        private void SetBubblePositionAndSize(float bubbleWidth, float bubbleHeight)
        {
            float fixedX = Msg_BG.anchoredPosition.x;
            float fixedY = Msg_BG.anchoredPosition.y;

            if (IsOwnMessage)
            {
                Msg_BG.anchorMin = new Vector2(1f, 0.5f);
                Msg_BG.anchorMax = new Vector2(1f, 0.5f);
                Msg_BG.pivot = new Vector2(1f, 0.5f);
            }
            else
            {
                Msg_BG.anchorMin = new Vector2(0f, 0.5f);
                Msg_BG.anchorMax = new Vector2(0f, 0.5f);
                Msg_BG.pivot = new Vector2(0f, 0.5f);
            }

            Msg_BG.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                bubbleWidth
            );

            Msg_BG.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                bubbleHeight
            );

            Msg_BG.anchoredPosition = new Vector2(
                fixedX,
                fixedY
            );

            if (Root != null)
            {
                Root.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    bubbleHeight
                );
            }
        }
    }
}