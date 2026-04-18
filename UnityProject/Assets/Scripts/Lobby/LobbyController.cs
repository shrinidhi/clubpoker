using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using ClubPoker.Core;

namespace ClubPoker.Lobby
{
    public class LobbyController : MonoBehaviour
    {
        private AsyncOperationHandle<SceneInstance> _preloadHandle;
        private bool _isPreloaded = false;
        public Button Profile_Button;
        public GameObject Profile_Screen;
        public Button Transaction_Button;
        public GameObject Transaction_Screen;
        public Button Lobby_Button;
        public GameObject Lobby_Panel;
        public Button CreateTable_Button;
        public GameObject CreateTablePanel;
        private void OnEnable()
        {
            // Start preloading GameTable as soon as Lobby becomes active
          //  PreloadGameTableAsync().Forget();
            Profile_Button.onClick.AddListener(Profile_ButtonOnTap);
            Transaction_Button.onClick.AddListener(Transaction_ButtonOnTap);
            Lobby_Button.onClick.AddListener(Lobby_ButtonOnTap);
            CreateTable_Button.onClick.AddListener(CreateTable_ButtonOnTap);
        }

        void CreateTable_ButtonOnTap()
        {
            CreateTablePanel.SetActive(true);
        }
        void Lobby_ButtonOnTap()
        {
            Lobby_Panel.SetActive(true);
        }
        void Profile_ButtonOnTap()
        {
            GameSceneManager.Instance.LoadScene("Scene_Profile");
        }

        void Transaction_ButtonOnTap()
        {
            Transaction_Screen.SetActive(true);
        }

        private async UniTaskVoid PreloadGameTableAsync()
        {
            if (_isPreloaded) return;

            Debug.Log("[LobbyController] Starting GameTable preload...");

            _preloadHandle = Addressables.LoadSceneAsync(
                "Scene_GameTable",
                UnityEngine.SceneManagement.LoadSceneMode.Additive,
                activateOnLoad: false  // Load but don't activate yet!
            );

            await _preloadHandle;

            if (_preloadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                _isPreloaded = true;
                Debug.Log("[LobbyController] GameTable preloaded successfully!");
            }
            else
            {
                Debug.LogError("[LobbyController] GameTable preload failed!");
            }
        }

        public async UniTask JoinTable()
        {
            Debug.Log("[LobbyController] Joining table...");

            if (_isPreloaded)
            {
                // Already preloaded - activate instantly!
                await _preloadHandle.Result.ActivateAsync();
                Debug.Log("[LobbyController] GameTable activated instantly!");
            }
            else
            {
                // Not preloaded yet - load normally
                Debug.Log("[LobbyController] Preload not ready - loading normally");
                ClubPoker.Core.GameSceneManager.Instance.LoadScene("Scene_GameTable");
            }
        }

        private void OnDisable()
        {
            // Release preloaded scene if not used
            if (_preloadHandle.IsValid() && !_isPreloaded)
            {
                Addressables.Release(_preloadHandle);
            }
        }
    }
}