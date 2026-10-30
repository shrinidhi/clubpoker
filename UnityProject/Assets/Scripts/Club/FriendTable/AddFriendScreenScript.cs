using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.Core;

public class AddFriendScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public InputField EnterNameInputField;
    public Button SendRequestButton;

    public SendFriendRequestResponse CreatedRequest { get; private set; }
    public event Action<SendFriendRequestResponse> OnFriendRequestSent;

    private string sentUsername;

    public GameObject FriendTableScreen;

    public PlayerByUsernameData FoundPlayer { get; private set; }

    // This event reports a successful lookup, not a sent friend request.
    public event Action<PlayerByUsernameData> OnPlayerFound;

    private bool busy;
    private int viewVersion;

    [Header("Keyboard Popup Adjustment")]
    public Canvas ParentCanvas;
    public float KeyboardPadding = 20f;

    private RectTransform popupRect;
    private Vector2 originalPopupPosition;
    private bool popupPositionCached;
    private bool popupMoved;
    public Transform AddFriendPopup;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (SendRequestButton != null)
            SendRequestButton.onClick.AddListener(SendRequestButtonOnTap);

        if (EnterNameInputField != null)
            EnterNameInputField.onValueChanged.AddListener(OnUsernameChanged);


        if (AddFriendPopup != null)
        {
            popupRect = AddFriendPopup.GetComponent<RectTransform>();

            if (popupRect != null)
            {
                originalPopupPosition = popupRect.anchoredPosition;
                popupPositionCached = true;
            }

            if (ParentCanvas == null)
                ParentCanvas = AddFriendPopup.GetComponentInParent<Canvas>();
        }
    }

    private void OnEnable()
    {
        ++viewVersion;
        FoundPlayer = null;
        CreatedRequest = null;
        sentUsername = null;

        if (EnterNameInputField != null)
            EnterNameInputField.SetTextWithoutNotify("");

        UpdateControls();
    }

    private void OnDisable()
    {
        ++viewVersion;
        RestorePopupPosition();
    }

    private void CloseButtonOnTap()
    {
        if (busy)
            return;
        FriendTableScreen.SetActive(true);
        gameObject.SetActive(false);
    }

    private void OnUsernameChanged(string value)
    {
        FoundPlayer = null;
        CreatedRequest = null;
        UpdateControls();
    }

    private void SendRequestButtonOnTap()
    {
        if (busy)
            return;

        string username = EnterNameInputField != null
            ? EnterNameInputField.text.Trim()
            : "";

        if (string.IsNullOrWhiteSpace(username))
        {
            SetMessage(Loc.Get("friend.enter_username"));
            return;
        }

        if (string.Equals(username, sentUsername, StringComparison.OrdinalIgnoreCase))
        {
            SetMessage(Loc.Get("friend.already_sent"));
            return;
        }

        FindPlayerAsync(username, viewVersion).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask FindPlayerAsync(string username, int version)
    {
        busy = true;
        FoundPlayer = null;
        CreatedRequest = null;
        UpdateControls();

        PlayerByUsernameData player = null;
        SendFriendRequestResponse sentRequest = null;

        try
        {
            var manager = AuthManager.Instance;
            if (manager == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            string myId = manager.Session?.Id;
            var result = await manager.GetPlayerByUsernameAsync(username);

            if (!IsCurrent(version))
                return;

            if (AuthManager.Instance != manager ||
                !string.Equals(myId, manager.Session?.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Your session changed. Reopen this screen and try again.");
            }

            if (!string.IsNullOrWhiteSpace(myId) &&
                string.Equals(myId, result.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetMessage(Loc.Get("friend.not_yourself"));
                return;
            }

            player = result;
            FoundPlayer = player;
            
            // POST uses the ID returned by the username lookup.
            sentRequest = await manager.SendFriendRequestAsync(player.Id);

            if (AuthManager.Instance != manager ||
                !string.Equals(myId, manager.Session?.Id, StringComparison.Ordinal))
                return;

            if (IsCurrent(version))
            {
                CreatedRequest = sentRequest;
                sentUsername = username;
                SetMessage(Loc.Get("friend.request_sent"));
            }
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
                SetMessage(e.Message);

            Debug.LogError("[Add Friend] Request failed: " + e.Message);
        }
        finally
        {
            if (this != null)
            {
                busy = false;
                UpdateControls();
            }
        }

        if (this == null || sentRequest == null)
            return;

        if (IsCurrent(version))
            OnPlayerFound?.Invoke(player);

        OnFriendRequestSent?.Invoke(sentRequest);
    }

    private void UpdateControls()
    {
        if (CloseButton != null)
            CloseButton.interactable = !busy;

        if (EnterNameInputField != null)
            EnterNameInputField.interactable = !busy;

        if (SendRequestButton != null)
        {
            SendRequestButton.interactable =
                !busy &&
                EnterNameInputField != null &&
                !string.IsNullOrWhiteSpace(EnterNameInputField.text) &&
                !string.Equals(
                    EnterNameInputField.text.Trim(), sentUsername,
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SetMessage(string message)
    {
        InformationPrefabScript.Instance.ShowMessage(message);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (SendRequestButton != null)
            SendRequestButton.onClick.RemoveListener(SendRequestButtonOnTap);

        if (EnterNameInputField != null)
            EnterNameInputField.onValueChanged.RemoveListener(OnUsernameChanged);
    }

    private void Update()
    {
        if (popupRect == null || !popupPositionCached)
            return;

        if (TouchScreenKeyboard.visible)
        {
            MovePopupAboveKeyboard();
        }
        else if (popupMoved)
        {
            RestorePopupPosition();
        }
    }

    private void MovePopupAboveKeyboard()
    {
        Rect keyboardArea = TouchScreenKeyboard.area;

        // Fallback if Android does not report keyboard area.
        float keyboardHeight = keyboardArea.height > 0
            ? keyboardArea.height
            : Screen.height * 0.35f;

        float keyboardTop = keyboardArea.height > 0
            ? keyboardArea.yMax
            : Screen.height - keyboardHeight;

        Vector3[] corners = new Vector3[4];
        popupRect.GetWorldCorners(corners);

        float popupBottom = RectTransformUtility.WorldToScreenPoint(
            ParentCanvas != null ? ParentCanvas.worldCamera : null,
            corners[0]
        ).y;

        float overlap = keyboardTop - popupBottom;

        if (overlap <= 0)
        {
            RestorePopupPosition();
            return;
        }

        float scaleFactor = ParentCanvas != null
            ? ParentCanvas.scaleFactor
            : 1f;

        float moveAmount = (overlap + KeyboardPadding) / scaleFactor;

        popupRect.anchoredPosition =
            originalPopupPosition + Vector2.up * moveAmount;

        popupMoved = true;
    }

    private void RestorePopupPosition()
    {
        if (!popupMoved || popupRect == null)
            return;

        popupRect.anchoredPosition = originalPopupPosition;
        popupMoved = false;
    }
}
