using UnityEngine;

namespace ClubPoker.UI
{
    /// <summary>
    /// Per-scene position for the shared toast. The toast lives in Bootstrap and
    /// survives scene loads, so a scene that needs it somewhere other than the
    /// default (e.g. the game table, where the default sits on the action buttons)
    /// drops one of these anywhere in its hierarchy. Scenes without one use the
    /// toast's default.
    /// </summary>
    public class ToastPlacement : MonoBehaviour
    {
        [Tooltip("Height of the toast above the bottom of the screen while visible, " +
                 "in canvas units.")]
        public float visibleY = 450f;
    }
}
