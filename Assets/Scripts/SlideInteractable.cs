using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Constrains a grabbed object to slide along a single local axis between min/max, and fires OnReached when
/// the slide passes `completeAt` (0..1). Attach alongside an XRGrabInteractable.</summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class SlideInteractable : MonoBehaviour
{
    public Vector3 axis = Vector3.right;
    public float min = 0f;
    public float max = 1f;
    [Range(0f, 1f)] public float completeAt = 0.9f;

    public Action OnReached;

    XRGrabInteractable grab;
    Vector3 startLocalPos;
    bool fired;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        startLocalPos = transform.localPosition;
    }

    void LateUpdate()
    {
        if (!grab) return;
        // Project local position onto axis, clamp.
        var delta = transform.localPosition - startLocalPos;
        var t = Vector3.Dot(delta, axis);
        t = Mathf.Clamp(t, min, max);
        transform.localPosition = startLocalPos + axis * t;

        if (fired) return;
        float norm = Mathf.InverseLerp(min, max, t);
        if (norm >= completeAt) { fired = true; OnReached?.Invoke(); }
    }
}
