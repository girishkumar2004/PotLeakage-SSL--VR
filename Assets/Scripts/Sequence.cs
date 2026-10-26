using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class Sequence : MonoBehaviour
{
    public string SequenceName;
    [Tooltip("Informational sequence: every task moves on by itself once its voice-over has finished.")]
    public bool autoAdvanceAfterVoice;
    [Tooltip("Create Tasks for sequence")]
    public List<Task> TaskList = new List<Task>();
    
}
