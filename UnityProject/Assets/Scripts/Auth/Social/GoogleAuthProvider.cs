using Cysharp.Threading.Tasks;

namespace ClubPoker.Auth
{
    /// <summary>
    /// Google sign-in → Google ID token (a JWT the server verifies against our
    /// Google Cloud web client ID).
    ///
    /// NOT WIRED YET — returns a failure until the Android side is in place:
    ///   - Android Credential Manager (androidx.credentials + googleid) through a
    ///     small native plugin, requesting an ID token for the WEB client ID
    ///     backend verifies against (not the Android client ID).
    ///   - Google Cloud: Android OAuth client for com.game.clubpoker with the
    ///     SHA-1 of Poker.keystore AND the Play App Signing key.
    /// Map the plugin's user-cancelled result to SocialTokenResult.Cancel().
    /// </summary>
    public class GoogleAuthProvider : ISocialAuthProvider
    {
        public SocialProvider Provider => SocialProvider.Google;

        public UniTask<SocialTokenResult> SignInAsync()
        {
            return UniTask.FromResult(
                SocialTokenResult.Fail("Google sign-in isn't available yet."));
        }

        public void SignOut()
        {
            // Credential Manager: clearCredentialState().
        }
    }
}
