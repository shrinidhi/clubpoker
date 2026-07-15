using ClubPoker.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    public class HandHistoryPanel : MonoBehaviour
    {
        public Button CloseButton;
        public Button PreviousButton;
        public Button NextButton;

        public Text PageCount;
        public Text Round_Pair_Pot_Text;

        public SmallCardSO SmallCardSO;

        public Transform HandPlayerContent;
        public GameObject HandPlayerPrefab;

        private int currentIndex;

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
        bool firsttimeShow = false;

        public Text Date_TimeText;
        public Text Small_BigBlind;
        public Text TableId;
        public Text Variant_Text;
        public Button PlayerNameHideOn_OffButton;
        public Sprite OnButtonSprite;
        public Sprite OffButtonSprite;

        private bool hidePlayerNames = false;

        public GameObject headerData;
        public GameObject headerText;
        public GameObject HideName;
        private void Start()
        {
            CloseButton.onClick.AddListener(() =>
                gameObject.SetActive(false));

            PreviousButton.onClick.AddListener(Previous);
            NextButton.onClick.AddListener(Next);
            HandSummaryButton.onClick.AddListener(HandSummaryButtonOnTap);
            HandDetailButton.onClick.AddListener(HandDetailButtonOnTap);
            PlayerNameHideOn_OffButton.onClick.AddListener(TogglePlayerNames);

            UpdateHideButtonSprite();
        }

        void TogglePlayerNames()
        {
            hidePlayerNames = !hidePlayerNames;
            UpdateHideButtonSprite();
            Render();
        }

        void UpdateHideButtonSprite()
        {
            if (PlayerNameHideOn_OffButton == null)
                return;

            if (PlayerNameHideOn_OffButton.image == null)
                return;

            PlayerNameHideOn_OffButton.image.sprite =
                hidePlayerNames ? OnButtonSprite : OffButtonSprite;
        }

        void HandSummaryButtonOnTap()
        {
            HandSummeryPanel.SetActive(true);
            HandDetailPanel.SetActive(false);
            HandSummaryButton.image.sprite = SelectButtonSprite;
            HandDetailButton.image.sprite = UnSeletButtonSprite;
            Open();
        }

        void HandDetailButtonOnTap()
        {
            HandSummeryPanel.SetActive(false);
            HandDetailPanel.SetActive(true);
            HandDetailButton.image.sprite = SelectButtonSprite;
            HandSummaryButton.image.sprite = UnSeletButtonSprite;
            Open();
        }

        private void OnEnable()
        {
            Open();
            if (HandHistoryManager.Instance.HandLogs.Count != 0)
            {
                HandSummeryPanel.SetActive(true);
                HandDetailPanel.SetActive(false);
                HandSummaryButton.image.sprite = SelectButtonSprite;
                HandDetailButton.image.sprite = UnSeletButtonSprite;
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);

            if (HandHistoryManager.Instance == null)
                return;

            if (HandHistoryManager.Instance.HandLogs.Count == 0)
            {
                NoDataMsg.SetActive(true);
                HandSummeryPanel.SetActive(false);
                HandDetailPanel.SetActive(false);
               // PageCount.gameObject.SetActive(false);
                TopButtonGrid.SetActive(false);
                headerData.SetActive(false);
                headerText.SetActive(true);
                HideName.SetActive(false);
                return;
            }

            NoDataMsg.SetActive(false);
            headerData.SetActive(true);
            headerText.SetActive(false);
            HideName.SetActive(true);
            if (firsttimeShow == false)
            {
                HandSummeryPanel.SetActive(true);
                firsttimeShow = true;
            }
            

            PageCount.gameObject.SetActive(true);
            TopButtonGrid.SetActive(true);

            currentIndex =
                HandHistoryManager.Instance.HandLogs.Count - 1;

            Render();
        }

        void Render()
        {
            var logs =
                HandHistoryManager.Instance.HandLogs;

            if (logs == null || logs.Count == 0)
                return;

            var record =
                logs[currentIndex];

            Round_Pair_Pot_Text.text =
                "Round " + record.RoundNumber +
                " - " + record.WinningHand +
                " - Pot +" + record.PotAmount;

            Date_TimeText.text = record.StartDateTime;
            var table = TableContext.CurrentTable;
            Small_BigBlind.text = table.SmallBlind + "/" + table.BigBlind;
            Variant_Text.text = VariantUtils.ToDisplayName(table.Variant);
            TableId.text = record.TableId;
            Clear(HandPlayerContent);

            int maxHoleCardCount = 0;

            foreach (var p in record.Players)
            {
                if (p.HoleCards != null &&
                    p.HoleCards.Count > maxHoleCardCount)
                {
                    maxHoleCardCount = p.HoleCards.Count;
                }
            }

            for (int i = 0; i < record.Players.Count; i++)
            {
                var player = record.Players[i];

                GameObject obj =
                    Instantiate(
                        HandPlayerPrefab,
                        HandPlayerContent);

                HandPlayerPrefab item =
                    obj.GetComponent<HandPlayerPrefab>();

                item.SetData(
                    GetDisplayPlayerName(record, player.Username),
                    player.HandName,
                    player.ChipDifference,
                    player.IsWinner,
                    player.HoleCards,
                    record.BoardCards,
                    player.BestHandCards,
                    GetSeatRole(record, player.Seat),
                    true,
                    maxHoleCardCount
                );
            }

            PageCount.text =
                (currentIndex + 1) +
                "/" +
                logs.Count;

            RenderDetails();
        }

        public void RenderDetails()
        {
            Clear(HandDetailContent);

            var record =
                HandHistoryManager.Instance.HandLogs[currentIndex];

            string currentStreet = "";
            HandDetailPrefab currentSection = null;

            int i = 0;

            foreach (var action in record.Actions)
            {
                if (currentStreet != action.Street)
                {
                    currentStreet = action.Street;

                    GameObject sectionObj =
                        Instantiate(
                            HandDetailPrefab,
                            HandDetailContent);

                    currentSection =
                        sectionObj.GetComponent<HandDetailPrefab>();

                    currentSection.GameStateName.text =
                        currentStreet;

                    AddCardsByStreet(
                        currentSection,
                        record,
                        currentStreet);
                }

                GameObject playerObj =
                    Instantiate(
                        currentSection.PlayerTurnDetailPrefab,
                        currentSection.PlayerTurnDetailContent);

                PlayerTurnDetailPrefab playerItem =
                    playerObj.GetComponent<PlayerTurnDetailPrefab>();

                HandHistoryPlayer actionPlayer =
                    record.Players.Find(
                        p => p.PlayerId == action.PlayerId);

                string seatRole =
                    actionPlayer != null
                        ? GetSeatRole(record, actionPlayer.Seat)
                        : "";

                playerItem.SetData(
                    GetDisplayPlayerName(record, action.Username),
                    action.Action,
                    action.Amount,
                    action.ChipsAfter,
                    seatRole
                );

                playerItem.SetRowColor(i);

                i++;

                UpdateSectionHeight(currentSection);
            }

            RenderShowdown(record);

            if (ShowDown != null)
            {
                ShowDown.transform.SetAsLastSibling();
            }
        }

        void AddCardsByStreet(
            HandDetailPrefab section,
            HandHistoryRecord record,
            string street)
        {
            foreach (Transform child in section.CardContent)
                Destroy(child.gameObject);

            List<string> cards =
                new List<string>();

            if (street == "FLOP" &&
                record.BoardCards.Count >= 3)
            {
                cards =
                    record.BoardCards.GetRange(0, 3);
            }
            else if (street == "TURN" &&
                     record.BoardCards.Count >= 4)
            {
                cards =
                    record.BoardCards.GetRange(3, 1);
            }
            else if (street == "RIVER" &&
                     record.BoardCards.Count >= 5)
            {
                cards =
                    record.BoardCards.GetRange(4, 1);
            }

            section.CardContent
                .gameObject
                .SetActive(cards.Count > 0);

            foreach (string card in cards)
            {
                GameObject cardObj =
                    Instantiate(
                        section.CardPrefab,
                        section.CardContent);

                cardObj.GetComponent<CardPrefab>()
                    .SetCard(card);
            }
        }

        void UpdateSectionHeight(
            HandDetailPrefab section)
        {
            Canvas.ForceUpdateCanvases();

            RectTransform contentRect =
                section.PlayerTurnDetailContent
                    .GetComponent<RectTransform>();

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                contentRect);

            float height =
                LayoutUtility.GetPreferredHeight(
                    contentRect);

            if (height <= 0)
                height = contentRect.rect.height;

            section.RootRect.sizeDelta =
                new Vector2(
                    section.RootRect.sizeDelta.x,
                    height + 120f);

            contentRect.anchorMin =
                new Vector2(0.5f, 1f);

            contentRect.anchorMax =
                new Vector2(0.5f, 1f);

            contentRect.pivot =
                new Vector2(0.5f, 1f);

            contentRect.anchoredPosition =
                new Vector2(
                    contentRect.anchoredPosition.x,
                    -121f);
        }

        void RenderShowdown(
            HandHistoryRecord record)
        {
            Clear(ShowdownContent);

            bool hasShowdown = false;

            foreach (var player in record.Players)
            {
                if (player.HoleCards != null &&
                    player.HoleCards.Count > 0)
                {
                    hasShowdown = true;
                    break;
                }
            }

            ShowDown.SetActive(hasShowdown);

            if (!hasShowdown)
                return;
            int maxHoleCardCount = 0;

       
            if (record.Variant == "texas_holdem")
            {
                maxHoleCardCount = 2;
            }
            else if(record.Variant == "PLO4")
            {
                maxHoleCardCount = 4;
            }
            else if(record.Variant == "PLO5")
            {
                maxHoleCardCount = 5;
            }
            else if(record.Variant == "PLO6")
            {
                maxHoleCardCount = 6;
            }
            for (int i = 0; i < record.Players.Count; i++)
            {
                var player =
                    record.Players[i];

                GameObject obj =
                    Instantiate(
                        HandPlayerPrefab,
                        ShowdownContent);

                HandPlayerPrefab item =
                    obj.GetComponent<HandPlayerPrefab>();

                item.SetData(
     GetDisplayPlayerName(record, player.Username),
     player.HandName,
     player.ChipDifference,
     player.IsWinner,
     player.HoleCards,
     record.BoardCards,
     player.BestHandCards,
     GetSeatRole(record, player.Seat),
     true,
     maxHoleCardCount
 );
            }

            UpdateShowdownLayout();
        }

        void UpdateShowdownLayout()
        {
            if (ShowDown == null ||
                !ShowDown.activeSelf)
                return;

            Canvas.ForceUpdateCanvases();

            RectTransform content =
                ShowdownContent
                    .GetComponent<RectTransform>();

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                content);

            float height =
                LayoutUtility.GetPreferredHeight(
                    content);

            if (height <= 0)
                height = content.rect.height;

            VerticalLayoutGroup layout =
                content.GetComponent<VerticalLayoutGroup>();

            if (layout != null)
            {
                height +=
                    layout.padding.top +
                    layout.padding.bottom;
            }

            RectTransform root =
                ShowDown.GetComponent<RectTransform>();

            root.sizeDelta =
                new Vector2(
                    root.sizeDelta.x,
                    height + 150f);

            content.anchorMin =
                new Vector2(0.5f, 1f);

            content.anchorMax =
                new Vector2(0.5f, 1f);

            content.pivot =
                new Vector2(0.5f, 1f);

            content.anchoredPosition =
                new Vector2(
                    content.anchoredPosition.x,
                    -145f);
        }

        void Clear(Transform parent)
        {
            foreach (Transform t in parent)
            {
                if (ShowDown != null &&
                    t.gameObject == ShowDown)
                {
                    continue;
                }

                Destroy(t.gameObject);
            }
        }

        void Next()
        {
            var logs =
                HandHistoryManager.Instance.HandLogs;

            if (currentIndex < logs.Count - 1)
            {
                currentIndex++;
                Render();
            }
        }

        void Previous()
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                Render();
            }
        }

        string GetDisplayPlayerName(
            HandHistoryRecord record,
            string realName)
        {
            if (!hidePlayerNames)
                return realName;

            int index =
                record.Players.FindIndex(
                    p => p.Username == realName);

            if (index >= 0)
                return "Player " + (index + 1);

            return "Player";
        }

        string GetSeatRole(
            HandHistoryRecord record,
            int seat)
        {
            if (seat == record.DealerSeat)
                return "BTN";

            if (seat == record.SmallBlindSeat)
                return "SB";

            if (seat == record.BigBlindSeat)
                return "BB";

            return "UTG";
        }
    }
}