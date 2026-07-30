using System;
using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CareerHandHistoryPanel : MonoBehaviour
{
    public Button CloseButton;
    public Button PreviousButton;
    public Button NextButton;

    public Text PageCount;
    public Text Round_Pair_Pot_Text;

    public Transform HandPlayerContent;
    public GameObject HandPlayerPrefab;

    public Transform HandDetailContent;
    public GameObject HandDetailPrefab;

    public Transform ShowdownContent;
    public GameObject ShowDown;

    public Button HandSummaryButton;
    public Button HandDetailButton;
    public GameObject HandSummeryPanel;
    public GameObject HandDetailPanel;
    public Sprite SelectButtonSprite;
    public Sprite UnSeletButtonSprite;

    public GameObject NoDataMsg;
    public GameObject TopButtonGrid;

    public Text Date_TimeText;
    public Text Small_BigBlind;
    public Text TableId;
    public Text Variant_Text;

    public Button PlayerNameHideOn_OffButton;
    public Sprite OnButtonSprite;
    public Sprite OffButtonSprite;

    public GameObject HeaderData;
    public GameObject HeaderText;
    public GameObject HideName;

    public Text ErrorText;
    public GameObject LoadingPanel;

    private readonly List<string> handIds = new List<string>();
    private readonly Dictionary<string, CareerHandDetailData> loadedHands =
        new Dictionary<string, CareerHandDetailData>();

    private int currentIndex;
    private bool hidePlayerNames;
    private bool isLoading;
    private bool initialized;

    private void Start()
    {
        SetupButtons();
        UpdateHideButtonSprite();
        initialized = true;
    }

    private void SetupButtons()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (PreviousButton != null)
            PreviousButton.onClick.AddListener(Previous);

        if (NextButton != null)
            NextButton.onClick.AddListener(Next);

        if (HandSummaryButton != null)
            HandSummaryButton.onClick.AddListener(HandSummaryButtonOnTap);

        if (HandDetailButton != null)
            HandDetailButton.onClick.AddListener(HandDetailButtonOnTap);

        if (PlayerNameHideOn_OffButton != null)
            PlayerNameHideOn_OffButton.onClick.AddListener(TogglePlayerNames);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (PreviousButton != null)
            PreviousButton.onClick.RemoveListener(Previous);

        if (NextButton != null)
            NextButton.onClick.RemoveListener(Next);

        if (HandSummaryButton != null)
            HandSummaryButton.onClick.RemoveListener(HandSummaryButtonOnTap);

        if (HandDetailButton != null)
            HandDetailButton.onClick.RemoveListener(HandDetailButtonOnTap);

        if (PlayerNameHideOn_OffButton != null)
            PlayerNameHideOn_OffButton.onClick.RemoveListener(TogglePlayerNames);
    }

    public void Open(List<CareerHandHistoryItem> hands, string selectedHandId)
    {
        gameObject.SetActive(true);

        handIds.Clear();
        loadedHands.Clear();

        if (hands != null)
        {
            foreach (CareerHandHistoryItem hand in hands)
            {
                if (hand != null && !string.IsNullOrEmpty(hand.HandId))
                    handIds.Add(hand.HandId);
            }
        }

        if (handIds.Count == 0)
        {
            ShowNoData();
            return;
        }

        currentIndex = handIds.IndexOf(selectedHandId);

        if (currentIndex < 0)
            currentIndex = 0;

        ShowDefaultTab();
        LoadCurrentHand().Forget();
    }

    public void Open(string handId)
    {
        gameObject.SetActive(true);

        handIds.Clear();
        loadedHands.Clear();

        if (!string.IsNullOrEmpty(handId))
            handIds.Add(handId);

        currentIndex = 0;

        if (handIds.Count == 0)
        {
            ShowNoData();
            return;
        }

        ShowDefaultTab();
        LoadCurrentHand().Forget();
    }

    private void ShowDefaultTab()
    {
        if (HandSummeryPanel != null)
            HandSummeryPanel.SetActive(true);

        if (HandDetailPanel != null)
            HandDetailPanel.SetActive(false);

        if (HandSummaryButton != null && HandSummaryButton.image != null)
            HandSummaryButton.image.sprite = SelectButtonSprite;

        if (HandDetailButton != null && HandDetailButton.image != null)
            HandDetailButton.image.sprite = UnSeletButtonSprite;
    }

    private async UniTaskVoid LoadCurrentHand()
    {
        if (isLoading || handIds.Count == 0)
            return;

        if (currentIndex < 0 || currentIndex >= handIds.Count)
            return;

        string requestedHandId = handIds[currentIndex];

        if (loadedHands.TryGetValue(requestedHandId, out CareerHandDetailData cachedData))
        {
            ShowDataState();
            Render(cachedData);
            return;
        }

        if (AuthManager.Instance == null)
        {
            ShowError("AuthManager not available");
            return;
        }

        isLoading = true;
        SetNavigationInteractable(false);
        ClearError();

        if (LoadingPanel != null)
            LoadingPanel.SetActive(true);

        try
        {
            CareerHandDetailData data =
                await AuthManager.Instance.GetCareerHandDetailAsync(requestedHandId);

            if (this == null)
                return;

            if (data == null)
            {
                ShowError("Hand detail could not be loaded");
                ShowNoData();
                return;
            }

            loadedHands[requestedHandId] = data;

            ShowDataState();
            Render(data);
        }
        catch (Exception e)
        {
            Debug.LogError("Career hand load failed: " + e);
            ShowError(e.Message);
            ShowNoData();
        }
        finally
        {
            isLoading = false;

            if (LoadingPanel != null)
                LoadingPanel.SetActive(false);

            UpdateNavigationButtons();
        }
    }

    private void Render(CareerHandDetailData record)
    {
        if (record == null)
            return;

        if (Round_Pair_Pot_Text != null)
        {
            Round_Pair_Pot_Text.text =
                "Round " + record.Round +
                " - " +
                (string.IsNullOrEmpty(record.HandName) ? "-" : record.HandName) +
                " - Pot +" + record.PotWon;
        }

        if (Date_TimeText != null)
            Date_TimeText.text = FormatTimestamp(record.Timestamp);

        if (Small_BigBlind != null)
            Small_BigBlind.text =
                string.IsNullOrEmpty(record.BlindsLabel)
                    ? "-"
                    : record.BlindsLabel;

        if (Variant_Text != null)
            Variant_Text.text = GetVariantName(record.Variant);

        if (TableId != null)
            TableId.text = record.HandId;

        Clear(HandPlayerContent);

        int fallbackHoleCardCount = GetVariantHoleCardCount(record.Variant);

        if (record.Players != null)
        {
            for (int i = 0; i < record.Players.Count; i++)
            {
                CareerHandPlayer player = record.Players[i];

                GameObject obj =
                    Instantiate(HandPlayerPrefab, HandPlayerContent);

                CareerHandPlayerPrefab item =
                    obj.GetComponent<CareerHandPlayerPrefab>();

                if (item == null)
                {
                    Destroy(obj);
                    continue;
                }

                bool isWinner =
                    record.Winner != null &&
                    record.Winner.Id == player.Id;

                int chipDifference = player.Chips - player.BuyIn;

                

                item.SetData(
                    GetDisplayPlayerName(record, player.Username),
                    player.HandName,
                    chipDifference,
                    isWinner,
                    player.HoleCards,
                    record.CommunityCards,
                    record.BestHandCards,
                    GetSeatRole(record, i),
                    true,
                    fallbackHoleCardCount
                );
            }
        }

        if (PageCount != null)
            PageCount.text = (currentIndex + 1) + "/" + handIds.Count;

        UpdateNavigationButtons();
        RenderDetails(record);
    }

    private void RenderDetails(CareerHandDetailData record)
    {
        Clear(HandDetailContent);

        if (record.Actions == null)
        {
            RenderShowdown(record);
            return;
        }

        string currentStreet = "PRE_FLOP";
        CareerHandDetailPrefab currentSection =
            CreateDetailSection(record, currentStreet, null);

        int rowIndex = 0;

        foreach (CareerHandAction action in record.Actions)
        {
            if (action == null)
                continue;

            if (action.Type == "street")
            {
                currentStreet = string.IsNullOrEmpty(action.Street)
                    ? currentStreet
                    : action.Street.ToUpper();

                currentSection =
                    CreateDetailSection(record, currentStreet, action.Cards);

                continue;
            }

            if (action.Type != "action" && action.Type != "blind")
                continue;

            if (currentSection == null)
            {
                currentSection =
                    CreateDetailSection(record, currentStreet, null);
            }

            GameObject playerObj =
                Instantiate(
                    currentSection.PlayerTurnDetailPrefab,
                    currentSection.PlayerTurnDetailContent
                );

            CareerPlayerTurnDetailPrefab playerItem =
                playerObj.GetComponent<CareerPlayerTurnDetailPrefab>();

            if (playerItem == null)
            {
                Destroy(playerObj);
                continue;
            }

            string actionName;

            if (action.Type == "blind")
                actionName = action.Role;
            else
                actionName = action.Action;

            playerItem.SetData(
                GetDisplayPlayerName(record, action.Username),
                actionName,
                action.Amount,
                action.ChipsAfter,
                GetActionSeatRole(record, action)
            );

            playerItem.SetRowColor(rowIndex);
            rowIndex++;

            UpdateSectionHeight(currentSection);
        }

        RenderShowdown(record);

        if (ShowDown != null)
            ShowDown.transform.SetAsLastSibling();
    }

    private CareerHandDetailPrefab CreateDetailSection(
        CareerHandDetailData record,
        string street,
        List<string> streetCards)
    {
        GameObject sectionObj =
            Instantiate(HandDetailPrefab, HandDetailContent);

        CareerHandDetailPrefab section =
            sectionObj.GetComponent<CareerHandDetailPrefab>();

        if (section == null)
        {
            Destroy(sectionObj);
            return null;
        }

        if (section.GameStateName != null)
            section.GameStateName.text = street;

        AddCardsByStreet(section, record, street, streetCards);

        return section;
    }

    private void AddCardsByStreet(
        CareerHandDetailPrefab section,
        CareerHandDetailData record,
        string street,
        List<string> streetCards)
    {
        Clear(section.CardContent);

        List<string> cards = new List<string>();

        if (streetCards != null && streetCards.Count > 0)
        {
            cards.AddRange(streetCards);
        }
        else if (record.CommunityCards != null)
        {
            if (street == "FLOP" && record.CommunityCards.Count >= 3)
                cards.AddRange(record.CommunityCards.GetRange(0, 3));
            else if (street == "TURN" && record.CommunityCards.Count >= 4)
                cards.Add(record.CommunityCards[3]);
            else if (street == "RIVER" && record.CommunityCards.Count >= 5)
                cards.Add(record.CommunityCards[4]);
        }

        if (section.CardContent != null)
            section.CardContent.gameObject.SetActive(cards.Count > 0);

        foreach (string card in cards)
        {
            GameObject cardObj =
                Instantiate(section.CardPrefab, section.CardContent);

            CareerCardPrefab cardPrefab =
                cardObj.GetComponent<CareerCardPrefab>();

            if (cardPrefab != null)
                cardPrefab.SetCard(card);
            else
                Destroy(cardObj);
        }
    }

    private void RenderShowdown(CareerHandDetailData record)
    {
        Clear(ShowdownContent);

        bool hasShowdown =
            record.ShowdownCards != null &&
            record.ShowdownCards.Count > 0;

        if (ShowDown != null)
            ShowDown.SetActive(hasShowdown);

        if (!hasShowdown)
            return;

        int fallbackHoleCardCount =
            GetVariantHoleCardCount(record.Variant);

        foreach (CareerShowdownPlayer showdownPlayer in record.ShowdownCards)
        {
            CareerHandPlayer player =
                record.Players?.Find(x => x.Id == showdownPlayer.PlayerId);

            bool isWinner =
                record.Winner != null &&
                record.Winner.Id == showdownPlayer.PlayerId;

            int chipDifference =
                player != null
                    ? player.Chips - player.BuyIn
                    : 0;


            GameObject obj =
                Instantiate(HandPlayerPrefab, ShowdownContent);

            CareerHandPlayerPrefab item =
                obj.GetComponent<CareerHandPlayerPrefab>();

            if (item == null)
            {
                Destroy(obj);
                continue;
            }

            int playerIndex =
                record.Players != null
                    ? record.Players.FindIndex(
                        x => x.Id == showdownPlayer.PlayerId
                    )
                    : -1;

            item.SetData(
                GetDisplayPlayerName(record, showdownPlayer.Username),
                showdownPlayer.HandName,
                chipDifference,
                isWinner,
                showdownPlayer.HoleCards,
                record.CommunityCards,
                record.BestHandCards,
                GetSeatRole(record, playerIndex),
                true,
                fallbackHoleCardCount
            );
        }

        UpdateShowdownLayout();
    }

    private void UpdateSectionHeight(CareerHandDetailPrefab section)
    {
        if (section == null ||
            section.PlayerTurnDetailContent == null ||
            section.RootRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform contentRect =
            section.PlayerTurnDetailContent.GetComponent<RectTransform>();

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        float height =
            LayoutUtility.GetPreferredHeight(contentRect);

        if (height <= 0)
            height = contentRect.rect.height;

        section.RootRect.sizeDelta =
            new Vector2(
                section.RootRect.sizeDelta.x,
                height + 120f
            );

        contentRect.anchorMin = new Vector2(0.5f, 1f);
        contentRect.anchorMax = new Vector2(0.5f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        contentRect.anchoredPosition =
            new Vector2(
                contentRect.anchoredPosition.x,
                -121f
            );
    }

    private void UpdateShowdownLayout()
    {
        if (ShowDown == null ||
            !ShowDown.activeSelf ||
            ShowdownContent == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform content =
            ShowdownContent.GetComponent<RectTransform>();

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float height =
            LayoutUtility.GetPreferredHeight(content);

        if (height <= 0)
            height = content.rect.height;

        VerticalLayoutGroup layout =
            content.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
            height += layout.padding.top + layout.padding.bottom;

        RectTransform root =
            ShowDown.GetComponent<RectTransform>();

        root.sizeDelta =
            new Vector2(root.sizeDelta.x, height + 150f);

        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        content.anchoredPosition =
            new Vector2(
                content.anchoredPosition.x,
                -145f
            );
    }

    private void HandSummaryButtonOnTap()
    {
        if (HandSummeryPanel != null)
            HandSummeryPanel.SetActive(true);

        if (HandDetailPanel != null)
            HandDetailPanel.SetActive(false);

        if (HandSummaryButton != null && HandSummaryButton.image != null)
            HandSummaryButton.image.sprite = SelectButtonSprite;

        if (HandDetailButton != null && HandDetailButton.image != null)
            HandDetailButton.image.sprite = UnSeletButtonSprite;
    }

    private void HandDetailButtonOnTap()
    {
        if (HandSummeryPanel != null)
            HandSummeryPanel.SetActive(false);

        if (HandDetailPanel != null)
            HandDetailPanel.SetActive(true);

        if (HandDetailButton != null && HandDetailButton.image != null)
            HandDetailButton.image.sprite = SelectButtonSprite;

        if (HandSummaryButton != null && HandSummaryButton.image != null)
            HandSummaryButton.image.sprite = UnSeletButtonSprite;

        RebuildHandDetailLayout().Forget();
    }
    private async UniTaskVoid RebuildHandDetailLayout()
    {
        await UniTask.Yield();

        if (this == null || HandDetailPanel == null || !HandDetailPanel.activeInHierarchy)
            return;

        Canvas.ForceUpdateCanvases();

        if (HandDetailContent != null)
        {
            for (int i = 0; i < HandDetailContent.childCount; i++)
            {
                Transform child = HandDetailContent.GetChild(i);
                CareerHandDetailPrefab section = child.GetComponent<CareerHandDetailPrefab>();

                if (section != null)
                    UpdateSectionHeight(section);
            }
        }

        UpdateShowdownLayout();

        Canvas.ForceUpdateCanvases();

        if (HandDetailContent != null)
        {
            RectTransform detailContentRect = HandDetailContent.GetComponent<RectTransform>();

            if (detailContentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(detailContentRect);
        }

        if (HandDetailPanel != null)
        {
            RectTransform panelRect = HandDetailPanel.GetComponent<RectTransform>();

            if (panelRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }

        Canvas.ForceUpdateCanvases();
    }
    private void TogglePlayerNames()
    {
        hidePlayerNames = !hidePlayerNames;
        UpdateHideButtonSprite();

        if (handIds.Count == 0)
            return;

        string handId = handIds[currentIndex];

        if (loadedHands.TryGetValue(handId, out CareerHandDetailData data))
            Render(data);
    }

    private void UpdateHideButtonSprite()
    {
        if (PlayerNameHideOn_OffButton == null ||
            PlayerNameHideOn_OffButton.image == null)
            return;

        PlayerNameHideOn_OffButton.image.sprite =
            hidePlayerNames
                ? OnButtonSprite
                : OffButtonSprite;
    }

    private void Next()
    {
        if (isLoading)
            return;

        if (currentIndex < handIds.Count - 1)
        {
            currentIndex++;
            LoadCurrentHand().Forget();
        }
    }

    private void Previous()
    {
        if (isLoading)
            return;

        if (currentIndex > 0)
        {
            currentIndex--;
            LoadCurrentHand().Forget();
        }
    }

    private string GetDisplayPlayerName(
        CareerHandDetailData record,
        string realName)
    {
        if (!hidePlayerNames)
            return realName;

        int index =
            record.Players != null
                ? record.Players.FindIndex(
                    p => p.Username == realName
                )
                : -1;

        return index >= 0
            ? "Player " + (index + 1)
            : "Player";
    }

    private string GetSeatRole(
        CareerHandDetailData record,
        int playerIndex)
    {
        if (record.Positions == null || playerIndex < 0)
            return "";

        if (playerIndex == record.Positions.DealerIdx)
            return "BTN";

        if (playerIndex == record.Positions.SbIdx)
            return "SB";

        if (playerIndex == record.Positions.BbIdx)
            return "BB";

        return "UTG";
    }

    private string GetActionSeatRole(
        CareerHandDetailData record,
        CareerHandAction action)
    {
        if (!string.IsNullOrEmpty(action.Role))
            return action.Role;

        int playerIndex =
            record.Players != null
                ? record.Players.FindIndex(
                    p => p.Id == action.PlayerId
                )
                : -1;

        return GetSeatRole(record, playerIndex);
    }

    private int GetVariantHoleCardCount(string variant)
    {
        switch (variant)
        {
            case "texas_holdem":
                return 2;

            case "omaha":
            case "plo4":
            case "PLO4":
                return 4;

            case "plo5":
            case "PLO5":
                return 5;

            case "omaha_six":
            case "plo6":
            case "PLO6":
                return 6;

            default:
                return 2;
        }
    }

    private string GetVariantName(string variant)
    {
        switch (variant)
        {
            case "texas_holdem":
                return "NLH";

            case "omaha":
            case "plo4":
                return "PLO4";

            case "plo5":
                return "PLO5";

            case "omaha_six":
            case "plo6":
                return "PLO6";

            default:
                return string.IsNullOrEmpty(variant)
                    ? "-"
                    : variant;
        }
    }

    private string FormatTimestamp(long timestamp)
    {
        try
        {
            DateTimeOffset date =
                DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

            return date.LocalDateTime.ToString("MM/dd HH:mm");
        }
        catch
        {
            return "-";
        }
    }

    private void UpdateNavigationButtons()
    {
        if (PreviousButton != null)
            PreviousButton.interactable =
                !isLoading && currentIndex > 0;

        if (NextButton != null)
            NextButton.interactable =
                !isLoading &&
                currentIndex < handIds.Count - 1;
    }

    private void SetNavigationInteractable(bool interactable)
    {
        if (PreviousButton != null)
            PreviousButton.interactable = interactable;

        if (NextButton != null)
            NextButton.interactable = interactable;
    }

    private void ShowDataState()
    {
        if (NoDataMsg != null)
            NoDataMsg.SetActive(false);

        if (TopButtonGrid != null)
            TopButtonGrid.SetActive(true);

        if (HeaderData != null)
            HeaderData.SetActive(true);

        if (HeaderText != null)
            HeaderText.SetActive(false);

        if (HideName != null)
            HideName.SetActive(true);

        if (HandSummeryPanel != null &&
            HandDetailPanel != null &&
            !HandSummeryPanel.activeSelf &&
            !HandDetailPanel.activeSelf)
        {
            ShowDefaultTab();
        }
    }

    private void ShowNoData()
    {
        Clear(HandPlayerContent);
        Clear(HandDetailContent);
        Clear(ShowdownContent);

        if (ShowDown != null)
            ShowDown.SetActive(false);

        if (NoDataMsg != null)
            NoDataMsg.SetActive(true);

        if (TopButtonGrid != null)
            TopButtonGrid.SetActive(false);

        if (HeaderData != null)
            HeaderData.SetActive(false);

        if (HeaderText != null)
            HeaderText.SetActive(true);

        if (HideName != null)
            HideName.SetActive(false);

        if (HandSummeryPanel != null)
            HandSummeryPanel.SetActive(false);

        if (HandDetailPanel != null)
            HandDetailPanel.SetActive(false);

        if (PageCount != null)
            PageCount.text = "0/0";
    }

    private void Clear(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);

            if (ShowDown != null &&
                child.gameObject == ShowDown)
                continue;

            Destroy(child.gameObject);
        }
    }

    private void ShowError(string message)
    {
        if (ErrorText != null)
            ErrorText.text = message;

        Debug.LogWarning(message);
    }

    private void ClearError()
    {
        if (ErrorText != null)
            ErrorText.text = "";
    }

    private void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }
}