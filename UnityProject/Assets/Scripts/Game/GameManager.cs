using ClubPoker.Auth;
using ClubPoker.Networking;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class GameManager : MonoBehaviour
    {
        public Button Chat_Button;
        public GameObject ChatPanel;

        [Header("Side Menu")]
        public Button SideMenu_Button;
        public TableMenuController SideMenu;

        private const string PLO4_TOOLTIP_PREFS_KEY = "plo4_rules_shown";
        private const string PLO6_TOOLTIP_PREFS_KEY = "plo6_rules_shown";
        private DateTime sessionStartTime;
        public TimeSpan span;

        public Button RealTimeResultButton;
        public Button HandHistoryButton;

        public GameObject RealTimeResultPanel;
        public GameObject HandHistoryPanel;

        
        void Start()
        {
            var state = GameStateManager.Instance.CurrentState;

            if (state != null && PokerTableUI.Instance != null)
            {
                PokerTableUI.Instance.RenderFullTable(state);
                PokerTableUI.Instance.UpdatePlayerCount();
                PokerTableUI.Instance.RefreshSeatAvailability();
            }
            Chat_Button.onClick.AddListener(Chat_ButtonOnTap);

            if (SideMenu_Button != null)
                SideMenu_Button.onClick.AddListener(SideMenu_ButtonOnTap);

            SetupPLOTooltip();

            sessionStartTime = DateTime.Now;

            RealTimeResultButton.onClick.AddListener(RealTimeResultButtonOnTap);
            HandHistoryButton.onClick.AddListener(HandHistoryButtonOnTap);
        }

        void RealTimeResultButtonOnTap()
        {
            RealTimeResultPanel.SetActive(true);
        }

        void HandHistoryButtonOnTap()
        {
            HandHistoryPanel.SetActive(true);
        }

        void SetupPLOTooltip()
        {
            string variant = GameStateManager.Instance.Variant
                          ?? GameStateManager.Instance.CurrentState?.Variant;

            bool isPLO = variant == "omaha" || variant == "omaha_six"
                      || variant == "plo4"  || variant == "plo6";

            if (!isPLO) return;

            // Auto-show on first PLO4 or PLO6 game separately
            bool isPLO6 = variant == "omaha_six" || variant == "plo6";
            string prefsKey = isPLO6 ? PLO6_TOOLTIP_PREFS_KEY : PLO4_TOOLTIP_PREFS_KEY;

            if (PlayerPrefs.GetInt(prefsKey, 0) == 0)
            {
                PlayerPrefs.SetInt(prefsKey, 1);
                PlayerPrefs.Save();
                if (PokerTableUI.Instance != null)
                    PokerTableUI.Instance.ShowPLOTooltip(variant);
            }
        }

        void Chat_ButtonOnTap()
        {
            ChatPanel.SetActive(true);
        }

        void SideMenu_ButtonOnTap()
        {

            if (SideMenu != null)
                SideMenu.Open();
        }

       

        void Update()
        {
            span = DateTime.Now - sessionStartTime;
        }
    }
}

