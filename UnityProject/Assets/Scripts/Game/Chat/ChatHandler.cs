using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;
using UnityEngine.EventSystems;
using ClubPoker.Core;

namespace ClubPoker.Game
{
    public class ChatHandler : MonoBehaviour
    {
        public static ChatHandler Instance { get; private set; }

        [Header("Input UI")]
        public InputField ChatInputField;
        public Button SendButton;
        public Text WarningText;
        public Button Back_Button;

        [Header("Chat Display UI")]
        public Transform MessageContainer;
        public ScrollRect ChatScrollRect;
        public GameObject OwnMessagePrefab;
        public GameObject OtherMessagePrefab;

        [Header("Chat Settings")]
        public int BacklogLimit = 50;

        private const int MAX_MESSAGES = 5;
        private const float RATE_LIMIT_WINDOW = 10f;
        private const int MAX_CHAR_LIMIT = 200;
        private const string EVENT_PLAYER_CHAT = "player:chat";

        private readonly Queue<float> _messageTimestamps = new Queue<float>();
        private readonly HashSet<string> _loadedMessageIds = new HashSet<string>();

        private bool _isLoadingHistory;
        private Coroutine _scrollCoroutine;
        private Coroutine _waitForTableCoroutine;

        public List<string> DefaultMsg;

        public Button DefautlMsgButton;
        public GameObject DefaultMsgPanel;
        public Transform DefaultContent;
        public GameObject DefaultPrefab;
        public Button DefaultCloseButon;

        public Button ClearChatButton;
        public Button MantionButton;
        public GameObject MantionPanel;
        public Transform MantionContent;
        public GameObject MantionPrefab;
        public Button MantionCloseButton;

        public RectTransform Chatpopup;

        private float defaultTop;
        private float defaultBottom;
        private Vector2 defaultOffsetMin;
        private Vector2 defaultOffsetMax;
        private bool popupLayoutCached;
        private bool popupLayoutAdjusted;
        private bool keyboardWarningLogged;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject keyboardDecorView;
        private AndroidJavaObject keyboardContentView;
        private AndroidJavaObject keyboardVisibleFrame;
#endif

        public GameObject ChatScreen;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {

            CachePopupLayout();

            if (ChatInputField != null)
                ChatInputField.shouldHideMobileInput = true;
            if (SendButton != null)
            {
                SendButton.onClick.RemoveListener(OnSendClicked);
                SendButton.onClick.AddListener(OnSendClicked);
            }

            if (Back_Button != null)
            {
                Back_Button.onClick.RemoveListener(Back_ButtonOnTap);
                Back_Button.onClick.AddListener(Back_ButtonOnTap);
            }

            if (MantionButton != null)
            {
                MantionButton.onClick.RemoveListener(MantionButtonOnTap);
                MantionButton.onClick.AddListener(MantionButtonOnTap);
            }

            if (MantionCloseButton != null)
            {
                MantionCloseButton.onClick.RemoveListener(MantionCloseButtonOnTap);
                MantionCloseButton.onClick.AddListener(MantionCloseButtonOnTap);
            }

            if (ClearChatButton != null)
            {
                ClearChatButton.onClick.RemoveListener(ClearChatButtonOnTap);
                ClearChatButton.onClick.AddListener(ClearChatButtonOnTap);
            }

            if (ChatInputField != null)
            {
                ChatInputField.onValueChanged.RemoveListener(OnChatInputValueChanged);
                ChatInputField.onValueChanged.AddListener(OnChatInputValueChanged);
            }

            DefautlMsgButton.onClick.AddListener(DefautlMsgButtonontap);
            DefaultCloseButon.onClick.AddListener(DefaultCloseButonontap);
            HideWarning();
        }



        private void OnEnable()
        {
            if (_waitForTableCoroutine != null) StopCoroutine(_waitForTableCoroutine);
            _waitForTableCoroutine = StartCoroutine(WaitForTableAndLoadMessages());
        }

