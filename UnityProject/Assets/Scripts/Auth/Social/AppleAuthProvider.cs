using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
#endif

namespace ClubPoker.Auth
{
    /// <summary>
    /// Sign in with Apple → Apple identity token (a JWT the server verifies with
    /// Apple's public keys, aud = our bundle ID). iOS only.
    ///
    /// Native side: Plugins/iOS/AppleSignInBridge.mm (AuthenticationServices).
    /// Capability: Editor/AppleSignInPostProcess.cs. The App ID game.club.poker
    /// also needs Sign in with Apple enabled in the Apple Developer portal.
    ///
    /// Sent to /api/auth/social/apple:
    ///   identityToken      — the JWT
    ///   nonce              — raw nonce; the token's nonce claim is its SHA-256
    ///                        (hex), so the server can reject a replayed token
    ///   authorizationCode  — for a server-side token exchange, if wanted
    ///   user               — Apple's stable user id (also the token's sub)
    ///   email, givenName, familyName — FIRST sign-in only; Apple never sends
    ///                        them again, so the server should save them then
    /// </summary>
    public class AppleAuthProvider : ISocialAuthProvider
    {
        public SocialProvider Provider => SocialProvider.Apple;

        public async UniTask<SocialTokenResult> SignInAsync()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!_AppleSignInIsAvailable())
                return SocialTokenResult.Fail("Sign in with Apple needs iOS 13 or later.");

            // A second tap while the sheet is up joins the first sign-in.
            if (_pending != null)
                return await _pending.Task;

            string rawNonce = NewNonce();
            _pendingNonce = rawNonce;
            _pending = new UniTaskCompletionSource<SocialTokenResult>();
            UniTask<SocialTokenResult> task = _pending.Task;

            _AppleSignInSignIn(Sha256Hex(rawNonce), NativeCallback);

            SocialTokenResult result = await task;
            await UniTask.SwitchToMainThread();
            return result;
#else
            await UniTask.CompletedTask;
            return SocialTokenResult.Fail("Sign in with Apple works on iOS devices only.");
#endif
        }

        public void SignOut()
        {
            // Apple has no sign-out API — the app just forgets the user.
        }

#if UNITY_IOS && !UNITY_EDITOR
        // Codes from AppleSignInBridge.mm.
        private const int CodeSuccess = 0;
        private const int CodeCancelled = 1;

        private delegate void SignInCallback(int code, string payload);

        [DllImport("__Internal")]
        [return: MarshalAs(UnmanagedType.I1)]   // C bool is one byte
        private static extern bool _AppleSignInIsAvailable();

        [DllImport("__Internal")]
        private static extern void _AppleSignInSignIn(string hashedNonce, SignInCallback callback);

        // Held in a static so the native side never calls a collected delegate.
        private static readonly SignInCallback NativeCallback = OnNativeResult;
        private static UniTaskCompletionSource<SocialTokenResult> _pending;
        private static string _pendingNonce;

        [AOT.MonoPInvokeCallback(typeof(SignInCallback))]
        private static void OnNativeResult(int code, string payload)
        {
            UniTaskCompletionSource<SocialTokenResult> pending = _pending;
            string nonce = _pendingNonce;
            _pending = null;
            _pendingNonce = null;

            if (pending == null)
                return;

            if (code == CodeCancelled)
            {
                pending.TrySetResult(SocialTokenResult.Cancel());
                return;
            }

            if (code != CodeSuccess)
            {
                Debug.LogWarning($"[AppleAuthProvider] Sign-in error: {payload}");
                pending.TrySetResult(SocialTokenResult.Fail("Sign in with Apple failed. Please try again."));
                return;
            }

            try
            {
                JObject credential = JObject.Parse(payload);
                string identityToken = credential["identityToken"]?.ToString();

                var extras = new Dictionary<string, object> { ["nonce"] = nonce };
                foreach (string key in new[] { "authorizationCode", "user", "email", "givenName", "familyName" })
                {
                    string value = credential[key]?.ToString();
                    if (!string.IsNullOrEmpty(value))
                        extras[key] = value;
                }

                pending.TrySetResult(SocialTokenResult.Ok(identityToken, "identityToken", extras));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AppleAuthProvider] Bad credential payload: {e.Message}");
                pending.TrySetResult(SocialTokenResult.Fail("Sign in with Apple failed. Please try again."));
            }
        }

        private static string NewNonce()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
#endif
    }
}
