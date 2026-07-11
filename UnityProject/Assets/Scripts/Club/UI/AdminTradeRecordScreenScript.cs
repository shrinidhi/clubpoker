using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Personal Trade Record. Plain list of the club's chip records — no search or
/// filter (that's the Cashier ▸ Trade Record tab). Reuses ClubManager.GetChipRecordsAsync
/// and the existing TradeRecordRowScript prefab.
/// Backend is LIVE: GET /api/clubs/{clubId}/chips/records?limit=50
/// </summary>
public class AdminTradeRecordScreenScript : MonoBehaviour
{
    private const int PageLimit = 50;

    [Header("Header")]
    public Button Back_Button;

    [Header("List")]
    public ScrollRect ScrollView;
    public Transform RecordList_Content;
    public AdminTradeRecordRowScript RecordRowPrefab;

    [Header("Empty State")]
    public GameObject NoTradeRecords_Object;    // "No Trade Record"

    private readonly List<AdminTradeRecordRowScript> _rows = new List<AdminTradeRecordRowScript>();

    private void Start()
    {
        if (Back_Button != null) Back_Button.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        ClearList();
        LoadRecords().Forget();
    }

    private async UniTaskVoid LoadRecords()
    {
        try
        {
            var res = await ClubManager.Instance.GetChipRecordsAsync(
                ClubContext.ClubId, page: 1, search: null, filter: null, limit: PageLimit);

            ClearList();

            bool empty = res?.Records == null || res.Records.Count == 0;
            if (NoTradeRecords_Object != null) NoTradeRecords_Object.SetActive(empty);
            if (empty) return;

            foreach (var record in res.Records)
            {
                var row = Instantiate(RecordRowPrefab, RecordList_Content);
                row.Setup(record);
                _rows.Add(row);
            }

            ResetScroll();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminTradeRecordScreenScript] load error: {e.Message}");
            ShowToast("Failed to load trade records");
        }
    }

    // Rebuild the layout first so content height is known, then snap to top.
    private void ResetScroll()
    {
        if (ScrollView == null) return;
        Canvas.ForceUpdateCanvases();
        ScrollView.verticalNormalizedPosition = 1f;
    }

    private void ClearList()
    {
        _rows.Clear();
        if (RecordList_Content == null) return;
        for (int i = RecordList_Content.childCount - 1; i >= 0; i--)
            Destroy(RecordList_Content.GetChild(i).gameObject);
        if (NoTradeRecords_Object != null) NoTradeRecords_Object.SetActive(false);
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    // Same toast used across the club screens (Data/Export).
    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