        private void OnDisable()
        {
            ResetPopup();
            if (_waitForTableCoroutine != null)
            {
                StopCoroutine(_waitForTableCoroutine);
                _waitForTableCoroutine = null;
            }

            if (_scrollCoroutine != null)
            {
                StopCoroutine(_scrollCoroutine);
                _scrollCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            if (SendButton != null) SendButton.onClick.RemoveListener(OnSendClicked);
            if (Back_Button != null) Back_Button.onClick.RemoveListener(Back_ButtonOnTap);
            if (ChatInputField != null)
                ChatInputField.onValueChanged.RemoveListener(OnChatInputValueChanged);
            if (DefautlMsgButton != null)
                DefautlMsgButton.onClick.RemoveListener(DefautlMsgButtonontap);
            if (DefaultCloseButon != null)
                DefaultCloseButon.onClick.RemoveListener(DefaultCloseButonontap);
            if (MantionButton != null)
                MantionButton.onClick.RemoveListener(MantionButtonOnTap);
            if (MantionCloseButton != null)
                MantionCloseButton.onClick.RemoveListener(MantionCloseButtonOnTap);
            if (ClearChatButton != null)
                ClearChatButton.onClick.RemoveListener(ClearChatButtonOnTap);
#if UNITY_ANDROID && !UNITY_EDITOR
            ReleaseKeyboardObjects();
#endif
            if (Instance == this) Instance = null;
        }

        private IEnumerator WaitForTableAndLoadMessages()
        {
            float waitedTime = 0f;
            const float maximumWaitTime = 10f;

            while (waitedTime < maximumWaitTime)
            {
                if (SocketManager.Instance != null && !string.IsNullOrEmpty(SocketManager.Instance.CurrentTableId))
                {
                    //LoadChatHistory().Forget();
                    _waitForTableCoroutine = null;
                    yield break;
                }

                waitedTime += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }

            _waitForTableCoroutine = null;
        }

        public void OpenChat()
        {
            CachePopupLayout();
            ResetPopup();
            ChatScreen.SetActive(true);
            LoadChatHistory().Forget();
            ScrollToBottom();
        }

        private void Back_ButtonOnTap()
        {
            if (ChatInputField != null)
                ChatInputField.DeactivateInputField();

            ResetPopup();
            ChatScreen.SetActive(false);
        }

        private void OnSendClicked()
        {
            TrySendChat();
        }

        public void TrySendChat()
        {
            if (ChatInputField == null)
            {
                ShowWarning(Loc.Get("something_went_wrong"));
                return;
            }

            if (SocketManager.Instance == null)
            {
                ShowWarning(Loc.Get("something_went_wrong"));
                return;
            }

            if (!SocketManager.Instance.IsConnected)
            {
                ShowWarning(Loc.Get("chat_not_connected"));
                return;
            }

            string tableId = SocketManager.Instance.CurrentTableId;

            if (string.IsNullOrEmpty(tableId))
            {
                ShowWarning(Loc.Get("chat_not_connected"));
                return;
            }

            string message = ChatInputField.text?.Trim();

            if (string.IsNullOrEmpty(message))
            {
                ShowWarning(Loc.Get("chat_message_empty"));
                return;
            }

            if (message.Length > MAX_CHAR_LIMIT)
            {
                ShowWarning(Loc.Get("chat_max_chars", ("max", MAX_CHAR_LIMIT)));
                return;
            }

            if (IsRateLimited())
            {
                ShowWarning(Loc.Get("chat_too_many"));
                return;
            }

            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "tableId", tableId },
                { "text", message }
            };

            SocketManager.Instance.Emit(EVENT_PLAYER_CHAT, payload);
            RegisterMessageTimestamp();

            ChatInputField.text = "";
            ChatInputField.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
            HideWarning();
            Debug.Log($"[Chat] player:chat emitted | Table ID: {tableId} | Message: {message}");
        }

