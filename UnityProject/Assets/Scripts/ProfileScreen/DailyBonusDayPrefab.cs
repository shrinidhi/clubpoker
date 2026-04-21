using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    public class DailyBonusDayPrefab : MonoBehaviour
    {
        public TextMeshProUGUI Days_Text;
        public TextMeshProUGUI Coin_Text;

        public GameObject Tick_Mark;
        public GameObject Today_Highlight;

        public Image Background;

        public Color ClaimedColor;
        public Color TodayColor;
        public Color LockedColor;

        public void Setup(int day, int coins, bool claimed, bool isToday)
        {
            Days_Text.text = "Day " + day;
            Coin_Text.text = coins.ToString();

            Tick_Mark.SetActive(claimed);
           // Today_Highlight.SetActive(isToday);

            if (claimed)
            {
              //  Background.color = ClaimedColor;
            }
            else if (isToday)
            {
               // Background.color = TodayColor;
            }
            else
            {
               // Background.color = LockedColor;
            }
        }
    }
}