using UnityEngine;

namespace ClubPoker.Core
{
    public class SceneController : MonoBehaviour
    {
        [SerializeField] private string sceneName;

        private void Awake()
        {
            Debug.Log($"[SceneController] {sceneName} scene loaded.");
        }
    }
}