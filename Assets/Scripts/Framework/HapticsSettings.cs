using UnityEngine;

/// <summary>Scene switch for Haptics (put it anywhere in the scene). Off by default.</summary>
public class HapticsSettings : MonoBehaviour
{
    public bool enableHaptics = false;
    void Awake() { Haptics.enabled = enableHaptics; }
}
