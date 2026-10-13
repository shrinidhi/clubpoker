using Cysharp.Threading.Tasks;

namespace ClubPoker.Auth
{
    /// <summary>
    /// Sign in with Apple → Apple identity token (a JWT the server verifies
    /// with Apple's public keys, aud = our bundle ID). iOS only.
    ///
    /// NOT WIRED YET — returns a failure until the plugin is in:
    ///   - "Sign in with Apple Unity Plugin" (lupidan) → AppleAuthManager
    ///     .LoginWithAppleId(LoginOptions.IncludeEmail | IncludeFullName)
    ///     → credential.IdentityToken (UTF-8 bytes → string).
    ///   - Apple Developer: enable the Sign in with Apple capability on the
    ///     App ID game.club.poker.
    ///   - Email + name come on the FIRST sign-in only — send them along so the
    ///     server can save them.
    /// Map AuthorizationErrorCode.Canceled to SocialTokenResult.Cancel().
    /// </summary>
    public class AppleAuthProvider : ISocialAuthProvider
    {
        public SocialProvider Provider => SocialProvider.Apple;

        public UniTask<SocialTokenResult> SignInAsync()
        {
            return UniTask.FromResult(
                SocialTokenResult.Fail("Sign in with Apple isn't available yet."));
        }

        public void SignOut()
        {
            // Apple has no sign-out API — the app just forgets the user.
        }
    }
}
