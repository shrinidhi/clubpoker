using ClubPoker.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace ClubPoker.UI
{
    /// <summary>
    /// Opens the page behind a tapped &lt;link="id"&gt; in a TextMeshProUGUI.
    /// Attach to the text's GameObject; the text needs Raycast Target on to get taps.
    /// Ids: "terms", "privacy" (URLs in <see cref="LegalLinks"/>).
    /// </summary>
    public class TMPLinkHandler : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI textComponent;

        private void Awake()
        {
            if (textComponent == null)
                textComponent = GetComponent<TextMeshProUGUI>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (textComponent == null) return;

            // pressEventCamera is null on an Overlay canvas and the canvas camera
            // otherwise — hard-coding null misses every tap on a Camera canvas.
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                textComponent, eventData.position, eventData.pressEventCamera);

            if (linkIndex == -1) return;

            string linkId = textComponent.textInfo.linkInfo[linkIndex].GetLinkID();

            string url = UrlFor(linkId);
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning($"[TMPLinkHandler] No URL set for link '{linkId}'.");
                return;
            }

            Application.OpenURL(url);
        }

        private static string UrlFor(string linkId)
        {
            switch (linkId)
            {
                case "terms":   return LegalLinks.TermsUrl;
                case "privacy": return LegalLinks.PrivacyUrl;
                default:        return null;
            }
        }
    }
}
