using Cysharp.Threading.Tasks;
using ClubPoker.Core;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ClubPoker.Auth
{
    /// <summary>
    /// Google sign-in → Google ID token → { idToken }. Android and iOS.
    ///
    /// The ID token's audience is AppConfig.googleWebClientId — the web client
    /// the server verifies against.
    ///
    /// Android: Plugins/Android/GoogleSignInBridge.java shows the Credential
    /// Manager "Sign in with Google" sheet (the phone's Google accounts).
    /// Google Cloud also needs an Android OAuth client for com.game.clubpoker +
    /// the signing key's SHA-1, or the sheet fails.
    ///
    /// iOS: Plugins/iOS/GoogleSignInBridge.mm uses the Google Sign-In iOS SDK.
    /// Needs AppConfig.googleIosClientId (an iOS OAuth client for
    /// game.club.poker); Editor/GoogleSignInPostProcess.cs adds its reversed
    /// form to Info.plist as a URL scheme.
    ///
    /// Libraries for both: Editor/GoogleSignInDependencies.xml.
    /// </summary>
    public class GoogleAuthProvider : ISocialAuthProvider
    {
        public SocialProvider Provider => SocialProvider.Google;

        public async UniTask<SocialTokenResult> SignInAsync()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            string webClientId = ConfigManager.Instance.Config.googleWebClientId;
            if (string.IsNullOrEmpty(webClientId))
            {
                Debug.LogError("[GoogleAuthProvider] AppConfig.googleWebClientId is empty.");
                return SocialTokenResult.Fail("Google sign-in isn't set up.");
            }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
            var tcs = new UniTaskCompletionSource<SocialTokenResult>();

            using (var bridge = new AndroidJavaClass(BRIDGE))
                bridge.CallStatic("signIn", CurrentActivity(), webClientId, new Callback(tcs));

            SocialTokenResult result = await tcs.Task;

            // The bridge answers on a background Java thread — get back onto
            // Unity's main thread before anything touches Unity APIs.
            await UniTask.SwitchToMainThread();
            return result;
#elif UNITY_IOS && !UNITY_EDITOR
            string iosClientId = ConfigManager.Instance.Config.googleIosClientId;
            if (string.IsNullOrEmpty(iosClientId))
            {
                Debug.LogError("[GoogleAuthProvider] AppConfig.googleIosClientId is empty.");
                return SocialTokenResult.Fail("Google sign-in isn't set up.");
            }

            // A second tap while the sheet is up joins the first sign-in.
            if (_pending != null)
                return await _pending.Task;

            _pending = new UniTaskCompletionSource<SocialTokenResult>();
            UniTask<SocialTokenResult> task = _pending.Task;
            _GoogleSignInSignIn(iosClientId, webClientId, NativeCallback);

            SocialTokenResult result = await task;
            await UniTask.SwitchToMainThread();
            return result;
#else
            await UniTask.CompletedTask;
            return SocialTokenResult.Fail("Google sign-in works on Android and iOS devices only.");
#endif
        }

        public void SignOut()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var bridge = new AndroidJavaClass(BRIDGE))
                bridge.CallStatic("signOut", CurrentActivity());
#elif UNITY_IOS && !UNITY_EDITOR
            _GoogleSignInSignOut();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string BRIDGE = "com.game.clubpoker.auth.GoogleSignInBridge";

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

#if UNITY_IOS && !UNITY_EDITOR
        // Codes from GoogleSignInBridge.mm.
        private const int CodeSuccess = 0;
        private const int CodeCancelled = 1;

        private delegate void SignInCallback(int code, string payload);

        [DllImport("__Internal")]
        private static extern void _GoogleSignInSignIn(string iosClientId, string webClientId, SignInCallback callback);

        [DllImport("__Internal")]
        private static extern void _GoogleSignInSignOut();

        // Held in a static so the native side never calls a collected delegate.
        private static readonly SignInCallback NativeCallback = OnNativeResult;
        private static UniTaskCompletionSource<SocialTokenResult> _pending;

        [AOT.MonoPInvokeCallback(typeof(SignInCallback))]
        private static void OnNativeResult(int code, string payload)
        {
            UniTaskCompletionSource<SocialTokenResult> pending = _pending;
            _pending = null;
            if (pending == null)
                return;

            if (code == CodeSuccess)
            {
                pending.TrySetResult(SocialTokenResult.Ok(payload, "idToken"));
            }
            else if (code == CodeCancelled)
            {
                pending.TrySetResult(SocialTokenResult.Cancel());
            }
            else
            {
                Debug.LogWarning($"[GoogleAuthProvider] Sign-in error: {payload}");
                pending.TrySetResult(SocialTokenResult.Fail("Google sign-in failed. Please try again."));
            }
        }
#endif
    }
}
