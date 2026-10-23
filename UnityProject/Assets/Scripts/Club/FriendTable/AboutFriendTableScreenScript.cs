using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class AboutFriendTableScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public GameObject FriendTableMenuScreen;
    // Start is called before the first frame update
    void Start()
    {
        CloseButton.onClick.AddListener(CloseButtonOnTap);

    }

    void CloseButtonOnTap()
    {
        FriendTableMenuScreen.SetActive(true);
        gameObject.SetActive(false);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
