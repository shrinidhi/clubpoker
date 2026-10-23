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
    public Button AddfriendButton;
    public Button FriendReqButton;
    public Button SearchFriendButton;

    public GameObject AddFriendScreen;
    public GameObject FriendReqScreen;
    public GameObject SearchFriendScreen;

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
        AddfriendButton.onClick.AddListener(AddFriendOnTap);
        FriendReqButton.onClick.AddListener(FriendReqButtonOnTap);
        SearchFriendButton.onClick.AddListener(SearchFriendButtonOnTap);
    }


    void SearchFriendButtonOnTap()
    {
        SearchFriendScreen.SetActive(true);
    }

    void AddFriendOnTap()
    {
        AddFriendScreen.SetActive(true);
    }

    void FriendReqButtonOnTap()
    {
        FriendReqScreen.SetActive(true);
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
        AboutFriendTableScreen.SetActive(true);
        gameObject.SetActive(true);
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