        public async UniTaskVoid LoadChatHistory()
        {
            if (_isLoadingHistory)
            {
                Debug.Log("[Chat] History is already loading");
                return;
            }

            if (AuthManager.Instance == null)
            {
                Debug.LogError("[Chat] AuthManager.Instance is null");
                return;
            }

            if (SocketManager.Instance == null)
            {
                Debug.LogError("[Chat] SocketManager.Instance is null");
                return;
            }

            string tableId = SocketManager.Instance.CurrentTableId;

            if (string.IsNullOrEmpty(tableId))
            {
                Debug.LogWarning("[Chat] Current table ID is empty");
                return;
            }

            _isLoadingHistory = true;

            try
            {
                List<GameChatPayload> messages = await AuthManager.Instance.GetTableChatMessagesAsync(tableId, BacklogLimit);

                if (this == null) return;

                ClearMessages();

                if (messages != null && messages.Count > 0)
                {
                    for (int index = messages.Count - 1; index >= 0; index--)
                    {
                        AppendChatMessageInternal(messages[index], false);
                    }
                }

                ScrollToBottom();
                HideWarning();

                Debug.Log($"[Chat] History loaded successfully. Count: {messages?.Count ?? 0}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Chat] History load failed: {e.Message}");
                ShowWarning(Loc.Get("chat_history_failed"));
            }
            finally
            {
                _isLoadingHistory = false;
            }
        }

        public void AppendChatMessage(GameChatPayload payload)
        {
            AppendChatMessageInternal(payload, true);
        }
        private void OnChatInputValueChanged(string text)
        {
            if (MantionPanel == null)
                return;

            bool shouldOpenMention = IsTypingMention(text);

            if (shouldOpenMention)
            {
                if (!MantionPanel.activeSelf)
                {
                    LoadMentionPlayers();
                    MantionPanel.SetActive(true);
                }
            }
            else
            {
                MantionPanel.SetActive(false);
            }
        }

        private bool IsTypingMention(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int lastAtIndex = text.LastIndexOf('@');

            if (lastAtIndex < 0)
                return false;

            string textAfterAt = text.Substring(lastAtIndex + 1);

            return !textAfterAt.Contains(" ") &&
                   !textAfterAt.Contains("\n") &&
                   !textAfterAt.Contains("\t");
        }
        private void AppendChatMessageInternal(GameChatPayload payload, bool shouldScroll)
        {
            if (payload == null)
            {
                Debug.LogWarning("[Chat] Payload is null");
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.text))
            {
                Debug.LogWarning("[Chat] Empty chat message ignored");
                return;
            }

            if (!string.IsNullOrEmpty(payload.id))
            {
                if (!_loadedMessageIds.Add(payload.id))
                {
                    Debug.Log($"[Chat] Duplicate message ignored: {payload.id}");
                    return;
                }
            }

            string myPlayerId = GetMyPlayerId();
            string senderPlayerId = payload.EffectiveSenderId;

            bool isMyMessage = !string.IsNullOrEmpty(myPlayerId) &&
                               !string.IsNullOrEmpty(senderPlayerId) &&
                               string.Equals(myPlayerId, senderPlayerId, StringComparison.OrdinalIgnoreCase);

            GameObject selectedPrefab = isMyMessage ? OwnMessagePrefab : OtherMessagePrefab;

            if (selectedPrefab == null)
            {
                Debug.LogWarning(isMyMessage ? "[Chat] OwnMessagePrefab is missing" : "[Chat] OtherMessagePrefab is missing");
                return;
            }

            if (MessageContainer == null)
            {
                Debug.LogWarning("[Chat] MessageContainer is missing");
                return;
            }

            GameObject messageObject = Instantiate(selectedPrefab, MessageContainer);
            ChatMessageItemPrefab messageItem = messageObject.GetComponent<ChatMessageItemPrefab>();

            if (messageItem == null)
            {
                Debug.LogWarning("[Chat] ChatMessageItemPrefab component is missing on message prefab");
                Destroy(messageObject);
                return;
            }

            messageItem.SetData(payload.username, payload.text, FormatTimestamp(payload.timestamp), isMyMessage, payload.Avatar);
            messageObject.transform.SetAsLastSibling();

            Debug.Log($"[Chat] Message added | Mine: {isMyMessage} | Sender: {payload.username} | Sender ID: {senderPlayerId} | My ID: {myPlayerId}");


