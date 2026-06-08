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
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // API 27+ (Android 8.1+)
                if (AndroidJNI.FindClass("android/os/Build$VERSION") != IntPtr.Zero)
                {
                    int sdkInt = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                    if (sdkInt >= 27)
                    {
                        activity.Call("setShowWhenLocked", false);
                        activity.Call("setTurnScreenOn", false);
                    }
                }

                // Also clear window flag FLAG_SHOW_WHEN_LOCKED (0x00080000)
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using var window = activity.Call<AndroidJavaObject>("getWindow");
                    window.Call("clearFlags", 0x00080000);
                }));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AppInitializer] DisableShowWhenLocked failed: {e.Message}");
            }
#endif
        }
    }
}