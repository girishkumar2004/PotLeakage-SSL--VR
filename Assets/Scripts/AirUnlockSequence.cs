using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>TASK_5_AIR_Unlock. Only valve handle.001 is highlighted; grabbing it turns the handle (local rotation z -90 to -180),
/// then after a second the rig moves to the SST point and the SST pipe's GasPoint particles play.</summary>
public class AirUnlockSequence : MonoBehaviour
{
    public Transform valveHandle;
    public Transform sstPoint;
    public GameObject sstPipe, sstPipe1;
    public float fromZ = -90f, toZ = -180f, turnDuration = 1.2f, waitAfterTurn = 1f, endWait = 2f;

    XRGrabInteractable xr;
    Action done;
    Coroutine routine;
    Vector3 baseEuler;
    bool started;

    public void Arm(Action onDone)
    {
        Disarm();
        done = onDone;
        started = false;
        baseEuler = valveHandle.localEulerAngles;
        valveHandle.localRotation = Quaternion.Euler(baseEuler.x, baseEuler.y, fromZ);
        xr = SequenceHelperFunctions.EnsureInteractable(valveHandle.gameObject, false);
        if (xr) xr.selectEntered.AddListener(OnGrab);
        SequenceHelperFunctions.instance.HighlightObjectGreen(valveHandle.gameObject);
    }

    public void Disarm()
    {
        if (xr) xr.selectEntered.RemoveListener(OnGrab);
        xr = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (started && valveHandle) valveHandle.localRotation = Quaternion.Euler(baseEuler.x, baseEuler.y, toZ);
        done = null;
    }

    void OnGrab(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (started) return;
        started = true;
        SequenceHelperFunctions.instance.ClearHighlight(valveHandle.gameObject);
        routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float e = 0f;
        while (e < turnDuration)
        {
            e += Time.deltaTime;
            float z = Mathf.Lerp(fromZ, toZ, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / turnDuration)));
            valveHandle.localRotation = Quaternion.Euler(baseEuler.x, baseEuler.y, z);
            yield return null;
        }
        valveHandle.localRotation = Quaternion.Euler(baseEuler.x, baseEuler.y, toZ);
        yield return new WaitForSeconds(waitAfterTurn);

        if (sstPoint) SequenceHelperFunctions.instance.SnapCameraToTransform(sstPoint);
        if (sstPipe && sstPipe.activeInHierarchy) SstConnectionSequence.PlayGasPoints(sstPipe);
        if (sstPipe1 && sstPipe1.activeInHierarchy) SstConnectionSequence.PlayGasPoints(sstPipe1);
        yield return new WaitForSeconds(endWait);

        routine = null;
        var cb = done; done = null;
        cb?.Invoke();
    }
}
