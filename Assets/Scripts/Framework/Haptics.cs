using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>Optional controller haptics for correct / wrong picks. Off by default: set Haptics.enabled = true (or tick it on
/// HapticsSettings in the scene) to switch it on.</summary>
public static class Haptics
{
    public static bool enabled = false;
    public static float correctAmplitude = 0.35f, correctDuration = 0.08f;
    public static float wrongAmplitude = 0.8f, wrongDuration = 0.25f;

    public static void Pulse(bool correct)
    {
        if (!enabled) return;
        float amp = correct ? correctAmplitude : wrongAmplitude;
        float dur = correct ? correctDuration : wrongDuration;
        foreach (var p in Object.FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None))
            p.SendHapticImpulse(amp, dur);
    }
}
