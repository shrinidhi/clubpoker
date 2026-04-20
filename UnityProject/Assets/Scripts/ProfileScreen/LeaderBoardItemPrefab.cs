using ClubPoker.Networking.Models;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    public class LeaderBoardItemPrefab : MonoBehaviour
    {
        public TMP_Text rankText;
        public TMP_Text nameText;
        public TMP_Text TotalWinning;
        public Image avatarImage;
        public Image highlightTop3;

        public void SetData(int rank, string username, int win, bool isMe)
        {
            rankText.text = "#" + rank;
            nameText.text = username;
            TotalWinning.text = win.ToString();

            if (isMe)
            {
                highlightTop3.color = Color.green; 
            }
        }

        public void SetTopColor(Color color)
        {
            highlightTop3.color = color;
        }
    }
}
