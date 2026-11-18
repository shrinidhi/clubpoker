using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// Export Record tab: the club's past exports, newest first, 20 per page.
/// Loads the first page each time the tab opens and the next page when the
/// list is scrolled to the bottom.
///
/// A log only — the server can't send a past file again, so rows have no
/// download action.
/// </summary>
public class ExportHistoryPanelScript : MonoBehaviour
{
    private const int PageSize = 20;

    [SerializeField] private Transform content;               // ScrollRect content
    [SerializeField] private ExportHistoryRowView rowPrefab;
    [SerializeField] private ScrollRect scrollRect;           // optional: paging on scroll
    [SerializeField] private GameObject emptyState;           // "No records"
    [SerializeField] private GameObject loadingIndicator;     // optional
    [SerializeField] private ExportTypesSO exportTypes;       // type key → display name

    private int _loadedPage;
    private int _total;
    private int _shown;
    private bool _loading;
    private int _version;   // drops responses from a previous open

    private void Awake()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(OnScrolled);
    }

    private void OnEnable()
    {
        Reload();
    }

    public void Reload()
    {
        _version++;
        _loadedPage = 0;
        _total = 0;
        _shown = 0;
        _loading = false;

        ClearRows();
        SetEmpty(false);
        LoadNextPage().Forget();
    }

    private void OnScrolled(Vector2 position)
    {
        // y ≈ 0 is the bottom of a vertical ScrollRect.
        if (position.y <= 0.05f)
            LoadNextPage().Forget();
    }

    private bool HasMore => _loadedPage == 0 || _shown < _total;

    private async UniTaskVoid LoadNextPage()
    {
        if (_loading || !HasMore || string.IsNullOrEmpty(ClubContext.ClubId))
            return;

        _loading = true;
        int version = _version;
        int page = _loadedPage + 1;
        SetLoading(true);

        try
        {
            ExportHistoryResponse res =
                await ClubManager.Instance.GetExportHistoryAsync(ClubContext.ClubId, page, PageSize);

            if (version != _version || this == null)
                return;

            _loadedPage = page;
            _total = res?.Total ?? 0;

            if (res?.History != null)
            {
                foreach (ExportHistoryItem item in res.History)
                    AddRow(item);

                // A short page means the end, whatever total says.
                if (res.History.Count < PageSize)
                    _total = _shown;
            }

            SetEmpty(_shown == 0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ExportHistory] Load page {page} failed: {e.Message}");
            if (version == _version && _shown == 0)
                SetEmpty(true);
        }
        finally
        {
            if (version == _version)
            {
                _loading = false;
                SetLoading(false);
            }
        }
    }

    private void AddRow(ExportHistoryItem item)
    {
        if (content == null || rowPrefab == null)
            return;

        ExportHistoryRowView row = Instantiate(rowPrefab, content);
        row.gameObject.SetActive(true);
        row.Setup(item, TypeName);
        _shown++;
    }

    private void ClearRows()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            // Keep a row template that lives inside the content (disabled).
            if (rowPrefab != null && child == rowPrefab.transform)
                continue;
            Destroy(child.gameObject);
        }
    }

    private string TypeName(string key)
    {
        if (exportTypes != null && exportTypes.Types != null)
            foreach (ExportTypeData type in exportTypes.Types)
                if (type.TypeKey == key)
                    return type.DisplayName;

        // A type this build doesn't list (e.g. unionData) — show the key readably.
        return key == "unionData" ? "Union Data" : key;
    }

    private void SetEmpty(bool empty)
    {
        if (emptyState != null)
            emptyState.SetActive(empty);
    }

    private void SetLoading(bool loading)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(loading);
    }
}
