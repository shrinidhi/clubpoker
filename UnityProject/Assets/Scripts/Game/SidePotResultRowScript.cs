using UnityEngine;
using TMPro;

namespace ClubPoker.Game
{
    /// <summary>
    /// One winner line in the side pot results popup (game:side_pot_results):
    /// which pot, who took it, how much. One row per winner — a split pot spawns
    /// two rows, and the second leaves the pot label blank so the pot name isn't
    /// repeated down the list.
    /// </summary>
    public class SidePotResultRowScript : MonoBehaviour
    {
        public TextMeshProUGUI PotLabel_Text;    // "Side Pot 1"
        public TextMeshProUGUI Username_Text;    // "Arun09"
        public TextMeshProUGUI Amount_Text;      // "+550"

        /// <param name="showPotLabel">
        /// False for the second and later winners of the same pot — the label is
        /// blanked rather than hidden so the columns stay aligned under a layout
        /// group.
        /// </param>
        public void Setup(int potIndex, string username, int amount, bool showPotLabel = true)
        {
            if (PotLabel_Text != null)
                PotLabel_Text.text = showPotLabel ? $"Side Pot {potIndex}" : "";

            if (Username_Text != null)
                Username_Text.text = username;

            if (Amount_Text != null)
                Amount_Text.text = "+" + amount.ToString("N0");
        }
    }
}
