using System;
using System.Collections;
using UnityEngine;

/// <summary>Bath powder step with the updated crane: the driller connector (and driller.001 below it) is disabled, Cube.006 (the powder bin)
/// lowers slowly and the drop point particle effect (child of piper.001) plays for 7 seconds, then the task completes.
/// Cube.006 positions are local to its parent (Machine big cylinedr part.002).</summary>
public class BathPowderSequence : MonoBehaviour
{
    public Transform connector;              // driller part coneector.001 (parent of driller.001)
    public Transform driller;                // driller.001
    public Transform cube;                   // Cube.006
    public Transform piper;                  // piper.001 (only used to make sure the chain is active)
    public ParticleSystem dropPoint;         // drop point (child of piper.001)
    public Vector3 cubeStart = new Vector3(-0.0046000001f, -0.000509999983f, -0.0353399999f);
    public Vector3 cubeTarget = new Vector3(-0.0046000001f, -0.000509999983f, -0.0632100031f);
    public float lowerDuration = 3f, playSeconds = 7f;

    [Tooltip("Runs right before the drop point particle effect plays.")]
    public UnityEngine.Events.UnityEvent beforeParticle = new UnityEngine.Events.UnityEvent();

    Coroutine routine;
    bool running;

    public void Begin(Action onDone)
    {
        Cancel(false);
        routine = StartCoroutine(Run(onDone));
    }

    public void Cancel(bool finish = true)
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (running && finish && cube) cube.localPosition = cubeTarget;
        if (dropPoint) dropPoint.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        running = false;
    }

    IEnumerator Run(Action onDone)
    {
        running = true;
        if (connector) connector.gameObject.SetActive(false);
        if (driller) driller.gameObject.SetActive(false);
        if (dropPoint) dropPoint.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // make sure the whole chain (Cube.006, piper.001, drop point and their parents) is on, whatever an earlier cleanup did
        var tip = piper ? piper : cube;
        for (var tr = tip; tr != null && !tr.gameObject.activeInHierarchy; tr = tr.parent) tr.gameObject.SetActive(true);
        if (cube)
        {
            foreach (var a in cube.GetComponents<Animator>()) a.enabled = false;
            cube.gameObject.SetActive(true);
            cube.localPosition = cubeStart;
        }

        float e = 0f;
        while (cube && e < lowerDuration)
        {
            e += Time.deltaTime;
            cube.localPosition = Vector3.Lerp(cubeStart, cubeTarget, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / lowerDuration)));
            yield return null;
        }
        if (cube) cube.localPosition = cubeTarget;

        beforeParticle.Invoke();   // e.g. TransformPlacer.PlaceBetweenAnchors: Cube.006 between Plane.020 and Plane.023
        if (dropPoint)
        {
            dropPoint.gameObject.SetActive(true);
            dropPoint.Play(true);
        }
        yield return new WaitForSeconds(playSeconds);
        if (dropPoint) dropPoint.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        running = false;
        routine = null;
        onDone?.Invoke();
    }
}
