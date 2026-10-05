using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ClubMessageScreenScript : MonoBehaviour
{
    public Button BackButton;
    public Button DeleteReadMsg_Button;
    public Button MarkAsReadyAll_Button;
    public GameObject ClubMsgPrefab;
    public Transform Content;
    public GameObject AllDetailConfirmationScreen;
    public Button AllDeleteConfirmButton;
    public Button CancelButton;
    public ClubMessageShowScript ClubMessageShowScreen;
    [Header("Current club - pass through Open(clubId)")]
    public string ClubId;
    private readonly List<ClubMessageData> items = new List<ClubMessageData>();
    private readonly List<ClubMessagePrefabScript> rows = new List<ClubMessagePrefabScript>();
    private bool busy;
    private bool loaded;
    private int viewVersion;

    private void Awake()
    {
        if (BackButton != null) BackButton.onClick.AddListener(Back);
        if (DeleteReadMsg_Button != null) DeleteReadMsg_Button.onClick.AddListener(ShowConfirmation);
        if (MarkAsReadyAll_Button != null) MarkAsReadyAll_Button.onClick.AddListener(MarkAllRead);
        if (AllDeleteConfirmButton != null) AllDeleteConfirmButton.onClick.AddListener(ConfirmDeleteRead);
        if (CancelButton != null) CancelButton.onClick.AddListener(Cancel);
        HidePopups();
    }
    private void OnEnable()
    {
        ClubId = ClubContext.ClubId;
        Reload();
    }
    private void OnDisable() { ++viewVersion; HidePopups(); }
    private void OnDestroy()
    {
        if (BackButton != null) BackButton.onClick.RemoveListener(Back);
        if (DeleteReadMsg_Button != null) DeleteReadMsg_Button.onClick.RemoveListener(ShowConfirmation);
        if (MarkAsReadyAll_Button != null) MarkAsReadyAll_Button.onClick.RemoveListener(MarkAllRead);
        if (AllDeleteConfirmButton != null) AllDeleteConfirmButton.onClick.RemoveListener(ConfirmDeleteRead);
        if (CancelButton != null) CancelButton.onClick.RemoveListener(Cancel);
    }

    // Call from the club screen with the actual currently selected club ID.
    public void Open(string clubId)
    {
        if (string.IsNullOrWhiteSpace(clubId)) { Debug.LogError("[ClubMessages] Club ID is required."); return; }
        ClubId = clubId;
        HidePopups();
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else if (isActiveAndEnabled) Reload();
    }
    public void Reload()
    {
        if (!isActiveAndEnabled) return;
        loaded = false;
        items.Clear(); ClearRows(); UpdateControls();
        LoadAsync(++viewVersion).Forget();
    }
    private bool IsCurrent(int version) => this != null && isActiveAndEnabled && version == viewVersion;
    private ClubManager Manager()
    {
        if (ClubManager.Instance == null) throw new InvalidOperationException("ClubManager is missing in this scene.");
        return ClubManager.Instance;
    }
    private void Back() => gameObject.SetActive(false);
    private void Cancel()
    {
        if (AllDetailConfirmationScreen != null) AllDetailConfirmationScreen.SetActive(false);
    }
    private void HidePopups()
    {
        Cancel();
        if (ClubMessageShowScreen != null) ClubMessageShowScreen.Hide();
    }
    private void ShowConfirmation()
    {
        if (busy || !loaded) return;
     
        AllDetailConfirmationScreen.SetActive(true);
    }

    private async UniTask LoadAsync(int version)
    {
        await UniTask.Yield();
        while (IsCurrent(version) && busy) await UniTask.Yield();
        if (!IsCurrent(version)) return;
        busy = true; UpdateControls(); 
        string clubId = ClubId;
        try
        {
            var messages = new List<ClubMessageData>();
            var ids = new HashSet<string>();
            int page = 1;
            while (true)
            {
                var response = await Manager().GetClubMessagesAsync(clubId, page, 30);
                if (!IsCurrent(version)) return;
                int added = 0;
                if (response.Messages != null)
                    foreach (var item in response.Messages)
                        if (item != null && !string.IsNullOrEmpty(item.Id) && ids.Add(item.Id))
                        { messages.Add(item); ++added; }
                if (added == 0 || response.Total <= 0 || messages.Count >= response.Total) break;
                ++page;
            }
            items.Clear(); items.AddRange(messages); loaded = true;
            RenderList(); 
        }
        catch (Exception e) { if (IsCurrent(version)) ReportError("Could not load club messages", e); }
        finally { FinishWork(); }
    }
    private void ClearRows()
    {
        foreach (var row in rows)
            if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        rows.Clear();
    }
    private void RenderList()
    {
        ClearRows();
        if (Content == null || ClubMsgPrefab == null || ClubMsgPrefab.GetComponent<ClubMessagePrefabScript>() == null)
            throw new InvalidOperationException("Assign Content and ClubMsgPrefab with ClubMessagePrefabScript.");
        foreach (var item in items)
        {
            var instance = Instantiate(ClubMsgPrefab, Content);
            instance.SetActive(true);
            var row = instance.GetComponent<ClubMessagePrefabScript>();
            row.Setup(item, OpenMessage); rows.Add(row);
        }
        UpdateControls();
    }
    private void OpenMessage(ClubMessageData item)
    {
        if (busy || !loaded || item == null) return;
      
        ClubMessageShowScreen.Show(item, () => DeleteOne(item.Id));
        if (item.IsRead) return;
        string clubId = ClubId;
        MutateAsync(async () => { await Manager().MarkClubMessageReadAsync(clubId, item.Id); }, () =>
        {
            // Local read marker; the next GET supplies the server's readAt timestamp.
            item.ReadAt = DateTimeOffset.UtcNow.ToString("o");
            RenderList();
        }, "Could not mark club message read").Forget();
    }
    private void DeleteOne(string messageId)
    {
        string clubId = ClubId;
        MutateAsync(async () => { await Manager().DeleteClubMessageAsync(clubId, messageId); }, () =>
        {
            items.RemoveAll(item => item.Id == messageId);
            if (ClubMessageShowScreen != null) ClubMessageShowScreen.Hide();
            RenderList();
        }, "Could not delete club message").Forget();
    }
    private void MarkAllRead()
    {
        string clubId = ClubId;
        MutateAsync(async () => { await Manager().MarkAllClubMessagesReadAsync(clubId); }, () =>
        {
            string readAt = DateTimeOffset.UtcNow.ToString("o");
            foreach (var item in items) if (!item.IsRead) item.ReadAt = readAt;
            RenderList();
        }, "Could not mark all club messages read").Forget();
    }
    private void ConfirmDeleteRead()
    {
        string clubId = ClubId;
        MutateAsync(async () => { await Manager().DeleteReadClubMessagesAsync(clubId); }, () =>
        {
            items.RemoveAll(item => item.IsRead);
            HidePopups(); RenderList();
            // Fetch again because messages may have been read on another device.
            Reload();
        }, "Could not delete read club messages").Forget();
    }
    private async UniTask MutateAsync(Func<UniTask> request, Action apply, string error)
    {
        if (busy || !loaded || !isActiveAndEnabled) return;
        int version = viewVersion;
        busy = true; UpdateControls(); 
        try
        {
            await request();
            if (!IsCurrent(version)) return;
            apply();
        }
        catch (Exception e) { if (IsCurrent(version)) ReportError(error, e); }
        finally { FinishWork(); }
    }
    private void FinishWork()
    {
        busy = false;
        if (this != null) UpdateControls();
    }
    private void UpdateControls()
    {
        bool anyRead = false, anyUnread = false;
        foreach (var item in items) { anyRead |= item.IsRead; anyUnread |= !item.IsRead; }
        if (DeleteReadMsg_Button != null) DeleteReadMsg_Button.interactable = loaded && !busy && anyRead;
        if (MarkAsReadyAll_Button != null) MarkAsReadyAll_Button.interactable = loaded && !busy && anyUnread;
        if (AllDeleteConfirmButton != null) AllDeleteConfirmButton.interactable = loaded && !busy && anyRead;
        foreach (var row in rows) if (row != null) row.SetBusy(busy || !loaded);
        if (ClubMessageShowScreen != null) ClubMessageShowScreen.SetBusy(busy || !loaded);
      
        
    }
    private void ReportError(string message, Exception e)
    {
        Debug.LogError("[ClubMessages] " + message + ": " + e);
    }
}
