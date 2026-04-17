// 📁 CREATE AT: Assets/Scripts/UI/TMPLinkHandler.cs
// Attach to any GameObject with a TextMeshProUGUI that has <link> tags

using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace ClubPoker.UI
{
    public class TMPLinkHandler : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI textComponent;

        public void OnPointerClick(PointerEventData eventData)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                textComponent, eventData.position, null);

            if (linkIndex == -1) return;

            string linkId = textComponent.textInfo
                .linkInfo[linkIndex].GetLinkID();

            switch (linkId)
            {
                case "terms":
                    // TODO: replace with actual URL
                    Debug.Log("[TMPLinkHandler] Terms tapped.");
                    // Application.OpenURL("https://yoursite.com/terms");
                    break;

                case "privacy":
                    // TODO: replace with actual URL
                    Debug.Log("[TMPLinkHandler] Privacy tapped.");
                    // Application.OpenURL("https://yoursite.com/privacy");
                    break;
            }
        }
    }
}