using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Joystick Settings")]
    public RectTransform background;
    public RectTransform handle;
    public float handleRange = 100f;

    private Vector2 inputVector;

    public Vector2 Direction => inputVector;

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 position
        );

        position = Vector2.ClampMagnitude(position, handleRange);
        handle.anchoredPosition = position;

        inputVector = position / handleRange;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}
