using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class PosterShowManager : MonoBehaviour
{
    public GameObject PosterScreen;

    public Button CloseButton;
    public Button PreviousButton;
    public Button NextButton;

    public ScrollRect PosterScrollRect;
    public RectTransform PosterShowContent;

    public GameObject PosterShowImagePrefab;

    public GameObject DotPrefab;
    public Transform DotContent;

    public float SwipeThreshold = 50f;
    public float SnapDuration = 0.2f;

    private readonly List<PosterData> posters = new List<PosterData>();
    private readonly List<PosterImageShowPrefab> posterViews = new List<PosterImageShowPrefab>();
    private readonly List<PosterImageDotPrefab> dots = new List<PosterImageDotPrefab>();

    private int currentIndex = 0;
    private int dragStartIndex = 0;

    private float dragStartX;

    private bool isDragging;
    private bool isSnapping;
    private bool isLoading;

    private Coroutine snapCoroutine;

    private PosterScrollDragRelay dragRelay;

    private void Start()
    {
        if (PosterScreen != null) PosterScreen.SetActive(false);

        if (CloseButton != null) CloseButton.onClick.AddListener(OnCloseTap);
        if (PreviousButton != null) PreviousButton.onClick.AddListener(OnPreviousTap);
        if (NextButton != null) NextButton.onClick.AddListener(OnNextTap);

        SetupScroll();

        LoadWithDelay().Forget();
    }

    private async UniTaskVoid LoadWithDelay()
    {
        await UniTask.Delay(200);
        LoadPosters().Forget();
    }

    private void SetupScroll()
    {
        if (PosterScrollRect == null) return;

        PosterScrollRect.horizontal = true;
        PosterScrollRect.vertical = false;
        PosterScrollRect.inertia = false;
        PosterScrollRect.movementType = ScrollRect.MovementType.Clamped;

        if (PosterShowContent != null) PosterScrollRect.content = PosterShowContent;

        PosterScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        PosterScrollRect.onValueChanged.AddListener(OnScrollValueChanged);

        dragRelay = PosterScrollRect.GetComponent<PosterScrollDragRelay>();

        if (dragRelay == null) dragRelay = PosterScrollRect.gameObject.AddComponent<PosterScrollDragRelay>();

        dragRelay.Setup(this);
    }

    public void RefreshPosters()
    {
        LoadPosters().Forget();
    }

    private async UniTaskVoid LoadPosters()
    {
        if (isLoading) return;

        isLoading = true;

        try
        {
            var response = await ClubManager.Instance.GetPostersAsync(ClubContext.ClubId);

            posters.Clear();

            if (response != null && response.Posters != null)
            {
                foreach (PosterData poster in response.Posters)
                {
                    if (poster == null) continue;
                    if (string.IsNullOrEmpty(poster.Url)) continue;

                    posters.Add(poster);
                }
            }

            posters.Sort((a, b) => a.Order.CompareTo(b.Order));

            Debug.Log("Poster Count = " + posters.Count);

            if (posters.Count == 0)
            {
                ClearPosterViews();
                ClearDots();

                if (PosterScreen != null) PosterScreen.SetActive(false);

                return;
            }

            await DisplayAsync(0);

            Debug.Log("Poster Screen ON | Posters = " + posters.Count);
        }
        catch (Exception e)
        {
            Debug.LogError("[PosterShowManager] " + e);

            if (PosterScreen != null) PosterScreen.SetActive(false);
        }
        finally
        {
            isLoading = false;
        }
    }

    /// <summary>
    /// Open the viewer on posters the caller already has, at <paramref name="startIndex"/>
    /// — e.g. a thumbnail tapped in Club Info. No fetch.
    /// </summary>
    public void ShowPosters(List<PosterData> list, int startIndex)
    {
        if (list == null || list.Count == 0) return;

        posters.Clear();
        posters.AddRange(list);

        DisplayAsync(startIndex).Forget();
    }

    // Screen on, pages + dots built and sized, then jump to startIndex. Waits a frame
    // between steps so the viewport has a real width to size the pages from.
    private async UniTask DisplayAsync(int startIndex)
    {
        if (PosterScreen != null) PosterScreen.SetActive(true);

        await UniTask.Yield();
        Canvas.ForceUpdateCanvases();

        BuildPosters();
        BuildDots();

        currentIndex = 0;

        await UniTask.Yield();
        Canvas.ForceUpdateCanvases();

        SetupContentSize();

        await UniTask.Yield();
        Canvas.ForceUpdateCanvases();

        ShowPoster(startIndex, true);

        RefreshControls();
    }

    private void BuildPosters()
    {
        ClearPosterViews();

        if (PosterShowImagePrefab == null || PosterShowContent == null) return;

        for (int i = 0; i < posters.Count; i++)
        {
            GameObject obj = Instantiate(PosterShowImagePrefab, PosterShowContent);

            PosterImageShowPrefab view = obj.GetComponent<PosterImageShowPrefab>();

            if (view == null)
            {
                Debug.LogError("PosterImageShowPrefab script missing");
                Destroy(obj);
                continue;
            }

            view.Setup(posters[i].Url);

            posterViews.Add(view);
        }
    }

    private void SetupContentSize()
    {
        if (PosterScrollRect == null || PosterScrollRect.viewport == null || PosterShowContent == null) return;
        if (posterViews.Count == 0) return;

        float pageWidth = PosterScrollRect.viewport.rect.width;
        float pageHeight = PosterScrollRect.viewport.rect.height;

        if (pageWidth <= 0f) return;

        PosterShowContent.anchorMin = new Vector2(0f, 0f);
        PosterShowContent.anchorMax = new Vector2(0f, 1f);
        PosterShowContent.pivot = new Vector2(0f, 0.5f);
        PosterShowContent.sizeDelta = new Vector2(pageWidth * posterViews.Count, 0f);
        PosterShowContent.anchoredPosition = new Vector2(0f, PosterShowContent.anchoredPosition.y);

        for (int i = 0; i < posterViews.Count; i++)
        {
            RectTransform item = posterViews[i].transform as RectTransform;

            if (item == null) continue;

            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(0f, 0.5f);
            item.pivot = new Vector2(0f, 0.5f);

            item.sizeDelta = new Vector2(pageWidth, pageHeight);
            item.anchoredPosition = new Vector2(pageWidth * i, 0f);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void BuildDots()
    {
        ClearDots();

        if (posters.Count <= 1)
        {
            if (DotContent != null) DotContent.gameObject.SetActive(false);
            return;
        }

        if (DotContent == null || DotPrefab == null) return;

        DotContent.gameObject.SetActive(true);

        for (int i = 0; i < posters.Count; i++)
        {
            GameObject obj = Instantiate(DotPrefab, DotContent);
            PosterImageDotPrefab dot = obj.GetComponent<PosterImageDotPrefab>();

            if (dot != null) dots.Add(dot);
        }

        UpdateDots(0);
    }

    private void UpdateDots(int index)
    {
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] != null) dots[i].SetSelected(i == index);
        }
    }

    private void OnPreviousTap()
    {
        if (posterViews.Count <= 1 || isSnapping) return;

        int targetIndex = Mathf.Max(currentIndex - 1, 0);

        ShowPoster(targetIndex, false);
    }

    private void OnNextTap()
    {
        if (posterViews.Count <= 1 || isSnapping) return;

        int targetIndex = Mathf.Min(currentIndex + 1, posterViews.Count - 1);

        ShowPoster(targetIndex, false);
    }

    private void ShowPoster(int index, bool instant)
    {
        if (posterViews.Count == 0 || PosterScrollRect == null || PosterShowContent == null) return;

        index = Mathf.Clamp(index, 0, posterViews.Count - 1);

        if (snapCoroutine != null)
        {
            StopCoroutine(snapCoroutine);
            snapCoroutine = null;
        }

        PosterScrollRect.StopMovement();
        PosterScrollRect.velocity = Vector2.zero;

        if (instant)
        {
            currentIndex = index;

            Vector2 position = PosterShowContent.anchoredPosition;
            position.x = GetPagePosition(index);

            PosterShowContent.anchoredPosition = position;

            UpdateDots(currentIndex);
            RefreshControls();

            return;
        }

        snapCoroutine = StartCoroutine(SnapToPoster(index));
    }

    private IEnumerator SnapToPoster(int targetIndex)
    {
        if (PosterScrollRect == null || PosterShowContent == null) yield break;

        isSnapping = true;

        PosterScrollRect.StopMovement();
        PosterScrollRect.velocity = Vector2.zero;

        Vector2 startPosition = PosterShowContent.anchoredPosition;
        Vector2 targetPosition = startPosition;

        targetPosition.x = GetPagePosition(targetIndex);

        float timer = 0f;

        while (timer < SnapDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / SnapDuration);

            t = t * t * (3f - 2f * t);

            PosterShowContent.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);

            yield return null;
        }

        PosterShowContent.anchoredPosition = targetPosition;

        PosterScrollRect.StopMovement();
        PosterScrollRect.velocity = Vector2.zero;

        currentIndex = targetIndex;

        UpdateDots(currentIndex);
        RefreshControls();

        isSnapping = false;
        snapCoroutine = null;
    }

    private float GetPagePosition(int index)
    {
        if (PosterScrollRect == null || PosterScrollRect.viewport == null) return 0f;

        float pageWidth = PosterScrollRect.viewport.rect.width;

        return -(pageWidth * index);
    }

    public void OnPosterBeginDrag(float screenX)
    {
        if (posterViews.Count <= 1 || PosterScrollRect == null) return;

        if (snapCoroutine != null)
        {
            StopCoroutine(snapCoroutine);
            snapCoroutine = null;
        }

        isSnapping = false;
        isDragging = true;

        dragStartX = screenX;
        dragStartIndex = currentIndex;

        PosterScrollRect.StopMovement();
        PosterScrollRect.velocity = Vector2.zero;
    }

    public void OnPosterEndDrag(float screenX)
    {
        if (posterViews.Count <= 1 || PosterScrollRect == null) return;

        isDragging = false;

        PosterScrollRect.StopMovement();
        PosterScrollRect.velocity = Vector2.zero;

        float difference = screenX - dragStartX;

        int targetIndex = dragStartIndex;

        if (difference < -SwipeThreshold) targetIndex = Mathf.Min(dragStartIndex + 1, posterViews.Count - 1);
        else if (difference > SwipeThreshold) targetIndex = Mathf.Max(dragStartIndex - 1, 0);

        ShowPoster(targetIndex, false);
    }

    private void OnScrollValueChanged(Vector2 value)
    {
        if (posterViews.Count <= 1) return;

        int nearestIndex = GetNearestPosterIndex();

        if (isDragging) UpdateDots(nearestIndex);
    }

    private int GetNearestPosterIndex()
    {
        if (posterViews.Count <= 1 || PosterScrollRect == null || PosterScrollRect.viewport == null || PosterShowContent == null) return 0;

        float pageWidth = PosterScrollRect.viewport.rect.width;

        if (pageWidth <= 0f) return 0;

        float position = -PosterShowContent.anchoredPosition.x;

        int index = Mathf.RoundToInt(position / pageWidth);

        return Mathf.Clamp(index, 0, posterViews.Count - 1);
    }

    private void RefreshControls()
    {
        bool multiple = posterViews.Count > 1;

        if (PosterScrollRect != null)
        {
            PosterScrollRect.horizontal = multiple;

            if (!multiple)
            {
                PosterScrollRect.StopMovement();
                PosterScrollRect.velocity = Vector2.zero;
            }
        }

        if (PreviousButton != null)
        {
            PreviousButton.gameObject.SetActive(multiple);
            PreviousButton.interactable = multiple && currentIndex > 0;
        }

        if (NextButton != null)
        {
            NextButton.gameObject.SetActive(multiple);
            NextButton.interactable = multiple && currentIndex < posterViews.Count - 1;
        }

        if (DotContent != null) DotContent.gameObject.SetActive(multiple);

        UpdateDots(currentIndex);
    }

    private void OnCloseTap()
    {
        if (PosterScreen != null) PosterScreen.SetActive(false);
    }

    private void ClearPosterViews()
    {
        posterViews.Clear();

        if (PosterShowContent == null) return;

        for (int i = PosterShowContent.childCount - 1; i >= 0; i--)
        {
            Destroy(PosterShowContent.GetChild(i).gameObject);
        }
    }

    private void ClearDots()
    {
        dots.Clear();

        if (DotContent == null) return;

        for (int i = DotContent.childCount - 1; i >= 0; i--)
        {
            Destroy(DotContent.GetChild(i).gameObject);
        }
    }
}