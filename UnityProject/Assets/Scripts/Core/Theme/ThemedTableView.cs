using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Theme
{
    /// <summary>
    /// Drop on the game table root. Repaints the assigned Images whenever the
    /// applied theme changes. Empty sprite slots on the skin are skipped, so the
    /// sprite authored in the scene survives.
    ///
    /// Also drives the popup's live preview: set <see cref="PreviewSkin"/> to show
    /// a pending skin without touching ThemeManager state.
    /// </summary>
    public class ThemedTableView : MonoBehaviour
    {
        [Header("Targets")]
        public Image RoomBackground;
        public Image TableImage;
        public Image DealerButton;
        public List<Image> SeatFrames = new List<Image>();

        [Header("Behaviour")]
        [Tooltip("Off for preview instances inside the settings popup.")]
        public bool FollowAppliedTheme = true;

        [Tooltip("Paint TableImage with the skin's LargePreviewSprite (room + table " +
                 "in one shot) instead of the table sprite. For the popup's preview " +
                 "pane, where the real scene layout isn't reproduced.")]
        public bool UseLargePreview;

        private TableSkinSO _previewSkin;

        /// <summary>Non-null pins this view to a skin and ignores applied-theme changes.</summary>
        public TableSkinSO PreviewSkin
        {
            get => _previewSkin;
            set
            {
                _previewSkin = value;
                Repaint();
            }
        }

        private void Awake()
        {
            // Dropped straight on the table Image instead of a parent root — take
            // that Image as the table target so the component is useful unwired.
            if (TableImage == null)
                TableImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (FollowAppliedTheme)
                ThemeManager.Instance.OnThemeApplied += Repaint;

            Repaint();
        }

        private void OnDisable()
        {
            if (FollowAppliedTheme)
                ThemeManager.Instance.OnThemeApplied -= Repaint;
        }

        public void Repaint()
        {
            TableSkinSO skin = _previewSkin
                               ?? (FollowAppliedTheme ? ThemeManager.Instance.CurrentTable : null);

            if (skin == null)
                return;

            SetSprite(RoomBackground, skin.RoomBackgroundSprite);

            SetSprite(TableImage, UseLargePreview && skin.LargePreviewSprite != null
                ? skin.LargePreviewSprite
                : skin.TableSprite);
            SetSprite(DealerButton, skin.DealerButtonSprite);

            foreach (Image frame in SeatFrames)
                SetSprite(frame, skin.SeatFrameSprite);

            if (skin.OverrideFeltTint && TableImage != null)
                TableImage.color = skin.FeltTint;
        }

        private static void SetSprite(Image target, Sprite sprite)
        {
            if (target == null || sprite == null)
                return;

            target.sprite = sprite;
        }
    }
}
