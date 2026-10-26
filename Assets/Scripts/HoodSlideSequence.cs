using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Grab-then-slide animation for a hood panel (PotLine/Plane.023 or Plane.020): start -> intermediate (forward, towards the
/// player) -> final (back and slid sideways). Positions are LOCAL, same as the desktop project. Arm() highlights the plane;
/// grabbing it plays the animation and then calls onDone.</summary>
public class HoodSlideSequence : MonoBehaviour
{
    public Transform plane;
    public Vector3 startLocalPos, intermediateLocalPos, finalLocalPos;
    public float phaseADuration = 1.5f, phaseBDuration = 1.5f, finalWait = 0.5f;

    XRGrabInteractable xr;
    Action done;
    Coroutine routine;
    bool playing;

    public void Arm(Action onDone)
    {
        Disarm();
        if (!plane) return;
        done = onDone;
        plane.gameObject.SetActive(true);
        plane.localPosition = startLocalPos;
        xr = SequenceHelperFunctions.EnsureInteractable(plane.gameObject, false);
        EnsureThickCollider();
        if (xr)
        {
            xr.selectEntered.AddListener(OnGrabbed);
            xr.activated.AddListener(OnActivated);
        }
        if (SequenceHelperFunctions.instance) SequenceHelperFunctions.instance.HighlightObjectGreen(plane.gameObject);
    }

    public void Disarm()
    {
        if (xr) { xr.selectEntered.RemoveListener(OnGrabbed); xr.activated.RemoveListener(OnActivated); }
        xr = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (playing && plane) plane.localPosition = finalLocalPos;
        playing = false;
        done = null;
    }

    void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _) { OnGrabbed(null); }

    /// <summary>The hood is a paper-thin plane; give its box at least 15 cm of depth (world) so the controller ray cannot slip past it.</summary>
    void EnsureThickCollider()
    {
        var box = plane.GetComponent<BoxCollider>();
        if (!box) return;
        var s = plane.lossyScale;
        var size = box.size;
        for (int i = 0; i < 3; i++)
        {
            float worldLen = Mathf.Abs(size[i] * s[i]);
            if (worldLen < 0.15f && Mathf.Abs(s[i]) > 0.0001f) size[i] = 0.15f / Mathf.Abs(s[i]);
        }
        box.size = size;
    }

    void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (playing || !plane) return;
        playing = true;
        if (SequenceHelperFunctions.instance) SequenceHelperFunctions.instance.ClearHighlight(plane.gameObject);
        routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return Move(startLocalPos, intermediateLocalPos, phaseADuration);
        yield return Move(intermediateLocalPos, finalLocalPos, phaseBDuration);
        yield return new WaitForSeconds(finalWait);
        playing = false;
        routine = null;
        var cb = done;
        done = null;
        cb?.Invoke();
    }

    IEnumerator Move(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            plane.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        plane.localPosition = to;
    }
}

