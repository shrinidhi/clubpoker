using UnityEngine;
using UnityEngine.EventSystems;

public class PosterScrollDragRelay : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    private PosterShowManager manager;

    public void Setup(PosterShowManager posterManager)
    {
        manager = posterManager;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (manager != null) manager.OnPosterBeginDrag(eventData.position.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (manager != null) manager.OnPosterEndDrag(eventData.position.x);
    }
}