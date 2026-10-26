using System.Collections;
using UnityEngine;

/// <summary>Play-mode smoke test: starts every sequence in turn (no interaction), waits a moment on each and counts console errors and
/// exceptions. Add it to any object in the scene and tick `runOnStart` (or call Run()). The result is logged as "[SMOKE] ...".</summary>
public class SequenceSmokeTest : MonoBehaviour
{
    public bool runOnStart;
    public float secondsPerSequence = 1.0f;

    public int errors, exceptions, sequencesRun;
    public bool finished;

    void Start() { if (runOnStart) Run(); }

    public void Run()
    {
        errors = exceptions = sequencesRun = 0; finished = false;
        StartCoroutine(Go());
    }

    void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Exception) { exceptions++; Debug.Log("[SMOKE] exception: " + condition); }
        else if (type == LogType.Error || type == LogType.Assert) { errors++; Debug.Log("[SMOKE] error: " + condition); }
    }

    IEnumerator Go()
    {
        var h = SequenceHandler.instance;
        Application.logMessageReceived += OnLog;
        for (int i = 0; i < h.sequenceList.Count; i++)
        {
            h.StartSequence(i);
            sequencesRun++;
            yield return new WaitForSeconds(secondsPerSequence);
        }
        Application.logMessageReceived -= OnLog;
        finished = true;
        Debug.Log("[SMOKE] sequences=" + sequencesRun + " errors=" + errors + " exceptions=" + exceptions);
    }
}
