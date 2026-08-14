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

    [Header("UI")]
    public TextMeshProUGUI ErrorText;

    private const int CODE_LENGTH = 6;

    private readonly List<EnterCodeTextPrefab> codeTextItems = new List<EnterCodeTextPrefab>();
    private readonly List<string> enteredCharacters = new List<string>();

    private readonly string[] buttonSequence =
    {
        "1", "2", "3", "4", "5", "6",
        "7", "8", "9","Clear","0", "Undo"
    };

    private bool isJoining;

    private void Start()
    {
        GenerateCodeTextBoxes();
        GenerateCodeButtons();
        RefreshCodeText();

        if (CloseButton != null)
        {
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
            CloseButton.onClick.AddListener(CloseButtonOnTap);
        }

        ClearError();
    }

    private void OnEnable()
    {
        isJoining = false;
        ClearEnteredCode();
        ClearError();
        SetKeypadInteractable(true);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
    }

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

        ClearError();

        switch (value)
        {
            case "Clear":
                ClearEnteredCode();
                break;

            case "Undo":
                UndoLastCharacter();
                break;

            default:
                AddCharacter(value);
                break;
        }
    }

    private void AddCharacter(string value)
    {
        if (enteredCharacters.Count >= CODE_LENGTH)
            return;

        enteredCharacters.Add(value.ToUpperInvariant());
        RefreshCodeText();

        if (enteredCharacters.Count == CODE_LENGTH)
            JoinFriendTableAsync().Forget();
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
            codeTextItems[i].SetText(i < enteredCharacters.Count ? enteredCharacters[i] : "-");
    }

    public string GetEnteredCode()
    {
        return string.Join("", enteredCharacters);
    }

    private async UniTaskVoid JoinFriendTableAsync()
    {
        if (isJoining || enteredCharacters.Count != CODE_LENGTH)
            return;

        if (AuthManager.Instance == null)
        {
            await ShowWrongCodeAndReset("Unable to join table");
            return;
        }

        string shareCode = GetEnteredCode().ToUpperInvariant();

        isJoining = true;
        SetKeypadInteractable(false);
        ClearError();

        try
        {
            JoinByCodeResponse codeResponse = await AuthManager.Instance.JoinByCodeAsync(shareCode);

            if (codeResponse == null || string.IsNullOrEmpty(codeResponse.TableId))
            {
                await ShowWrongCodeAndReset("Code wrong");
                return;
            }

            string tableId = codeResponse.TableId;
            TableData tableData = await AuthManager.Instance.GetTableDetailAsync(tableId);

            if (tableData == null)
            {
                await ShowWrongCodeAndReset("Code wrong");
                return;
            }

            TableActiveData activeData = await AuthManager.Instance.GetTableActiveAsync(tableId);
            bool handInProgress = activeData != null && activeData.HandInProgress;
            int minBuyIn = tableData.MinBuyIn > 0 ? tableData.MinBuyIn : codeResponse.MinBuyIn;

            if (handInProgress)
            {
                SpectateData spectate = await AuthManager.Instance.SpectateTableAsync(tableId);

                if (TableJoinHandler.Instance == null)
                {
                    await ShowWrongCodeAndReset("Unable to join table");
                    return;
                }

                // Friend tables are opened from the main menu, not from inside a
                // club — lobby origin, so Back/Exit land on home.
                TableContext.EnterFromLobby(tableId, tableData);
                TableJoinHandler.Instance.BeginWatchAndWait(tableId, minBuyIn);
                await AuthManager.Instance.JoinWaitingListAsync(tableId);

                Debug.Log($"[FriendTable] Watch & Wait | Table: {tableId} | State: {spectate?.CurrentState?.GameState}");
                gameObject.SetActive(false);
                return;
            }

            await AuthManager.Instance.BuyInAsync(tableId, minBuyIn);

            try
            {
                await AuthManager.Instance.JoinTableAsync(tableId, minBuyIn);
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("Already seated"))
                    throw;
            }

            if (TableJoinHandler.Instance == null)
            {
                await ShowWrongCodeAndReset("Unable to join table");
                return;
            }

            TableContext.EnterFromLobby(tableId, tableData);
            TableJoinHandler.Instance.JoinTable(tableId);

            Debug.Log($"[FriendTable] Joined successfully | Code: {shareCode} | Table: {tableId}");
            gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogError("[JoinFriendTable] Join failed: " + e);
            await ShowWrongCodeAndReset("Code wrong");
        }
        finally
        {
            if (this != null && gameObject.activeInHierarchy)
            {
                isJoining = false;
                SetKeypadInteractable(true);
            }
        }
    }

    private async UniTask ShowWrongCodeAndReset(string message)
    {
        if (ErrorText != null)
            ErrorText.text = message;

        Debug.LogWarning("[JoinFriendTable] " + message);

        await UniTask.Delay(
            TimeSpan.FromSeconds(2),
            cancellationToken: destroyCancellationToken
        );

        if (this == null)
            return;

        ClearError();
        ClearEnteredCode();

        isJoining = false;
        SetKeypadInteractable(true);
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

    private void ClearError()
    {
        if (ErrorText != null)
            ErrorText.text = "";
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
        if (FriendTablMenuScreen != null)
            FriendTablMenuScreen.SetActive(true);

        gameObject.SetActive(false);
    }
}