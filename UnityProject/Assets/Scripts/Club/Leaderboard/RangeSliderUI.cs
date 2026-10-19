using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RangeSliderUI : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform Track;
    public RectTransform Fill;
    public RectTransform LeftHandle;
    public RectTransform RightHandle;

    [Header("Slider Steps")]
    public int[] AllowedValues =
    {
        1, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90,
        100, 200, 300, 400, 500, 600, 700, 800, 900,
        1000, 2000, 3000, 5000, 6000, 7000, 8000, 9000,
        10000, 20000, 30000, 40000, 50000,
        100000, 1000000
    };

    [Header("Starting Values")]
    public int LeftValue = 1;
    public int RightValue = 1000000;

    public int MinLimit { get; private set; }
    public int MaxLimit { get; private set; }

    [System.Serializable]
    public class RangeChangedEvent : UnityEvent<int, int> { }

    public RangeChangedEvent OnRangeChanged = new RangeChangedEvent();

    private int leftIndex;
    private int rightIndex;

    private float startX;
    private float endX;

    private float originalFillLeft;
    private float originalFillWidth;

    private bool initialized;
    private bool dragging;
    private bool draggingLeft;
    private int pointerId;

    private void Awake()
    {
        if (Track == null)
            Track = transform as RectTransform;
    }

    private void Start()
    {
        if (Track == null || LeftHandle == null || RightHandle == null)
        {
            enabled = false;
            return;
        }

        if (LeftHandle.parent != Track ||
            RightHandle.parent != Track ||
            (Fill != null && Fill.parent != Track))
        {
           
            enabled = false;
            return;
        }

        if (AllowedValues == null || AllowedValues.Length < 2)
        {
            enabled = false;
            return;
        }

        for (int i = 1; i < AllowedValues.Length; i++)
        {
            if (AllowedValues[i] <= AllowedValues[i - 1])
            {
                enabled = false;
                return;
            }
        }

        MinLimit = AllowedValues[0];
        MaxLimit = AllowedValues[AllowedValues.Length - 1];

        Canvas.ForceUpdateCanvases();

        startX = LeftHandle.localPosition.x;
        endX = RightHandle.localPosition.x;

        if (endX <= startX)
        {
            enabled = false;
            return;
        }

        if (Fill != null)
        {
            originalFillWidth = Fill.rect.width;

            originalFillLeft =
                Fill.localPosition.x -
                originalFillWidth * Fill.pivot.x;

            Image fillImage = Fill.GetComponent<Image>();

            if (fillImage != null)
                fillImage.raycastTarget = false;
        }

        initialized = true;
        SetRange(LeftValue, RightValue, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!initialized || dragging ||
            eventData.button != PointerEventData.InputButton.Left)
            return;

        float x;

        if (!GetPointerX(eventData, out x))
            return;

        float leftX = LeftHandle.localPosition.x;
        float rightX = RightHandle.localPosition.x;

        if (leftIndex == rightIndex)
        {
            draggingLeft = x < leftX;
        }
        else
        {
            Transform hit = eventData.pointerCurrentRaycast.gameObject != null
                ? eventData.pointerCurrentRaycast.gameObject.transform
                : null;

            if (hit != null &&
                (hit == LeftHandle || hit.IsChildOf(LeftHandle)))
            {
                draggingLeft = true;
            }
            else if (hit != null &&
                     (hit == RightHandle || hit.IsChildOf(RightHandle)))
            {
                draggingLeft = false;
            }
            else
            {
                draggingLeft =
                    Mathf.Abs(x - leftX) <= Mathf.Abs(x - rightX);
            }
        }

        dragging = true;
        pointerId = eventData.pointerId;

        UpdateValue(x);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.pointerId != pointerId)
            return;

        float x;

        if (!GetPointerX(eventData, out x))
            return;

        if (leftIndex == rightIndex)
        {
            float handleX = LeftHandle.localPosition.x;

            if (x < handleX)
                draggingLeft = true;
            else if (x > handleX)
                draggingLeft = false;
        }

        UpdateValue(x);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
            dragging = false;
    }

    private void OnDisable()
    {
        dragging = false;
    }

    private bool GetPointerX(PointerEventData eventData, out float x)
    {
        Vector2 point;

        bool success =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Track,
                eventData.position,
                eventData.pressEventCamera,
                out point);

        x = point.x;
        return success;
    }

    private void UpdateValue(float x)
    {
        float t = Mathf.InverseLerp(startX, endX, x);

        int index = Mathf.RoundToInt(
            t * (AllowedValues.Length - 1));

        if (draggingLeft)
            SetIndices(Mathf.Clamp(index, 0, rightIndex), rightIndex);
        else
            SetIndices(
                leftIndex,
                Mathf.Clamp(index, leftIndex, AllowedValues.Length - 1));
    }

    public void SetRange(int left, int right, bool notify = true)
    {
     
        if (!initialized)
        {
            LeftValue = left;
            RightValue = right;
            return;
        }

        int newLeftIndex = FindNearestIndex(left);
        int newRightIndex = FindNearestIndex(right);

        newRightIndex = Mathf.Max(newLeftIndex, newRightIndex);

        SetIndices(newLeftIndex, newRightIndex, notify);
    }

    private int FindNearestIndex(int value)
    {
        int nearestIndex = 0;
        long nearestDistance = long.MaxValue;

        for (int i = 0; i < AllowedValues.Length; i++)
        {
            long distance = System.Math.Abs(
                (long)AllowedValues[i] - value);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private void SetIndices(int left, int right, bool notify = true)
    {
        int newLeftValue = AllowedValues[left];
        int newRightValue = AllowedValues[right];

        bool changed =
            LeftValue != newLeftValue ||
            RightValue != newRightValue;

        leftIndex = left;
        rightIndex = right;

        LeftValue = newLeftValue;
        RightValue = newRightValue;

        RefreshUI();

        if (changed && notify)
            OnRangeChanged.Invoke(LeftValue, RightValue);
    }

    private void RefreshUI()
    {
        if (!initialized)
            return;

        float lastIndex = AllowedValues.Length - 1;

        float leftT = leftIndex / lastIndex;
        float rightT = rightIndex / lastIndex;

        Vector3 leftPosition = LeftHandle.localPosition;
        leftPosition.x = Mathf.Lerp(startX, endX, leftT);
        LeftHandle.localPosition = leftPosition;

        Vector3 rightPosition = RightHandle.localPosition;
        rightPosition.x = Mathf.Lerp(startX, endX, rightT);
        RightHandle.localPosition = rightPosition;

        if (Fill != null)
        {
            float travel = endX - startX;

            float width = Mathf.Max(
                0f,
                originalFillWidth - travel * (1f - (rightT - leftT)));

            if (leftIndex == rightIndex)
                width = 0f;

            Fill.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, width);

            Vector3 fillPosition = Fill.localPosition;

            fillPosition.x =
                originalFillLeft +
                travel * leftT +
                width * Fill.pivot.x;

            Fill.localPosition = fillPosition;
        }

    }

    public static string FormatValue(int value)
    {
        if (value >= 1000000)
        {
            return (value / 1000000f).ToString(
                "0.##",
                System.Globalization.CultureInfo.InvariantCulture) + "M";
        }

        if (value >= 1000)
        {
            return (value / 1000f).ToString(
                "0.##",
                System.Globalization.CultureInfo.InvariantCulture) + "K";
        }

        return value.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }
}