using UnityEngine;

namespace ClubPoker.Game
{
    public class BestHandCalculator : MonoBehaviour
    {
        public static BestHandCalculator Instance;

        private void Awake()
        {
            Instance = this;
        }

        public void Recalculate()
        {
            if (GameStateManager.Instance == null)
                return;

            var yourCards = GameStateManager.Instance.YourCards;
            var board = GameStateManager.Instance.CommunityCards;

            if (yourCards == null || board == null)
                return;

            Debug.Log(
                $"[BestHandCalculator] Recalculate → Hole:{yourCards.Count} Board:{board.Count}"
            );

            // TODO:
            // Real poker hand ranking logic here

            UpdateBestHandUI("Best Hand Updated");
        }

        private void UpdateBestHandUI(string text)
        {
            Debug.Log($"[BestHandUI] {text}");
        }
    }
}