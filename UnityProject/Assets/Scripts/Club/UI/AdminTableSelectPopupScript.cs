using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;

/// <summary>
/// "Choose a table" sub-popup for Scrolling Message. Lists the club's live tables
/// (GET /api/clubs/{clubId}/tables) and returns the picked one via a callback.
/// </summary>
public class AdminTableSelectPopupScript : MonoBehaviour
{
    [Header("Header")]
    public Button Close_Button;

    [Header("List")]
    public ScrollRect ScrollView;
    public Transform ListContainer;
    public AdminTableRowScript RowPrefab;

    [Header("Empty State")]
    public GameObject Empty_Object;    // "No tables"

    private readonly List<AdminTableRowScript> _rows = new List<AdminTableRowScript>();
    private Action<ClubTableData> _onPicked;

    private void Start()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(Close);
    }

    /// Open and route the picked table to <paramref name="onPicked"/>.
    public void Open(Action<ClubTableData> onPicked)
    {
        _onPicked = onPicked;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);   // OnEnable loads
    }

    private void OnEnable()
    {
        Load().Forget();
    }

    private async UniTaskVoid Load()
    {
        ClearList();
        try
        {
            var res = await ClubManager.Instance.GetClubTablesAsync(ClubContext.ClubId);
            var tables = res?.Tables;

            bool empty = tables == null || tables.Count == 0;
            if (Empty_Object != null) Empty_Object.SetActive(empty);
            if (empty || RowPrefab == null || ListContainer == null) return;

            foreach (var table in tables)
            {
                var row = Instantiate(RowPrefab, ListContainer);
                row.Setup(table, OnRowPicked);
                _rows.Add(row);
            }

            ResetScroll();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminTableSelectPopupScript] load error: {e.Message}");
            ShowToast("Failed to load tables");
        }
    }

    private void OnRowPicked(ClubTableData table)
    {
        var cb = _onPicked;
        _onPicked = null;
        gameObject.SetActive(false);
        cb?.Invoke(table);
    }

    private void ResetScroll()
    {
        if (ScrollView == null) return;
        Canvas.ForceUpdateCanvases();
        ScrollView.verticalNormalizedPosition = 1f;
    }

    private void ClearList()
    {
        _rows.Clear();
        if (ListContainer == null) return;
        for (int i = ListContainer.childCount - 1; i >= 0; i--)
            Destroy(ListContainer.GetChild(i).gameObject);
        if (Empty_Object != null) Empty_Object.SetActive(false);
    }

    private void Close()
    {
        _onPicked = null;    // dismissed without picking
        gameObject.SetActive(false);
    }

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
