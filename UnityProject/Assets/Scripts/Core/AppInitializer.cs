using UnityEngine;
using Cysharp.Threading.Tasks;

namespace ClubPoker.Core
{
    public class AppInitializer : MonoBehaviour
    {
        private async void Start()
        {
            Debug.Log("[AppInitializer] App started");

            //  - Load config
            await ConfigManager.Instance.LoadConfigAsync();

            //  - Validate config
            await ConfigValidator.Instance.ValidateAsync();

            //  - Initialize feature flags
            await FeatureFlagManager.Instance.InitializeAsync();

            //  - Load Splash scene
            Debug.Log("[AppInitializer] Initialization complete - Loading Splash");
            GameSceneManager.Instance.LoadScene("Scene_Splash");
        }
    }
}