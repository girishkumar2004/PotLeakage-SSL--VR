using System;
using System.Collections;
using UnityEngine;

/// <summary>Crust breaking with the updated PTM crane (crane_upd). The rig is at the PTM Crane point: crane_upd rolls to the pot line,
/// the crane's big cylinder part slides across, the rig snaps to SST and the driller (driller.001) works through the three holes.
/// All driller positions are local to its parent (Machine big cylinedr part.002).</summary>
public class CrustBathingSequence : MonoBehaviour
{
    [Header("Objects")]
    public Transform crane;          // crane_upd
    public Transform cylinder;       // Machine big cylinedr part.002
    public Transform driller;        // driller.001
    public Transform sstPoint;
    public GameObject hole1, hole2, hole3;

    [Header("Crane")]
    public Vector3 craneStart = new Vector3(0f, 0f, 13.81f);
    public Vector3 craneTarget = new Vector3(0f, 0f, 35.8040009f);
    public float craneDuration = 7f;

    [Header("Cylinder part")]
    public Vector3 cylinderStart = new Vector3(7.90999985f, 10.6007652f, 2.03035951f);
    public Vector3 cylinderTarget = new Vector3(-3.62299991f, 10.6007652f, 2.03035951f);
    public float cylinderDuration = 4f;

    [Header("Driller part connector (moved by the sequence; driller.001 is its child)")]
    public Transform connector;                 // driller part coneector.001
    public float upZ = -0.08175f;               // local z of the connector when "up"
    public float moveToHole1 = 3f, moveToHole = 1.8f, riseDuration = 1.2f, endWait = 3f;

    Coroutine routine;
    bool running, hold;
    Vector3 connectorPos;

    // Re-applied after every Animator/other writer so nothing can pin the connector while the sequence owns it.
    void LateUpdate()
    {
        if (hold && connector) connector.localPosition = connectorPos;
    }

    public void Begin(Action onDone)
    {
        Cancel(false);
        routine = StartCoroutine(Run(onDone));
    }

    /// <summary>Stops the sequence; when it was still running and `finish` is true it jumps to the end state.</summary>
    public void Cancel(bool finish = true)
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (running && finish)
        {
            if (crane) crane.localPosition = craneTarget;
            if (cylinder) cylinder.localPosition = cylinderTarget;
            if (connector) { var up = new Vector3(connector.localPosition.x, connector.localPosition.y, upZ); connectorPos = up; connector.localPosition = up; }
            SetHole(hole1, true); SetHole(hole2, true); SetHole(hole3, true);
        }
        running = false;
        hold = false;
    }

    static void SetHole(GameObject h, bool on) { if (h) h.SetActive(on); }

    static void FreeAnimators(Transform t)
    {
        if (!t) return;
        foreach (var a in t.GetComponents<Animator>()) a.enabled = false;
    }

    IEnumerator Run(Action onDone)
    {
        running = true;
        SetHole(hole1, false); SetHole(hole2, false); SetHole(hole3, false);
        FreeAnimators(crane); FreeAnimators(cylinder); FreeAnimators(connector); FreeAnimators(driller);
        if (crane) { crane.gameObject.SetActive(true); crane.localPosition = craneStart; }
        if (cylinder) { cylinder.gameObject.SetActive(true); cylinder.localPosition = cylinderStart; }
        if (driller) driller.gameObject.SetActive(true);
        if (connector) { connector.gameObject.SetActive(true); connectorPos = connector.localPosition; hold = true; }

        // 1. the crane rolls slowly to the leaked pot line (rig is at the PTM Crane point)
        yield return Move(crane, craneStart, craneTarget, craneDuration);

        // 2. the big cylinder part slides across
        yield return Move(cylinder, cylinderStart, cylinderTarget, cylinderDuration);

        // 3. rig to the SST point
        if (sstPoint && SequenceHelperFunctions.instance) SequenceHelperFunctions.instance.SnapCameraToTransform(sstPoint);
        yield return new WaitForSeconds(0.5f);

        // 4. the connector goes to each hole (its position in the connector's parent space), the hole switches on, then it moves back up
        var holes = new[] { hole1, hole2, hole3 };
        for (int i = 0; i < holes.Length; i++)
        {
            if (!holes[i] || !connector) continue;
            var target = connector.parent.InverseTransformPoint(holes[i].transform.position);
            yield return MoveConnector(connector.localPosition, target, i == 0 ? moveToHole1 : moveToHole);
            SetHole(holes[i], true);
            yield return MoveConnector(target, new Vector3(target.x, target.y, upZ), riseDuration);
        }

        yield return new WaitForSeconds(endWait);
        running = false;
        routine = null;
        onDone?.Invoke();
    }

    IEnumerator MoveConnector(Vector3 from, Vector3 to, float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.deltaTime;
            connectorPos = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / duration)));
            if (connector) connector.localPosition = connectorPos;
            yield return null;
        }
        connectorPos = to;
        if (connector) connector.localPosition = to;
    }

    static IEnumerator Move(Transform t, Vector3 from, Vector3 to, float duration)
    {
        if (!t) yield break;
        float e = 0f;
        while (e < duration)
        {
            e += Time.deltaTime;
            t.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / duration)));
            yield return null;
        }
        t.localPosition = to;
    }
}
