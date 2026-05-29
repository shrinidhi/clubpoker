using System;

namespace ClubPoker.Core
{
    public static class ToastEvents
    {
        public static event Action<string> OnShowToast;

        public static void Show(string message) => OnShowToast?.Invoke(message);
    }
}
