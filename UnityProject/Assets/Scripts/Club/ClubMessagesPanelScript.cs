using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class MessagesPanelScript : MonoBehaviour
{
    [Header("Buttons")]
    public Button Back_Button;
    public Button MarkAllRead_Button;
    public Button DeleteRead_Button;

    [Header("List")]
    public Transform Message_Content;     // ScrollRect content (vertical layout)
    public GameObject MessagePrefab;
    public GameObject EmptyState;          // shown when no messages

    private readonly List<ClubMessageData> _messages = new();
    private readonly List<MessagePrefabScript> _rows = new();

    private void Start()
    {
        if (Back_Button != null)        Back_Button.onClick.AddListener(BackButtonOnTap);
        if (MarkAllRead_Button != null) MarkAllRead_Button.onClick.AddListener(MarkAllReadOnTap);
        if (DeleteRead_Button != null)  DeleteRead_Button.onClick.AddListener(DeleteReadOnTap);
    }

    private void OnEnable()
    {
        LoadMessages().Forget();
    }

    // ── Data source ───────────────────────────────────────────────────────

    public async UniTaskVoid LoadMessages()
    {
        // TODO: replace with AuthManager messages API call, e.g.
        // var list = await AuthManager.Instance.GetClubMessagesAsync(ClubContext.ClubId);
        // SetMessages(list);
        await UniTask.CompletedTask;
        Populate();
    }

    /// <summary>Inject messages from an API response.</summary>
    public void SetMessages(List<ClubMessageData> messages)
    {
        _messages.Clear();
        if (messages != null)
            _messages.AddRange(messages);
        Populate();
    }

    // ── List building ─────────────────────────────────────────────────────

    private void Populate()
    {
        ClearRows();

        foreach (ClubMessageData msg in _messages)
        {
            GameObject obj = Instantiate(MessagePrefab, Message_Content);
            MessagePrefabScript row = obj.GetComponent<MessagePrefabScript>();
            row.Setup(msg, OnMessageTapped);
            _rows.Add(row);
        }

        if (EmptyState != null)
            EmptyState.SetActive(_messages.Count == 0);
    }

    private void ClearRows()
    {
        _rows.Clear();

        for (int i = Message_Content.childCount - 1; i >= 0; i--)
            Destroy(Message_Content.GetChild(i).gameObject);
    }

    // ── Actions ───────────────────────────────────────────────────────────

    private void OnMessageTapped(ClubMessageData msg)
    {
        if (msg == null || msg.IsRead)
            return;

        msg.IsRead = true;
        // TODO: AuthManager.Instance.MarkMessageReadAsync(msg.MessageId).Forget();

        // Update read/unread visuals without rebuilding the list.
        foreach (MessagePrefabScript r in _rows)
            r.Refresh();
    }

    private void MarkAllReadOnTap()
    {
        bool changed = false;
        foreach (ClubMessageData msg in _messages)
        {
            if (!msg.IsRead)
            {
                msg.IsRead = true;
                changed = true;
            }
        }

        if (!changed)
            return;

        // TODO: AuthManager.Instance.MarkAllMessagesReadAsync(ClubContext.ClubId).Forget();

        foreach (MessagePrefabScript row in _rows)
            row.Refresh();
    }

    private void DeleteReadOnTap()
    {
        int removed = _messages.RemoveAll(m => m.IsRead);
        if (removed == 0)
            return;

        // TODO: AuthManager.Instance.DeleteReadMessagesAsync(ClubContext.ClubId).Forget();

        Populate();
    }

    private void BackButtonOnTap()
    {
        gameObject.SetActive(false);
    }
}
