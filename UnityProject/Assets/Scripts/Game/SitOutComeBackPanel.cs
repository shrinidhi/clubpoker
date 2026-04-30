using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using System.Collections.Generic;

namespace ClubPoker.Game
{
    public class SitOutComeBackPanel : MonoBehaviour
    {
        public Button sitOutButton;
        public Button comeBackButton;
       

        private bool _isSittingOut;
        private bool _pendingRequest;

        private void Start()
        {
            sitOutButton.onClick.AddListener(OnSitOutClicked);
            comeBackButton.onClick.AddListener(OnComeBackClicked);

            SetState(false);
        }

        #region UI STATE

        public void SetState(bool isSittingOut)
        {
         
            sitOutButton.interactable = !isSittingOut;
            comeBackButton.interactable = isSittingOut;
        }

        #endregion

        #region SIT OUT

        private void OnSitOutClicked()
        {
            if (_pendingRequest || !SocketManager.Instance.IsConnected)
                return;

            _pendingRequest = true;

            // optimistic UI
            SetState(true);

            var payload = new Dictionary<string, object>
           {
            { "tableId", SocketManager.Instance.CurrentTableId }
            };


            SocketManager.Instance.Emit("player:sit_out", payload);
        }

        #endregion

        #region COME BACK

        private void OnComeBackClicked()
        {
            if (_pendingRequest || !SocketManager.Instance.IsConnected)
                return;

            _pendingRequest = true;

           
            SetState(false);

            var payload = new Dictionary<string, object>
           {
            { "tableId", SocketManager.Instance.CurrentTableId }
            };


            SocketManager.Instance.Emit("player:come_back", payload);
        }

        #endregion

        #region SERVER RESPONSE HOOKS

        public void OnSitOutConfirmed()
        {
            _pendingRequest = false;
            _isSittingOut = true;
            SetState(true);
        }

        public void OnComeBackConfirmed()
        {
            _pendingRequest = false;
            _isSittingOut = false;
            SetState(false);
        }

        public void OnSitOutRejected()
        {
            _pendingRequest = false;
            SetState(false);
        }

        public void OnComeBackRejected()
        {
            _pendingRequest = false;
            SetState(true);
        }

        #endregion
    }
}