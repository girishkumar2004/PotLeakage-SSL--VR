using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Turns XR interaction (hover / select / activate) on any XR interactable into plain UnityEvents, so the same event-friendly
/// helper functions (SetActive, SetDescription, CompleteTaskWithDelay, Animator.Play ...) can be wired in the Inspector with static
/// arguments. This replaces the desktop mouse EventTrigger / SequenceInteractionTrigger.
///
/// Add it next to an XRSimpleInteractable (or XRGrabInteractable). `armed` lets a task enable/disable the reaction from its own events
/// (Arm / Disarm); `oneShot` disarms again after the first select.</summary>
[DisallowMultipleComponent]
public class XRInteractionEvents : MonoBehaviour
{
    public bool armed = true;
    public bool oneShot = true;
    [Tooltip("Pulse the controller on select (global switch: Haptics.enabled).")]
    public bool hapticOnSelect;

    public UnityEvent onHoverEnter = new UnityEvent();
    public UnityEvent onHoverExit = new UnityEvent();
    public UnityEvent onSelect = new UnityEvent();
    public UnityEvent onActivate = new UnityEvent();

    XRBaseInteractable interactable;

    void Awake() { interactable = GetComponent<XRBaseInteractable>(); }

    void OnEnable()
    {
        if (!interactable) interactable = GetComponent<XRBaseInteractable>();
        if (!interactable) { Debug.LogWarning("XRInteractionEvents needs an XR interactable on " + name, this); return; }
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.activated.AddListener(OnActivated);
    }

    void OnDisable()
    {
        if (!interactable) return;
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.activated.RemoveListener(OnActivated);
    }

    public void Arm() { armed = true; }
    public void Disarm() { armed = false; }

    void OnHoverEntered(UnityEngine.XR.Interaction.Toolkit.HoverEnterEventArgs _) { if (armed) onHoverEnter.Invoke(); }
    void OnHoverExited(UnityEngine.XR.Interaction.Toolkit.HoverExitEventArgs _) { if (armed) onHoverExit.Invoke(); }

    void OnSelectEntered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (!armed) return;
        if (oneShot) armed = false;
        if (hapticOnSelect) Haptics.Pulse(true);
        onSelect.Invoke();
    }

    void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
    {
        if (!armed) return;
        if (oneShot) armed = false;
        onActivate.Invoke();
    }

    /// <summary>Test hook / scripted trigger: behaves exactly like a select.</summary>
    public void SimulateSelect()
    {
        if (!armed) return;
        if (oneShot) armed = false;
        onSelect.Invoke();
    }
}
