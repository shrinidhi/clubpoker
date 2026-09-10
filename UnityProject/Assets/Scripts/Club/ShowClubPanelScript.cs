using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using DG.Tweening;
using UnityEngine.EventSystems;

public class ShowClubPanelScript : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    private const float SwipeThreshold = 50f;   // floor, in screen px
    private const float ScrollDuration = 0.35f;

    [Header("Swipe Feel")]
    [Tooltip("How far the finger must travel, as a share of the viewport width, before the " +
             "swipe changes club. Below this the current club springs back. 0.3–0.4 feels " +
             "deliberate; lower is twitchy.")]
    [Range(0.05f, 0.9f)]
    public float SwipeDistanceRatio = 0.35f;

    [Tooltip("Gap between two club cards, in UI units. Cards are positioned by this script, " +
             "so no layout group is involved.")]
    public float CardSpacing = 40f;

    // Card width, read off the first card once it exists. Everything else is derived.
    private float cardWidth;

    // Canvas scale factor, needed to turn a screen-pixel drag into UI units.
    private Canvas parentCanvas;

    [Tooltip("Temporary: logs viewport / card / content geometry so the carousel can be " +
             "diagnosed on device. Turn off before shipping.")]
    public bool LogGeometry = false;

    public Transform IndicatorGrid;
    public GameObject IndicatorPrefab;

    public float IndicatorHideDelay = 0.8f;
    public float IndicatorFadeDuration = 0.3f;

    private List<GameObject> indicatorItems =
        new List<GameObject>();

    private Tween indicatorHideTween;


    void Start()
    {
        contentRect =
            Club_Content.GetComponent<RectTransform>();

        // The carousel positions its own cards, so a ScrollRect would only fight it —
        // clamping the snap and re-centring the content behind our back. If one is still
        // assigned in the scene, switch it off rather than deleting the reference.
        if (ClubScrollRect != null)
        {
            if (ClubScrollRect.viewport != null)
                viewportRect = ClubScrollRect.viewport;

            ClubScrollRect.enabled = false;
        }

        if (viewportRect == null &&
            Club_Content.parent != null)
        {
            viewportRect =
                Club_Content.parent.GetComponent<RectTransform>();
        }

        // Centring a card means content.x = -index * step, which only holds if the content
        // itself is centred on the viewport. Pinned here so the scene cannot get it wrong.
        if (contentRect != null)
        {
            contentRect.anchorMin = CardAnchor;
            contentRect.anchorMax = CardAnchor;
            contentRect.pivot = CardAnchor;

            contentRect.anchoredPosition = Vector2.zero;

            if (viewportRect != null)
                contentRect.sizeDelta = viewportRect.rect.size;
        }

        if (Previous_Button != null)
        {
            Previous_Button.onClick.AddListener(
                PreviousButtonOnTap
            );
        }

        if (Next_Button != null)
        {
            Next_Button.onClick.AddListener(
                NextButtonOnTap
            );
        }

        LoadClubs().Forget();
    }

    private void OnEnable()
    {
        ClubSocketHandler.OnMembershipApproved +=
            HandleMembershipApproved;

        ClubSocketHandler.OnKicked +=
            HandleMembershipKill;
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnMembershipApproved -=
            HandleMembershipApproved;

        ClubSocketHandler.OnKicked -=
            HandleMembershipKill;
    }

    private void HandleMembershipKill(
        ClubKickedPayload payload)
    {
        LoadClubs().Forget();
    }

    private void HandleMembershipApproved(
        ClubMembershipApprovedPayload payload)
    {
        LoadClubs().Forget();
    }

    private void HandleJoinNotification(string json)
    {
        Debug.Log(
            "[ShowClub] player:join_notification received => " +
            json
        );

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

            if (clubs == null)
                clubs = new List<ClubListData>();

            foreach (ClubListData club in clubs)
            {
                GameObject obj =
                    Instantiate(
                        ClubPrefab,
                        Club_Content
                    );

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

            if (clubs.Count > 0)
            {
                ClubListData firstClub =
                    clubs[0];

                Debug.Log(
                    "[ShowClub] Default First Club: " +
                    firstClub.Name
                );

                Debug.Log(
                    "[ShowClub] Default First Club ID: " +
                    firstClub.ClubId
                );

                if (ClubSocketHandler.Instance != null)
                {
                    ClubSocketHandler.Instance.JoinClubPage(
                        firstClub.ClubId
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[ShowClub] " +
                        "ClubSocketHandler.Instance is null."
                    );
                }
            }

            GenerateIndicators();

            await UniTask.DelayFrame(2);

            Canvas.ForceUpdateCanvases();

            if (contentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    contentRect
                );
            }

            Canvas.ForceUpdateCanvases();

            if (JoinAndCreateClub_Panel != null)
            {
                JoinAndCreateClub_Panel.SetActive(
                    clubs.Count == 0
                );
            }


            currentIndex = 0;

            SetScrollPositionInstant();

            UpdateScrollButtons();

            LogCarouselGeometry("after load");
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "[ShowClub] LoadClubs failed: " +
                e.Message
            );
        }

        isLoadingClubs = false;
    }

    private void GenerateIndicators()
    {
        ClearIndicators();

        if (IndicatorGrid == null ||
            IndicatorPrefab == null)
            return;

        for (int i = 0;
                 i < clubItems.Count;
                 i++)
        {
            GameObject indicator =
                Instantiate(
                    IndicatorPrefab,
                    IndicatorGrid
                );

            indicatorItems.Add(indicator);
        }

        UpdateIndicatorHighlight();

        HideIndicatorsImmediate();
    }


    private void ClearIndicators()
    {
        indicatorHideTween?.Kill();

        foreach (GameObject indicator
                 in indicatorItems)
        {
            if (indicator != null)
                Destroy(indicator);
        }

        indicatorItems.Clear();
    }


    private void UpdateIndicatorHighlight()
    {
        int highlighted = CurrentClubIndex;

        for (int i = 0;
             i < indicatorItems.Count;
             i++)
        {
            if (indicatorItems[i] == null)
                continue;

            Image image =
                indicatorItems[i].GetComponent<Image>();

            if (image == null)
                continue;

            Color color = image.color;

            if (i == highlighted)
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


    private void ShowIndicators()
    {
        if (indicatorItems.Count == 0)
            return;

        indicatorHideTween?.Kill();

        foreach (GameObject indicator
                 in indicatorItems)
        {
            if (indicator == null)
                continue;

            CanvasGroup canvasGroup =
                indicator.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup =
                    indicator.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f;

            indicator.SetActive(true);
        }

        UpdateIndicatorHighlight();

        indicatorHideTween =
            DOVirtual.DelayedCall(
                IndicatorHideDelay,
                HideIndicators
            );
    }


    private void HideIndicators()
    {
        indicatorHideTween?.Kill();

        foreach (GameObject indicator
                 in indicatorItems)
        {
            if (indicator == null)
                continue;

            CanvasGroup canvasGroup =
                indicator.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup =
                    indicator.AddComponent<CanvasGroup>();
            }

            canvasGroup
                .DOFade(
                    0f,
                    IndicatorFadeDuration
                )
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

        foreach (GameObject indicator
                 in indicatorItems)
        {
            if (indicator == null)
                continue;

            CanvasGroup canvasGroup =
                indicator.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup =
                    indicator.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;

            indicator.SetActive(false);
        }
    }

    void ClearClubs()
    {
        scrollTween?.Kill();

        clubItems.Clear();

        // Re-measured off the next batch of cards.
        cardWidth = 0f;

        if (Club_Content == null)
            return;

        for (int i =
                 Club_Content.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                Club_Content.GetChild(i).gameObject
            );
        }
    }

    private void PreviousButtonOnTap()
    {
        StepBy(-1);
    }

    private void NextButtonOnTap()
    {
        StepBy(+1);
    }

    /// <summary>
    /// Move one club left or right. currentIndex is a virtual index with no bounds — it may
    /// go negative or past the club count — so the ends need no special case: stepping off
    /// one end simply lands on the club that ArrangeCards has already placed there.
    /// </summary>
    private void StepBy(int direction)
    {
        if (isDragging ||
            IsTweenPlaying() ||
            clubItems.Count <= 1)
            return;

        currentIndex += direction;

        SmoothScrollToCurrentIndex();

        ShowIndicators();
    }

    // ── Card placement ──────────────────────────────────────────────────────
    //
    // No ScrollRect, no layout group, no content size fitter. Cards are positioned by
    // hand at multiples of one "step" (card width + spacing), and centring club i is just
    // content.x = -i * step. That removes every device-dependent variable the old setup
    // fought with: clamping, side padding, fitter-driven content width.
    //
    // currentIndex is virtual and unbounded. ArrangeCards places each card at the copy of
    // its slot nearest the current index, so the list is endless in both directions and
    // wrapping is an ordinary one-step slide.

    private float Step
    {
        get
        {
            if (cardWidth <= 0f)
                MeasureCard();

            return cardWidth + CardSpacing;
        }
    }

    private void MeasureCard()
    {
        if (Club_Content == null || Club_Content.childCount == 0)
            return;

        RectTransform card =
            Club_Content.GetChild(0) as RectTransform;

        if (card != null && card.rect.width > 0f)
            cardWidth = card.rect.width;
    }

    /// <summary>
    /// Cards are placed by anchoredPosition, which only means "offset from the content's
    /// centre" when the card is centre-anchored. A stretch-anchored prefab would instead be
    /// glued to the content's edges and ignore the placement entirely, so pin it once —
    /// keeping the size it currently renders at.
    /// </summary>
    private void NormalizeCard(RectTransform card)
    {
        if (card == null)
            return;

        if (card.anchorMin == CardAnchor &&
            card.anchorMax == CardAnchor &&
            card.pivot == CardAnchor)
            return;

        Vector2 size = card.rect.size;

        card.anchorMin = CardAnchor;
        card.anchorMax = CardAnchor;
        card.pivot = CardAnchor;

        card.sizeDelta = size;
    }

    private static readonly Vector2 CardAnchor = new Vector2(0.5f, 0.5f);

    /// <summary>
    /// Put every card at the repeat of its slot closest to the club on screen, so the two
    /// neighbours are always populated however far currentIndex has travelled. Cards only
    /// ever jump while off screen.
    /// </summary>
    private void ArrangeCards()
    {
        int count = Club_Content != null ? Club_Content.childCount : 0;

        if (count == 0)
            return;

        MeasureCard();

        float step = Step;

        for (int i = 0; i < count; i++)
        {
            RectTransform card =
                Club_Content.GetChild(i) as RectTransform;

            if (card == null)
                continue;

            NormalizeCard(card);

            // Which repeat of slot i sits nearest the centred index.
            int repeat =
                count > 1
                    ? Mathf.RoundToInt((currentIndex - i) / (float)count)
                    : 0;

            card.anchoredPosition =
                new Vector2(
                    (i + repeat * count) * step,
                    0f
                );
        }
    }

    /// <summary>Content x that centres the current virtual index.</summary>
    private float GetTargetXForIndex(int index)
    {
        return -index * Step;
    }

    /// <summary>The club (in load order) currently centred — currentIndex can be any integer.</summary>
    private int CurrentClubIndex
    {
        get
        {
            int count = clubItems.Count;

            if (count == 0)
                return 0;

            return ((currentIndex % count) + count) % count;
        }
    }

    // Temporary: dump the geometry the snap depends on. Turn off once the carousel is right.
    private void LogCarouselGeometry(string where)
    {
        if (!LogGeometry)
            return;

        RectTransform card =
            Club_Content != null && Club_Content.childCount > 0
                ? Club_Content.GetChild(0).GetComponent<RectTransform>()
                : null;

        HorizontalLayoutGroup layout =
            Club_Content != null
                ? Club_Content.GetComponent<HorizontalLayoutGroup>()
                : null;

        Debug.Log(
            $"[ShowClub/{where}] " +
            $"viewportW={(viewportRect != null ? viewportRect.rect.width : -1f)} " +
            $"contentW={(contentRect != null ? contentRect.rect.width : -1f)} " +
            $"cardW={(card != null ? card.rect.width : -1f)} " +
            $"children={(Club_Content != null ? Club_Content.childCount : 0)} " +
            $"layoutGroup={(layout != null ? (layout.enabled ? "on" : "disabled") : "MISSING")} " +
            $"padL={(layout != null ? layout.padding.left : -1)} " +
            $"spacing={(layout != null ? layout.spacing : -1f)} " +
            $"index={currentIndex} " +
            $"targetX={GetTargetXForIndex(currentIndex)} " +
            $"actualX={(contentRect != null ? contentRect.anchoredPosition.x : -1f)}"
        );
    }

    private void SmoothScrollToCurrentIndex()
    {
        if (contentRect == null ||
            viewportRect == null ||
            Club_Content.childCount == 0)
            return;

        scrollTween?.Kill();

        // Move the dot with the finger release, not a third of a second later.
        UpdateIndicatorHighlight();

        // Re-seat the cards around the new index before sliding, so the card being scrolled
        // to is already in place even when the index just wrapped past either end.
        ArrangeCards();

        scrollTween =
            contentRect
                .DOAnchorPosX(
                    GetTargetXForIndex(currentIndex),
                    ScrollDuration
                )
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    UpdateScrollButtons();

                    UpdateIndicatorHighlight();

                    ShowIndicators();

                    LogCarouselGeometry("after snap");
                });
    }

    private void SetScrollPositionInstant()
    {
        if (contentRect == null ||
            Club_Content == null ||
            Club_Content.childCount == 0)
            return;

        ArrangeCards();

        contentRect.anchoredPosition =
            new Vector2(
                GetTargetXForIndex(currentIndex),
                contentRect.anchoredPosition.y
            );
    }

    public void OnBeginDrag(
        PointerEventData eventData)
    {
        if (clubItems.Count <= 1)
            return;

        isDragging = true;
        dragStartPosition = eventData.position;
        scrollTween?.Kill();

        ShowIndicators();
    }

    // The content is moved by hand now — there is no ScrollRect to drag it.
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || contentRect == null)
            return;

        contentRect.anchoredPosition +=
            new Vector2(
                eventData.delta.x / CanvasScale,
                0f
            );
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        if (clubItems.Count == 0)
            return;

        float threshold = GetSwipeThresholdPixels();

        float dragDistanceX = eventData.position.x - dragStartPosition.x;
        bool draggedLeft = dragDistanceX < -threshold;
        bool draggedRight = dragDistanceX > threshold;

        // Past the threshold: exactly one club, however far the drag ran. Below it, the
        // current club slides back. currentIndex is unbounded, so the ends need no case
        // of their own — stepping off one wraps onto the other.
        if (draggedLeft)
        {
            currentIndex++;
        }
        else if (draggedRight)
        {
            currentIndex--;
        }

        SmoothScrollToCurrentIndex();
        ShowIndicators();
    }

    // Screen pixels → UI units. Without this the drag runs at the wrong speed on any device
    // whose canvas is not scaling 1:1.
    private float CanvasScale
    {
        get
        {
            if (parentCanvas == null)
                parentCanvas = GetComponentInParent<Canvas>();

            float scale =
                parentCanvas != null
                    ? parentCanvas.scaleFactor
                    : 1f;

            return Mathf.Approximately(scale, 0f) ? 1f : scale;
        }
    }

    // Drag distance is in screen pixels, so the threshold has to be too — a fixed px value
    // means a different swipe on every device. Measured off the viewport each release, which
    // costs nothing and survives resolution changes.
    private float GetSwipeThresholdPixels()
    {
        if (viewportRect == null)
            return SwipeThreshold;

        Vector3[] corners = new Vector3[4];
        viewportRect.GetWorldCorners(corners);

        Canvas canvas = viewportRect.GetComponentInParent<Canvas>();

        // Overlay canvases have no camera; WorldToScreenPoint(null, p) is the identity there.
        Camera cam =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

        float left  = RectTransformUtility.WorldToScreenPoint(cam, corners[0]).x;
        float right = RectTransformUtility.WorldToScreenPoint(cam, corners[3]).x;

        float viewportWidthPixels = Mathf.Abs(right - left);

        return Mathf.Max(
            SwipeThreshold,
            viewportWidthPixels * SwipeDistanceRatio
        );
    }

    private bool IsTweenPlaying()
    {
        return
            scrollTween != null &&
            scrollTween.IsActive() &&
            scrollTween.IsPlaying();
    }

    private void UpdateScrollButtons()
    {
        bool canScroll = clubItems.Count > 1;

        if (Previous_Button != null)
        {
            Previous_Button.gameObject.SetActive(canScroll);
        }
        if (Next_Button != null)
        {
            Next_Button.gameObject.SetActive(canScroll);
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

    public void OnClubSelected(
        ClubListData club)
    {
        Debug.Log("Selected Club: " +club.Name);
        Debug.Log("Club ID: " +club.ClubId);
        Debug.Log("Club Code: " +club.ClubCode);
        if (ClubSocketHandler.Instance != null)
        {
            ClubSocketHandler.Instance.JoinClubPage(club.ClubId);
        }
        ClubContext.SelectClub(club);
        {
            GameSceneManager.Instance.LoadScene(SCENE_CLUB);
        }
    }

    private void OnDestroy()
    {
        scrollTween?.Kill();

        indicatorHideTween?.Kill();


        if (Previous_Button != null)
        {
            Previous_Button.onClick.RemoveListener(
                PreviousButtonOnTap
            );
        }


        if (Next_Button != null)
        {
            Next_Button.onClick.RemoveListener(
                NextButtonOnTap
            );
        }
    }
}