using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[System.Serializable]
public class TransactionResponse
{
    public int page;
    public int totalPages;
    public List<TransactionData> transactions;
}

[System.Serializable]
public class TransactionData
{
    public string type;
    public int amount;
    public string description;
    public string date;
    public string handId;
}
public class TransactionScreenScript : MonoBehaviour
{
    [Header("UI")]
    public Transform content;
    public GameObject itemPrefab;
    public ScrollRect scrollRect;
    public GameObject loader;

    private int currentPage = 1;
    private int totalPages = 1;
    private bool isLoading = false;

    string apiUrl = "https://your-api.com/api/economy/transactions";

    public Button Back_Button;

    void Start()
    {
        LoadTransactions();
        scrollRect.onValueChanged.AddListener(OnScroll);
        Back_Button.onClick.AddListener(Back_ButtonOnTap);
    }

    void Back_ButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        ResetList();
        LoadTransactions();
    }

    void ResetList()
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        currentPage = 1;
        totalPages = 1;
    }
    void LoadTransactions()
    {
        if (isLoading || currentPage > totalPages) return;

        StartCoroutine(GetTransactions());
    }


    IEnumerator GetTransactions()
    {
        isLoading = true;
        loader.SetActive(true);

        yield return new WaitForSeconds(0.5f); 

        List<TransactionData> dummyList = GetDummyData(currentPage);

        foreach (var data in dummyList)
        {
            GameObject obj = Instantiate(itemPrefab, content);
            obj.GetComponent<TransactionPrefabScript>().Setup(data);
        }

        totalPages = 5;
        currentPage++;

        loader.SetActive(false);
        isLoading = false;
    }

    /* IEnumerator GetTransactions()
     {
         isLoading = true;
         loader.SetActive(true);

         string url = apiUrl + "?page=" + currentPage;

         UnityWebRequest request = UnityWebRequest.Get(url);

         string token = PlayerPrefs.GetString("TOKEN", "");
         if (!string.IsNullOrEmpty(token))
         {
             request.SetRequestHeader("Authorization", "Bearer " + token);
         }

         yield return request.SendWebRequest();

         loader.SetActive(false);
         isLoading = false;

         if (request.result == UnityWebRequest.Result.Success)
         {
             TransactionResponse res = JsonUtility.FromJson<TransactionResponse>(request.downloadHandler.text);

             totalPages = res.totalPages;

             foreach (var data in res.transactions)
             {
                 GameObject obj = Instantiate(itemPrefab, content);
                 obj.GetComponent<TransactionPrefabScript>().Setup(data);
             }

             currentPage++;
         }
         else
         {
             Debug.LogError("API Error: " + request.error);
         }
     }*/
    void OnScroll(Vector2 pos)
    {
        if (pos.y <= 0.1f) 
        {
            LoadTransactions();
        }
    }



    List<TransactionData> GetDummyData(int page)
    {
        List<TransactionData> list = new List<TransactionData>();

        for (int i = 0; i < 50; i++)
        {
            TransactionData data = new TransactionData();

            // random type
            int type = Random.Range(0, 4);

            switch (type)
            {
                case 0:
                    data.type = "BONUS";
                    data.amount = 500;
                    data.description = "Daily Bonus";
                    data.handId = "";
                    break;

                case 1:
                    data.type = "BUYIN";
                    data.amount = -1000;
                    data.description = "Table Buy-in";
                    data.handId = "";
                    break;

                case 2:
                    data.type = "WIN";
                    data.amount = 2000;
                    data.description = "Win Hand";
                    data.handId = "HAND_" + Random.Range(1000, 9999);
                    break;

                case 3:
                    data.type = "LOSE";
                    data.amount = -500;
                    data.description = "Lost Hand";
                    data.handId = "HAND_" + Random.Range(1000, 9999);
                    break;
            }

            data.date = System.DateTime.Now.AddMinutes(-i * 5).ToString("dd MMM yyyy HH:mm");

            list.Add(data);
        }

        return list;
    }
}
