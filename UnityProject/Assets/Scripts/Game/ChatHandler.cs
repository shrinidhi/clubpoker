using System;
using System.Collections.Generic;
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
      
        private const int MAX_MESSAGES = 5;
        private const float RATE_LIMIT_WINDOW = 10f;
        private const int MAX_CHAR_LIMIT = 200;

        private readonly Queue<float> _messageTimestamps = new Queue<float>();

        private const string EVENT_PLAYER_CHAT = "player:chat";

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
                SendButton.onClick.AddListener(OnSendClicked);

            if (ChatInputField != null)
                ChatInputField.onSubmit.AddListener(OnReturnPressed);

            if (Back_Button != null)
                Back_Button.onClick.AddListener(Back_ButtonOnTap);

            HideWarning();
        }

        private void Back_ButtonOnTap()
        {
            gameObject.SetActive(false);
        }

        private void OnSendClicked()
        {
            TrySendChat();
        }

        private void OnReturnPressed(string text)
        {
            TrySendChat();
        }

        public void TrySendChat()
        {
            if (SocketManager.Instance == null || !SocketManager.Instance.IsConnected)
            {
                ShowWarning("Socket not connected");
                return;
            }

            string message = ChatInputField.text.Trim();

            if (string.IsNullOrEmpty(message))
            {
                ShowWarning("Message empty");
                return;
            }

            if (message.Length > MAX_CHAR_LIMIT)
            {
                ShowWarning("Max 200 characters allowed");
                return;
            }

            if (IsRateLimited())
            {
                ShowWarning("Too many messages. Please wait...");
                return;
            }

            var payload = new Dictionary<string, object>
            {
                { "tableId", SocketManager.Instance.CurrentTableId },
                { "text", message }
            };

            SocketManager.Instance.Emit(EVENT_PLAYER_CHAT, payload);

            RegisterMessageTimestamp();

            ChatInputField.text = "";
            ChatInputField.ActivateInputField();

            HideWarning();

            Debug.Log($"[Chat] Emit player:chat → {message}");
        }

        public void AppendChatMessage(GameChatPayload payload)
        {
            if (payload == null)
                return;

            bool isMine = payload.playerId == GetMyPlayerId();

            GameObject prefab = isMine
                ? OwnMessagePrefab
                : OtherMessagePrefab;

            if (prefab == null || MessageContainer == null)
            {
                Debug.LogWarning("[Chat] Message prefab/container missing");
                return;
            }

            GameObject messageObj =
                Instantiate(prefab, MessageContainer);

            ChatMessageItemPrefab item =
                messageObj.GetComponent<ChatMessageItemPrefab>();

            if (item != null)
            {
                item.SetData(
                    payload.username,
                    payload.text,
                    FormatTimestamp(payload.timestamp),
                    isMine
                );
            }

            Canvas.ForceUpdateCanvases();

            if (ChatScrollRect != null)
            {
                ChatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        public void HandleServerRateLimit()
        {
            ShowWarning("Chat rate limit reached. Please wait.");
            Debug.LogWarning("[Chat] Server rate limit hit C001");
        }

        private bool IsRateLimited()
        {
            float currentTime = Time.time;

            while (_messageTimestamps.Count > 0 &&
                   currentTime - _messageTimestamps.Peek() > RATE_LIMIT_WINDOW)
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
            if (WarningText != null)
            {
                WarningText.gameObject.SetActive(false);
                WarningText.text = "";
            }
        }

        private string GetMyPlayerId()
        {
            var auth = AuthManager.Instance;

            if (auth == null || auth.Session == null)
                return string.Empty;

            return auth.Session.Id;
        }

        private string FormatTimestamp(string timestamp)
        {
            if (DateTime.TryParse(timestamp, out DateTime time))
            {
                return time.ToLocalTime().ToString("HH:mm");
            }

            return "";
        }
    }
}