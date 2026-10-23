using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Auth;
using ClubPoker.Core;

public class FriendRequestScreenScript : MonoBehaviour
{
    public Button CLoseButton;
    public Button IncomingReq_Button;
    public Button OutGoingReq_Button;
    public Sprite SelectButtonSprite;
    public Sprite UnSelectButtonSprite;
    public GameObject IncomingReq_Panel;
    public GameObject OutGoingReq_Panel;
    public Transform IncomingReq_Content;
    public Transform OutGoingReq_Content;
    public GameObject IncomingPrefab;
    public GameObject OutgoingPrefab;

    [Header("Optional")]
    public Text IncomingMessageText;
    public Text OutgoingMessageText;
    public GameObject IncomingNoDataAvailable;
    public GameObject OutgoingNoDataAvailable;

    private readonly List<GameObject> incomingRows = new List<GameObject>();
    private readonly List<GameObject> outgoingRows = new List<GameObject>();
    private int viewVersion;
    private readonly HashSet<string> pendingRequestIds = new HashSet<string>();

    public event Action OnFriendRequestsChanged;

    private void Awake()
    {
        if (CLoseButton != null)
            CLoseButton.onClick.AddListener(CloseButtonOnTap);

        if (IncomingReq_Button != null)
            IncomingReq_Button.onClick.AddListener(IncomingReqButtonOnTap);

        if (OutGoingReq_Button != null)
            OutGoingReq_Button.onClick.AddListener(OutgoingReqButtonOnTap);
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ClubSocketHandler.OnFriendsChanged += OnSocketFriendsChanged;
        SetSelectedTab(true);
        Reload();
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        ++viewVersion;
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        int version = ++viewVersion;

        ClearRows(incomingRows);
        ClearRows(outgoingRows);

        SetActive(IncomingNoDataAvailable, false);
        SetActive(OutgoingNoDataAvailable, false);
        SetMessage(IncomingMessageText, "Loading...");
        SetMessage(OutgoingMessageText, "Loading...");

        LoadRequestsAsync(version).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadRequestsAsync(int version)
    {
        // Both lists load independently so one failed request does not hide the other.
        await UniTask.WhenAll(
            LoadDirectionAsync("incoming", version),
            LoadDirectionAsync("outgoing", version));

        await RefreshLayoutAsync(version);
    }

    private async UniTask LoadDirectionAsync(string direction, int version)
    {
        bool incoming = direction == "incoming";
        Transform content = incoming ? IncomingReq_Content : OutGoingReq_Content;
        GameObject prefab = incoming ? IncomingPrefab : OutgoingPrefab;
        List<GameObject> rows = incoming ? incomingRows : outgoingRows;
        Text message = incoming ? IncomingMessageText : OutgoingMessageText;
        GameObject noData = incoming ? IncomingNoDataAvailable : OutgoingNoDataAvailable;

        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            if (content == null || prefab == null)
                throw new InvalidOperationException("Assign the request Content and Prefab.");

            if (incoming && prefab.GetComponent<Incomingreqprefab>() == null)
                throw new InvalidOperationException("Incomingreqprefab component is missing.");

            if (!incoming && prefab.GetComponent<OutGoingReqPrefab>() == null)
                throw new InvalidOperationException("OutGoingReqPrefab component is missing.");

            var response = await AuthManager.Instance.GetFriendRequestsAsync(direction);

            if (!IsCurrent(version))
                return;

            var loadedIds = new HashSet<string>();

            if (response.Requests != null)
            {
                foreach (var request in response.Requests)
                {
                    if (request == null ||
                        string.IsNullOrWhiteSpace(request.Id) ||
                        !loadedIds.Add(request.Id))
                        continue;

                    GameObject instance = Instantiate(prefab, content);
                    rows.Add(instance);

                    if (incoming)
                    {
                        var row = instance.GetComponent<Incomingreqprefab>();
                        row.SetData(request, IncomingActionOnTap);
                        row.SetBusy(pendingRequestIds.Contains(request.Id));
                    }
                    else
                    {
                        var row = instance.GetComponent<OutGoingReqPrefab>();
                        row.SetData(request, OutgoingCancelOnTap);
                        row.SetBusy(pendingRequestIds.Contains(request.Id));
                    }

                    instance.SetActive(true);
                }
            }

            bool empty = rows.Count == 0;
            SetActive(noData, empty);
            SetMessage(
                message,
                empty && noData == null
                    ? incoming ? Loc.Get("friend.no_incoming") : Loc.Get("friend.no_outgoing")
                    : "");

            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            ClearRows(rows);
            SetActive(noData, false);
            SetMessage(message, "Failed to load requests.");

            Debug.LogError("[Friend Requests] " + direction + ": " + e.Message);
            await RefreshLayoutAsync(version);
        }
    }

    private void IncomingActionOnTap(Incomingreqprefab row, bool accept)
    {
        if (row == null || row.Data == null ||
            string.IsNullOrWhiteSpace(row.Data.Id) ||
            !pendingRequestIds.Add(row.Data.Id))
            return;

        row.SetBusy(true);
        SetMessage(IncomingMessageText, accept ? Loc.Get("friend.accepting") : Loc.Get("friend.rejecting"));
        UpdateRequestAsync(row.Data.Id, true, accept).Forget();
    }

