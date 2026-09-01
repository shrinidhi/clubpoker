using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Theme
{
    /// <summary>
    /// One thumbnail in the tab grid. Two independent visual states:
    ///   Selected — pending pick, gold outline
    ///   Applied  — what the player is actually playing with, green tick
    /// </summary>
    public class ThemeOptionCell : MonoBehaviour
    {
        [Header("Targets")]
        public Image Thumbnail;
        public Button Button;

        [Tooltip("Gold outline shown on the pending selection.")]
        public GameObject SelectedOutline;

        [Tooltip("Green tick shown on the currently applied option.")]
        public GameObject AppliedTick;

        [Tooltip("Shown when the option is not owned yet.")]
        public GameObject LockedOverlay;

        private Action _onClick;

        public void Bind(Sprite preview, bool locked, Action onClick)
        {
            if (Thumbnail != null)
                Thumbnail.sprite = preview;

            _onClick = onClick;

            if (LockedOverlay != null)
                LockedOverlay.SetActive(locked);

            if (Button != null)
            {
                Button.onClick.RemoveAllListeners();
                Button.interactable = !locked;
                Button.onClick.AddListener(() => _onClick?.Invoke());
            }
        }

        public void SetState(bool selected, bool applied)
        {
            if (SelectedOutline != null)
                SelectedOutline.SetActive(selected);

            if (AppliedTick != null)
                AppliedTick.SetActive(applied);
        }
    }
}
