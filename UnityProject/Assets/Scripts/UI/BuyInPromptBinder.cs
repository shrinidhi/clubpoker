using UnityEngine;
using ClubPoker.Game;

namespace ClubPoker.UI
{
    /// <summary>
    /// Hands the table screen a buy-in popup without ClubPoker.Game having to
    /// reference this assembly — it can't, since this one already references it.
    /// Put this on the BuyInView object in the table scene.
    /// </summary>
    public class BuyInPromptBinder : MonoBehaviour
    {
        [SerializeField] private BuyInView buyInView;

        private void Awake()
        {
            if (buyInView == null)
                buyInView = GetComponent<BuyInView>();

            if (buyInView == null)
            {
                Debug.LogError("[BuyInPromptBinder] No BuyInView assigned — '+' will refuse to open.");
                return;
            }

            TakeSeatFlow.ShowBuyInPrompt = Open;
        }

        private void OnDestroy()
        {
            // Only clear our own binding: a scene reload can build the new binder
            // before the old one is torn down, and clearing blindly would drop it.
            if (TakeSeatFlow.ShowBuyInPrompt == (System.Action<BuyInPromptRequest>)Open)
                TakeSeatFlow.ShowBuyInPrompt = null;
        }

        private void Open(BuyInPromptRequest request)
        {
            buyInView.Init(
                request.TableId,
                request.MinBuyIn,
                request.MaxBuyIn,
                request.SmallBlind,
                request.BigBlind,
                request.OnConfirm);
        }
    }
}
