using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class FriendTableMenuScreen : MonoBehaviour
{
    public Button CreateTableButton;
    public Button JoinTableButton;
    public Button ShowTableListButton;
    public Button AboutFriendTableButton;
    public Button CloseButton;

    public GameObject CreateFrindTable;
    public GameObject JoinTableScreen;
    public GameObject FriendTableListScreen;
    public GameObject AboutFriendTableScreen;
    // Start is called before the first frame update
    void Start()
    {
        CreateTableButton.onClick.AddListener(CreateTableButtonOnTap);
        JoinTableButton.onClick.AddListener(JoinTableButtonOnTap);
        ShowTableListButton.onClick.AddListener(ShowTableListButtonOnTap);
        AboutFriendTableButton.onClick.AddListener(AboutFriendTableButtonOnTap);
        CloseButton.onClick.AddListener(CloseButtonOnTap);
    }


    void CreateTableButtonOnTap()
    {
        CreateFrindTable.SetActive(true);
        gameObject.SetActive(false);
    }
    void JoinTableButtonOnTap()
    {
        JoinTableScreen.SetActive(true);
        gameObject.SetActive(false);
    }

    void ShowTableListButtonOnTap()
    {
        FriendTableListScreen.SetActive(true);
        gameObject.SetActive(false);
    }

    void AboutFriendTableButtonOnTap()
    {

    }


    void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