    private void OutgoingCancelOnTap(OutGoingReqPrefab row)
    {
        if (row == null || row.Data == null ||
            string.IsNullOrWhiteSpace(row.Data.Id) ||
            !pendingRequestIds.Add(row.Data.Id))
            return;

        row.SetBusy(true);
        SetMessage(OutgoingMessageText, "Cancelling request...");
        UpdateRequestAsync(row.Data.Id, false, false).Forget();
    }

    private async UniTask UpdateRequestAsync(string requestId, bool incoming, bool accept)
    {
        bool succeeded = false;
        string sessionId = AuthManager.Instance != null
            ? AuthManager.Instance.Session?.Id
            : null;

        try
        {
            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            if (!incoming)
                await AuthManager.Instance.CancelFriendRequestAsync(requestId);
            else if (accept)
                await AuthManager.Instance.AcceptFriendRequestAsync(requestId);
            else
                await AuthManager.Instance.RejectFriendRequestAsync(requestId);

            succeeded = true;
        }
        catch (Exception e)
        {
            if (this != null && isActiveAndEnabled && IsSameSession(sessionId))
                SetMessage(incoming ? IncomingMessageText : OutgoingMessageText, e.Message);

            Debug.LogError("[Friend Requests] Action failed: " + e.Message);
        }
        finally
        {
            if (this != null)
            {
                pendingRequestIds.Remove(requestId);
                UpdatePendingButtons();
            }
        }

        if (this == null || !succeeded || !IsSameSession(sessionId))
            return;

        // Reload invalidates any older list response and refreshes empty states.
        if (isActiveAndEnabled)
            Reload();

        OnFriendRequestsChanged?.Invoke();
    }

    private static bool IsSameSession(string sessionId)
    {
        return AuthManager.Instance != null &&
               string.Equals(AuthManager.Instance.Session?.Id, sessionId, StringComparison.Ordinal);
    }

    private void UpdatePendingButtons()
    {
        foreach (var instance in incomingRows)
        {
            if (instance == null)
                continue;

            var row = instance.GetComponent<Incomingreqprefab>();
            if (row != null && row.Data != null)
                row.SetBusy(pendingRequestIds.Contains(row.Data.Id));
        }

        foreach (var instance in outgoingRows)
        {
            if (instance == null)
                continue;

            var row = instance.GetComponent<OutGoingReqPrefab>();
            if (row != null && row.Data != null)
                row.SetBusy(pendingRequestIds.Contains(row.Data.Id));
        }
    }

    private void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private void IncomingReqButtonOnTap()
    {
        SetSelectedTab(true);
        RefreshLayoutAsync(viewVersion).Forget();
    }

    private void OutgoingReqButtonOnTap()
    {
        SetSelectedTab(false);
        RefreshLayoutAsync(viewVersion).Forget();
    }

    private void SetSelectedTab(bool incoming)
    {
        SetActive(IncomingReq_Panel, incoming);
        SetActive(OutGoingReq_Panel, !incoming);

        SetButtonSprite(IncomingReq_Button, incoming);
        SetButtonSprite(OutGoingReq_Button, !incoming);
    }

    private void SetButtonSprite(Button button, bool selected)
    {
        if (button == null || button.image == null)
            return;

        Sprite sprite = selected ? SelectButtonSprite : UnSelectButtonSprite;

        if (sprite == null)
            return;

        button.image.sprite = sprite;

        if (button.transition == Selectable.Transition.SpriteSwap)
        {
            SpriteState state = button.spriteState;
            state.highlightedSprite = sprite;
            state.pressedSprite = sprite;
            state.selectedSprite = sprite;
            state.disabledSprite = sprite;
            button.spriteState = state;
        }
    }

    private async UniTask RefreshLayoutAsync(int version)
    {
        await UniTask.NextFrame();

        if (!IsCurrent(version))
            return;

        Canvas.ForceUpdateCanvases();
        RebuildContent(IncomingReq_Content);
        RebuildContent(OutGoingReq_Content);
        Canvas.ForceUpdateCanvases();
    }

    private static void RebuildContent(Transform content)
    {
        if (content == null || !content.gameObject.activeInHierarchy)
            return;

        var layouts = content.GetComponentsInChildren<RectTransform>(false);

        for (int i = layouts.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layouts[i]);

        Transform parent = content.parent;

        while (parent != null)
        {
            if (parent is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            if (parent.GetComponent<Canvas>() != null)
                break;

            parent = parent.parent;
        }
    }

    private void ClearRows(List<GameObject> rows)
    {
        foreach (var row in rows)
        {
            if (row == null)
                continue;

            row.SetActive(false);
            Destroy(row);
        }

        rows.Clear();
    }

    private static void SetMessage(Text target, string message)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = message;
        target.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private void OnSocketFriendsChanged()
    {
        if (isActiveAndEnabled)
            Reload();
    }

    private void OnDestroy()
    {
        ClubSocketHandler.OnFriendsChanged -= OnSocketFriendsChanged;
        if (CLoseButton != null)
            CLoseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (IncomingReq_Button != null)
            IncomingReq_Button.onClick.RemoveListener(IncomingReqButtonOnTap);

        if (OutGoingReq_Button != null)
            OutGoingReq_Button.onClick.RemoveListener(OutgoingReqButtonOnTap);
    }
}
