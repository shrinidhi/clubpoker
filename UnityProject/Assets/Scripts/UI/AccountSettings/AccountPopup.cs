using System;
using ClubPoker.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    /// <summary>
    /// One of the Account setting popups (bind email, change password,
    /// self-exclusion). Opened by <see cref="AccountSettingsPanel"/>, which brings
    /// its option list back when the popup closes.
    /// </summary>
    public abstract class AccountPopup : MonoBehaviour
    {
        // How long the success toast is read before the app leaves for Login.
        private const int EXIT_DELAY_MS = 1200;

        [SerializeField] private Button closeButton;

        public event Action Closed;

        /// <summary>True while a request is in flight — taps are ignored.</summary>
        protected bool Busy { get; set; }

        protected virtual void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            // Keeps the field being typed in above the on-screen keyboard.
            if (GetComponent<KeyboardAvoider>() == null)
                gameObject.AddComponent<KeyboardAvoider>();
        }

        public void Show()
        {
            gameObject.SetActive(true);   // first time: runs Awake before OnShown
            Busy = false;
            OnShown();
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;

            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>
        /// For a change that ends the session: say it worked, close the popup and
        /// the whole Account setting panel, then leave. <paramref name="exit"/> runs
        /// even if the scene was unloaded meanwhile.
        /// </summary>
        protected async UniTaskVoid SucceedAndExitAsync(string toast, Action exit)
        {
            ToastEvents.Show(toast);

            var panel = GetComponentInParent<AccountSettingsPanel>(true);
            gameObject.SetActive(false);
            if (panel != null) panel.Hide();

            await UniTask.Delay(EXIT_DELAY_MS, ignoreTimeScale: true);
            exit();
        }

        /// <summary>Reset the popup to its starting state.</summary>
        protected abstract void OnShown();

        protected static void SetError(TMPro.TextMeshProUGUI label, string message)
        {
            if (label == null) return;

            bool show = !string.IsNullOrEmpty(message);
            label.gameObject.SetActive(show);
            if (show) label.text = message;
        }
    }
}
