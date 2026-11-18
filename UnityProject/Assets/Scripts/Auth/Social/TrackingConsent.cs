using Cysharp.Threading.Tasks;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ClubPoker.Auth
{
    /// <summary>Same values as iOS ATTrackingManagerAuthorizationStatus.</summary>
    public enum TrackingStatus
    {
        NotDetermined = 0,
        Restricted = 1,
        Denied = 2,
        Authorized = 3
    }

    /// <summary>
    /// iOS App Tracking Transparency prompt ("Allow Club Poker to track your
    /// activity…"). Facebook only hands out a classic access token when the
    /// player allows tracking; otherwise it falls back to Limited Login.
    ///
    /// iOS shows the system prompt once per install. Later calls return the
    /// saved answer immediately (the player can change it in Settings).
    /// Other platforms and the editor always report Authorized.
    ///
    /// Native side: Plugins/iOS/TrackingConsentBridge.mm.
    /// Info.plist text: Editor/TrackingConsentPostProcess.cs.
    /// </summary>
    public static class TrackingConsent
    {
#if UNITY_IOS && !UNITY_EDITOR
        private delegate void StatusCallback(int status);

        [DllImport("__Internal")] private static extern int _TrackingConsentGetStatus();
        [DllImport("__Internal")] private static extern void _TrackingConsentRequest(StatusCallback callback);

        // Held in a static so the native side never calls a collected delegate.
        private static readonly StatusCallback Callback = OnStatus;
        private static UniTaskCompletionSource<TrackingStatus> _pending;

        [AOT.MonoPInvokeCallback(typeof(StatusCallback))]
        private static void OnStatus(int status)
        {
            UniTaskCompletionSource<TrackingStatus> pending = _pending;
            _pending = null;
            pending?.TrySetResult((TrackingStatus)status);
        }
#endif

        public static TrackingStatus Status
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return (TrackingStatus)_TrackingConsentGetStatus();
#else
                return TrackingStatus.Authorized;
#endif
            }
        }

        /// <summary>
        /// Shows the system prompt if the player hasn't answered yet, else
        /// returns the saved answer. Call while the app is in the foreground
        /// (e.g. from a button tap) — iOS ignores the request otherwise.
        /// </summary>
        public static UniTask<TrackingStatus> RequestAsync()
        {
#if UNITY_IOS && !UNITY_EDITOR
            TrackingStatus current = Status;
            if (current != TrackingStatus.NotDetermined)
                return UniTask.FromResult(current);

            if (_pending != null)
                return _pending.Task;

            _pending = new UniTaskCompletionSource<TrackingStatus>();
            UniTask<TrackingStatus> task = _pending.Task;
            _TrackingConsentRequest(Callback);
            return task;
#else
            return UniTask.FromResult(TrackingStatus.Authorized);
#endif
        }
    }
}
