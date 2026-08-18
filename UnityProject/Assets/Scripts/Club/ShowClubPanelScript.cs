using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using DG.Tweening;
using UnityEngine.EventSystems;

public class ShowClubPanelScript : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public GameObject ClubPrefab;
    public Transform Club_Content;
    public ClubBadgeSO ClubBadgeSO;

    private List<ClubPrefabScript> clubItems = new List<ClubPrefabScript>();
    public GameObject ShowClub_TableScreen;
    public ShowClubTableScreenScript ShowClubTableScreenScript;
    public GameObject JoinAndCreateClub_Panel;

    public Button Previous_Button;
    public Button Next_Button;
    public ScrollRect ClubScrollRect;

    private const string SCENE_CLUB = "Scene_Club";

    private int currentIndex = 0;
    private RectTransform contentRect;
    private RectTransform viewportRect;
    private Tween scrollTween;
    private bool isDragging = false;
    private bool isLoadingClubs = false;
    private Vector2 dragStartPosition;
    private const float SwipeThreshold = 50f;

    public Transform IndicatorGrid;
    public GameObject IndicatorPrefab;
    public float IndicatorHideDelay = 0.8f;
    public float IndicatorFadeDuration = 0.3f;

    private List<GameObject> indicatorItems = new List<GameObject>();
    private Tween indicatorHideTween;
    void Start()
    {
        contentRect = Club_Content.GetComponent<RectTransform>();

        if (ClubScrollRect != null)
        {
            ClubScrollRect.horizontal = true;
            ClubScrollRect.vertical = false;
            ClubScrollRect.inertia = false;
            ClubScrollRect.movementType =
    ScrollRect.MovementType.Elastic;

            ClubScrollRect.elasticity = 0.08f;

            if (ClubScrollRect.viewport != null)
                viewportRect = ClubScrollRect.viewport;
        }

        if (viewportRect == null && Club_Content.parent != null)
            viewportRect = Club_Content.parent.GetComponent<RectTransform>();

        if (Previous_Button != null)
            Previous_Button.onClick.AddListener(PreviousButtonOnTap);

        if (Next_Button != null)
            Next_Button.onClick.AddListener(NextButtonOnTap);

        LoadClubs().Forget();
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnMembershipApproved += HandleMembershipApproved;
        ClubSocketHandler.OnKicked += HandleMembershipKill;
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnMembershipApproved -= HandleMembershipApproved;
        ClubSocketHandler.OnKicked -= HandleMembershipKill;
    }


    private void HandleMembershipKill(ClubKickedPayload payload)
    {
        LoadClubs().Forget();
    }
    private void HandleMembershipApproved(ClubMembershipApprovedPayload payload)
    {
        LoadClubs().Forget();
    }

    private void HandleJoinNotification(string json)
    {
        Debug.Log("[ShowClub] player:join_notification received => " + json);

        LoadClubs().Forget();
    }

    public async UniTaskVoid LoadClubs()
    {
        if (isLoadingClubs)
            return;

        isLoadingClubs = true;

        try
        {
            ClearClubs();

            List<ClubListData> clubs =
                await AuthManager.Instance.GetClubsAsync();

            foreach (ClubListData club in clubs)
            {
                GameObject obj =
                    Instantiate(ClubPrefab, Club_Content);

                ClubPrefabScript prefab =
                    obj.GetComponent<ClubPrefabScript>();

                Sprite badgeSprite =
                    GetBadgeSprite(club.Badge);

                prefab.Setup(
                    club,
                    badgeSprite,
                    this
                );

                clubItems.Add(prefab);
            }

            // DEFAULT FIRST CLUB
            // No button tap required.
            if (clubs != null && clubs.Count > 0)
            {
                ClubListData firstClub = clubs[0];

                Debug.Log(
                    "[ShowClub] Default First Club: " +
                    firstClub.Name);

                Debug.Log(
                    "[ShowClub] Default First Club ID: " +
                    firstClub.ClubId);

                if (ClubSocketHandler.Instance != null)
                {
                    ClubSocketHandler.Instance.JoinClubPage(
                        firstClub.ClubId
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[ShowClub] ClubSocketHandler.Instance is null.");
                }
            }

            GenerateIndicators();

            await UniTask.DelayFrame(2);

            if (contentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    contentRect);

            if (JoinAndCreateClub_Panel != null)
                JoinAndCreateClub_Panel.SetActive(
                    clubs.Count == 0);

            currentIndex = 0;

            StopScrollVelocity();

            SetScrollPositionInstant();

            UpdateScrollButtons();
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "[ShowClub] LoadClubs failed: " +
                e.Message);
        }

        isLoadingClubs = false;
    }
    private void GenerateIndicators()
    {
        ClearIndicators();

        if (IndicatorGrid == null || IndicatorPrefab == null)
            return;

        for (int i = 0; i < clubItems.Count; i++)
        {
            GameObject indicator = Instantiate(IndicatorPrefab, IndicatorGrid);
            indicatorItems.Add(indicator);
        }

        UpdateIndicatorHighlight();
        HideIndicatorsImmediate();
    }

    private void ClearIndicators()
    {
        indicatorHideTween?.Kill();

        foreach (GameObject indicator in indicatorItems)
        {
            if (indicator != null)
                Destroy(indicator);
        }

        indicatorItems.Clear();
    }

    private void UpdateIndicatorHighlight()
    {
        for (int i = 0; i < indicatorItems.Count; i++)
        {
            if (indicatorItems[i] == null)
                continue;

            Image image = indicatorItems[i].GetComponent<Image>();

            if (image != null)
            {
                Color color = image.color;

                if (i == currentIndex)
                {
                    color.a = 1f;
                }
                else
                {
                    color.a = 0.35f;
                }

                image.color = color;
            }
        }
    }

    private void ShowIndicators()
    {
        if (indicatorItems.Count == 0)
            return;

        indicatorHideTween?.Kill();

        foreach (GameObject indicator in indicatorItems)
        {
            if (indicator == null)
                continue;

            CanvasGroup canvasGroup =
                indicator.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = indicator.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 1f;
            indicator.SetActive(true);
        }

        UpdateIndicatorHighlight();

        indicatorHideTween = DOVirtual.DelayedCall(
            IndicatorHideDelay,
            HideIndicators
        );
    }

    private void HideIndicators()
    {
        indicatorHideTween?.Kill();

        foreach (GameObject indicator in indicatorItems)
        {
            if (indicator == null)
                continue;

            CanvasGroup canvasGroup =
                indicator.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = indicator.AddComponent<CanvasGroup>();

            canvasGroup
                .DOFade(0f, IndicatorFadeDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (indicator != null)
                        indicator.SetActive(false);
                });
        }
    }

    private void HideIndicatorsImmediate()
    {
        indicatorHideTween?.Kill();

        foreach (GameObject indicator in indicatorItems)
        {
            if (indicator == null)
                continue;

            CanvasGroup canvasGroup =
                indicator.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = indicator.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            indicator.SetActive(false);
        }
    }
    void ClearClubs()
    {
        scrollTween?.Kill();
        StopScrollVelocity();

        clubItems.Clear();

        for (int i = Club_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Club_Content.GetChild(i).gameObject);
        }
    }

    private void PreviousButtonOnTap()
    {
        if (isDragging || IsTweenPlaying() || clubItems.Count == 0)
            return;

        StopScrollVelocity();

        if (currentIndex > 0)
        {
            currentIndex--;
            SmoothScrollToCurrentIndex();
            ShowIndicators();
            return;
        }

        ScrollFirstToLast();
    }

    private void NextButtonOnTap()
    {
        if (isDragging || IsTweenPlaying() || clubItems.Count == 0)
            return;

        StopScrollVelocity();

        if (currentIndex < clubItems.Count - 1)
        {
            currentIndex++;
            SmoothScrollToCurrentIndex();
            ShowIndicators();
            return;
        }

        ScrollLastToFirst();
    }
    private void ScrollLastToFirst()
    {
        if (contentRect == null ||
            viewportRect == null ||
            Club_Content.childCount == 0)
            return;

        StopScrollVelocity();
        scrollTween?.Kill();

        GameObject temporaryFirst =
            Instantiate(
                Club_Content.GetChild(0).gameObject,
                Club_Content
            );

        temporaryFirst.name = "Temporary_First_Club";

        RectTransform temporaryRect =
            temporaryFirst.GetComponent<RectTransform>();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();

        float targetX = GetTargetXForRect(temporaryRect);

        scrollTween = contentRect
            .DOAnchorPosX(targetX, 0.35f)
            .SetEase(Ease.OutCubic)
            .OnUpdate(StopScrollVelocity)
            .OnComplete(() =>
            {
                Destroy(temporaryFirst);

                currentIndex = 0;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

                SetScrollPositionInstant();
                StopScrollVelocity();
                UpdateScrollButtons();
                UpdateIndicatorHighlight();
                ShowIndicators();
            });
    }
    private void ScrollFirstToLast()
    {
        if (contentRect == null ||
            viewportRect == null ||
            Club_Content.childCount == 0)
            return;

        StopScrollVelocity();
        scrollTween?.Kill();

        int lastIndex = Club_Content.childCount - 1;

        GameObject temporaryLast =
            Instantiate(
                Club_Content.GetChild(lastIndex).gameObject,
                Club_Content
            );

        temporaryLast.name = "Temporary_Last_Club";
        temporaryLast.transform.SetAsFirstSibling();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();

        
        RectTransform realFirstRect =
            Club_Content.GetChild(1).GetComponent<RectTransform>();

        float realFirstX = GetTargetXForRect(realFirstRect);

        contentRect.anchoredPosition = new Vector2(
            realFirstX,
            contentRect.anchoredPosition.y
        );

        Canvas.ForceUpdateCanvases();

        RectTransform temporaryRect =
            temporaryLast.GetComponent<RectTransform>();

        float targetX = GetTargetXForRect(temporaryRect);

        scrollTween = contentRect
            .DOAnchorPosX(targetX, 0.35f)
            .SetEase(Ease.OutCubic)
            .OnUpdate(StopScrollVelocity)
            .OnComplete(() =>
            {
                Destroy(temporaryLast);

                currentIndex = clubItems.Count - 1;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

                SetScrollPositionInstant();
                StopScrollVelocity();
                UpdateScrollButtons();
                UpdateIndicatorHighlight();
                ShowIndicators();
            });
    }
    private float GetTargetXForRect(RectTransform item)
    {
        if (contentRect == null ||
            viewportRect == null ||
            item == null)
        {
            return contentRect != null
                ? contentRect.anchoredPosition.x
                : 0f;
        }

        Vector3[] viewportCorners = new Vector3[4];
        viewportRect.GetWorldCorners(viewportCorners);

        Vector3[] itemCorners = new Vector3[4];
        item.GetWorldCorners(itemCorners);

        float viewportCenterX =
            (viewportCorners[0].x + viewportCorners[3].x) * 0.5f;

        float itemCenterX =
            (itemCorners[0].x + itemCorners[3].x) * 0.5f;

        float difference =
            viewportCenterX - itemCenterX;

        return contentRect.anchoredPosition.x + difference;
    }
    private void SmoothScrollToCurrentIndex()
    {
        if (contentRect == null || viewportRect == null)
            return;

        StopScrollVelocity();

        float targetX = GetTargetXForIndex(currentIndex);

        scrollTween?.Kill();

        scrollTween = contentRect
            .DOAnchorPosX(targetX, 0.35f)
            .SetEase(Ease.OutCubic)
            .OnUpdate(StopScrollVelocity)
            .OnComplete(() =>
            {
                StopScrollVelocity();
                UpdateScrollButtons();
                ShowIndicators();
            });
    }

    private void SetScrollPositionInstant()
    {
        if (contentRect == null || viewportRect == null)
            return;

        float targetX = GetTargetXForIndex(currentIndex);

        contentRect.anchoredPosition =
            new Vector2(targetX, contentRect.anchoredPosition.y);
    }

    private int GetClosestItemIndex()
    {
        if (viewportRect == null || Club_Content.childCount == 0)
            return currentIndex;

        Vector3[] viewportCorners = new Vector3[4];
        viewportRect.GetWorldCorners(viewportCorners);

        float viewportCenterX =
            (viewportCorners[0].x + viewportCorners[3].x) * 0.5f;

        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < Club_Content.childCount; i++)
        {
            RectTransform item =
                Club_Content.GetChild(i).GetComponent<RectTransform>();

            if (item == null)
                continue;

            Vector3[] itemCorners = new Vector3[4];
            item.GetWorldCorners(itemCorners);

            float itemCenterX =
                (itemCorners[0].x + itemCorners[3].x) * 0.5f;

            float distance =
                Mathf.Abs(viewportCenterX - itemCenterX);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private float GetTargetXForIndex(int index)
    {
        if (contentRect == null || viewportRect == null)
            return 0f;

        if (Club_Content.childCount == 0)
            return 0f;

        index = Mathf.Clamp(index, 0, Club_Content.childCount - 1);

        RectTransform item =
            Club_Content.GetChild(index).GetComponent<RectTransform>();

        if (item == null)
            return contentRect.anchoredPosition.x;

        Vector3[] viewportCorners = new Vector3[4];
        viewportRect.GetWorldCorners(viewportCorners);

        Vector3[] itemCorners = new Vector3[4];
        item.GetWorldCorners(itemCorners);

        float viewportCenterX =
            (viewportCorners[0].x + viewportCorners[3].x) * 0.5f;

        float itemCenterX =
            (itemCorners[0].x + itemCorners[3].x) * 0.5f;

        float difference = viewportCenterX - itemCenterX;

        return contentRect.anchoredPosition.x + difference;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;

        dragStartPosition = eventData.position;

        scrollTween?.Kill();
        StopScrollVelocity();

        ShowIndicators();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        StopScrollVelocity();

        if (clubItems.Count == 0)
            return;

        float dragDistanceX =
            eventData.position.x - dragStartPosition.x;

        bool draggedLeft =
            dragDistanceX < -SwipeThreshold;

        bool draggedRight =
            dragDistanceX > SwipeThreshold;

        if (currentIndex == clubItems.Count - 1 && draggedLeft)
        {
            ScrollLastToFirst();
            return;
        }

        if (currentIndex == 0 && draggedRight)
        {
            ScrollFirstToLast();
            return;
        }

        currentIndex = GetClosestItemIndex();

        currentIndex = Mathf.Clamp(
            currentIndex,
            0,
            clubItems.Count - 1
        );

        SmoothScrollToCurrentIndex();
    }

    private void StopScrollVelocity()
    {
        if (ClubScrollRect != null)
            ClubScrollRect.velocity = Vector2.zero;
    }

    private bool IsTweenPlaying()
    {
        return scrollTween != null &&
               scrollTween.IsActive() &&
               scrollTween.IsPlaying();
    }

    private void UpdateScrollButtons()
    {
        bool canScroll = clubItems.Count > 1;

        if (Previous_Button != null)
            Previous_Button.gameObject.SetActive(canScroll);

        if (Next_Button != null)
            Next_Button.gameObject.SetActive(canScroll);
    }

    Sprite GetBadgeSprite(string badgeKey)
    {
        if (string.IsNullOrEmpty(badgeKey))
            return null;

        if (ClubBadgeSO == null || ClubBadgeSO.ClubBadges == null)
            return null;

        foreach (ClubBadgeData badge in ClubBadgeSO.ClubBadges)
        {
            if (badge.BadgeName.ToLower() == badgeKey.ToLower())
            {
                return badge.BadgeImage;
            }
        }

        return null;
    }

    public void OnClubSelected(ClubListData club)
    {
        Debug.Log("Selected Club: " + club.Name);
        Debug.Log("Club ID: " + club.ClubId);
        Debug.Log("Club Code: " + club.ClubCode);

        if (ClubSocketHandler.Instance != null)
            ClubSocketHandler.Instance.JoinClubPage(club.ClubId);

        // Carry the selection across the scene boundary.
        ClubContext.SelectClub(club);

        {
            // Running in MainMenu — no controller here. Load ClubScene; its
            // ClubViewController reads ClubContext.SelectedClub on Start.
            GameSceneManager.Instance.LoadScene(SCENE_CLUB);
        }
    }
}