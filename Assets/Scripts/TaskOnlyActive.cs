using UnityEngine;

/// <summary>Keeps `target` enabled only while `sequenceName` is the running task; every other task disables it
/// (e.g. the walkie-talkie exists only during TASK_06_EMERGENCY_COMMUNICATION).</summary>
public class TaskOnlyActive : MonoBehaviour
{
    public GameObject target;
    public string sequenceName = "TASK_06_EMERGENCY_COMMUNICATION";

    void OnEnable() { SequenceHandler.OnTaskStarted += OnTaskStarted; }
    void OnDisable() { SequenceHandler.OnTaskStarted -= OnTaskStarted; }

    void OnTaskStarted(int sequenceIndex, Task task)
    {
        var h = SequenceHandler.instance;
        if (!target || h == null || sequenceIndex < 0 || sequenceIndex >= h.sequenceList.Count) return;
        bool wanted = h.sequenceList[sequenceIndex].name == sequenceName;
        if (target.activeSelf != wanted) target.SetActive(wanted);
    }
}
