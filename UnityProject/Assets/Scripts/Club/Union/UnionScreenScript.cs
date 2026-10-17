using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class UnionScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Button Union_JoinButton;
    public Button Union_CreateButton;

    public GameObject Union_JoinScreen;
    public GameObject Union_CreateScreen;
    // Start is called before the first frame update
    void Start()
    {
        CloseButton.onClick.AddListener(CloseButtonOnTap);
        Union_JoinButton.onClick.AddListener(Union_JoinButtonOnTap);
        Union_CreateButton.onClick.AddListener(Union_CreateButtonOnTap);
    }

    void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    void Union_JoinButtonOnTap()
    {
        Union_JoinScreen.SetActive(true);
    }

    void Union_CreateButtonOnTap()
    {
        Union_CreateScreen.SetActive(true);
    }

}
