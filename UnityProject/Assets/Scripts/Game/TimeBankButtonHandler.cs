using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking;

namespace ClubPoker.Game
{
    public class TimeBankButtonHandler : MonoBehaviour
    {
        public Button TimeBankButton;
        public GameObject ActivationVFX;

        private bool _usedThisTurn = false;

        private void Start()
        {
            TimeBankButton.onClick.AddListener(OnTimeBankClicked);

            ResetState();
        }

        private void OnTimeBankClicked()
        {
            ActivateTimeBank();
        }

       
        public void OnYourTurnStart(bool hasTimeBank)
        {
            _usedThisTurn = false;

            TimeBankButton.gameObject.SetActive(hasTimeBank);
            TimeBankButton.interactable = hasTimeBank;
        }

        public void ActivateTimeBank()
        {
            if (_usedThisTurn) return;
            if (!SocketManager.Instance.IsConnected) return;

            _usedThisTurn = true;

            
            TimeBankButton.interactable = false;

            PlayActivationAnimation();

            var payload = new System.Collections.Generic.Dictionary<string, object>
            {
                { "tableId", SocketManager.Instance.CurrentTableId }
            };

            Debug.Log("[TimeBank] Emit player:activate_time_bank");

            SocketManager.Instance.Emit("player:activate_time_bank", payload);
        }

       
        public void OnTimeBankConfirmed()
        {
            Debug.Log("[TimeBank] Server confirmed activation");
         
        }

        
        public void ResetState()
        {
            _usedThisTurn = false;
            TimeBankButton.interactable = true;
            TimeBankButton.gameObject.SetActive(true);
        }

        private void PlayActivationAnimation()
        {
            if (ActivationVFX != null)
                ActivationVFX.SetActive(true);

            Debug.Log("[TimeBank] Activation animation played");
        }
    }
}