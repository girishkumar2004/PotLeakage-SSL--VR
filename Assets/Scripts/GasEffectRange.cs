using UnityEngine;

/// <summary>Keeps the SST pipe gas particles (GasPoint / GasDensity_Fill) off until the valve is opened in TASK_5_AIR_Unlock and
/// off again once TASK_LEAKAGE_STOP is over. The air-unlock task starts the effect when the handle is turned; every later task up to and
/// including TASK_LEAKAGE_STOP keeps it playing; any task before TASK_5, or after TASK_LEAKAGE_STOP, stops and clears it.</summary>
public class GasEffectRange : MonoBehaviour
{
    public GameObject[] roots;
    public string startSequence = "TASK_5_AIR_Unlock";
    public string lastSequence = "TASK_LEAKAGE_STOP";

    void OnEnable() { SequenceHandler.OnTaskStarted += OnTaskStarted; }
    void OnDisable() { SequenceHandler.OnTaskStarted -= OnTaskStarted; }

    void OnTaskStarted(int sequenceIndex, Task task)
    {
        var h = SequenceHandler.instance;
        if (h == null) return;
        int start = h.sequenceList.FindIndex(s => s && s.name == startSequence);
        int last = h.sequenceList.FindIndex(s => s && s.name == lastSequence);
        if (start < 0 || last < 0) return;

        if (sequenceIndex > start && sequenceIndex <= last)
        {
            foreach (var r in roots)
                if (r && r.activeInHierarchy) SstConnectionSequence.PlayGasPoints(r);
        }
        else if (sequenceIndex != start)
        {
            Stop();   // before the valve is opened, or after the leakage check
        }
        // sequenceIndex == start: the air-unlock sequence starts the gas when the valve handle is turned
    }

    public void Stop()
    {
        foreach (var r in roots)
        {
            if (!r) continue;
            foreach (var ps in r.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.isPlaying || ps.particleCount > 0) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
