using Cysharp.Threading.Tasks;
using ClubPoker.Core;
using UnityEngine;

namespace ClubPoker.Auth
{
    /// <summary>
    /// Google sign-in → Google ID token → { idToken }. Android only for now.
    ///
    /// Plugins/Android/GoogleSignInBridge.java shows the Credential Manager
    /// "Sign in with Google" sheet (the phone's Google accounts) and returns the
    /// chosen account's ID token. Its audience is AppConfig.googleWebClientId —
    /// the web client the server verifies against. Google Cloud also needs an
    /// Android OAuth client for com.game.clubpoker + the signing key's SHA-1,
    /// or the sheet fails. Libraries: Editor/GoogleSignInDependencies.xml.
    ///
    /// iOS: not built (would need the Google Sign-In iOS SDK).
    /// </summary>
    public class GoogleAuthProvider : ISocialAuthProvider
    {
        private const string BRIDGE = "com.game.clubpoker.auth.GoogleSignInBridge";

        public SocialProvider Provider => SocialProvider.Google;

        public async UniTask<SocialTokenResult> SignInAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string webClientId = ConfigManager.Instance.Config.googleWebClientId;
            if (string.IsNullOrEmpty(webClientId))
            {
                Debug.LogError("[GoogleAuthProvider] AppConfig.googleWebClientId is empty.");
                return SocialTokenResult.Fail("Google sign-in isn't set up.");
            }

            var tcs = new UniTaskCompletionSource<SocialTokenResult>();

            using (var bridge = new AndroidJavaClass(BRIDGE))
                bridge.CallStatic("signIn", CurrentActivity(), webClientId, new Callback(tcs));

            SocialTokenResult result = await tcs.Task;

            // The bridge answers on a background Java thread — get back onto
            // Unity's main thread before anything touches Unity APIs.
            await UniTask.SwitchToMainThread();
            return result;
#else
            await UniTask.CompletedTask;
            return SocialTokenResult.Fail("Google sign-in works on Android devices only.");
#endif
        }

        public void SignOut()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var bridge = new AndroidJavaClass(BRIDGE))
                bridge.CallStatic("signOut", CurrentActivity());
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        /// Receives GoogleSignInBridge.Callback calls (method names must match Java).
        private class Callback : AndroidJavaProxy
        {
            private readonly UniTaskCompletionSource<SocialTokenResult> _tcs;

            public Callback(UniTaskCompletionSource<SocialTokenResult> tcs)
                : base(BRIDGE + "$Callback")
            {
                _tcs = tcs;
            }

            public void onSuccess(string idToken) =>
                _tcs.TrySetResult(SocialTokenResult.Ok(idToken, "idToken"));

            public void onCancel() =>
                _tcs.TrySetResult(SocialTokenResult.Cancel());

            public void onError(string message)
            {
                Debug.LogWarning($"[GoogleAuthProvider] Sign-in error: {message}");
                _tcs.TrySetResult(SocialTokenResult.Fail("Google sign-in failed. Please try again."));
            }
        }
#endif
    }
}
