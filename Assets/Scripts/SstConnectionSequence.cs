using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>TASK_4_SST_CONNECTION. Stage 1: the SST_PIPE_ani tool (without its BézierCurve.002) is highlighted; grab and drag it onto
/// SST_PIPE_ani_1, which is highlighted as the drop target. Dropped: SST_PIPE_ani is disabled and SST_PIPE_ani_1 stays unhighlighted.
/// Stage 2: round_PIPES is highlighted; grabbing it highlights SST_PIPE_ani_1 and its BézierCurve.002. Dropped: round_PIPES goes away,
/// SST_PIPE_ani_1 + its BézierCurve.002 show unhighlighted, and every GasPoint particle plays.</summary>
public class SstConnectionSequence : MonoBehaviour
{
    public GameObject sstPipe, sstPipeBezier;       // SST_PIPE_ani and its BézierCurve.002
    public GameObject sstPipe1, sstPipe1Bezier;     // SST_PIPE_ani_1 and its BézierCurve.002
    public GameObject roundPipes, roundPipes1;      // round_PIPES (unused), round_PIPES (1) = the tool dragged in stage 2
    public string voiceKey = "SELECT_SST_CONNECTOR";
    public string description = "Select SST Cooling Connector in order to reduce the intensity of the leakage.";
    public string voiceKey2 = "SELECT_HOSE_PIPE";
    public string description2 = "Select Hose pipe in order to reduce the intensity of the leakage.";
    public float dropRadius = 1.2f;
    [Tooltip("Off: the task completes as soon as SST_PIPE_ani is dropped on SST_PIPE_ani_1. On: continues with the round_PIPES (1) step.")]
    public bool includeRoundPipesStep = true;
    public float gasPlaySeconds = 3f;
    [Tooltip("Off: the gas starts in TASK_5_AIR_Unlock (when the valve is opened), not here.")]
    public bool playGasAtEnd = false;

    SequenceHelperFunctions H { get { return SequenceHelperFunctions.instance; } }

    readonly List<System.Action> undo = new List<System.Action>();
    Action done;
    Vector3 pipeStartPos, roundStartPos;
    Quaternion pipeStartRot, roundStartRot;
    int stage;

    public void Begin(Action onDone)
    {
        Cancel();
        done = onDone;
        stage = 1;
        if (roundPipes1) roundPipes1.SetActive(false);
        if (roundPipes) roundPipes.SetActive(false);
        if (sstPipe1) sstPipe1.SetActive(false);
        if (sstPipe1Bezier) sstPipe1Bezier.SetActive(false);
        if (sstPipeBezier) sstPipeBezier.SetActive(false);   // the tool is shown without its curve
        sstPipe.SetActive(true);
        pipeStartPos = sstPipe.transform.position; pipeStartRot = sstPipe.transform.rotation;

        Say();
        H.HighlightObjectGreen(sstPipe);
        ArmDrag(sstPipe, OnPipeGrabbed, OnPipeDropped);
    }

    public void Cancel()
    {
        foreach (var u in undo) u();
        undo.Clear();
        StopAllCoroutines();
        done = null;
    }

    void Say()
    {
        bool second = stage == 2;
        H.SetDescription(second ? description2 : description);
        H.PlayVoiceOver(second ? voiceKey2 : voiceKey);
    }

    // ---------------------------------------------------------------- stage 1
    void OnPipeGrabbed()
    {
        H.ClearHighlight(sstPipe);
        sstPipe1.SetActive(true);
        sstPipe1Bezier.SetActive(false);
        H.HighlightObjectGreen(sstPipe1);                  // drop target
    }

    void OnPipeDropped()
    {
        H.ClearHighlight(sstPipe1);
        if (Near(sstPipe, sstPipe1))
        {
            Disarm();
            sstPipe.SetActive(false);
            sstPipe1.SetActive(true);                      // stays visible, no highlight
            if (includeRoundPipesStep) StartStage2();
            else { var cb = done; done = null; cb?.Invoke(); }
        }
        else
        {
            sstPipe1.SetActive(false);
            sstPipe.transform.SetPositionAndRotation(pipeStartPos, pipeStartRot);
            H.HighlightObjectGreen(sstPipe);
        }
    }

    // ---------------------------------------------------------------- stage 2
    void StartStage2()
    {
        stage = 2;
        Say();
        roundPipes1.SetActive(true);
        roundStartPos = roundPipes1.transform.position; roundStartRot = roundPipes1.transform.rotation;
        H.HighlightObjectGreen(roundPipes1);
        ArmDrag(roundPipes1, OnRoundGrabbed, OnRoundDropped);
    }

    void OnRoundGrabbed()
    {
        H.ClearHighlight(roundPipes1);
        sstPipe1.SetActive(true);
        sstPipe1Bezier.SetActive(true);
        H.HighlightObjectGreen(sstPipe1);                  // covers the pipe and its BézierCurve.002
    }

    void OnRoundDropped()
    {
        H.ClearHighlight(sstPipe1);
        if (Near(roundPipes1, sstPipe1))
        {
            Disarm();
            roundPipes1.SetActive(false);
            if (roundPipes) roundPipes.SetActive(false);
            sstPipe1.SetActive(true);
            sstPipe1Bezier.SetActive(true);                // final look, no highlight
            StartCoroutine(PlayGas());
        }
        else
        {
            sstPipe1Bezier.SetActive(false);
            roundPipes1.transform.SetPositionAndRotation(roundStartPos, roundStartRot);
            H.HighlightObjectGreen(roundPipes1);
        }
    }

    IEnumerator PlayGas()
    {
        if (playGasAtEnd) PlayGasPoints(sstPipe1);
        yield return new WaitForSeconds(gasPlaySeconds);
        var cb = done; done = null;
        cb?.Invoke();
    }

    /// <summary>Plays the particle system of every GasPoint under `root`.</summary>
    public static void PlayGasPoints(GameObject root)
    {
        if (!root) return;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name != "GasPoint") continue;
            // make sure every parent up to root is active
            for (var t = ps.transform; t != null && t != root.transform.parent; t = t.parent) t.gameObject.SetActive(true);
            ps.Play(true);
        }
    }

    // ---------------------------------------------------------------- drag/drop plumbing
    void Disarm()
    {
        foreach (var u in undo) u();
        undo.Clear();
    }

    void ArmDrag(GameObject go, System.Action onGrab, System.Action onDrop)
    {
        var xr = SequenceHelperFunctions.EnsureInteractable(go, true);
        if (!xr) return;
        xr.movementType = XRBaseInteractable.MovementType.Instantaneous;
        UnityEngine.Events.UnityAction<SelectEnterEventArgs> en = _ => onGrab();
        UnityEngine.Events.UnityAction<SelectExitEventArgs> ex = _ => onDrop();
        xr.selectEntered.AddListener(en);
        xr.selectExited.AddListener(ex);
        undo.Add(() => { if (xr) { xr.selectEntered.RemoveListener(en); xr.selectExited.RemoveListener(ex); } });
    }

    bool Near(GameObject dragged, GameObject target)
    {
        return Vector3.Distance(Center(dragged), Center(target)) <= dropRadius;
    }

    static Vector3 Center(GameObject go)
    {
        bool init = false; Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
        }
        return b.center;
    }
}
