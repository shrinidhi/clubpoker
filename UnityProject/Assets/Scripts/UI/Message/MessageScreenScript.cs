using System;
using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class MessageScreenScript : MonoBehaviour
{
    public Button BackButton;
    public Button AllButton;
    public Button NoticeButton;
    public Button MessageButton;
    public Button SystemButton;
    public Button DeleteReadMsg_Button;
    public Button MarkAsReadyAll_Button; // Keep your existing Inspector field name.
    public GameObject MsgPrefab;
    public Transform Content;

    [Header("Detail popup")]
    public MessageShowScreenScript MessageShowScreen;
   

    private readonly List<NotificationData> items = new List<NotificationData>();
    private readonly List<MessagePrefab> rows = new List<MessagePrefab>();
    private string selectedType = "all";
    private bool busy;
    private int viewVersion;

    public GameObject AllDetailConfirmationScreen;
    public Button AllDeleteConfirmButton;
    public Button CancelButton;

    public Sprite SelectButtonSprite;
    public Sprite UnSelectButtonSprite;

    private void Awake()
    {
        if (BackButton != null) BackButton.onClick.AddListener(Back);
        if (AllButton != null) AllButton.onClick.AddListener(SelectAll);
        if (NoticeButton != null) NoticeButton.onClick.AddListener(SelectNotice);
        if (MessageButton != null) MessageButton.onClick.AddListener(SelectMessage);
        if (SystemButton != null) SystemButton.onClick.AddListener(SelectSystem);
        if (DeleteReadMsg_Button != null) DeleteReadMsg_Button.onClick.AddListener(DeleteRead);
        if (MarkAsReadyAll_Button != null) MarkAsReadyAll_Button.onClick.AddListener(MarkAllRead);
        if (MessageShowScreen != null) MessageShowScreen.Hide();
        AllDeleteConfirmButton.onClick.AddListener(AllDeleteConfirmButtonOnTap);
        CancelButton.onClick.AddListener(CancelButtonOnTap);
    }
    private void OnEnable() { int version = ++viewVersion; LoadAsync(version).Forget(); }
    private void OnDisable()
    {
        ++viewVersion;
        if (MessageShowScreen != null) MessageShowScreen.Hide();
    }
    private void OnDestroy()
    {
        if (BackButton != null) BackButton.onClick.RemoveListener(Back);
        if (AllButton != null) AllButton.onClick.RemoveListener(SelectAll);
        if (NoticeButton != null) NoticeButton.onClick.RemoveListener(SelectNotice);
        if (MessageButton != null) MessageButton.onClick.RemoveListener(SelectMessage);
        if (SystemButton != null) SystemButton.onClick.RemoveListener(SelectSystem);
        if (DeleteReadMsg_Button != null) DeleteReadMsg_Button.onClick.RemoveListener(DeleteRead);
        if (MarkAsReadyAll_Button != null) MarkAsReadyAll_Button.onClick.RemoveListener(MarkAllRead);
    }
    private bool IsCurrent(int version) => this != null && isActiveAndEnabled && version == viewVersion;
    private AuthManager Auth()
    {
        if (AuthManager.Instance == null) throw new InvalidOperationException("Please sign in before opening messages.");
        return AuthManager.Instance;
    }
    private void Back()
    {
        gameObject.SetActive(false);
      
    }
    private void SelectAll() => Select("all");
    private void SelectNotice() => Select("notice");
    private void SelectMessage() => Select("email");
    private void SelectSystem() => Select("system");
    private void Select(string type)
    {
        if (busy) return;
        selectedType = type;
        RenderList();
    }
    private void DeleteRead()
    {
        AllDetailConfirmationScreen.SetActive(true);
    }
        private void MarkAllRead() => MarkAllReadAsync().Forget();

    private async UniTask LoadAsync(int version)
    {
        // A previous server mutation may still be finishing after this view reopened.
        await UniTask.Yield();
        while (IsCurrent(version) && busy) await UniTask.Yield();
        if (!IsCurrent(version)) return;
        busy = true; UpdateControls(); 
       
        try
        {
            var loaded = new List<NotificationData>();
            var ids = new HashSet<string>();
            int page = 1;
            while (true)
            {
                var response = await Auth().GetNotificationsAsync(page, 30);
                if (!IsCurrent(version)) return;
                int added = 0;
                if (response.Notifications != null)
                    foreach (var item in response.Notifications)
                        if (item != null && !string.IsNullOrEmpty(item.Id) && ids.Add(item.Id))
                        { loaded.Add(item); ++added; }
                if (added == 0 || loaded.Count >= response.Total || response.Total <= 0) break;
                ++page;
            }
            items.Clear(); items.AddRange(loaded);
            RenderList();
        }
        catch (Exception e)
        {
            if (IsCurrent(version)) {}
        }
        finally { FinishWork(); }
    }

    private void RenderList()
    {
        foreach (var row in rows)
            if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        rows.Clear();
        if (Content == null || MsgPrefab == null || MsgPrefab.GetComponent<MessagePrefab>() == null)
        {
           
            Debug.LogError("[Messages] Content or MessagePrefab configuration is missing.");
            return;
        }
        foreach (var item in items)
        {
            if (selectedType != "all" && !string.Equals(item.Type, selectedType, StringComparison.OrdinalIgnoreCase)) continue;
            var instance = Instantiate(MsgPrefab, Content);
            instance.SetActive(true);
            var row = instance.GetComponent<MessagePrefab>();
            if (row == null) { Destroy(instance); throw new InvalidOperationException("MsgPrefab needs the MessagePrefab component."); }
            row.Setup(item, OpenMessage);
            rows.Add(row);
        }
        
        UpdateControls();
    }

    private void OpenMessage(NotificationData item) => OpenMessageAsync(item).Forget();
    private async UniTask OpenMessageAsync(NotificationData item)
    {
        if (busy || item == null) return;
        if (MessageShowScreen == null) {}
        MessageShowScreen.Show(item, () => DeleteOneAsync(item.Id).Forget());
        if (item.IsRead) return;
        int version = viewVersion;
        busy = true; UpdateControls();
        try
        {
            await Auth().MarkNotificationReadAsync(item.Id);
            if (!IsCurrent(version)) return;
            item.IsRead = true; RenderList();
        }
        catch (Exception e) { if (IsCurrent(version)) {} }
        finally { FinishWork(); }
    }

    private async UniTask DeleteOneAsync(string id)
    {
        if (busy || !isActiveAndEnabled) return;
        int version = viewVersion;
        busy = true; UpdateControls();
        try
        {
            await Auth().DeleteNotificationAsync(id);
            if (!IsCurrent(version)) return;
            items.RemoveAll(item => item.Id == id);
            if (MessageShowScreen != null) MessageShowScreen.Hide();
            RenderList();
        }
        catch (Exception e) { if (IsCurrent(version)) {} }
        finally { FinishWork(); }
    }

    private async UniTask MarkAllReadAsync()
    {
        if (busy || !isActiveAndEnabled) return;
        int version = viewVersion;
        busy = true; UpdateControls(); 
        try
        {
            var result = await Auth().MarkAllNotificationsReadAsync();
            if (!IsCurrent(version)) return;
            foreach (var item in items) item.IsRead = true;
            RenderList(); 
        }
        catch (Exception e) { if (IsCurrent(version)) {} }
        finally { FinishWork(); }
    }

    private async UniTask DeleteReadAsync()
    {
        if (busy || !isActiveAndEnabled) return;

        int version = viewVersion;
        busy = true;
        UpdateControls();

        if (AllDeleteConfirmButton != null)
            AllDeleteConfirmButton.interactable = false;

        try
        {
            await Auth().DeleteReadNotificationsAsync();
            if (!IsCurrent(version)) return;

            items.RemoveAll(item => item.IsRead);

            if (MessageShowScreen != null)
                MessageShowScreen.Hide();

            if (AllDetailConfirmationScreen != null)
                AllDetailConfirmationScreen.SetActive(false);

            RenderList();
        }
        catch (Exception e)
        {
            Debug.LogError("[Messages] Delete read failed: " + e.Message);
        }
        finally
        {
            FinishWork();

            if (this != null && AllDeleteConfirmButton != null)
                AllDeleteConfirmButton.interactable = true;
        }
    }

    private void FinishWork()
    {
        busy = false;
        if (this == null) return;
     
        UpdateControls();
    }
    private void UpdateControls()
    {
        UpdateFilterButton(AllButton, "all");
        UpdateFilterButton(NoticeButton, "notice");
        UpdateFilterButton(MessageButton, "email");
        UpdateFilterButton(SystemButton, "system");

        bool anyUnread = false, anyRead = false;
        foreach (var item in items)
        {
            anyUnread |= !item.IsRead;
            anyRead |= item.IsRead;
        }

        if (MarkAsReadyAll_Button != null) MarkAsReadyAll_Button.interactable = !busy && anyUnread;
        if (DeleteReadMsg_Button != null) DeleteReadMsg_Button.interactable = !busy && anyRead;
        foreach (var row in rows) if (row != null) row.SetBusy(busy);
        if (MessageShowScreen != null) MessageShowScreen.SetBusy(busy);
    }

    private void UpdateFilterButton(Button button, string type)
    {
        if (button == null) return;

        button.transition = Selectable.Transition.None;

        if (button.image != null)
        {
            button.image.overrideSprite = null;
            button.image.sprite = selectedType == type
                ? SelectButtonSprite
                : UnSelectButtonSprite;
        }
    }

    private void AllDeleteConfirmButtonOnTap()
    {
        if (busy) return;
        DeleteReadAsync().Forget();
    }

    void CancelButtonOnTap()
    {
        AllDetailConfirmationScreen.SetActive(false);
    }

}