            if (shouldScroll && ChatScreen.activeInHierarchy)
                ScrollToBottom();
        }

        public void ClearMessages()
        {
            _loadedMessageIds.Clear();

            if (MessageContainer == null) return;

            for (int index = MessageContainer.childCount - 1; index >= 0; index--)
            {
                Transform child = MessageContainer.GetChild(index);
                if (child != null) Destroy(child.gameObject);
            }
        }

        private void ScrollToBottom()
        {
            if (!ChatScreen.activeInHierarchy)
                return;

            if (_scrollCoroutine != null)
                StopCoroutine(_scrollCoroutine);

            _scrollCoroutine = StartCoroutine(ScrollToBottomCoroutine());
        }

        private IEnumerator ScrollToBottomCoroutine()
        {
            yield return null;

            if (!isActiveAndEnabled || !ChatScreen.activeInHierarchy)
            {
                _scrollCoroutine = null;
                yield break;
            }

            Canvas.ForceUpdateCanvases();

            RectTransform containerRect = MessageContainer as RectTransform;

            if (containerRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            Canvas.ForceUpdateCanvases();

            if (ChatScrollRect != null)
            {
                ChatScrollRect.StopMovement();
                ChatScrollRect.verticalNormalizedPosition = 0f;
            }

            _scrollCoroutine = null;
        }

        public void HandleServerRateLimit()
        {
            ShowWarning(Loc.Get("chat_too_many"));
            Debug.LogWarning("[Chat] Server rate limit hit C001");
        }

        private bool IsRateLimited()
        {
            float currentTime = Time.time;

            while (_messageTimestamps.Count > 0 && currentTime - _messageTimestamps.Peek() > RATE_LIMIT_WINDOW)
            {
                _messageTimestamps.Dequeue();
            }

            return _messageTimestamps.Count >= MAX_MESSAGES;
        }

        private void RegisterMessageTimestamp()
        {
            _messageTimestamps.Enqueue(Time.time);
        }

        private void ShowWarning(string message)
        {
            if (WarningText != null)
            {
                WarningText.gameObject.SetActive(true);
                WarningText.text = message;
            }

            Debug.LogWarning($"[Chat Warning] {message}");
        }

        private void HideWarning()
        {
            if (WarningText == null) return;

            WarningText.gameObject.SetActive(false);
            WarningText.text = "";
        }

        private string GetMyPlayerId()
        {
            return AuthManager.Instance?.Session?.Id ?? string.Empty;
        }

        private string FormatTimestamp(string timestamp)
        {
            if (string.IsNullOrEmpty(timestamp)) return "";

            if (DateTimeOffset.TryParse(timestamp, out DateTimeOffset parsedTime))
                return parsedTime.ToLocalTime().ToString("HH:mm");

            if (DateTime.TryParse(timestamp, out DateTime fallbackTime))
                return fallbackTime.ToLocalTime().ToString("HH:mm");

            return "";
        }


        public void SendDefaultMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (ChatInputField != null)
                ChatInputField.text = message;

            TrySendChat();
        }

        void DefautlMsgButtonontap()
        {
            LoadDefaultMessages();
            DefaultMsgPanel.SetActive(true);
        }


        void DefaultCloseButonontap()
        {
            DefaultMsgPanel.SetActive(false);
        }

        public void LoadDefaultMessages()
        {
            foreach (Transform child in DefaultContent)
                Destroy(child.gameObject);

            foreach (string msg in DefaultMsg)
            {
                GameObject obj = Instantiate(DefaultPrefab, DefaultContent);
                obj.GetComponent<DefaultMsgPrefab>().DefaultMsg.text = msg;
            }
        }


        private void MantionButtonOnTap()
        {
            LoadMentionPlayers();

            if (MantionPanel != null)
                MantionPanel.SetActive(true);
        }

        private void MantionCloseButtonOnTap()
        {
            if (MantionPanel != null)
                MantionPanel.SetActive(false);
        }

        private void LoadMentionPlayers()
        {
            ClearMentionPlayers();

            if (MantionContent == null || MantionPrefab == null)
            {
                Debug.LogError("[Mention] Mention content or prefab missing");
                return;
            }

            if (GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState == null ||
                GameStateManager.Instance.CurrentState.Players == null)
            {
                Debug.LogWarning("[Mention] Player list not available");
                return;
            }

            string myPlayerId = GetMyPlayerId();

            foreach (GamePlayer player in GameStateManager.Instance.CurrentState.Players)
            {
                if (player == null)
                    continue;

                if (string.IsNullOrEmpty(player.Id))
                    continue;

                if (string.Equals(
                    player.Id,
                    myPlayerId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(player.Username))
                    continue;

                GameObject obj = Instantiate(MantionPrefab, MantionContent);
                MationPrefab prefab = obj.GetComponent<MationPrefab>();

                if (prefab != null)
                    prefab.SetData(player.Username, OnMentionPlayerSelected);
                else
                    Destroy(obj);
            }
        }

        private void OnMentionPlayerSelected(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || ChatInputField == null)
                return;

            string currentText = ChatInputField.text ?? "";
            int lastAtIndex = currentText.LastIndexOf('@');

            if (lastAtIndex >= 0)
                currentText = currentText.Substring(0, lastAtIndex);

            if (!string.IsNullOrEmpty(currentText) && !currentText.EndsWith(" "))
                currentText += " ";

            ChatInputField.text = currentText + "@" + username + " ";
            ChatInputField.caretPosition = ChatInputField.text.Length;
            ChatInputField.ActivateInputField();
            ChatInputField.Select();

            if (MantionPanel != null)
                MantionPanel.SetActive(false);
        }

        private void ClearMentionPlayers()
        {
            if (MantionContent == null)
                return;

            for (int i = MantionContent.childCount - 1; i >= 0; i--)
                Destroy(MantionContent.GetChild(i).gameObject);
        }

        private void ClearChatButtonOnTap()
        {
            ClearMessages();

            if (ChatInputField != null)
                ChatInputField.text = "";

            HideWarning();

            if (ChatScrollRect != null)
                ChatScrollRect.verticalNormalizedPosition = 1f;

            Debug.Log("[Chat] Local chat cleared");
        }


        private void CachePopupLayout()
        {
            if (popupLayoutCached || Chatpopup == null)
                return;

            defaultOffsetMin = Chatpopup.offsetMin;
            defaultOffsetMax = Chatpopup.offsetMax;
            defaultTop = -defaultOffsetMax.y;
            defaultBottom = defaultOffsetMin.y;
            popupLayoutCached = true;
        }

        // moveUp is the keyboard overlap in screen pixels, not Canvas units.
        private void MovePopup(float moveUp)
        {
            CachePopupLayout();

            if (!popupLayoutCached ||
                !(Chatpopup.parent is RectTransform parent))
                return;

            Canvas canvas = Chatpopup.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            Camera camera = rootCanvas != null &&
                            rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            Vector2 screenPoint = new Vector2(Screen.width * 0.5f, moveUp);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPoint, camera, out Vector2 keyboardTop))
                return;

            float anchorBottom =
                parent.rect.yMin + parent.rect.height * Chatpopup.anchorMin.y;
            float anchorTop =
                parent.rect.yMin + parent.rect.height * Chatpopup.anchorMax.y;

            float originalBottom = anchorBottom + defaultBottom;
            float originalTop = anchorTop - defaultTop;

            // Keep the original top edge and shrink only the covered bottom.
            float desiredBottom = Mathf.Max(originalBottom, keyboardTop.y);
            desiredBottom = Mathf.Min(desiredBottom, originalTop - 1f);

            Vector2 min = defaultOffsetMin;
            min.y = desiredBottom - anchorBottom;

            if ((Chatpopup.offsetMin - min).sqrMagnitude < 0.01f &&
                (Chatpopup.offsetMax - defaultOffsetMax).sqrMagnitude < 0.01f)
                return;

            Chatpopup.offsetMin = min;
            Chatpopup.offsetMax = defaultOffsetMax;
            popupLayoutAdjusted = true;
            RefreshPopupLayout();
        }

        private void Update()
        {
            // Retained for existing ChatHandler lifecycle compatibility.
        }

        private void LateUpdate()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (Chatpopup == null ||
                ChatScreen == null ||
                !ChatScreen.activeInHierarchy)
            {
                ResetPopup();
                return;
            }

            float overlap = GetKeyboardOverlapPixels();

            if (overlap > 1f)
                MovePopup(overlap);
            else
                ResetPopup();
