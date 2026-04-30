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
        // Start is called before the first frame update
        void Start()
        {
            Chat_Button.onClick.AddListener(Chat_ButtonOnTap);
        }


        void Chat_ButtonOnTap()
        {
            ChatPanel.SetActive(true);
        }
        // Update is called once per frame
        void Update()
        {

        }
    }
}

