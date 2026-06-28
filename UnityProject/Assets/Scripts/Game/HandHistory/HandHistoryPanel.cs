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

        public Transform BoardContent;
        public GameObject BoardPrefab;

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

        public GameObject Board;

        public GameObject NoDataMsg;
        public GameObject TopButtonGrid;
        bool firsttimeShow = false;
        private void Start()
        {
            CloseButton.onClick.AddListener(() =>
                gameObject.SetActive(false));

            PreviousButton.onClick.AddListener(Previous);
            NextButton.onClick.AddListener(Next);
            HandSummaryButton.onClick.AddListener(HandSummaryButtonOnTap);
            HandDetailButton.onClick.AddListener(HandDetailButtonOnTap);
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
                PageCount.gameObject.SetActive(false);
                TopButtonGrid.SetActive(false);
                return;
            }
            else
            {
                NoDataMsg.SetActive(false);
                if (firsttimeShow == false)
                {
                    HandSummeryPanel.SetActive(true);
                    firsttimeShow = true;
                }
                PageCount.gameObject.SetActive(true);
                TopButtonGrid.SetActive(true);
            }
                

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

            Clear(BoardContent);
            Clear(HandPlayerContent);
            bool hasBoard =
    record.BoardCards != null &&
    record.BoardCards.Count > 0;

            Board.SetActive(hasBoard);
            if (hasBoard)
            {
                foreach (var card in record.BoardCards)
                {
                    var obj =
                        Instantiate(
                            BoardPrefab,
                            BoardContent);

                    obj.GetComponent<BoardPrefab>()
                        .SetCard(
                            card,
                            SmallCardSO);
                }
            }


            foreach (var player in record.Players)
            {
                var obj =
                    Instantiate(
                        HandPlayerPrefab,
                        HandPlayerContent);

                var item =
                    obj.GetComponent<HandPlayerPrefab>();

                item.SetData(
                    player.Username,
                    player.HandName,
                    player.ChipDifference,
                    player.IsWinner,
                    player.HoleCards,
                    SmallCardSO,
                    true   
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
            Clear(ShowdownContent);

            var record =
                HandHistoryManager.Instance.HandLogs[currentIndex];

            string currentStreet = "";

            HandDetailPrefab currentSection = null;

            foreach (var action in record.Actions)
            {
                if (currentStreet != action.Street)
                {
                    currentStreet =
                        action.Street;

                    var sectionObj =
                        Instantiate(
                            HandDetailPrefab,
                            HandDetailContent);

                    currentSection =
                        sectionObj.GetComponent<HandDetailPrefab>();

                    currentSection.GameStateName.text =
                        currentStreet;

                    if (currentStreet == "PRE_FLOP")
                    {
                        currentSection.CardContent
                            .gameObject
                            .SetActive(false);
                    }
                    else
                    {
                        currentSection.CardContent
                            .gameObject
                            .SetActive(true);
                    }

                    if (currentStreet == "FLOP" &&
                        record.BoardCards.Count >= 3)
                    {
                        AddStreetCards(
                            currentSection,
                            record.BoardCards.GetRange(0, 3));
                    }
                    else if (currentStreet == "TURN" &&
                             record.BoardCards.Count >= 4)
                    {
                        AddStreetCards(
                            currentSection,
                            record.BoardCards.GetRange(3, 1));
                    }
                    else if (currentStreet == "RIVER" &&
                             record.BoardCards.Count >= 5)
                    {
                        AddStreetCards(
                            currentSection,
                            record.BoardCards.GetRange(4, 1));
                    }


                }

                var playerObj =
                    Instantiate(
                        currentSection.PlayerTurnDetailPrefab,
                        currentSection.PlayerTurnDetailContent);

                var playerItem =
                    playerObj.GetComponent<PlayerTurnDetailPrefab>();
              
                
                playerItem.SetData(
    action.Username,
    action.Action,
    action.Amount,
    action.ChipsAfter
);
              
                Canvas.ForceUpdateCanvases();

                RectTransform contentRect =
                    currentSection.PlayerTurnDetailContent.GetComponent<RectTransform>();

                RectTransform rootRect =
                    currentSection.RootRect;

                rootRect.sizeDelta = new Vector2(
                    rootRect.sizeDelta.x,
                    contentRect.rect.height + 80f);

                float baseY = -contentRect.sizeDelta.y / 2f;
                contentRect.anchoredPosition = new Vector2(
                    contentRect.anchoredPosition.x,
                    baseY - 80f);
            }

            RenderShowdown(record);
            StartCoroutine(UpdateShowdownLayout());
            if (ShowDown != null)
            {
                ShowDown.transform.SetAsLastSibling();
            }
        }

        void RenderShowdown(HandHistoryRecord record)
        {
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

            Clear(ShowdownContent);

            if (!hasShowdown)
                return;

            foreach (var player in record.Players)
            {
                if (player.HoleCards == null ||
                    player.HoleCards.Count == 0)
                    continue;

                var obj =
                    Instantiate(
                        HandPlayerPrefab,
                        ShowdownContent);

                var item =
                    obj.GetComponent<HandPlayerPrefab>();

                item.PlayerName.text =
                    player.Username;

                item.PairText.text =
                    player.HandName;

                item.WinTrophy.SetActive(
                    player.IsWinner);

                item.Chips.text = "";

                foreach (var card in player.HoleCards)
                {
                    var cardObj =
                        Instantiate(
                            item.CardPrefab,
                            item.CardContent);

                    cardObj.GetComponent<CardPrefab>()
                        .SetCard(
                            card,
                            SmallCardSO);
                }
            }
        }

        private IEnumerator UpdateShowdownLayout()
        {
            ShowDown.SetActive(false);
            yield return null;
            yield return new WaitForEndOfFrame();
            
            Canvas.ForceUpdateCanvases();

            RectTransform showdownRect =
                ShowdownContent.GetComponent<RectTransform>();

            LayoutRebuilder.ForceRebuildLayoutImmediate(showdownRect);

            Canvas.ForceUpdateCanvases();

            RectTransform showDownRoot =
                ShowDown.GetComponent<RectTransform>();

            float height = LayoutUtility.GetPreferredHeight(showdownRect);

            if (height <= 0)
                height = showdownRect.rect.height;

            showDownRoot.sizeDelta = new Vector2(
                showDownRoot.sizeDelta.x,
                height + 80f);

            showdownRect.anchoredPosition = new Vector2(
                showdownRect.anchoredPosition.x,
                -(height / 2f) - 80f);
            ShowDown.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }


        void AddStreetCards(
            HandDetailPrefab section,
            List<string> cards)
        {
            foreach (var card in cards)
            {
                var cardObj =
                    Instantiate(
                        section.CardPrefab,
                        section.CardContent);

                cardObj.GetComponent<CardPrefab>()
                    .SetCard(
                        card,
                        SmallCardSO);
            }
        }

        void Clear(Transform parent)
        {
            foreach (Transform t in parent)
            {
                if ((ShowDown != null && t.gameObject == ShowDown) ||
                    (Board != null && t.gameObject == Board))
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
    }
}