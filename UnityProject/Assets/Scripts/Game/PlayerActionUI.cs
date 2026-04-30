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
            PlayActionAnimation(payload);
            UpdatePot(payload.Pot);
            UpdatePlayerChips(payload.UpdatedChips);
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