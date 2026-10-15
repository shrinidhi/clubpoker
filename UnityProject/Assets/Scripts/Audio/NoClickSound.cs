using UnityEngine;

namespace ClubPoker.Audio
{
    /// <summary>
    /// Opt-out marker for UIClickSounds: put on a Button (or a parent) that already
    /// plays its own sound — e.g. table action buttons, whose sound comes from
    /// game:player_acted.
    /// </summary>
    public class NoClickSound : MonoBehaviour { }
}
