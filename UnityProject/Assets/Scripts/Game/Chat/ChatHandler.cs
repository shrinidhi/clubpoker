using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;

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
                    LoadChatHistory().Forget();
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
            gameObject.SetActive(true);
            LoadChatHistory().Forget();
        }

        private void Back_ButtonOnTap()
        {
            gameObject.SetActive(false);
        }

        private void OnSendClicked()
        {
            TrySendChat();
        }

        public void TrySendChat()
        {
            if (ChatInputField == null)
            {
                ShowWarning("Chat input field missing");
                return;
            }

            if (SocketManager.Instance == null)
            {
                ShowWarning("Socket manager not available");
                return;
            }

            if (!SocketManager.Instance.IsConnected)
            {
                ShowWarning("Socket not connected");
                return;
            }

            string tableId = SocketManager.Instance.CurrentTableId;

            if (string.IsNullOrEmpty(tableId))
            {
                ShowWarning("Table is not connected");
                return;
            }

            string message = ChatInputField.text?.Trim();

            if (string.IsNullOrEmpty(message))
            {
                ShowWarning("Message empty");
                return;
            }

            if (message.Length > MAX_CHAR_LIMIT)
            {
                ShowWarning($"Maximum {MAX_CHAR_LIMIT} characters allowed");
                return;
            }

            if (IsRateLimited())
            {
                ShowWarning("Too many messages. Please wait...");
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
            ChatInputField.ActivateInputField();
            ChatInputField.Select();

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
                ShowWarning("Could not load chat history");
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

            messageItem.SetData(payload.username, payload.text, FormatTimestamp(payload.timestamp));
            messageObject.transform.SetAsLastSibling();

            Debug.Log($"[Chat] Message added | Mine: {isMyMessage} | Sender: {payload.username} | Sender ID: {senderPlayerId} | My ID: {myPlayerId}");

            if (shouldScroll) ScrollToBottom();
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
            if (_scrollCoroutine != null) StopCoroutine(_scrollCoroutine);
            _scrollCoroutine = StartCoroutine(ScrollToBottomCoroutine());
        }

        private IEnumerator ScrollToBottomCoroutine()
        {
            yield return null;

            Canvas.ForceUpdateCanvases();

            RectTransform containerRect = MessageContainer as RectTransform;
            if (containerRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

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
            ShowWarning("Chat rate limit reached. Please wait.");
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
    }
}