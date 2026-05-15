using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class ActionButtonHandler : MonoBehaviour
    {
        [Header("Action Buttons")]
        public Button Fold_Button;
        public Button Check_Button;
        public Button Call_Button;
        public Button Raise_Button;
        public Button All_In_Button;

        [Header("Raise Input")]
        public InputField RaiseAmountInput;

        public GameObject YourTurn;

        private int minimumRaiseAmount = 0;
        public GameObject ActionButtonGrid;

        private int currentPlayerChips = 0;
        private int callAmount = 0;
        private void Start()
        {
            BindButtons();
            SetInteractable(false);
        }

        private void BindButtons()
        {
            Fold_Button.onClick.AddListener(Fold);
            Check_Button.onClick.AddListener(Check);
            Call_Button.onClick.AddListener(Call);
            Raise_Button.onClick.AddListener(Raise);
            All_In_Button.onClick.AddListener(AllIn);
        }

        public void EnableActions(List<string> validActions, bool canCheck, int minimumRaise, int playerChips, int currentCallAmount)
        {
            SetInteractable(false);

            if (validActions == null)
                return;

            minimumRaiseAmount = minimumRaise;
            currentPlayerChips = playerChips;
            callAmount = currentCallAmount;

            YourTurn.SetActive(true);
            ActionButtonGrid.SetActive(true);

            if (RaiseAmountInput != null)
            {
                RaiseAmountInput.contentType = InputField.ContentType.IntegerNumber;
                RaiseAmountInput.text = minimumRaiseAmount > 0 ? minimumRaiseAmount.ToString() : "";
                RaiseAmountInput.placeholder.GetComponent<Text>().text =
                    minimumRaiseAmount > 0 ? $"Min Raise {minimumRaiseAmount}" : "Raise Amount";
            }

            foreach (string action in validActions)
            {
                switch (action.ToLower())
                {
                    case "fold":
                        Fold_Button.interactable = true;
                        break;

                    case "check":
                        Check_Button.interactable = true;
                        break;

                    case "call":
                        Call_Button.interactable = currentPlayerChips >= callAmount;
                        break;

                    case "raise":
                        Raise_Button.interactable = currentPlayerChips >= minimumRaiseAmount;
                        break;

                    case "all_in":
                        All_In_Button.interactable = currentPlayerChips > 0;
                        break;
                }
            }

            if (canCheck)
            {
                Check_Button.interactable = true;
                Call_Button.interactable = false;
            }
        }

        public void SetInteractable(bool state)
        {
            if (YourTurn != null)
                YourTurn.SetActive(state);
            ActionButtonGrid.SetActive(state);
            Fold_Button.interactable = state;
            Check_Button.interactable = state;
            Call_Button.interactable = state;
            Raise_Button.interactable = state;
            All_In_Button.interactable = state;
        }

        private void LockUI()
        {
            SetInteractable(false);

            if (TurnManager.Instance != null)
                TurnManager.Instance.EndTurn();
        }

        private void Fold()
        {
            TableJoinHandler.Instance?.Fold();
            LockUI();
        }

        private void Check()
        {
            TableJoinHandler.Instance?.Check();
            LockUI();
        }

        private void Call()
        {
            if (currentPlayerChips < callAmount)
            {
                Debug.LogWarning("Not enough chips to call.");
                return;
            }

            TableJoinHandler.Instance?.Call();
            LockUI();
        }

        private void Raise()
        {
            int amount = 0;

            if (RaiseAmountInput != null)
                int.TryParse(RaiseAmountInput.text, out amount);

            if (amount < minimumRaiseAmount)
            {
                amount = minimumRaiseAmount;

                if (RaiseAmountInput != null)
                    RaiseAmountInput.text = amount.ToString();
            }

            if (amount <= 0)
                return;

            if (amount > currentPlayerChips)
            {
                Debug.LogWarning("Not enough chips to raise.");
                return;
            }

            TableJoinHandler.Instance?.Raise(amount);
            LockUI();
        }
        private void AllIn()
        {
            if (currentPlayerChips <= 0)
            {
                Debug.LogWarning("Not enough chips to go all-in.");
                return;
            }

            TableJoinHandler.Instance?.AllIn();
            LockUI();
        }
    }
}