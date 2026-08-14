using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    /// <summary>
    /// A settings row whose body is revealed by its own ON/OFF switch.
    ///
    /// The switch is a Button with two sprites rather than a Unity Toggle — the
    /// design uses custom ON/OFF artwork, and a Toggle's checkmark model doesn't fit
    /// it. State is held here and the sprite swapped to match.
    ///
    /// OFF collapses to just the header row; ON reveals the description and slider.
    /// The switch is the only control — no separate expand button.
    ///
    /// Deliberately does NOT use Unity's layout groups: the popup is hand-placed and
    /// a layout group would seize control of every child's position. The owning panel
    /// re-stacks using Height below.
    ///
    /// Lives in ClubPoker.Game rather than ClubPoker.UI because UI already depends on
    /// Game — putting it in UI would make that circular.
    /// </summary>
    public class CollapsibleSection : MonoBehaviour
    {
        [Header("Switch")]
        [Tooltip("Button that flips the section on and off.")]
        public Button SwitchButton;

        [Tooltip("Image on the button whose sprite is swapped. Defaults to the button's own Image.")]
        public Image SwitchImage;

        public Sprite OnSprite;
        public Sprite OffSprite;

        [Header("Layout")]
        [Tooltip("Header row — always visible.")]
        public RectTransform HeaderRect;

        [Tooltip("Description + slider. Hidden when OFF.")]
        public RectTransform BodyRect;

        /// <summary>Raised after the body is shown or hidden, so the panel can re-stack.</summary>
        public System.Action<bool> OnChanged;

        public bool IsOn { get; private set; }

        /// <summary>
        /// How tall this section currently is: header always, body only when open.
        /// The panel stacks sections using this.
        /// </summary>
        public float Height
        {
            get
            {
                float h = HeaderRect != null ? HeaderRect.rect.height : 0f;

                if (IsOn && BodyRect != null)
                    h += BodyRect.rect.height;

                return h;
            }
        }

        private void Awake()
        {
            if (SwitchImage == null && SwitchButton != null)
                SwitchImage = SwitchButton.GetComponent<Image>();

            if (SwitchButton != null)
                SwitchButton.onClick.AddListener(Flip);
        }

        /// <summary>
        /// Set state without raising OnChanged — for seeding from saved settings.
        /// </summary>
        public void SetOn(bool on)
        {
            IsOn = on;
            ApplyVisuals();
        }

        private void Flip()
        {
            IsOn = !IsOn;
            ApplyVisuals();
            OnChanged?.Invoke(IsOn);
        }

        private void ApplyVisuals()
        {
            if (SwitchImage != null)
            {
                Sprite sprite = IsOn ? OnSprite : OffSprite;

                if (sprite != null)
                    SwitchImage.sprite = sprite;
            }

            if (BodyRect != null)
                BodyRect.gameObject.SetActive(IsOn);
        }
    }
}
