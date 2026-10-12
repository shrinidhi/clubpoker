using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class JackPotScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Button OFX_JackpotButton;
    public Button BadBeat_JackPotButton;
    public Button Spin_JackPotButton;

    public GameObject BadBeatJackPot_Screen;

    public TextMeshProUGUI BadBeatCurrentAmount;
    // Start is called before the first frame update
    void Start()
    {
        CloseButton.onClick.AddListener(CloseButtonOnTap);
        BadBeat_JackPotButton.onClick.AddListener(BadBeat_JackPotButtonOnTap);
    }

    void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    void BadBeat_JackPotButtonOnTap()
    {
        BadBeatJackPot_Screen.SetActive(true);
    }



}