#endif
        }

        private float GetKeyboardOverlapPixels()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (keyboardDecorView == null)
                {
                    using (var unityPlayer =
                           new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity =
                           unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                    {
                        keyboardDecorView =
                            window.Call<AndroidJavaObject>("getDecorView");
                    }

                    // android.R.id.content represents the drawable app area.
                    keyboardContentView =
                        keyboardDecorView.Call<AndroidJavaObject>(
                            "findViewById", 16908290);
                    keyboardVisibleFrame =
                        new AndroidJavaObject("android.graphics.Rect");
                }

                if (keyboardContentView != null)
                {
                    keyboardDecorView.Call(
                        "getWindowVisibleDisplayFrame", keyboardVisibleFrame);

                    int contentHeight = keyboardContentView.Call<int>("getHeight");
                    int visibleBottom = keyboardVisibleFrame.Get<int>("bottom");
                    int contentTop = GetAndroidViewScreenTop(keyboardContentView);
                    float hiddenPixels =
                        Mathf.Max(0, contentTop + contentHeight - visibleBottom);

                    // Ignore navigation/status bars when the keyboard is closed.
                    bool keyboardOpen = TouchScreenKeyboard.visible ||
                        hiddenPixels > contentHeight * 0.15f;

                    if (!keyboardOpen || contentHeight <= 0)
                        return 0f;

                    // If Android already resized the app, hiddenPixels is zero:
                    // no second keyboard offset is applied.
                    return Mathf.Clamp(
                        hiddenPixels * Screen.height / contentHeight,
                        0f, Screen.height);
                }
            }
            catch (Exception e)
            {
                if (!keyboardWarningLogged)
                {
                    keyboardWarningLogged = true;
                    Debug.LogWarning(
                        "[Chat] Native keyboard measurement failed: " + e.Message);
                }

                ReleaseKeyboardObjects();
            }

            return TouchScreenKeyboard.visible
                ? Mathf.Clamp(TouchScreenKeyboard.area.height, 0f, Screen.height)
                : 0f;
