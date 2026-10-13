using Cysharp.Threading.Tasks;

namespace ClubPoker.Auth
{
    public enum SocialProvider
    {
        Google,
        Facebook,
        Apple   // iOS only — required by App Store when Google/Facebook login is offered
    }

    /// <summary>
    /// Gets a sign-in token from one provider's SDK (Google / Facebook). Knows
    /// nothing about our server — AuthManager.SocialLoginAsync sends the token
    /// on and starts the session.
    /// </summary>
    public interface ISocialAuthProvider
    {
        SocialProvider Provider { get; }

        /// Shows the provider's sign-in UI and returns its token.
        UniTask<SocialTokenResult> SignInAsync();

        /// Forgets the provider's signed-in account, so the next sign-in
        /// shows the account picker again. Called on logout.
        void SignOut();
    }

    public class SocialTokenResult
    {
        public bool   Success   { get; private set; }

        /// The player closed the provider's sign-in UI — not an error.
        public bool   Cancelled { get; private set; }

        /// Google: ID token. Facebook: access token. Apple: identity token.
        public string Token     { get; private set; }

        public string Error     { get; private set; }

        public static SocialTokenResult Ok(string token) =>
            new SocialTokenResult { Success = true, Token = token };

        public static SocialTokenResult Cancel() =>
            new SocialTokenResult { Cancelled = true };

        public static SocialTokenResult Fail(string error) =>
            new SocialTokenResult { Error = error };
    }
}
