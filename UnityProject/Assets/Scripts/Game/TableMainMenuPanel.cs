using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClubPoker.Game
{
    public class TableMainMenuPanel : MonoBehaviour
    {

        public Button TableSettingButton;
        public Button TableThemeButton;
        public Button RealTimeResultButton;
        public Button HandHistoryButton;
        public Button BacktoHomeButton;
        public Button ExitButton;
        // Start is called before the first frame update
        void Start()
        {
            TableSettingButton.onClick.AddListener(TableSettingButtonOnTap);
            TableThemeButton.onClick.AddListener(TableThemeButtonOnTap);
            RealTimeResultButton.onClick.AddListener(RealTimeResultButtonOnTap);
            HandHistoryButton.onClick.AddListener(HandHistoryButtonOnTap);
            BacktoHomeButton.onClick.AddListener(BacktoHomeButtonOnTap);
            ExitButton.onClick.AddListener(ExitButtonOnTap);
        }

        void TableSettingButtonOnTap()
        {

        }

        void TableThemeButtonOnTap()
        {

        }

        void RealTimeResultButtonOnTap()
        {

        }

        void HandHistoryButtonOnTap()
        {

        }

        void BacktoHomeButtonOnTap()
        {

        }

        void ExitButtonOnTap()
        {

        }
    }
}