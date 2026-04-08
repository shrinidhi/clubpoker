using UnityEngine;
using Cysharp.Threading.Tasks;

namespace ClubPoker.Core
{
    public class AppInitializer : MonoBehaviour
    {
        private async void Start()
        {
            Debug.Log("[AppInitializer] App started");

            await ConfigManager.Instance.LoadConfigAsync();

            await ConfigValidator.Instance.ValidateAsync();

            await FeatureFlagManager.Instance.InitializeAsync();

            
            Debug.Log("[AppInitializer] Config loaded - Loading Splash scene");
            GameSceneManager.Instance.LoadScene("Scene_Splash");
        }
    }
}