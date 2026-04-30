// ActionButtonHandler.cs

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

        public void EnableActions(
            List<string> validActions,
            bool canCheck
        )
        {
            SetInteractable(false);

            if (validActions == null)
                return;

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
                        Call_Button.interactable = true;
                        break;

                    case "raise":
                        Raise_Button.interactable = true;
                        break;

                    case "all_in":
                        All_In_Button.interactable = true;
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
            {
                TurnManager.Instance.EndTurn();
            }
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
            TableJoinHandler.Instance?.Call();
            LockUI();
        }

        private void Raise()
        {
            int amount = 0;

            if (RaiseAmountInput != null)
            {
                int.TryParse(RaiseAmountInput.text, out amount);
            }

            TableJoinHandler.Instance?.Raise(amount);
            LockUI();
        }

        private void AllIn()
        {
            TableJoinHandler.Instance?.AllIn();
            LockUI();
        }
    }
}