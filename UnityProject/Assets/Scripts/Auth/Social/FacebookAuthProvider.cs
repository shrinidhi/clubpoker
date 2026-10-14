using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Facebook.Unity;
using UnityEngine;

namespace ClubPoker.Auth
{
    /// <summary>
    /// Facebook sign-in (Facebook SDK for Unity) → token for the server.
    ///
    /// Android: always a classic access token → { accessToken }.
    /// iOS: if the player declines tracking (ATT), Facebook switches to Limited
    /// Login — no access token, only an authentication JWT → { authenticationToken }.
    /// The server verifies each kind differently (Graph /debug_token vs. JWT
    /// against Facebook's public keys).
    ///
    /// App ID + Client Token live in FacebookSDK/SDK/Resources/FacebookSettings.
    /// </summary>
    public class FacebookAuthProvider : ISocialAuthProvider
    {
        private static readonly List<string> Permissions =
            new List<string> { "public_profile", "email" };

        public SocialProvider Provider => SocialProvider.Facebook;

        public async UniTask<SocialTokenResult> SignInAsync()
        {
            if (!await EnsureInitializedAsync())
                return SocialTokenResult.Fail("Couldn't start Facebook. Please try again.");

            ILoginResult result = await LogInAsync();

            if (result == null)
                return SocialTokenResult.Fail("Facebook login failed. Please try again.");

            if (result.Cancelled)
                return SocialTokenResult.Cancel();

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogWarning($"[FacebookAuthProvider] Login error: {result.Error}");
                return SocialTokenResult.Fail("Facebook login failed. Please try again.");
            }

            string accessToken = AccessToken.CurrentAccessToken?.TokenString;
            if (!string.IsNullOrEmpty(accessToken))
                return SocialTokenResult.Ok(accessToken, "accessToken");

#if UNITY_IOS
            // Limited Login (tracking declined) — only an authentication JWT.
            string jwt = FB.Mobile.CurrentAuthenticationToken()?.TokenString;
            if (!string.IsNullOrEmpty(jwt))
                return SocialTokenResult.Ok(jwt, "authenticationToken");
#endif

            return SocialTokenResult.Fail("Facebook login failed. Please try again.");
        }

        public void SignOut()
        {
            if (FB.IsInitialized)
                FB.LogOut();
        }

        // FB.Init once per app run; later calls reuse it.
        private static UniTask<bool> EnsureInitializedAsync()
        {
            if (FB.IsInitialized)
                return UniTask.FromResult(true);

            var tcs = new UniTaskCompletionSource<bool>();
            FB.Init(() =>
            {
                if (FB.IsInitialized) FB.ActivateApp();
                tcs.TrySetResult(FB.IsInitialized);
            });
            return tcs.Task;
        }

        private static UniTask<ILoginResult> LogInAsync()
        {
            var tcs = new UniTaskCompletionSource<ILoginResult>();

#if UNITY_IOS
            // ENABLED asks for the classic token; iOS falls back to Limited
            // Login on its own when tracking isn't allowed.
            FB.Mobile.LoginWithTrackingPreference(
                LoginTracking.ENABLED, Permissions, null, r => tcs.TrySetResult(r));
#else
            FB.LogInWithReadPermissions(Permissions, r => tcs.TrySetResult(r));
#endif
            return tcs.Task;
        }
    }
}
