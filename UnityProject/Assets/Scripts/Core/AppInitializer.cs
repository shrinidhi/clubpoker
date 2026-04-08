using UnityEngine;

namespace ClubPoker.Core
{
    public class AppInitializer : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[AppInitializer] App started - Loading Splash scene");
            GameSceneManager.Instance.LoadScene("Scene_Splash");
        }
    }
}