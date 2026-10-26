using System;
using UnityEngine;

/// <summary>Disables objects once the task list has moved past a given task (e.g. the driller after TASK_CRUST_BATHING).</summary>
public class TaskAfterCleanup : MonoBehaviour
{
    [Serializable]
    public class Rule
    {
        public string afterSequence;
        [Tooltip("On: also applies while the named sequence itself is running (from its start).")]
        public bool includeSequence;
        public GameObject[] disable;
    }

    public Rule[] rules;

    void OnEnable() { SequenceHandler.OnTaskStarted += OnTaskStarted; }
    void OnDisable() { SequenceHandler.OnTaskStarted -= OnTaskStarted; }

    void OnTaskStarted(int sequenceIndex, Task task)
    {
        var h = SequenceHandler.instance;
        if (h == null || rules == null) return;
        foreach (var rule in rules)
        {
            int idx = h.sequenceList.FindIndex(s => s && s.name == rule.afterSequence);
            if (idx < 0 || sequenceIndex < idx || (!rule.includeSequence && sequenceIndex == idx)) continue;
            foreach (var go in rule.disable) if (go && go.activeSelf) go.SetActive(false);
        }
    }
}
