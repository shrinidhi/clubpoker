using UnityEngine;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class ToastMessage : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private RectTransform toastRect;

        private const float ANIMATION_DURATION = 0.3f;
        private const float HIDDEN_Y = -120f;
        private const float VISIBLE_Y = 100f;
        private const float DISPLAY_DURATION = 4f;

        private CancellationTokenSource _cts;

        private void Awake()
        {
            ToastEvents.OnShowToast += Show;
        }

        private void OnDestroy()
        {
            ToastEvents.OnShowToast -= Show;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void Start()
        {
            toastRect.anchoredPosition = new Vector2(0f, HIDDEN_Y);
            toastRect.gameObject.SetActive(false);
        }

        public void Show(string message)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            ShowAsync(message, _cts.Token).Forget();
        }

        private async UniTaskVoid ShowAsync(string message, CancellationToken ct)
        {
            // slide down current if visible
            if (toastRect.gameObject.activeSelf)
            {
                toastRect.DOKill();
                await toastRect.DOAnchorPosY(HIDDEN_Y, ANIMATION_DURATION)
                    .SetEase(Ease.InBack)
                    .AsyncWaitForCompletion()
                    .AsUniTask().AttachExternalCancellation(ct);
            }

            if (ct.IsCancellationRequested) return;

            if (messageText != null)
                messageText.text = message;

            toastRect.gameObject.SetActive(true);
            toastRect.DOKill();
            toastRect.DOAnchorPosY(VISIBLE_Y, ANIMATION_DURATION).SetEase(Ease.OutBack);

            await UniTask.Delay((int)(DISPLAY_DURATION * 1000), cancellationToken: ct);

            toastRect.DOAnchorPosY(HIDDEN_Y, ANIMATION_DURATION)
                .SetEase(Ease.InBack)
                .OnComplete(() => toastRect.gameObject.SetActive(false));
        }
    }
}
