using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class BadBeatRecordScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Transform Content;
    public GameObject BadBeatRecordPrefab;
    // Start is called before the first frame update
    void Start()
    {
        CloseButton.onClick.AddListener(CloseButtonOnTap);
    }

    void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

   
}