#elif UNITY_IOS && !UNITY_EDITOR
            return TouchScreenKeyboard.visible
                ? Mathf.Clamp(TouchScreenKeyboard.area.yMax, 0f, Screen.height)
                : 0f;
#else
            return 0f;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int GetAndroidViewScreenTop(AndroidJavaObject view)
        {
            IntPtr positions = AndroidJNI.NewIntArray(2);

            try
            {
                IntPtr method = AndroidJNIHelper.GetMethodID(
                    view.GetRawClass(), "getLocationOnScreen", "([I)V");

                AndroidJNI.CallVoidMethod(
                    view.GetRawObject(),
                    method,
                    new[] { new jvalue { l = positions } });

                return AndroidJNI.FromIntArray(positions)[1];
            }
            finally
            {
                AndroidJNI.DeleteLocalRef(positions);
            }
        }

        private void ReleaseKeyboardObjects()
        {
            keyboardVisibleFrame?.Dispose();
            keyboardContentView?.Dispose();
            keyboardDecorView?.Dispose();

            keyboardVisibleFrame = null;
            keyboardContentView = null;
            keyboardDecorView = null;
        }
#endif

        private void ResetPopup()
        {
            if (!popupLayoutCached || Chatpopup == null || !popupLayoutAdjusted)
                return;

            Chatpopup.offsetMin = defaultOffsetMin;
            Chatpopup.offsetMax = defaultOffsetMax;
            popupLayoutAdjusted = false;
            RefreshPopupLayout();
        }

        private void RefreshPopupLayout()
        {
            if (Chatpopup == null || !Chatpopup.gameObject.activeInHierarchy)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Chatpopup);
            Canvas.ForceUpdateCanvases();

            if (ChatScrollRect != null)
            {
                ChatScrollRect.StopMovement();
                ChatScrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
