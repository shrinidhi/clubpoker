using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Club Poster. Up to 4 posters. Add → pick+preview (sub-popup) → Save stages a row
/// locally (no upload). Post → confirm → uploads all staged rows (POST as base64). Delete
/// removes a staged row locally, or DELETEs an already-uploaded one.
/// Backend is LIVE: GET/POST/DELETE /api/clubs/{clubId}/posters
/// </summary>
public class AdminClubPosterScreenScript : MonoBehaviour
{
    private const int MaxPosters = 4;

    [Header("Header")]
    public Button Close_Button;

    [Header("List")]
    public ScrollRect ScrollView;
    public Transform ListContainer;
    public AdminPosterCellScript CellPrefab;

    [Header("Empty State")]
    public GameObject Empty_Object;         // "No poster uploaded yet"

    [Header("Add / Post")]
    public Button Add_Button;
    public Button Post_Button;

    [Header("Sub-popup")]
    public AdminPosterUploadPopupScript UploadPopup;

    [Header("Shared")]
    public AlertPopup AlertPopup;

    // Working set: server posters (Id set) + staged ones (Id null, Url = base64 data URI).
    private readonly List<PosterData> _posters = new List<PosterData>();
    private readonly List<AdminPosterCellScript> _cells = new List<AdminPosterCellScript>();
    private bool _busy;

    private bool HasStaged
    {
        get
        {
            foreach (var p in _posters)
                if (p != null && string.IsNullOrEmpty(p.Id)) return true;
            return false;
        }
    }

    private void Start()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(OnCloseTap);
        if (Add_Button   != null) Add_Button.onClick.AddListener(OnAddTap);
        if (Post_Button  != null) Post_Button.onClick.AddListener(OnPostTap);
    }

    private void OnEnable()
    {
        Load().Forget();
    }

    private async UniTask Load()
    {
        try
        {
            var res = await ClubManager.Instance.GetPostersAsync(ClubContext.ClubId);
            _posters.Clear();
            if (res?.Posters != null) _posters.AddRange(res.Posters);
            Render();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminClubPosterScreenScript] load error: {e.Message}");
            ShowToast("Failed to load posters");
        }
    }

    // Rebuild cells from the working set.
    private void Render()
    {
        ClearCells();

        foreach (var p in _posters)
        {
            if (CellPrefab == null || ListContainer == null) break;
            var cell = Instantiate(CellPrefab, ListContainer);
            cell.Setup(p, OnDeleteTap);
            _cells.Add(cell);
        }

        if (Empty_Object != null) Empty_Object.SetActive(_posters.Count == 0);
        RefreshButtons();
        ResetScroll();
    }

    private void RefreshButtons()
    {
        if (Add_Button  != null) Add_Button.interactable  = _posters.Count < MaxPosters && !_busy;
        // Post only matters when there's something un-uploaded to publish.
        if (Post_Button != null) Post_Button.interactable = HasStaged && !_busy;
    }

    // ── Add / Save (local staging) ─────────────────────────────────────────────

    private void OnAddTap()
    {
        if (_busy) return;
        if (_posters.Count >= MaxPosters)
        {
            ShowToast("Up to 4 posters can be uploaded");
            return;
        }

        if (UploadPopup != null)
            UploadPopup.Open(OnPosterSaved);
    }

    // Save in the sub-popup → stage a row locally (no API yet).
    private void OnPosterSaved(string dataUri, string filename, long fileSize)
    {
        _posters.Add(new PosterData
        {
            Id       = null,           // null = staged, not yet uploaded
            Url      = dataUri,
            Filename = filename,
            FileSize = fileSize,
        });
        Render();
    }

    // ── Post (upload staged) ────────────────────────────────────────────────────

    private void OnPostTap()
    {
        if (_busy || !HasStaged) return;

        if (AlertPopup != null)
            AlertPopup.Show("Tips", "Confirm to display posters in this order?",
                showCancel: true, onConfirm: () => Post().Forget());
        else
            Post().Forget();
    }

    private async UniTaskVoid Post()
    {
        _busy = true;
        RefreshButtons();
        try
        {
            // Upload every staged poster (top → bottom keeps order).
            foreach (var p in _posters)
            {
                if (p == null || !string.IsNullOrEmpty(p.Id)) continue;   // already on server
                await ClubManager.Instance.UploadPosterAsync(
                    ClubContext.ClubId, p.Url, p.Filename, p.FileSize);
            }

            ShowToast("Posters published");
            await Load();      // resync (staged rows now have ids/order from server)
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminClubPosterScreenScript] post error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to publish" : e.Message);
        }
        finally
        {
            _busy = false;
            RefreshButtons();
        }
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    private void OnDeleteTap(PosterData poster)
    {
        if (_busy || poster == null) return;

        // Staged (not uploaded) → just drop it locally, no confirm needed.
        if (string.IsNullOrEmpty(poster.Id))
        {
            _posters.Remove(poster);
            Render();
            return;
        }

        // Already uploaded → confirm, then DELETE.
        if (AlertPopup != null)
            AlertPopup.Show("Tips", "Delete this poster?", showCancel: true,
                onConfirm: () => Delete(poster).Forget());
        else
            Delete(poster).Forget();
    }

    private async UniTaskVoid Delete(PosterData poster)
    {
        _busy = true;
        RefreshButtons();
        try
        {
            await ClubManager.Instance.DeletePosterAsync(ClubContext.ClubId, poster.Id);
            _posters.Remove(poster);     // keep staged rows intact (no full reload)
            Render();
            ShowToast("Poster deleted");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminClubPosterScreenScript] delete error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Delete failed" : e.Message);
        }
        finally
        {
            _busy = false;
            RefreshButtons();
        }
    }

    // ── Close ──────────────────────────────────────────────────────────────────

    private void OnCloseTap()
    {
        if (_busy) return;

        // Staged-but-not-posted rows would be lost → confirm.
        if (HasStaged && AlertPopup != null)
        {
            AlertPopup.Show("Tips", "Current content will not be saved, confirm to close?",
                showCancel: true, onConfirm: Close);
            return;
        }

        Close();
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private void ResetScroll()
    {
        if (ScrollView == null) return;
        Canvas.ForceUpdateCanvases();
        ScrollView.verticalNormalizedPosition = 1f;
    }

    private void ClearCells()
    {
        _cells.Clear();
        if (ListContainer == null) return;
        for (int i = ListContainer.childCount - 1; i >= 0; i--)
            Destroy(ListContainer.GetChild(i).gameObject);
    }

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
