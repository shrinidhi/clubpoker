using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class KickedPlayerPrefab : MonoBehaviour
{
    public Text Playername;
    public Text Time;

    public void SetData(string playerName, string kickedTime)
    {
        Playername.text = playerName;
        Time.text = kickedTime;
    }
}