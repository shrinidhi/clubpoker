using Cysharp.Threading.Tasks;

namespace ClubPoker.Auth
{
    /// <summary>
    /// Facebook sign-in → Facebook access token (the server verifies it with
    /// Facebook's Graph API).
    ///
    /// NOT WIRED YET — returns a failure until the Facebook SDK for Unity is in:
    ///   - FB.Init (once) → FB.LogInWithReadPermissions({ "public_profile", "email" })
    ///     → AccessToken.CurrentAccessToken.TokenString.
    ///   - Use the SAME Meta app as the web client (Facebook user IDs are per app),
    ///     with an Android platform: package com.game.clubpoker + key hashes for
    ///     both keystores. App ID + Client Token go in the SDK's Facebook Settings.
    /// Map result.Cancelled to SocialTokenResult.Cancel().
    /// </summary>
    public class FacebookAuthProvider : ISocialAuthProvider
    {
        public SocialProvider Provider => SocialProvider.Facebook;

        public UniTask<SocialTokenResult> SignInAsync()
        {
            return UniTask.FromResult(
                SocialTokenResult.Fail("Facebook sign-in isn't available yet."));
        }

        public void SignOut()
        {
            // FB.LogOut().
        }
    }
}
