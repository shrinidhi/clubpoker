using System;

namespace ClubPoker.Core
{
    public static class ToastEvents
    {
        public static event Action<string> OnShowToast;

        /// True while the shared toast view (ToastMessage) exists. OnShowToast has
        /// other listeners (audio), so a non-null event doesn't mean anything draws.
        public static bool HasView { get; set; }

        public static void Show(string message) => OnShowToast?.Invoke(message);
    }
}
