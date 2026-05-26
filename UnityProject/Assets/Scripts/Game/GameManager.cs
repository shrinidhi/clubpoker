using ClubPoker.Auth;
using Cysharp.Threading.Tasks;
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

        private const string PLO4_TOOLTIP_PREFS_KEY = "plo4_rules_shown";
        private const string PLO6_TOOLTIP_PREFS_KEY = "plo6_rules_shown";

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

            SetupPLOTooltip();
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

        void Update()
        {
        }
    }
}

