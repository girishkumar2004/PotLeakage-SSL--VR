using System;
using UnityEngine;
using UnityEngine.Events;

public enum CompletionMode
{
    Manual,
    AudioComplete
}

[System.Serializable]
public class Task
{
    public enum TypeOfInteraction
    {
        None,
        Interact,
        Grab,
        RayInteract
    }

    public string TaskName;
    public bool TaskCompleted;
    public bool TriggerCompleted;
    public TypeOfInteraction typeOfInteraction;
    public UnityEvent EventsToFollow = new UnityEvent();
    [TextArea(2, 5)]
    public string instructionText;
    public string ttsTextOrKey;
    public bool useTTS;
    public AudioClip audioClipOverride;
    public CompletionMode completionMode;
    public bool AutoAdvanceAfterVoiceOver;

    public bool IsAutoAdvance => AutoAdvanceAfterVoiceOver || completionMode == CompletionMode.AudioComplete;
}
