using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Sits on the call slider so the controller can spring the handle back when it is released early.</summary>
public class WalkieSliderRelease : MonoBehaviour, IPointerUpHandler
{
    public WalkieController owner;
    public void OnPointerUp(PointerEventData eventData) { if (owner) owner.OnSliderReleased(); }
}
