using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

public class TradeRecordViewScript : MonoBehaviour
{
    [Header("Search & Filter")]
    public TMP_InputField Search_InputField;
    public TMP_Dropdown Filter_Dropdown;    // ALL | Send Out | Claim Back | Request

    [Header("List")]
    public Transform RecordList_Content;
    public GameObject RecordRowPrefab;
    
    public TextMeshProUGUI NoTradeRecords_Text;

    private int _currentPage = 1;
    private string _currentSearch = "";
    private string _currentFilter = "";
    private List<GameObject> _rows = new List<GameObject>();

    private static readonly string[] FilterKeys = { "", "SEND", "CLAIM_BACK", "REQUEST" };

    private void Start()
    {
        Search_InputField.onValueChanged.AddListener(OnSearchChanged);
        Filter_Dropdown.onValueChanged.AddListener(OnFilterChanged);

        Filter_Dropdown.ClearOptions();
        Filter_Dropdown.AddOptions(new List<string> { "ALL", "Send Out", "Claim Back", "Request" });
    }

    public void Init()
    {
        _currentPage   = 1;
        _currentSearch = "";
        _currentFilter = "";
        Search_InputField.text = "";
        Filter_Dropdown.value  = 0;
        ClearList();
        LoadRecords().Forget();
    }

    private async UniTaskVoid LoadRecords(bool append = false)
    {
        try
        {
            var res = await ClubChipManager.Instance.GetChipRecordsAsync(
                ClubContext.ClubId, _currentPage, _currentSearch, _currentFilter);

            if (!append) ClearList();

            if (res?.Records == null || res.Records.Count == 0)
            {
                if (NoTradeRecords_Text != null) NoTradeRecords_Text.gameObject.SetActive(true);
                return;
            }

            if (NoTradeRecords_Text != null) NoTradeRecords_Text.gameObject.SetActive(false);

            foreach (var record in res.Records)
            {
                var obj = Instantiate(RecordRowPrefab, RecordList_Content);
                var row = obj.GetComponent<TradeRecordRowScript>();
                row.Setup(record);
                _rows.Add(obj);
            }

        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TradeRecordViewScript] LoadRecords error: {e.Message}");
        }
    }

    private System.Threading.CancellationTokenSource _searchCts;

    private void OnSearchChanged(string search)
    {
        _currentSearch = search;
        _currentPage   = 1;
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        DebounceSearch(_searchCts.Token).Forget();
    }

    private async UniTaskVoid DebounceSearch(System.Threading.CancellationToken token)
    {
        try
        {
            await UniTask.Delay(400, cancellationToken: token);
            LoadRecords().Forget();
        }
        catch (System.OperationCanceledException) { }
    }

    private void OnFilterChanged(int index)
    {
        _currentFilter = FilterKeys[index];
        _currentPage   = 1;
        LoadRecords().Forget();
    }

    private void ClearList()
    {
        _rows.Clear();
        for (int i = RecordList_Content.childCount - 1; i >= 0; i--)
            Destroy(RecordList_Content.GetChild(i).gameObject);
        if (NoTradeRecords_Text != null) NoTradeRecords_Text.gameObject.SetActive(false);
    }
}
