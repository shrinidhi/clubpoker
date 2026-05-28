using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
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

    private int currentIndex = 0;
    private RectTransform contentRect;
    private RectTransform viewportRect;
    private Tween scrollTween;
    private bool isDragging = false;

    void Start()
    {
        contentRect = Club_Content.GetComponent<RectTransform>();

        if (ClubScrollRect != null)
        {
            ClubScrollRect.horizontal = true;
            ClubScrollRect.vertical = false;
            ClubScrollRect.inertia = false;
            ClubScrollRect.movementType = ScrollRect.MovementType.Clamped;
            ClubScrollRect.elasticity = 0f;

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

     public async UniTaskVoid LoadClubs()
    {
        ClearClubs();

        List<ClubListData> clubs =
            await AuthManager.Instance.GetClubsAsync();

        foreach (ClubListData club in clubs)
        {
            GameObject obj = Instantiate(ClubPrefab, Club_Content);

            ClubPrefabScript prefab =
                obj.GetComponent<ClubPrefabScript>();

            Sprite badgeSprite = GetBadgeSprite(club.Badge);

            prefab.Setup(club, badgeSprite, this);
            clubItems.Add(prefab);

           
        }
        await UniTask.DelayFrame(2);

        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        if (JoinAndCreateClub_Panel != null)
            JoinAndCreateClub_Panel.SetActive(clubs.Count == 0);

        currentIndex = 0;
        StopScrollVelocity();
        SetScrollPositionInstant();
        UpdateScrollButtons();
    }

    void ClearClubs()
    {
        scrollTween?.Kill();
        StopScrollVelocity();

        clubItems.Clear();

        for (int i = 0; i < Club_Content.childCount; i++)
        {
            Destroy(Club_Content.GetChild(i).gameObject);
        }
    }
    private void PreviousButtonOnTap()
    {
        if (isDragging)
            return;

        if (IsTweenPlaying())
            return;

        if (currentIndex <= 0)
            return;

        StopScrollVelocity();

        currentIndex--;
        SmoothScrollToCurrentIndex();
    }

    private void NextButtonOnTap()
    {
        if (isDragging)
            return;

        if (IsTweenPlaying())
            return;

        if (currentIndex >= clubItems.Count - 1)
            return;

        StopScrollVelocity();

        currentIndex++;
        SmoothScrollToCurrentIndex();
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

        scrollTween?.Kill();
        StopScrollVelocity();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        StopScrollVelocity();

        currentIndex = GetClosestItemIndex();

        currentIndex = Mathf.Clamp(
            currentIndex,
            0,
            Mathf.Max(0, clubItems.Count - 1)
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
        bool hasClubs = clubItems.Count > 0;

        if (Previous_Button != null)
        {
            Previous_Button.gameObject.SetActive(hasClubs);
            Previous_Button.interactable = currentIndex > 0;
        }

        if (Next_Button != null)
        {
            Next_Button.gameObject.SetActive(hasClubs);
            Next_Button.interactable = currentIndex < clubItems.Count - 1;
        }
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

        ShowClub_TableScreen.SetActive(true);
        ShowClubTableScreenScript.ShowData(club);
    }
}