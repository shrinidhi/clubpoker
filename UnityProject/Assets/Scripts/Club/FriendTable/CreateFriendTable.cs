using ClubPoker.Auth;
using ClubPoker.Lobby;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateFriendTable : MonoBehaviour
{
    public Transform VariantContent;
    public GameObject VariantPrefab;
    public GameObject VariantScreen;
    public GameObject FriendCreateTable_Popup;

    public string Variant_Name;

    [Header("Input Fields")]
    public TMP_InputField Maxplayer_InputField;
    public TMP_InputField SmallBlind_InputField;
    public TMP_InputField BigBlind_InputField;
    public TMP_InputField Min_Amount_InputField;
    public TMP_InputField Max_Amount_InputField;
    public TMP_InputField TableName_InputField;

    [Header("Buttons")]
    public Button FriendCreateTable_Button;
    public Button VariantScreenBack_Button;
    public Button FriendCreateTablePopup_Close_Button;

    [Header("Popup")]
    public GameObject FriendCreateTablePopup;
    public GameObject FriendTableMenu;
    public TextMeshProUGUI ErrorText;
    public TextAsset ClubTableVariantJson;

    [SerializeField] private VariantSO VariantSO;

    private ClubTableVariantResponse clubTableVariantResponse;
    private bool isCreatingTable;

    public GameObject CodeSharePanel;
    public TextMeshProUGUI CodeText;
    public Button ShareButton;

    private string createdTableId;
    private string createdShareCode;
    private string createdTableName;
    public Button ShareCloseButton;

    public Button JoinHost;
    private int createdMinBuyIn;
    private bool isJoiningHost;


    private void Start()
    {
        ParseVariantJson();
        GenerateVariants();

        if (FriendCreateTable_Button != null)
        {
            FriendCreateTable_Button.onClick.RemoveListener(CreateTableButtonOnTap);
            FriendCreateTable_Button.onClick.AddListener(CreateTableButtonOnTap);
        }

        if (VariantScreenBack_Button != null)
        {
            VariantScreenBack_Button.onClick.RemoveListener(VariantScreenBack_ButtonOnTap);
            VariantScreenBack_Button.onClick.AddListener(VariantScreenBack_ButtonOnTap);
        }

        if (FriendCreateTablePopup_Close_Button != null)
        {
            FriendCreateTablePopup_Close_Button.onClick.RemoveListener(FriendCreateTablePopup_Close_ButtonOnTap);
            FriendCreateTablePopup_Close_Button.onClick.AddListener(FriendCreateTablePopup_Close_ButtonOnTap);
        }

        if (ShareButton != null)
        {
            ShareButton.onClick.RemoveListener(ShareTableCode);
            ShareButton.onClick.AddListener(ShareTableCode);
        }

        if (JoinHost != null)
        {
            JoinHost.onClick.RemoveListener(JoinHostButtonOnTap);
            JoinHost.onClick.AddListener(JoinHostButtonOnTap);
        }

        ShareCloseButton.onClick.AddListener(ShareCloseButtonOnTap);

        SetupInputListeners();
    }

    private void OnEnable()
    {
        SetInputField();
        ClearError();
        VariantScreen.SetActive(true);
    }

    private void OnDestroy()
    {
        if (FriendCreateTable_Button != null) FriendCreateTable_Button.onClick.RemoveListener(CreateTableButtonOnTap);
        if (VariantScreenBack_Button != null) VariantScreenBack_Button.onClick.RemoveListener(VariantScreenBack_ButtonOnTap);
        if (FriendCreateTablePopup_Close_Button != null) FriendCreateTablePopup_Close_Button.onClick.RemoveListener(FriendCreateTablePopup_Close_ButtonOnTap);
    }

    private void ParseVariantJson()
    {
        if (ClubTableVariantJson == null)
        {
            Debug.LogError("ClubTableVariantJson Missing");
            return;
        }

        try
        {
            clubTableVariantResponse = JsonConvert.DeserializeObject<ClubTableVariantResponse>(ClubTableVariantJson.text);
        }
        catch (Exception e)
        {
            Debug.LogError("Club variant JSON parse failed: " + e.Message);
        }
    }

    private void SetInputField()
    {
        if (Maxplayer_InputField != null) Maxplayer_InputField.text = "4";
        if (SmallBlind_InputField != null) SmallBlind_InputField.text = "5";
        if (BigBlind_InputField != null) BigBlind_InputField.text = "10";
        if (Min_Amount_InputField != null) Min_Amount_InputField.text = "200";
        if (Max_Amount_InputField != null) Max_Amount_InputField.text = "2000";
        if (TableName_InputField != null) TableName_InputField.text = "";
    }

    private void SetupInputListeners()
    {
        if (Maxplayer_InputField != null)
        {
            Maxplayer_InputField.onEndEdit.RemoveAllListeners();
            Maxplayer_InputField.onEndEdit.AddListener(ValidateMaxPlayers);
            Maxplayer_InputField.onValueChanged.AddListener(_ => ClearError());
        }

        if (SmallBlind_InputField != null) SmallBlind_InputField.onValueChanged.AddListener(_ => ClearError());
        if (BigBlind_InputField != null) BigBlind_InputField.onValueChanged.AddListener(_ => ClearError());
        if (Min_Amount_InputField != null) Min_Amount_InputField.onValueChanged.AddListener(_ => ClearError());
        if (Max_Amount_InputField != null) Max_Amount_InputField.onValueChanged.AddListener(_ => ClearError());
        if (TableName_InputField != null) TableName_InputField.onValueChanged.AddListener(_ => ClearError());
    }

    private void ValidateMaxPlayers(string value)
    {
        int maxAllowed = Variant_Name == "omaha_six" ? 7 : 9;

        if (!int.TryParse(value, out int players))
            return;

        if (players < 2)
        {
            Maxplayer_InputField.text = "2";
            ShowError("Minimum players must be 2");
            return;
        }

        if (players > maxAllowed)
        {
            Maxplayer_InputField.text = maxAllowed.ToString();
            ShowError(Variant_Name == "omaha_six" ? "PLO6 maximum players is 7" : "Maximum players must be between 2 and 9");
        }
    }

    private void VariantScreenBack_ButtonOnTap()
    {
        gameObject.SetActive(false);
        FriendTableMenu.SetActive(true);

    }

    void ShareCloseButtonOnTap()
    {

        CodeSharePanel.SetActive(false);
        FriendTableMenu.SetActive(true);
        gameObject.SetActive(false);
    }

    private void FriendCreateTablePopup_Close_ButtonOnTap()
    {
        if (FriendCreateTablePopup != null) FriendCreateTablePopup.SetActive(false);
        if (FriendCreateTable_Popup != null) FriendCreateTable_Popup.SetActive(false);
        if (VariantScreen != null) VariantScreen.SetActive(true);
    }

    private void GenerateVariants()
    {
        ClearOldVariants();

        if (clubTableVariantResponse?.ClubTableVariants == null)
        {
            Debug.LogError("Club Variant Data Missing");
            return;
        }

        foreach (ClubTableVariantData variant in clubTableVariantResponse.ClubTableVariants)
        {
            GameObject obj = Instantiate(VariantPrefab, VariantContent);
            Club_VariantPrefabScreen prefab = obj.GetComponent<Club_VariantPrefabScreen>();
            Sprite sprite = VariantSO != null ? VariantSO.GetVariantSprite(variant.VariantName) : null;

            if (prefab != null) prefab.SetData(variant, sprite, OnVariantSelected);
        }
    }

    private void ClearOldVariants()
    {
        if (VariantContent == null) return;

        for (int i = VariantContent.childCount - 1; i >= 0; i--)
            Destroy(VariantContent.GetChild(i).gameObject);
    }

    private void OnVariantSelected(ClubTableVariantData variantData)
    {
        if (variantData == null) return;

        Variant_Name = variantData.VariantKey;
        SetInputField();
        ClearError();

        int maxAllowed = Variant_Name == "omaha_six" ? 7 : 9;
        Maxplayer_InputField.text = maxAllowed == 7 ? "7" : "4";

        if (VariantScreen != null) VariantScreen.SetActive(false);
        if (FriendCreateTable_Popup != null) FriendCreateTable_Popup.SetActive(true);

        Debug.Log("Selected Variant: " + variantData.VariantName);
    }

    private async void CreateTableButtonOnTap()
    {
        if (isCreatingTable) return;

        if (!ValidateInputs(out CreateTableRequest request)) return;

        if (AuthManager.Instance == null)
        {
            ShowError("AuthManager not available");
            return;
        }

        isCreatingTable = true;
        SetCreateButtonInteractable(false);
        ClearError();

        try
        {
            CreateTableResponse response = await AuthManager.Instance.CreateTableAsync(request);

            if (response == null || string.IsNullOrEmpty(response.TableId))
            {
                ShowError("Table could not be created");
                return;
            }

            createdTableId = response.TableId;
            createdShareCode = response.ShareCode;
            createdTableName = request.Name;
            createdMinBuyIn = request.MinBuyIn;
            Debug.Log($"Friend table created | Table ID: {createdTableId} | Share Code: {createdShareCode}");

            if (FriendCreateTable_Popup != null) FriendCreateTable_Popup.SetActive(false);
            if (CodeSharePanel != null) CodeSharePanel.SetActive(true);
            if (CodeText != null) CodeText.text = createdShareCode;
        }
        catch (Exception e)
        {
            ShowError(string.IsNullOrEmpty(e.Message) ? "Table creation failed" : e.Message);
            Debug.LogError("Create friend table failed: " + e);
        }
        finally
        {
            isCreatingTable = false;
            SetCreateButtonInteractable(true);
        }
    }
    private void ShareTableCode()
    {
        if (string.IsNullOrEmpty(createdShareCode))
        {
            ShowError("Share code not available");
            return;
        }

        string message =
            $"Join my Club Poker table!\n\n" +
            $"Table: {createdTableName}\n" +
            $"Share Code: {createdShareCode}\n\n" +
            $"Open Club Poker and enter this code to join.";

        new NativeShare()
            .SetSubject("Join My Club Poker Table")
            .SetText(message)
            .Share();
    }
    private bool ValidateInputs(out CreateTableRequest request)
    {
        request = null;

        if (string.IsNullOrEmpty(Variant_Name))
        {
            ShowError("Please select a variant");
            return false;
        }

        if (!TryGetValue(Maxplayer_InputField, "Enter valid maximum players", out int maxPlayers)) return false;
        if (!TryGetValue(SmallBlind_InputField, "Enter valid small blind", out int smallBlind)) return false;
        if (!TryGetValue(BigBlind_InputField, "Enter valid big blind", out int bigBlind)) return false;
        if (!TryGetValue(Min_Amount_InputField, "Enter valid minimum buy-in", out int minBuyIn)) return false;
        if (!TryGetValue(Max_Amount_InputField, "Enter valid maximum buy-in", out int maxBuyIn)) return false;

        int maxAllowed = Variant_Name == "omaha_six" ? 7 : 9;

        if (maxPlayers < 2 || maxPlayers > maxAllowed)
        {
            ShowError(Variant_Name == "omaha_six" ? "Players must be between 2 and 7" : "Players must be between 2 and 9");
            return false;
        }

        if (smallBlind <= 0)
        {
            ShowError("Small blind must be greater than 0");
            return false;
        }

        if (bigBlind <= smallBlind)
        {
            ShowError("Big blind must be greater than small blind");
            return false;
        }

        if (minBuyIn <= 0)
        {
            ShowError("Minimum buy-in must be greater than 0");
            return false;
        }

        if (maxBuyIn < minBuyIn)
        {
            ShowError("Maximum buy-in must be greater than minimum buy-in");
            return false;
        }

        string tableName = TableName_InputField != null ? TableName_InputField.text.Trim() : "";

        if (string.IsNullOrEmpty(tableName))
            tableName = $"Friend Table ({GetVariantShortName(Variant_Name)} {smallBlind}/{bigBlind})";

        request = new CreateTableRequest
        {
            Variant = Variant_Name,
            MaxPlayers = maxPlayers,
            SmallBlind = smallBlind,
            BigBlind = bigBlind,
            MinBuyIn = minBuyIn,
            MaxBuyIn = maxBuyIn,
            Name = tableName
        };

        Debug.Log("Create friend table request:\n" + JsonConvert.SerializeObject(request, Formatting.Indented));
        return true;
    }

    private bool TryGetValue(TMP_InputField inputField, string error, out int value)
    {
        value = 0;

        if (inputField != null && int.TryParse(inputField.text.Trim(), out value))
            return true;

        ShowError(error);
        return false;
    }

    private string GetVariantShortName(string variant)
    {
        switch (variant)
        {
            case "texas_holdem": return "NLH";
            case "omaha":
            case "plo4": return "PLO4";
            case "omaha_six":
            case "plo6": return "PLO6";
            default: return variant;
        }
    }

    private void SetCreateButtonInteractable(bool interactable)
    {
        if (FriendCreateTable_Button != null) FriendCreateTable_Button.interactable = interactable;
    }

    private void ShowError(string message)
    {
        if (ErrorText != null) ErrorText.text = message;
        Debug.LogWarning(message);
        ClearErrorAfterDelay().Forget();
    }

    private void ClearError()
    {
        if (ErrorText != null) ErrorText.text = "";
    }

    private async UniTaskVoid ClearErrorAfterDelay()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: destroyCancellationToken);
        ClearError();
    }


    private void JoinHostButtonOnTap()
    {
        JoinHostTableAsync().Forget();
    }

    private async UniTaskVoid JoinHostTableAsync()
    {
        if (isJoiningHost)
            return;

        if (string.IsNullOrEmpty(createdTableId))
        {
            ShowError("Table not created");
            return;
        }

        if (AuthManager.Instance == null)
        {
            ShowError("AuthManager not available");
            return;
        }

        if (ClubPoker.Game.TableJoinHandler.Instance == null)
        {
            ShowError("Table join handler not available");
            return;
        }

        isJoiningHost = true;

        if (JoinHost != null)
            JoinHost.interactable = false;

        ClearError();

        try
        {
            int buyInAmount = createdMinBuyIn;

            if (buyInAmount <= 0)
            {
                TableData tableData = await AuthManager.Instance.GetTableDetailAsync(createdTableId);

                if (tableData == null)
                {
                    ShowError("Table details could not be loaded");
                    return;
                }

                buyInAmount = tableData.MinBuyIn;
            }

            await AuthManager.Instance.BuyInAsync(createdTableId, buyInAmount);

            try
            {
                await AuthManager.Instance.JoinTableAsync(createdTableId, buyInAmount);
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("Already seated"))
                    throw;
            }

            if (CodeSharePanel != null)
                CodeSharePanel.SetActive(false);

            // Friend table host — created from the main menu, so lobby origin.
            TableContext.EnterFromLobby(createdTableId);
            ClubPoker.Game.TableJoinHandler.Instance.JoinTable(createdTableId);

            Debug.Log(
                $"Host joined friend table | " +
                $"Table ID: {createdTableId} | " +
                $"Buy-In: {buyInAmount}"
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Host join failed: " + e);
            ShowError(string.IsNullOrEmpty(e.Message) ? "Failed to join table" : e.Message);
        }
        finally
        {
            isJoiningHost = false;

            if (JoinHost != null)
                JoinHost.interactable = true;
        }
    }
}