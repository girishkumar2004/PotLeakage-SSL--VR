using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>TASK_CONTROL_MACHINE. Cube.016 (the AD button on the machine) is highlighted. Each press steps the Pot Control display
/// (VoltageDisplay / KADataDisplay ActiveDigits) and shows the next description with its voice-over; after the last step the task
/// completes 3 seconds later. The first description and voice-over play when the task starts.</summary>
public class ControlMachineSequence : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        public string voltage;      // e.g. "4.200"
        public string beam;         // e.g. "312"
        [TextArea] public string description;
        public string voiceKey;
    }

    public Transform cube;
    public TMP_Text voltageText, beamText;
    public string startVoltage = "4.544", startBeam = "322";
    public string startDescription = "Check the Voltage and Beam Level. Make sure both are within the safe operating limits.";
    public Step[] steps =
    {
        new Step { voltage = "4.200", beam = "312", voiceKey = "CONTROL_MACHINE_PRESS_AD",
            description = "Press AD again to continue adjusting the voltage and beam level toward the safe operating range." },
        new Step { voltage = "4.100", beam = "330", voiceKey = "CONTROL_MACHINE_VOLTAGE_4_1",
            description = "The voltage has reached 4.1 volts. Continue adjusting until the voltage is below 4.1 volts and the beam level is above 350." },
        new Step { voltage = "4.000", beam = "360", voiceKey = "CONTROL_MACHINE_SAFE_RANGE",
            description = "The voltage and beam level are now within the safe operating range." },
    };
    public float endWait = 3f;

    const string VoltageSuffix = "<size=70%> <color=#FFFFFF>V</color></size>";

    XRGrabInteractable xr;
    Action done;
    int next;
    Coroutine routine;

    /// <summary>Called at task start; the start description/voice-over come from the task event and its voice key.</summary>
    public void Arm(Action onDone)
    {
        Disarm();
        done = onDone;
        next = 0;
        SetDisplay(startVoltage, startBeam);
        xr = SequenceHelperFunctions.EnsureInteractable(cube.gameObject, false);
        if (xr) xr.selectEntered.AddListener(OnPress);
        SequenceHelperFunctions.instance.HighlightObjectGreen(cube.gameObject);
    }

    public void Disarm()
    {
        if (xr) xr.selectEntered.RemoveListener(OnPress);
        xr = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        done = null;
    }

    void OnPress(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (next >= steps.Length) return;
        var s = steps[next++];
        SetDisplay(s.voltage, s.beam);
        var h = SequenceHelperFunctions.instance;
        h.SetDescription(s.description);
        h.PlayVoiceOver(s.voiceKey);
        if (next >= steps.Length)
        {
            h.ClearHighlight(cube.gameObject);
            if (xr) xr.selectEntered.RemoveListener(OnPress);
            routine = StartCoroutine(Finish());
        }
    }

    IEnumerator Finish()
    {
        yield return new WaitForSeconds(endWait);
        routine = null;
        var cb = done; done = null;
        cb?.Invoke();
    }

    void SetDisplay(string voltage, string beam)
    {
        if (voltageText) voltageText.text = voltage + VoltageSuffix;
        if (beamText) beamText.text = beam;
    }
}
