using System;
using UnityEngine;

/// <summary>CLOSE_HOOD. Plane.023 is highlighted first; grabbing it slides it closed (towards the viewer's left). Then the
/// "Let us close the left hood now" description and voice-over play and Plane.020 is highlighted; grabbing it slides it closed
/// (towards the viewer's right). The task completes when both are closed. The first description/voice-over come from the task start.</summary>
public class CloseHoodSequence : MonoBehaviour
{
    public HoodSlideSequence rightHood;   // Plane.023
    public HoodSlideSequence leftHood;    // Plane.020
    public string secondDescription = "Let us close the left hood now";
    public string secondVoiceKey = "CLOSE_HOOD_LEFT";

    Action done;

    public void Begin(Action onDone)
    {
        Cancel();
        done = onDone;
        rightHood.Arm(() =>
        {
            var h = SequenceHelperFunctions.instance;
            h.SetDescription(secondDescription);
            h.PlayVoiceOver(secondVoiceKey);
            leftHood.Arm(() => { var cb = done; done = null; cb?.Invoke(); });
        });
    }

    public void Cancel()
    {
        if (rightHood) rightHood.Disarm();
        if (leftHood) leftHood.Disarm();
        done = null;
    }
}
