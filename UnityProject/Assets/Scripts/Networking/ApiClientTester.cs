using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Core;
using ClubPoker.Networking.Models;

namespace ClubPoker.Networking
{
    public class ApiClientTester : MonoBehaviour
    {
        private async void Start()
        {
             await UniTask.WaitUntil(() => ConfigManager.Instance != null && 
                ConfigManager.Instance.IsConfigLoaded);
    
            await TestLoginAsync();
        }

        private async UniTask TestLoginAsync()
        {
            try
            {
                // Test POST - Login
                LoginRequest request = new LoginRequest
                {
                    Email = "tag1_xo4k@poker.dev",
                    Password = "Test1234!!"
                };

                LoginResponse response = await ApiClient.Instance
                    .Post<LoginResponse>("/api/auth/login", request);

                // Store tokens
                ApiClient.Instance.SetTokens(
                    response.Tokens.AccessToken,
                    response.Tokens.RefreshToken
                );
                // Debug.Log($"[ApiClientTester] POST works - Login: {response.Player.Username}");
                // Debug.Log($"[ApiClientTester] Role: {response.Player.Role}");

                Debug.Log($"[ApiClientTester] Login success: {response.Player.Username}");

                // Test GET - Profile (with token)
                PlayerData player = await ApiClient.Instance.Get<PlayerData>("/api/player/profile");

                Debug.Log($"[ApiClientTester] GET works - Profile: {player.Username}");

                // Test PUT - Update Profile
                var updateRequest = new
                {
                    username = "SharkKing99"
                };

                PlayerData updatedPlayer = await ApiClient.Instance.Put<PlayerData>("/api/player/profile/update", updateRequest);

                Debug.Log($"[ApiClientTester] PUT works - Username: {updatedPlayer.Username}");
            }
            catch (AuthException e)
            {
                Debug.LogError($"[ApiClientTester] AuthException caught! Code: {e.Code} - {e.Message}");
            }
            catch (ValidationException e)
            {
                Debug.LogError($"[ApiClientTester] ValidationException caught! Code: {e.Code} - {e.Message}");
            }
            catch (NetworkException e)
            {
                Debug.LogError($"[ApiClientTester] Network error: {e.Message}");
            }
        }
    }
}