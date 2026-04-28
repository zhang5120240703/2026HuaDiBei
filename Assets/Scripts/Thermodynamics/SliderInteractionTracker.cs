using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SliderInteractionTracker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler
{
    private Action<bool> onInteractionChanged;
    private bool isInteracting;

    public void Initialize(Action<bool> interactionCallback)
    {
        onInteractionChanged = interactionCallback;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetInteracting(true);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        SetInteracting(true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        SetInteracting(false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetInteracting(false);
    }

    private void OnDisable()
    {
        SetInteracting(false);
    }

    private void SetInteracting(bool value)
    {
        if (isInteracting == value)
        {
            return;
        }

        isInteracting = value;
        onInteractionChanged?.Invoke(value);
    }
}
