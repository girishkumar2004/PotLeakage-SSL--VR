using PotLeakage.VFX;
using UnityEngine;

/// <summary>Keeps the molten metal stream playing from the first task through the last task of a range of the task list
/// (TASK_03_LEAKAGE_OCCURRED .. TASK_THROW_FUSED_ALUMINA) and stops it on every task outside that range.</summary>
public class MoltenStreamRange : MonoBehaviour
{
    public MoltenMetalStreamParticles stream;
    public string firstSequence = "TASK_03_LEAKAGE_OCCURRED";
    public string lastSequence = "TASK_THROW_FUSED_ALUMINA";

    void OnEnable() { SequenceHandler.OnTaskStarted += OnTaskStarted; }
    void OnDisable() { SequenceHandler.OnTaskStarted -= OnTaskStarted; }

    void OnTaskStarted(int sequenceIndex, Task task)
    {
        var h = SequenceHandler.instance;
        if (!stream || h == null) return;
        int first = h.sequenceList.FindIndex(s => s && s.name == firstSequence);
        int last = h.sequenceList.FindIndex(s => s && s.name == lastSequence);
        if (first < 0 || last < 0) return;

        if (sequenceIndex >= first && sequenceIndex <= last)
        {
            if (!stream.gameObject.activeInHierarchy) stream.gameObject.SetActive(true);
            if (!stream.isStreamActive || (stream.particleSys && !stream.particleSys.isPlaying)) stream.PlayStream();
        }
        else if (stream.isStreamActive || (stream.particleSys && stream.particleSys.isPlaying))
        {
            stream.StopStream();
        }
    }
}
