using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>TASK_06_EMERGENCY_COMMUNICATION. The walkie-talkie is highlighted; pressing it moves the rig to the Talk point (no call canvas),
/// enables both characters and plays their running animation at the same time. When both have arrived and stopped, the
/// "Pot leakage has occurred" clip plays, then the "Let me START with SST Tool" clip, then after a short wait the task completes.</summary>
public class EmergencyCallSequence : MonoBehaviour
{
    public GameObject walkie;
    public Transform talkPoint;
    public GameObject[] characters;
    public string animationState = "Scene";
    public AudioClip leakageClip, sstToolClip;
    public float endWait = 1f;
    [Tooltip("Arrived = moved at least this far (m), and every character slower than this speed (m/s) for this long (s).")]
    public float arrivedMinDistance = 2f, arrivedMaxSpeed = 0.25f, arrivedHold = 0.25f;

    XRGrabInteractable xr;
    Action done;
    Coroutine routine;
    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        if (!source) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    public void Arm(Action onDone)
    {
        Disarm();
        done = onDone;
        walkie.SetActive(true);
        var h = SequenceHelperFunctions.instance;
        h.HighlightObjectGreen(walkie);
        SequenceHelperFunctions.FitBoxColliderIfNeeded(walkie);
        xr = SequenceHelperFunctions.EnsureInteractable(walkie, false);
        if (xr) { xr.selectEntered.AddListener(OnPressed); xr.activated.AddListener(OnActivated); }
    }

    public void Disarm()
    {
        if (xr) { xr.selectEntered.RemoveListener(OnPressed); xr.activated.RemoveListener(OnActivated); }
        xr = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (source) source.Stop();
        done = null;
    }

    void OnPressed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _) { Begin(); }
    void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _) { Begin(); }

    void Begin()
    {
        if (routine != null) return;
        routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        var h = SequenceHelperFunctions.instance;
        h.ClearHighlight(walkie);

        // 1. camera to the Talk point, both characters start running at the same moment
        if (talkPoint) h.SnapCameraToTransform(talkPoint);
        float longest = 0f;
        foreach (var c in characters)
        {
            if (!c) continue;
            c.SetActive(true);
            var an = c.GetComponent<Animator>();
            if (!an) continue;
            an.enabled = true;
            an.Rebind();
            an.Play(animationState, 0, 0f);
            foreach (var clip in an.runtimeAnimatorController.animationClips)
                if (clip.name == animationState) longest = Mathf.Max(longest, clip.length / Mathf.Max(0.01f, an.speed));
        }

        // 2. wait until both have arrived: they run in, then stand still for the rest of the (longer) clip
        var tracked = new System.Collections.Generic.List<Transform>();
        foreach (var c in characters)
        {
            if (!c) continue;
            Transform hips = null;
            foreach (var tr in c.GetComponentsInChildren<Transform>(true))
                if (tr.name.EndsWith("Hips")) { hips = tr; break; }
            tracked.Add(hips ? hips : c.transform);
        }
        var startPos = new Vector3[tracked.Count];
        var lastPos = new Vector3[tracked.Count];
        yield return null;
        for (int i = 0; i < tracked.Count; i++) { startPos[i] = lastPos[i] = tracked[i].position; }
        float elapsed = 0f, stillFor = 0f;
        while (elapsed < longest + 0.1f)
        {
            yield return null;
            elapsed += Time.deltaTime;
            bool allArrived = true;
            for (int i = 0; i < tracked.Count; i++)
            {
                var p = tracked[i].position;
                float travelled = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(startPos[i].x, startPos[i].z));
                float speed = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lastPos[i].x, lastPos[i].z)) / Mathf.Max(Time.deltaTime, 0.0001f);
                lastPos[i] = p;
                if (travelled < arrivedMinDistance || speed > arrivedMaxSpeed) allArrived = false;
            }
            stillFor = allArrived ? stillFor + Time.deltaTime : 0f;
            if (stillFor >= arrivedHold) break;
        }

        // 3. voice clips, one after the other
        yield return Play(leakageClip);
        yield return Play(sstToolClip);

        yield return new WaitForSeconds(endWait);
        routine = null;
        var cb = done; done = null;
        cb?.Invoke();
    }

    IEnumerator Play(AudioClip clip)
    {
        if (!clip) yield break;
        source.clip = clip;
        source.Play();
        yield return new WaitForSeconds(clip.length + 0.05f);
    }
}
