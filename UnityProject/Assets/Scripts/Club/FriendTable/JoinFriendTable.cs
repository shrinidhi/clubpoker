using System;
using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Game;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinFriendTable : MonoBehaviour
{
    [Header("Keypad")]
    public Transform CodeNumberContent;
    public GameObject CodeNumberButtonPrefab;

    [Header("Entered Code")]
    public Transform EnterCodeTextContent;
    public GameObject EnterCodeTextPrefab;

    [Header("Buttons")]
    public Button CloseButton;

    [Header("Screens")]
    public GameObject FriendTablMenuScreen;

    private const int CODE_LENGTH = 6;
    private readonly List<EnterCodeTextPrefab> codeTextItems = new List<EnterCodeTextPrefab>();
    private readonly List<string> enteredCharacters = new List<string>();
    private readonly string[] buttonSequence =
    {
        "1", "2", "3", "4", "5", "6",
        "7", "8", "9", "Clear", "0", "Undo"
    };

    private bool isJoining;
    private int viewVersion;

    public InputField EnterCodeInputField;
    public Button JoinTable;

    [Header("Keyboard Popup Adjustment")]
    public Canvas ParentCanvas;
    public float KeyboardPadding = 20f;

    private RectTransform popupRect;
    private Vector2 originalPopupPosition;
    private bool popupPositionCached;
    private bool popupMoved;
    public Transform JoinFriendTablePopup;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (JoinTable != null)
            JoinTable.onClick.AddListener(JoinTableOnTap);

        if (EnterCodeInputField != null)
        {
            EnterCodeInputField.contentType = InputField.ContentType.Alphanumeric;
            EnterCodeInputField.characterValidation = InputField.CharacterValidation.Alphanumeric;
            EnterCodeInputField.keyboardType = TouchScreenKeyboardType.ASCIICapable;
            EnterCodeInputField.lineType = InputField.LineType.SingleLine;
            EnterCodeInputField.characterLimit = CODE_LENGTH;
            EnterCodeInputField.onValueChanged.AddListener(OnEnteredCodeChanged);
        }

        if (JoinFriendTablePopup != null)
        {
            popupRect = JoinFriendTablePopup.GetComponent<RectTransform>();

            if (popupRect != null)
            {
                originalPopupPosition = popupRect.anchoredPosition;
                popupPositionCached = true;
            }

            if (ParentCanvas == null)
                ParentCanvas = JoinFriendTablePopup.GetComponentInParent<Canvas>();
        }
    }

    private void Start()
    {
        // Legacy keypad generation is retained below but is no longer called.
        SetLegacyKeypadVisible(false);
        UpdateControls();
    }

    private void OnEnable()
    {
        ++viewVersion;
        ClearEnteredCode();

        if (EnterCodeInputField != null)
            EnterCodeInputField.SetTextWithoutNotify("");

        SetLegacyKeypadVisible(false);
        UpdateControls();
    }

    private void OnDisable()
    {
        ++viewVersion;
        RestorePopupPosition();
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (JoinTable != null)
            JoinTable.onClick.RemoveListener(JoinTableOnTap);

        if (EnterCodeInputField != null)
            EnterCodeInputField.onValueChanged.RemoveListener(OnEnteredCodeChanged);
    }

    private void SetLegacyKeypadVisible(bool visible)
    {
        if (CodeNumberContent != null)
            CodeNumberContent.gameObject.SetActive(visible);

        if (EnterCodeTextContent != null)
            EnterCodeTextContent.gameObject.SetActive(visible);
    }

    private void OnEnteredCodeChanged(string value)
    {
        // Keep letters and numbers, including when text is pasted.
        string digits = "";
        if (!string.IsNullOrEmpty(value))
        {
            foreach (char character in value)
            {
                if (IsCodeCharacter(character))
                    digits += char.ToUpperInvariant(character);

                if (digits.Length == CODE_LENGTH)
                    break;
            }
        }

        if (EnterCodeInputField != null && EnterCodeInputField.text != digits)
            EnterCodeInputField.SetTextWithoutNotify(digits);


        UpdateControls();
    }

    private void JoinTableOnTap()
    {
        if (isJoining)
            return;

        string code = GetEnteredCode();
        if (!IsValidCode(code))
        {

            InformationPrefabScript.Instance.ShowMessage("Please enter a 6-character table code (letters or numbers).");
            return;
        }

        JoinFriendTableAsync().Forget();
    }

    private static bool IsCodeCharacter(char character)
    {
        return (character >= '0' && character <= '9') ||
               (character >= 'A' && character <= 'Z') ||
               (character >= 'a' && character <= 'z');
    }

    private static bool IsValidCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != CODE_LENGTH)
            return false;

        foreach (char character in code)
            if (!IsCodeCharacter(character))
                return false;

        return true;
    }

    private void UpdateControls()
    {
        if (EnterCodeInputField != null)
            EnterCodeInputField.interactable = !isJoining;

        if (JoinTable != null)
            JoinTable.interactable = !isJoining && IsValidCode(GetEnteredCode());

        if (CloseButton != null)
            CloseButton.interactable = !isJoining;
    }

    // Legacy keypad methods are retained for compatibility.
    private void GenerateCodeTextBoxes()
    {
        ClearContent(EnterCodeTextContent);
        codeTextItems.Clear();
        enteredCharacters.Clear();

        if (EnterCodeTextContent == null || EnterCodeTextPrefab == null)
        {
            Debug.LogError("[JoinFriendTable] Enter code content or prefab missing");
            return;
        }

        for (int i = 0; i < CODE_LENGTH; i++)
        {
            GameObject itemObject = Instantiate(EnterCodeTextPrefab, EnterCodeTextContent);
            EnterCodeTextPrefab item = itemObject.GetComponent<EnterCodeTextPrefab>();

            if (item == null)
            {
                Debug.LogError("[JoinFriendTable] EnterCodeTextPrefab component missing");
                Destroy(itemObject);
                continue;
            }

            item.SetText("-");
            codeTextItems.Add(item);
        }
    }

    private void GenerateCodeButtons()
    {
        ClearContent(CodeNumberContent);

        if (CodeNumberContent == null || CodeNumberButtonPrefab == null)
        {
            Debug.LogError("[JoinFriendTable] Button content or prefab missing");
            return;
        }

        foreach (string value in buttonSequence)
        {
            GameObject buttonObject = Instantiate(CodeNumberButtonPrefab, CodeNumberContent);
            CodeNumberButtonPrefab buttonItem = buttonObject.GetComponent<CodeNumberButtonPrefab>();

            if (buttonItem == null)
            {
                Debug.LogError("[JoinFriendTable] CodeNumberButtonPrefab component missing");
                Destroy(buttonObject);
                continue;
            }

            buttonItem.SetData(value, OnCodeButtonClicked);
        }
    }

    private void OnCodeButtonClicked(string value)
    {
        if (isJoining)
            return;

        switch (value)
        {
            case "Clear": ClearEnteredCode(); break;
            case "Undo": UndoLastCharacter(); break;
            default: AddCharacter(value); break;
        }
    }

    private void AddCharacter(string value)
    {
        if (enteredCharacters.Count >= CODE_LENGTH)
            return;

        enteredCharacters.Add(value.ToUpperInvariant());
        RefreshCodeText();
        // Joining is now triggered exclusively by the Join button.
    }

    private void ClearEnteredCode()
    {
        enteredCharacters.Clear();
        RefreshCodeText();
    }

    private void UndoLastCharacter()
    {
        if (enteredCharacters.Count == 0)
            return;

        enteredCharacters.RemoveAt(enteredCharacters.Count - 1);
        RefreshCodeText();
    }

    private void RefreshCodeText()
    {
        for (int i = 0; i < codeTextItems.Count; i++)
            if (codeTextItems[i] != null)
                codeTextItems[i].SetText(i < enteredCharacters.Count ? enteredCharacters[i] : "-");
    }

    public string GetEnteredCode()
    {
        return EnterCodeInputField != null ? EnterCodeInputField.text.Trim() : "";
    }

    private bool IsCurrent(int version)
    {
        return this != null && isActiveAndEnabled && version == viewVersion;
    }

    private async UniTask JoinFriendTableAsync()
    {
        string shareCode = GetEnteredCode().ToUpperInvariant();
        if (isJoining || !IsValidCode(shareCode))
            return;

        int version = viewVersion;
        isJoining = true;
        UpdateControls();
        SetKeypadInteractable(false);


        try
        {
            if (AuthManager.Instance == null || TableJoinHandler.Instance == null)
            {
                await ShowWrongCodeAndReset(Loc.Get("unable_join_table"));
                return;
            }

            JoinByCodeResponse codeResponse = await AuthManager.Instance.JoinByCodeAsync(shareCode);
            if (!IsCurrent(version)) return;

            if (codeResponse == null || string.IsNullOrEmpty(codeResponse.TableId))
            {
                await ShowWrongCodeAndReset(Loc.Get("wrong_code"));
                return;
            }

            string tableId = codeResponse.TableId;
            TableData tableData = await AuthManager.Instance.GetTableDetailAsync(tableId);
            if (!IsCurrent(version)) return;

            if (tableData == null)
            {
                await ShowWrongCodeAndReset(Loc.Get("wrong_code"));
                return;
            }

            TableActiveData activeData = await AuthManager.Instance.GetTableActiveAsync(tableId);
            if (!IsCurrent(version)) return;

            bool handInProgress = activeData != null && activeData.HandInProgress;
            int minBuyIn = tableData.MinBuyIn > 0 ? tableData.MinBuyIn : codeResponse.MinBuyIn;

            if (handInProgress)
            {
                SpectateData spectate = await AuthManager.Instance.SpectateTableAsync(tableId);
                if (!IsCurrent(version)) return;

                if (TableJoinHandler.Instance == null)
                {
                    await ShowWrongCodeAndReset(Loc.Get("unable_join_table"));
                    return;
                }

                // Lobby origin ensures Back and Exit return to the home screen.
                TableContext.EnterFromLobby(tableId, tableData);
                TableJoinHandler.Instance.BeginWatchAndWait(tableId, minBuyIn);
                await AuthManager.Instance.JoinWaitingListAsync(tableId);

                Debug.Log($"[FriendTable] Watch & Wait | Table: {tableId} | State: {spectate?.CurrentState?.GameState}");
                if (IsCurrent(version)) gameObject.SetActive(false);
                return;
            }

            await AuthManager.Instance.BuyInAsync(tableId, minBuyIn);

            try
            {
                await AuthManager.Instance.JoinTableAsync(tableId, minBuyIn);
            }
            catch (Exception e)
            {
                if (e.Message == null || !e.Message.Contains("Already seated"))
                    throw;
            }

            if (!IsCurrent(version)) return;

            if (TableJoinHandler.Instance == null)
            {
                await ShowWrongCodeAndReset(Loc.Get("unable_join_table"));
                return;
            }

            TableContext.EnterFromLobby(tableId, tableData);
            TableJoinHandler.Instance.JoinTable(tableId);

            Debug.Log($"[FriendTable] Joined successfully | Code: {shareCode} | Table: {tableId}");
            gameObject.SetActive(false);
        }
        catch (OperationCanceledException)
        {
            // The object was destroyed while an error message was displayed.
        }
        catch (Exception e)
        {
            Debug.LogError("[JoinFriendTable] Join failed: " + e);
            await ShowWrongCodeAndReset(e.Message);
        }
        finally
        {
            if (this != null)
            {
                isJoining = false;
                if (isActiveAndEnabled)
                {
                    UpdateControls();
                    SetKeypadInteractable(true);
                }
            }
        }
    }

    private async UniTask ShowWrongCodeAndReset(string message)
    {
        int version = viewVersion;
        InformationPrefabScript.Instance.ShowMessage(message);

        Debug.LogWarning("[JoinFriendTable] " + message);

        bool cancelled = await UniTask.Delay(
            TimeSpan.FromSeconds(2),
            cancellationToken: destroyCancellationToken).SuppressCancellationThrow();

        if (cancelled || !IsCurrent(version))
            return;

        ClearEnteredCode();
        if (EnterCodeInputField != null)
            EnterCodeInputField.SetTextWithoutNotify("");
    }

    private void SetKeypadInteractable(bool interactable)
    {
        if (CodeNumberContent == null)
            return;

        for (int i = 0; i < CodeNumberContent.childCount; i++)
        {
            Button button = CodeNumberContent.GetChild(i).GetComponentInChildren<Button>();
            if (button != null)
                button.interactable = interactable;
        }
    }

    private void ClearContent(Transform content)
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    private void CloseButtonOnTap()
    {
        if (isJoining)
            return;

        if (FriendTablMenuScreen != null)
            FriendTablMenuScreen.SetActive(true);

        gameObject.SetActive(false);
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
