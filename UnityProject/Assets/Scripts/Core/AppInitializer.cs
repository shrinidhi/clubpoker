using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace ClubPoker.Core
{
    public class AppInitializer : MonoBehaviour
    {
        private async void Start()
        {
            Debug.Log("[AppInitializer] App started");
            DisableShowWhenLocked();

            //  - Load config
            await ConfigManager.Instance.LoadConfigAsync();

            // //  - Validate config
            // await ConfigValidator.Instance.ValidateAsync();

            //  - Initialize feature flags
            await FeatureFlagManager.Instance.InitializeAsync();

            //  - Load Splash scene
            Debug.Log("[AppInitializer] Initialization complete - Loading Splash");
            GameSceneManager.Instance.LoadScene("Scene_Splash");
        }

        private void DisableShowWhenLocked()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
                            using (AndroidJavaObject window = currentActivity.Call<AndroidJavaObject>("getWindow"))
                            {
                                // Android Constant Values
                                // 524288 = FLAG_SHOW_WHEN_LOCKED
                                // 4194304 = FLAG_TURN_SCREEN_ON
                                const int FLAG_SHOW_WHEN_LOCKED = 524288;
                                const int FLAG_TURN_SCREEN_ON = 4194304;

                                // Explicitly strip these flags from the window object
                                window.Call("clearFlags", FLAG_SHOW_WHEN_LOCKED);
                                window.Call("clearFlags", FLAG_TURN_SCREEN_ON);
                            }
                        }));
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to clear Android window flags: " + e.Message);
            }
            #endif
        }
    }
}