using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;
using TMPro;

namespace ClubPoker.UI
{
    public class ForceUpgradeScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button updateButton;

        [SerializeField] private string appStoreUrl = "https://apps.apple.com/app/clubpoker";
        [SerializeField] private string playStoreUrl = "https://play.google.com/store/apps/details?id=com.game.clubpoker";

        private void Awake()
        {
            // Non-dismissible - disable back button
            updateButton.onClick.AddListener(OpenStore);
        }

        public void Show(string currentVersion, string minimumVersion)
        {
            gameObject.SetActive(true);

            messageText.text = "A new version of ClubPoker is required to continue playing. Please update to the latest version.";
            versionText.text = $"Your version: {currentVersion}\nRequired version: {minimumVersion}";

            Debug.Log("[ForceUpgradeScreen] Showing force upgrade screen");
        }

        private void OpenStore()
        {
    #if UNITY_IOS
            Application.OpenURL(appStoreUrl);
    #elif UNITY_ANDROID
            Application.OpenURL(playStoreUrl);
    #else
            Application.OpenURL(appStoreUrl);
    #endif
        }

        // Prevent back button dismissing screen
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Do nothing - non dismissible!
                Debug.Log("[ForceUpgradeScreen] Back button disabled!");
            }
        }
    }
}