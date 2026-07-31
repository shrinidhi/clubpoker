using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

namespace ClubPoker.Game
{
    public class PlayerActionUI : MonoBehaviour
    {
        public static PlayerActionUI Instance;

        [Header("Pot UI")]
        public Text PotText;

        [Header("Player Action Label")]
        public Text ActionLabelText;

        [Header("Player Chips Text")]
        public Text PlayerChipsText;

        private Coroutine actionLabelCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void HandlePlayerAction(PlayerActedPayload payload)
        {
            if (payload == null)
                return;

            ClearPreviousActionLabel();

            // Pot and chips are meaningful even when the server sends no action
            // string — apply them first so they survive a missing action.
            UpdatePot(payload.Pot);

            // Chips come from the state snapshot, never from the event —
            // player_acted carries no chip count, and the snapshot is the single
            // authority for balances anyway.
            int? chips = GameStateManager.Instance != null
                ? GameStateManager.Instance.GetPlayerById(payload.PlayerId)?.Chips
                : null;

            if (chips.HasValue)
                UpdatePlayerChips(chips.Value);

            // Action can be null: the server auto-acts for disconnected players and
            // doesn't always name the action (it reports lastAction:null in
            // state_update too). Dereferencing it here used to NRE and abort the
            // whole player_acted handler.
            if (string.IsNullOrEmpty(payload.Action))
            {
                Debug.LogWarning($"[PlayerActionUI] player_acted with no action for {payload.PlayerId}");
                return;
            }

            PlayActionAnimation(payload);
            ShowActionLabel(
                payload.Action.ToUpper(),
                payload.Amount
            );
        }

        private void PlayActionAnimation(PlayerActedPayload payload)
        {
            switch (payload.Action.ToLower())
            {
                case "fold":
                    PlayFoldAnimation(payload.PlayerId);
                    break;

                case "call":
                case "raise":
                case "bet":
                case "all_in":
                    PlayChipMovementAnimation(
                        payload.PlayerId,
                        payload.Amount
                    );
                    break;

                case "check":
                    PlayCheckGesture(payload.PlayerId);
                    break;

                default:
                    Debug.Log(
                        $"[PlayerActionUI] Unknown action: {payload.Action}"
                    );
                    break;
            }
        }

        private void PlayFoldAnimation(string playerId)
        {
            Debug.Log($"[Animation] Fold animation -> {playerId}");

            // Example:
            // flip cards / dim cards / fold animation
        }

        private void PlayChipMovementAnimation(
            string playerId,
            int amount
        )
        {
            Debug.Log(
                $"[Animation] Chip movement -> {playerId} | Amount: {amount}"
            );
            PokerTableUI.Instance.PlayCoinToPot(
                           playerId,
                           amount
                       );
            // Example:
            // animate chips to pot
        }

        private void PlayCheckGesture(string playerId)
        {
            Debug.Log($"[Animation] Check gesture -> {playerId}");

            // Example:
            // hand tap / check icon
        }

        private void UpdatePot(int pot)
        {
            if (PotText != null)
            {
                PotText.text = "Pot : " + pot.ToString();
            }

            Debug.Log($"[UI] Pot updated -> {pot}");
        }

        private void UpdatePlayerChips(int chips)
        {
            if (PlayerChipsText != null)
            {
                PlayerChipsText.text = "Chips : " + chips.ToString();
            }

            Debug.Log($"[UI] Chips updated -> {chips}");
        }

        private void ShowActionLabel(string action, int amount)
        {
            if (ActionLabelText == null)
                return;

            string label = action;

            if (amount > 0)
            {
                label += " " + amount;
            }

            ActionLabelText.text = label;
            ActionLabelText.gameObject.SetActive(true);

            if (actionLabelCoroutine != null)
            {
                StopCoroutine(actionLabelCoroutine);
            }

            actionLabelCoroutine =
                StartCoroutine(HideActionLabelAfterDelay());
        }

        private IEnumerator HideActionLabelAfterDelay()
        {
            yield return new WaitForSeconds(2f);

            ClearPreviousActionLabel();
        }

        private void ClearPreviousActionLabel()
        {
            if (ActionLabelText != null)
            {
                ActionLabelText.text = "";
                ActionLabelText.gameObject.SetActive(false);
            }
        }


        public void ClearAllActionLabels()
        {
            Debug.Log("[PlayerActionUI] Clearing all action labels");
        }
    }
}