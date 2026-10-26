using System.Collections;
using UnityEngine;

/// <summary>Reusable event-friendly helpers ported from the desktop framework (all single-argument, so they can be wired from
/// Task.EventsToFollow, XR interaction events, Animator-driven UnityEvents, etc.). Every delayed call is tied to the task that
/// started it: if the task changes first the call is dropped (ResetTaskState stops these coroutines).</summary>
public partial class SequenceHelperFunctions
{
    // ---------------------------------------------------------------- highlight (names match the desktop framework)
    public void HighlightObject(GameObject target) { HighlightObjectGreen(target); }

    /// <summary>Yellow variant of the highlight (e.g. for a hovered / "look at this" prop).</summary>
    public void HighlightObjectYellow(GameObject target)
    {
        if (!target) return;
        var h = target.GetComponent<VRHighlight>();
        if (!h) h = target.AddComponent<VRHighlight>();
        h.yellow = true;
        h.SetHighlight(true);
        if (!activeHighlights.Contains(h)) activeHighlights.Add(h);
    }

    public void RemoveHighlight(GameObject target) { ClearHighlight(target); }
    public void RemoveHighlightGreen(GameObject target) { ClearHighlight(target); }
    public void RemoveHighlightYellow(GameObject target)
    {
        ClearHighlight(target);
        if (target) { var h = target.GetComponent<VRHighlight>(); if (h) h.yellow = false; }
    }

    public void ClearAllHighlights()
    {
        foreach (var h in activeHighlights) if (h) h.SetHighlight(false);
        activeHighlights.Clear();
    }

    // ---------------------------------------------------------------- particles
    /// <summary>Plays a particle system and stops its emission after the system's own Duration (set it on the particle itself).</summary>
    public void PlayParticleOnce(ParticleSystem ps)
    {
        if (!ps) return;
        PlayParticle(ps);
        StartCoroutine(StopEmissionAfter(ps, Mathf.Max(0.05f, ps.main.duration)));
    }

    /// <summary>Same for every top-level particle system under the object (sub-emitters follow their parent).</summary>
    public void PlayParticleOnce(GameObject go)
    {
        if (!go) return;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject != go && ps.transform.parent != null && ps.transform.parent.GetComponentInParent<ParticleSystem>() != null) continue;
            PlayParticleOnce(ps);
        }
    }

    /// <summary>Stops and clears every particle system under the object.</summary>
    public void StopParticlesOf(GameObject root)
    {
        if (!root) return;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    IEnumerator StopEmissionAfter(ParticleSystem ps, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ---------------------------------------------------------------- delayed completion
    /// <summary>Completes the current task after `seconds`, then waits for the voice-over to finish (+0.3 s) so it is never cut off.</summary>
    public void CompleteTaskWithDelay(float seconds)
    {
        int token = Token();
        StartCoroutine(DelayedComplete(token, seconds, true));
    }

    /// <summary>Completes the current task after `seconds` without waiting for the voice-over.</summary>
    public void AdvanceTaskWithDelay(float seconds)
    {
        int token = Token();
        StartCoroutine(DelayedComplete(token, seconds, false));
    }

    /// <summary>The task is ready to move on: proceeds once the voice-over has finished (replaces the old NEXT button).</summary>
    public void ArmAutoAdvance() { CompleteTaskWithDelay(0f); }

    IEnumerator DelayedComplete(int token, float seconds, bool waitForVoice)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        if (waitForVoice)
        {
            float end = Time.time + 30f;
            yield return null;
            while (VoiceOverPlayer.instance && VoiceOverPlayer.instance.IsPlaying && Time.time < end) yield return null;
            yield return new WaitForSeconds(0.3f);
        }
        CompleteIfCurrent(token);
    }

    // ---------------------------------------------------------------- temporary message
    /// <summary>Shows a message in the description (e.g. "wrong tool") and, after a few seconds, puts the running task's own instruction
    /// back. Dropped if the task has changed in the meantime.</summary>
    public void ShowTemporaryDescription(string text)
    {
        SetDescription(text);
        StartCoroutine(RestoreDescription(Token(), 3.5f));
    }

    IEnumerator RestoreDescription(int token, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (handler == null || handler.TaskToken != token) yield break;
        var list = handler.sequenceList;
        if (list == null || handler.currentSequence < 0 || handler.currentSequence >= list.Count || list[handler.currentSequence] == null) yield break;
        var tasks = list[handler.currentSequence].TaskList;
        if (tasks == null || handler.currentTask < 0 || handler.currentTask >= tasks.Count) yield break;
        string own = tasks[handler.currentTask].instructionText;
        if (!string.IsNullOrEmpty(own)) SetDescription(own);
    }

    // ---------------------------------------------------------------- voice
    /// <summary>Speaks the text of a UI label by finding the recorded clip with the same text (there is no runtime TTS in VR).</summary>
    public void SpeakTextOf(TMPro.TMP_Text label)
    {
        if (!label || string.IsNullOrEmpty(label.text) || !VoiceOverPlayer.instance) return;
        string plain = System.Text.RegularExpressions.Regex.Replace(label.text, "<[^>]+>", "").Trim();
        VoiceOverPlayer.instance.PlayByText(plain);
    }

    // ---------------------------------------------------------------- haptics (off by default, see Haptics)
    public void HapticCorrect() { Haptics.Pulse(true); }
    public void HapticWrong() { Haptics.Pulse(false); }

    // ---------------------------------------------------------------- misc event helpers
    public void SetActive_True(GameObject target) { if (target) target.SetActive(true); }
    public void SetActive_False(GameObject target) { if (target) target.SetActive(false); }
    public void ToggleActive(GameObject target) { if (target) target.SetActive(!target.activeSelf); }

    public void StartAudioSource(AudioSource source) { if (source) source.Play(); }
    public void StopAudioSource(AudioSource source) { if (source) source.Stop(); }

    /// <summary>Convenience for the collected-tools panel (matches the desktop name).</summary>
    public void HideCollectedToolsPanel()
    {
        var p = FindFirstObjectByType<PotLeakage.UI.CollectedToolsPanelController>(FindObjectsInactive.Include);
        if (p) p.HidePanel();
    }
}
