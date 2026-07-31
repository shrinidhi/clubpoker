using UnityEngine;
using DG.Tweening;

namespace ClubPoker.UI
{
    /// <summary>
    /// Spins a UI graphic for as long as it is active.
    ///
    /// Drop it on any loading indicator and forget about it — there is nothing to
    /// call. Show/hide the object (or its parent overlay) and the tween starts and
    /// stops with it. Same rotation the lobby loader uses, so every spinner in the
    /// app reads identically.
    /// </summary>
    public class UISpinner : MonoBehaviour
    {
        [Tooltip("Seconds per full revolution.")]
        [SerializeField] private float secondsPerRevolution = 1f;

        [Tooltip("Clockwise when true — matches the lobby loader.")]
        [SerializeField] private bool clockwise = true;

        [Tooltip("Leave empty to spin this object's own transform.")]
        [SerializeField] private Transform target;

        private Transform _target;

        private void Awake()
        {
            _target = target != null ? target : transform;
        }

        private void OnEnable()
        {
            // Kill before starting: re-enabling while a tween is somehow still live
            // would stack loops and the spinner would speed up each time.
            _target.DOKill();
            _target.rotation = Quaternion.identity;

            float degrees = clockwise ? -360f : 360f;

            _target
                .DORotate(new Vector3(0f, 0f, degrees),
                          Mathf.Max(0.01f, secondsPerRevolution),
                          RotateMode.FastBeyond360)
                .SetLoops(-1)
                .SetEase(Ease.Linear)
                // Keeps spinning if the game is paused — which is often exactly when
                // a loader is on screen.
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            // Tweens outlive the object otherwise, and DOTween logs on a destroyed
            // target when the scene unloads.
            _target.DOKill();
            _target.rotation = Quaternion.identity;
        }
    }
}
