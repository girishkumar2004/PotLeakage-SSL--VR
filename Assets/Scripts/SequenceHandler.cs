using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs Sequence -> Task. Each TASK_* child of ListOfSequence carries a Sequence whose Task.EventsToFollow
/// is wired in the inspector (SetTitle, SnapCameraToTransform, HighlightObjectGreen, CompleteOnGrab ...).
/// A task ends when something calls CompleteCurrentTask() (normally SequenceHelperFunctions.CompleteOn*).
/// </summary>
public class SequenceHandler : MonoBehaviour
{
    public static SequenceHandler instance;

    public bool autostart = true;
    [Tooltip("Filled automatically from the children of ListOfSequence when empty.")]
    public List<Sequence> sequenceList = new List<Sequence>();
    public Transform listOfSequence;
    public int currentSequence, currentTask;
    public float delayBetweenTasks = 1.5f;

    public SequenceHelperFunctions helper;

    /// <summary>Incremented every time a task starts; completion hooks compare against it to ignore stale callbacks.</summary>
    public int TaskToken { get; private set; }
    public bool Finished { get; private set; }

    /// <summary>Optional gate predicate. If set and returns false, CompleteCurrentTask() is ignored.</summary>
    public System.Func<bool> canAdvanceTask;

    public static event System.Action<int, Task> OnTaskStarted;
    public static event System.Action<int, Task> OnTaskCompletedEvent;
    public static event System.Action OnSequenceFinished;

    bool advancing;

    void Awake() { instance = this; if (!helper) helper = GetComponent<SequenceHelperFunctions>(); }

    void Start()
    {
        if (sequenceList.Count == 0 && listOfSequence)
            foreach (Transform child in listOfSequence)
            { var s = child.GetComponent<Sequence>(); if (s) sequenceList.Add(s); }
        if (autostart) StartSequence(0);
    }

    public Task Current
    {
        get
        {
            if (currentSequence < 0 || currentSequence >= sequenceList.Count) return null;
            var list = sequenceList[currentSequence].TaskList;
            return currentTask >= 0 && currentTask < list.Count ? list[currentTask] : null;
        }
    }

    public void StartSequence(int index)
    {
        Finished = false; currentSequence = index; currentTask = 0; RunCurrent();
    }

    void RunCurrent()
    {
        advancing = false;
        canAdvanceTask = null;
        var t = Current;
        if (t == null) { Finish(); return; }
        TaskToken++;
        if (helper) helper.ResetTaskState();
        t.TaskCompleted = false;
        t.EventsToFollow?.Invoke();
        OnTaskStarted?.Invoke(currentSequence, t);
    }

    /// <summary>Marks the running task done and moves on after delayBetweenTasks.</summary>
    public void CompleteCurrentTask()
    {
        var t = Current;
        if (t == null || advancing) return;
        if (canAdvanceTask != null && !canAdvanceTask()) return;
        advancing = true;
        t.TaskCompleted = true;
        OnTaskCompletedEvent?.Invoke(currentSequence, t);
        StartCoroutine(AdvanceAfterDelay());
    }

    IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(delayBetweenTasks);
        currentTask++;
        if (currentTask >= sequenceList[currentSequence].TaskList.Count) { currentSequence++; currentTask = 0; }
        RunCurrent();
    }

    void Finish()
    {
        Finished = true;
        if (helper) helper.ResetTaskState();
        OnSequenceFinished?.Invoke();
    }
}
