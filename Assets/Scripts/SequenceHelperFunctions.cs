using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// VR rewrite of the desktop SequenceHelperFunctions. Every public method is a generic building block meant to be
/// called from Task.EventsToFollow (UnityEvent) in the inspector - nothing here is tied to a specific task or object.
/// </summary>
public partial class SequenceHelperFunctions : MonoBehaviour
{
    public static SequenceHelperFunctions instance;

    [Header("References")]
    public SequenceHandler handler;
    public InstructionPanel panel;
    public XROrigin xrOrigin;
    [Tooltip("Parent of the named camera points (CameraSystem/TransformPoints).")]
    public Transform transformPoints;
    [Tooltip("Shared AudioSource used by PlaySFX; auto-created on this GameObject if missing.")]
    public AudioSource sfxSource;
    [Tooltip("Optional named clips playable via PlaySFX(string).")]
    public AudioClip[] namedClips;

    public string titleString, desString;

    readonly List<VRHighlight> activeHighlights = new List<VRHighlight>();
    readonly List<System.Action> cleanups = new List<System.Action>();

    void Awake()
    {
        instance = this;
        if (!handler) handler = GetComponent<SequenceHandler>();
        EnsureStraightControllerRays();
    }

    void OnEnable()
    {
        SequenceHandler.OnTaskStarted += HandleTaskStarted;
    }

    void OnDisable()
    {
        SequenceHandler.OnTaskStarted -= HandleTaskStarted;
    }

    void HandleTaskStarted(int sequenceIndex, Task task)
    {
        if (panel != null)
        {
            if (panel.transform.parent != null && !panel.transform.parent.gameObject.activeSelf)
            {
                panel.transform.parent.gameObject.SetActive(true);
            }
            if (!panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(true);
            }

            panel.EnsureNextButtonListener();

            if (handler != null && handler.sequenceList != null && handler.sequenceList.Count > 0)
            {
                panel.SetTotalSteps(handler.sequenceList.Count);
            }
            panel.SetStep(sequenceIndex + 1);

            // If task specifies instruction text or task name, provide fallback if not set via EventsToFollow
            string resolvedTitle = null;
            string resolvedDes = null;

            // Try resolving key from task or sequence
            string key = task.ttsTextOrKey;
            string seqName = "";
            if (handler != null && handler.sequenceList != null && sequenceIndex >= 0 && sequenceIndex < handler.sequenceList.Count && handler.sequenceList[sequenceIndex])
            {
                seqName = handler.sequenceList[sequenceIndex].name;   // the running order, not the hierarchy child order
            }

            if (seqName.IndexOf("EMERGENCY_COMMUNICATION", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (task.TaskName != null && task.TaskName.IndexOf("Emergency Communication", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                key = "TASK_04";
            }

            if (string.IsNullOrEmpty(key))
            {
                if (!string.IsNullOrEmpty(seqName))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(seqName, @"TASK_(\d{1,2})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) key = "TASK_" + int.Parse(m.Groups[1].Value).ToString("D2");
                }
            }

            if (!string.IsNullOrEmpty(key) && TrainingDataProvider.instance != null)
            {
                var text = TrainingDataProvider.instance.GetDisplay(key);
                if (!string.IsNullOrEmpty(text))
                {
                    var nl = text.IndexOf('\n');
                    if (nl > 0)
                    {
                        resolvedTitle = text.Substring(0, nl).Trim();
                        resolvedDes = text.Substring(nl + 1).Trim();
                    }
                    else
                    {
                        resolvedDes = text;
                    }
                }
            }

            if (string.IsNullOrEmpty(titleString))
            {
                titleString = !string.IsNullOrEmpty(resolvedTitle) ? resolvedTitle : task.TaskName;
            }
            if (string.IsNullOrEmpty(desString))
            {
                desString = !string.IsNullOrEmpty(resolvedDes) ? resolvedDes : task.instructionText;
            }
            Refresh();
        }
    }

    // ---------------------------------------------------------------- UI
    public void SetTitle(string title) { titleString = title; Refresh(); }
    public void SetDescription(string description) { desString = description; Refresh(); }
    public void SetProgress(string progress) { if (panel) panel.SetProgress(progress); }
    void Refresh()
    {
        if (panel)
        {
            if (panel.transform.parent != null && !panel.transform.parent.gameObject.activeSelf)
            {
                panel.transform.parent.gameObject.SetActive(true);
            }
            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            panel.EnsureNextButtonListener();
            panel.Show(LanguageService.Localize(titleString), LanguageService.Localize(desString));   // English text is looked up in training.json for Hindi / Odia
        }
    }

    // ---------------------------------------------------------------- Camera / rig
    /// <summary>Moves the XR rig so the headset sits at the point (horizontally) and faces the point's forward.</summary>
    public void SnapCameraToTransform(Transform point)
    {
        if (!point) return;
        // comfort fade (CameraFader, ~0.15 s) when one exists; the rig itself always moves instantly, never glides
        if (CameraFader.instance && CameraFader.instance.enabled) { CameraFader.instance.FadeThen(() => SnapCameraInstant(point)); return; }
        SnapCameraInstant(point);
    }

    public void SnapCameraInstant(Transform point)
    {
        if (!point) return;
        var rig = Rig();
        if (!rig) return;
        var fwd = Vector3.ProjectOnPlane(point.forward, Vector3.up);
        if (fwd.sqrMagnitude > 0.0001f) rig.MatchOriginUpCameraForward(Vector3.up, fwd.normalized);
        var cam = rig.Camera.transform.position;
        rig.MoveCameraToWorldLocation(new Vector3(point.position.x, cam.y, point.position.z));
    }

    public void SnapCameraToNamedPoint(string pointName)
    {
        if (!transformPoints) return;
        var p = FindChildByName(transformPoints, pointName);
        if (p) SnapCameraToTransform(p);
        else Debug.LogWarning($"SnapCameraToNamedPoint: no point named '{pointName}' under {transformPoints.name}");
    }

    static Transform FindChildByName(Transform root, string n)
    {
        foreach (Transform c in root) if (c.name == n) return c;
        foreach (Transform c in root) { var r = FindChildByName(c, n); if (r) return r; }
        return null;
    }

    XROrigin Rig() { if (!xrOrigin) xrOrigin = FindFirstObjectByType<XROrigin>(); return xrOrigin; }

    // ---------------------------------------------------------------- Highlight
    public void HighlightObjectGreen(GameObject target)
    {
        if (!target) return;
        var h = target.GetComponent<VRHighlight>();
        if (!h) h = target.AddComponent<VRHighlight>();
        h.SetHighlight(true);
        if (!activeHighlights.Contains(h)) activeHighlights.Add(h);
    }

    public void ClearHighlight(GameObject target)
    {
        if (!target) return;
        var h = target.GetComponent<VRHighlight>();
        if (h) { h.SetHighlight(false); activeHighlights.Remove(h); }
    }

    // ---------------------------------------------------------------- Interaction
    /// <summary>Makes the object grabbable (if needed) and completes the current task when it is grabbed. The object follows the hand.</summary>
    public void CompleteOnGrab(GameObject target) { Hook(target, true); }

    /// <summary>Completes the current task when the object is selected (trigger on ray). The object stays where it is.</summary>
    public void CompleteOnSelect(GameObject target) { Hook(target, false); }

    /// <summary>Completes the current task when the trigger is pressed while holding / pointing at the object.</summary>
    public void CompleteOnActivate(GameObject target)
    {
        var xr = EnsureInteractable(target, false);
        if (!xr) return;
        int token = Token();
        UnityEngine.Events.UnityAction<ActivateEventArgs> cb = _ => CompleteIfCurrent(token);
        xr.activated.AddListener(cb);
        cleanups.Add(() => { if (xr) xr.activated.RemoveListener(cb); });
    }

    public void CompleteAfterSeconds(float seconds)
    {
        int token = Token();
        StartCoroutine(Wait(seconds, token));
    }

    IEnumerator Wait(float s, int token) { yield return new WaitForSeconds(s); CompleteIfCurrent(token); }

    /// <summary>Completes immediately (use as the last event of a task that needs no interaction).</summary>
    public void CompleteTask() { if (handler) handler.CompleteCurrentTask(); }

    /// <summary>Logs the step id (e.g. "6.2") and completes the current task. Mirrors the desktop Complete("x.y").</summary>
    public void Complete(string stepId)
    {
        if (!string.IsNullOrEmpty(stepId)) Debug.Log($"[Sequence] Complete {stepId}");
        if (handler) handler.CompleteCurrentTask();
    }

    /// <summary>Completes when the object is dropped into (socketed by) the given snap zone.</summary>
    public void CompleteOnSocketed(XRSocketInteractor socket)
    {
        if (!socket) return;
        int token = Token();
        UnityEngine.Events.UnityAction<SelectEnterEventArgs> cb = _ => CompleteIfCurrent(token);
        socket.selectEntered.AddListener(cb);
        cleanups.Add(() => { if (socket) socket.selectEntered.RemoveListener(cb); });
    }

    // ---------------------------------------------------------------- Audio / Particles
    AudioSource Sfx()
    {
        if (sfxSource) return sfxSource;
        sfxSource = GetComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        return sfxSource;
    }

    /// <summary>Plays a one-shot clip through the shared SFX source.</summary>
    public void PlaySFX(AudioClip clip) { if (clip) Sfx().PlayOneShot(clip); }

    /// <summary>Plays a clip from namedClips by its asset name (case-insensitive).</summary>
    public void PlaySFX(string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || namedClips == null) return;
        for (int i = 0; i < namedClips.Length; i++)
            if (namedClips[i] && string.Equals(namedClips[i].name, clipName, System.StringComparison.OrdinalIgnoreCase))
            { Sfx().PlayOneShot(namedClips[i]); return; }
        Debug.LogWarning($"PlaySFX: no clip named '{clipName}' in namedClips");
    }

    /// <summary>Plays a particle system (with its children) so VFX runs reliably on standalone VR builds.</summary>
    public void PlayParticle(ParticleSystem ps)
    {
        if (!ps) return;
        if (!ps.gameObject.activeInHierarchy) ps.gameObject.SetActive(true);
        ps.Play(true);
    }

    /// <summary>Stops and clears a particle system.</summary>
    public void StopParticle(ParticleSystem ps)
    {
        if (!ps) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // ---------------------------------------------------------------- Object placement (grab-drop targets)
    /// <summary>Enables the GameObject and applies the green highlight (used for drop zones + tools that light up alone).</summary>
    public void EnableAndHighlight(GameObject target)
    {
        if (!target) return;
        if (!target.activeSelf) target.SetActive(true);
        HighlightObjectGreen(target);
    }

    /// <summary>Snaps `target` to `point` (position + rotation). Use to pre-place a tool at the right side of the ToolBox or near SST.</summary>
    public void MoveObjectToTransform(GameObject target, Transform point)
    {
        if (!target || !point) return;
        target.transform.SetPositionAndRotation(point.position, point.rotation);
    }

    /// <summary>Reparents the target under `parent`, resets local pose. Useful to attach a dropped tool at the SST or ToolBox drop slot.</summary>
    public void AttachAsChildOfTransform(GameObject target, Transform parent)
    {
        if (!target || !parent) return;
        target.transform.SetParent(parent, worldPositionStays: false);
        target.transform.localPosition = Vector3.zero;
        target.transform.localRotation = Quaternion.identity;
    }

    /// <summary>One-shot "pick this tool from the toolbox" helper: enables the tool, highlights it, makes it grabbable, and completes the task
    /// when the tool is socketed into `dropZone` (e.g. the right-side ToolBox slot or the SST drop pad).</summary>
    public void PickToolToDropZone(GameObject tool, XRSocketInteractor dropZone)
    {
        if (!tool || !dropZone) return;
        EnableAndHighlight(tool);
        EnsureInteractable(tool, true);
        EnableAndHighlight(dropZone.gameObject);
        CompleteOnSocketed(dropZone);
    }

    // ---------------------------------------------------------------- Animation
    /// <summary>Fires an Animator trigger. Works for both state machines and simple triggered clips.</summary>
    public void PlayAnimationTrigger(Animator animator, string triggerName)
    {
        if (!animator || string.IsNullOrEmpty(triggerName)) return;
        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    /// <summary>Plays a specific Animator state (base layer).</summary>
    public void PlayAnimationState(Animator animator, string stateName)
    {
        if (!animator || string.IsNullOrEmpty(stateName)) return;
        animator.Play(stateName, 0, 0f);
    }

    /// <summary>Enables the animator GameObject and plays the trigger (one call per task event).</summary>
    public void EnableAndPlayAnimation(Animator animator, string triggerName)
    {
        if (!animator) return;
        if (!animator.gameObject.activeSelf) animator.gameObject.SetActive(true);
        PlayAnimationTrigger(animator, triggerName);
    }

    // ---------------------------------------------------------------- Particle (at point)
    /// <summary>Moves `ps` to `point` and plays it. For VFX that must appear at a dynamic location (e.g. where a tool lands).</summary>
    public void PlayParticleAt(ParticleSystem ps, Transform point)
    {
        if (!ps) return;
        if (point) ps.transform.SetPositionAndRotation(point.position, point.rotation);
        PlayParticle(ps);
    }

    // ---------------------------------------------------------------- Voice over
    /// <summary>Plays the TTS clip registered under `key` in StreamingAssets/TTSCache/cache_manifest.json.</summary>
    public void PlayVoiceOver(string key)
    {
        if (VoiceOverPlayer.instance) VoiceOverPlayer.instance.Play(key);
    }

    public void StopVoiceOver()
    {
        if (VoiceOverPlayer.instance) VoiceOverPlayer.instance.Stop();
    }

    // ---------------------------------------------------------------- Step counter
    /// <summary>Pulls display text for `key` from TrainingData/training.json and sets title + description.
    /// Use this in task EventsToFollow — no need to duplicate the strings. Keys match TASK_01, TASK_02, TASK_03_SLIDE_HOOD, etc.</summary>
    public void SetTaskContent(string key)
    {
        if (string.IsNullOrEmpty(key) || TrainingDataProvider.instance == null) return;
        var text = TrainingDataProvider.instance.GetDisplay(key);
        if (string.IsNullOrEmpty(text)) return;
        // First line (before newline) becomes title; remainder is body. If one line, title = empty.
        var nl = text.IndexOf('\n');
        if (nl > 0) { SetTitle(text.Substring(0, nl).Trim()); SetDescription(text.Substring(nl + 1).Trim()); }
        else SetDescription(text);
    }

    public void SetStep(int step) { if (panel) panel.SetStep(step); }
    public void SetTotalSteps(int total) { if (panel) panel.SetTotalSteps(total); }
    public void SetNextVisible(bool v) { if (panel) panel.SetNextVisible(v); }

    // ---------------------------------------------------------------- VFX activation
    /// <summary>Enables a VFX GameObject (and all its particle children) and triggers the molten-aluminium controller
    /// if present. One call per task event — handles the quirk where SetActive alone doesn't fully kick the controller.</summary>
    public void EnableAndPlayVFX(GameObject vfxRoot)
    {
        if (!vfxRoot) return;
        if (!vfxRoot.activeSelf) vfxRoot.SetActive(true);
        // Enable all child particle emitters
        foreach (var ps in vfxRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
            ps.Play(true);
        }
        // Kick the Molten controller via reflection to avoid a hard dependency on the PotLeakage.VFX assembly.
        foreach (var mb in vfxRoot.GetComponents<MonoBehaviour>())
        {
            var t = mb.GetType();
            if (t.Name == "MoltenAluminiumVFXController")
            {
                var m = t.GetMethod("PlayMoltenMetalOverflow") ?? t.GetMethod("StartFloorSpill") ?? t.GetMethod("StartMagmaDrool");
                if (m != null) { try { m.Invoke(mb, null); } catch (System.Exception e) { Debug.LogWarning("VFX kick failed: " + e.Message); } }
                break;
            }
        }
    }

    // ---------------------------------------------------------------- Walkie
    /// <summary>Starts the walkie-talkie call flow directly: enables walkie, shows the WalkieCommunicationCanvas,
    /// user slides CallSlider to call, then conversation plays, then task auto-completes.</summary>
    public void StartWalkie(WalkieController walkie)
    {
        if (!walkie) return;
        walkie.gameObject.SetActive(true);
        walkie.BeginCall();
        int token = Token();
        walkie.OnConversationComplete = () => CompleteIfCurrent(token);
        cleanups.Add(() => { if (walkie) walkie.OnConversationComplete = null; });
    }

    /// <summary>Wires the complete walkie interaction: highlights the walkie-talkie in 3D, makes it raycastable & selectable
    /// via XR interactable. When selected/pointed with trigger, activates WalkieCommunicationCanvas and initiates the call.</summary>
    public void StartWalkieTask(GameObject walkieObj)
    {
        SetupWalkieInteraction(walkieObj, null);
    }

    public void SetupWalkieInteraction(GameObject walkieObj, WalkieController controller)
    {
        if (!walkieObj) return;
        if (!walkieObj.activeSelf) walkieObj.SetActive(true);

        // Find controller if omitted
        if (!controller) controller = walkieObj.GetComponentInChildren<WalkieController>(true);

        // Highlight walkietalkie green
        HighlightObjectGreen(walkieObj);

        // Ensure collider is correctly fitted to renderer bounds
        FitBoxColliderIfNeeded(walkieObj);

        // Ensure XR interactable (simple/kinematic) so NearFar Interactor ray highlights and selects it
        var xr = EnsureInteractable(walkieObj, followHand: false);
        if (!xr) return;

        // Ensure root BoxCollider is assigned to interactable
        var rootBox = walkieObj.GetComponent<BoxCollider>();
        if (rootBox && (xr.colliders == null || xr.colliders.Count == 0 || !xr.colliders.Contains(rootBox)))
        {
            xr.colliders.Clear();
            xr.colliders.Add(rootBox);
        }

        // Ensure all NearFarInteractors allow hovered activation and use straight, non-bending, responsive rays
        EnsureStraightControllerRays();

        // Ensure WalkieCommunicationCanvas is oriented facing the player at comfortable eye-level
        var canvasTr = walkieObj.transform.Find("WalkieCommunicationCanvas");
        if (canvasTr)
        {
            PositionWalkieCanvas(canvasTr);
        }

        // Hide NEXT button on panel so task cannot be bypassed
        if (panel) panel.SetNextVisible(false);

        // Gate task advance in SequenceHandler until communication finishes
        bool conversationDone = false;
        if (handler)
        {
            handler.canAdvanceTask = () => conversationDone;
            cleanups.Add(() => { if (handler != null && handler.canAdvanceTask != null) handler.canAdvanceTask = null; });
        }

        int token = Token();
        bool activated = false;
        System.Action activateWalkie = () =>
        {
            if (activated) return;
            if (handler && handler.TaskToken != token) return;
            activated = true;
            ClearHighlight(walkieObj);
            if (canvasTr) PositionWalkieCanvas(canvasTr);

            if (controller)
            {
                controller.gameObject.SetActive(true);
                controller.BeginCall();
                controller.OnConversationComplete = () =>
                {
                    conversationDone = true;
                    if (handler) handler.canAdvanceTask = null;
                    if (panel) panel.SetNextVisible(true);
                    CompleteIfCurrent(token);
                };
                cleanups.Add(() => { if (controller) controller.OnConversationComplete = null; });
            }
            else
            {
                conversationDone = true;
                if (handler) handler.canAdvanceTask = null;
                if (panel) panel.SetNextVisible(true);
                CompleteIfCurrent(token);
            }
        };

        UnityEngine.Events.UnityAction<SelectEnterEventArgs> onSelect = _ => activateWalkie();
        UnityEngine.Events.UnityAction<ActivateEventArgs> onActivate = _ => activateWalkie();

        xr.selectEntered.AddListener(onSelect);
        xr.activated.AddListener(onActivate);
        cleanups.Add(() =>
        {
            if (xr)
            {
                xr.selectEntered.RemoveListener(onSelect);
                xr.activated.RemoveListener(onActivate);
            }
        });
    }

    // ---------------------------------------------------------------- Tool selection
    /// <summary>Starts the tool-selection flow using the configured ToolSelectionSequencer. Completes when all pairs done.</summary>
    public void StartToolSelection(ToolSelectionSequencer seq)
    {
        if (!seq) return;
        int token = Token();
        seq.OnAllComplete = () => CompleteIfCurrent(token);
        cleanups.Add(() => { if (seq) seq.OnAllComplete = null; });
        seq.Begin();
    }

    // ---------------------------------------------------------------- Slide interactions
    /// <summary>Converts an object into a 1-axis slide: grab it, drag along `axis` between min/max, task completes when
    /// the slide reaches `completeAt` (0..1).</summary>
    public void MakeSlider(GameObject target, Vector3 axis, float min, float max, float completeAt = 0.9f)
    {
        if (!target) return;
        var slider = target.GetComponent<SlideInteractable>();
        if (!slider) slider = target.AddComponent<SlideInteractable>();
        slider.axis = axis.normalized == Vector3.zero ? Vector3.right : axis.normalized;
        slider.min = min; slider.max = max; slider.completeAt = Mathf.Clamp01(completeAt);
        EnsureInteractable(target, true);
        int token = Token();
        slider.OnReached = () => CompleteIfCurrent(token);
        cleanups.Add(() => { if (slider) slider.OnReached = null; });
        EnableAndHighlight(target);
    }

    /// <summary>Highlights the hood plane; when the user grabs it the plane animates (forward, back, slide sideways) and the task completes.</summary>
    public void StartHoodSlide(HoodSlideSequence hood)
    {
        if (!hood) return;
        int token = Token();
        hood.Arm(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (hood) hood.Disarm(); });
    }

    /// <summary>Runs the crane_roller + driller crust breaking animation and completes the task when it ends.</summary>
    public void StartCrustBathing(CrustBathingSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Begin(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Cancel(); });
    }

    /// <summary>Drag-and-drop SST tool + round pipes; completes after the gas particles have played.</summary>
    public void StartSstConnection(SstConnectionSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Begin(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Cancel(); });
    }

    /// <summary>Highlights the valve handle; grabbing it turns the valve, moves to SST and plays the gas effect.</summary>
    public void StartAirUnlock(AirUnlockSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Arm(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Disarm(); });
    }

    /// <summary>Press the highlighted Fused Alumina Bag to throw it at the cement; it burns away on impact, then the task completes.</summary>
    public void StartFusedAlumina(FusedAluminaSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Arm(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Disarm(); });
    }

    /// <summary>Closes Plane.023 then Plane.020 by grabbing each highlighted hood in turn.</summary>
    public void StartCloseHood(CloseHoodSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Begin(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Cancel(); });
    }

    /// <summary>Press the highlighted walkie-talkie: camera to Talk, both characters run in, then the two voice clips; completes afterwards.</summary>
    public void StartEmergencyCall(EmergencyCallSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Arm(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Disarm(); });
    }

    /// <summary>Shows the end-of-training checklist panel in front of the player.</summary>
    public void ShowCompletionPanel(CompletionChecklistPanel panelToShow)
    {
        if (!panelToShow) return;
        panelToShow.Show();
        cleanups.Add(() => { if (panelToShow) panelToShow.Hide(); });
    }

    /// <summary>Control machine: highlighted button steps the voltage/beam display; completes after the last step.</summary>
    public void StartControlMachine(ControlMachineSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Arm(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Disarm(); });
    }

    /// <summary>Disables the driller, lowers Cube.005 and plays BathPowder_Impact, then completes the task.</summary>
    public void StartBathPowder(BathPowderSequence seq)
    {
        if (!seq) return;
        int token = Token();
        seq.Begin(() => CompleteIfCurrent(token));
        cleanups.Add(() => { if (seq) seq.Cancel(); });
    }

    void Hook(GameObject target, bool followHand)
    {
        var xr = EnsureInteractable(target, followHand);
        if (!xr) return;
        int token = Token();
        UnityEngine.Events.UnityAction<SelectEnterEventArgs> cb = _ => CompleteIfCurrent(token);
        xr.selectEntered.AddListener(cb);
        cleanups.Add(() => { if (xr) xr.selectEntered.RemoveListener(cb); });
    }

    int Token() { return handler ? handler.TaskToken : 0; }

    void CompleteIfCurrent(int token)
    {
        if (handler && handler.TaskToken == token) handler.CompleteCurrentTask();
    }

    /// <summary>Called by SequenceHandler at the start of every task.</summary>
    public void ResetTaskState()
    {
        titleString = null;
        desString = null;
        foreach (var c in cleanups) c();
        cleanups.Clear();
        foreach (var h in activeHighlights) if (h) h.SetHighlight(false);
        activeHighlights.Clear();
        StopAllCoroutines();
    }

    // ---------------------------------------------------------------- Interactable setup
    /// <summary>Adds a box collider (fitted to all renderers), kinematic Rigidbody and XRGrabInteractable when missing.</summary>
    public static XRGrabInteractable EnsureInteractable(GameObject go, bool followHand)
    {
        if (!go) return null;
        var xr = go.GetComponent<XRGrabInteractable>();
        if (!xr)
        {
            if (!go.GetComponentInChildren<Collider>(false)) FitBoxCollider(go);
            // Rigidbody is added by XRGrabInteractable's RequireComponent; keep it kinematic so the machine/crane never fall.
            xr = go.AddComponent<XRGrabInteractable>();
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            xr.throwOnDetach = false;
        }
        xr.trackPosition = followHand;
        xr.trackRotation = followHand;
        return xr;
    }

    public static void FitBoxColliderIfNeeded(GameObject go)
    {
        if (!go) return;
        var box = go.GetComponent<BoxCollider>();
        if (!box || Vector3.Scale(box.size, go.transform.lossyScale).magnitude < 0.1f)
        {
            FitBoxCollider(go);
        }
    }

    public static void FitBoxCollider(GameObject go)
    {
        if (!go) return;
        var rends = go.GetComponentsInChildren<Renderer>(false);
        if (rends.Length == 0) { if (!go.GetComponent<BoxCollider>()) go.AddComponent<BoxCollider>(); return; }
        var t = go.transform;
        Bounds b = new Bounds(); bool init = false;
        foreach (var r in rends)
        {
            if (r.GetComponent<CanvasRenderer>() != null) continue;
            var wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = t.InverseTransformPoint(corner);
                if (!init) { b = new Bounds(local, Vector3.zero); init = true; } else b.Encapsulate(local);
            }
        }
        var col = go.GetComponent<BoxCollider>();
        if (!col) col = go.AddComponent<BoxCollider>();
        col.center = b.center; col.size = b.size;
    }

    /// <summary>Places the WalkieCommunicationCanvas directly in front of the player's view at eye level, facing the player upright.</summary>
    public void PositionWalkieCanvas(Transform canvasTr)
    {
        if (!canvasTr) return;
        var rig = Rig();
        var cam = rig && rig.Camera ? rig.Camera.transform : (Camera.main ? Camera.main.transform : null);
        // Canvas may be parented under a scaled object (the walkie): keep a constant world scale of 0.002.
        float parentScale = canvasTr.parent ? Mathf.Max(0.0001f, canvasTr.parent.lossyScale.x) : 1f;
        Vector3 localScale = Vector3.one * (0.002f / parentScale);
        if (cam)
        {
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.001f) fwd = cam.forward;
            fwd.Normalize();
            // Place 1.2m in front of player, chest/eye level
            canvasTr.position = cam.position + fwd * 1.2f + Vector3.down * 0.15f;
            canvasTr.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            canvasTr.localScale = localScale;
        }
        else
        {
            var sst = GameObject.Find("SST");
            if (sst)
            {
                var fwd = Vector3.ProjectOnPlane(sst.transform.forward, Vector3.up).normalized;
                canvasTr.position = sst.transform.position + fwd * 1.2f + Vector3.down * 0.15f;
                canvasTr.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                canvasTr.localScale = localScale;
            }
        }

        var canvas = canvasTr.GetComponent<Canvas>();
        if (canvas)
        {
            var eventCam = Camera.main ? Camera.main : (rig ? rig.Camera : null);
            if (eventCam && canvas.worldCamera != eventCam)
            {
                canvas.worldCamera = eventCam;
            }
        }
    }

    /// <summary>Configures NearFarInteractors, casters and line visual controllers across the scene to produce straight, responsive controller rays without curve bending or lag.</summary>
    public static void EnsureStraightControllerRays()
    {
        // 1. Ensure Camera tag is MainCamera so Camera.main is never null
        var cam = Camera.main;
        if (!cam)
        {
            var rig = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (rig && rig.Camera)
            {
                rig.Camera.tag = "MainCamera";
                cam = rig.Camera;
            }
        }

        // 2. Ensure all WorldSpace canvases have a valid worldCamera
        if (cam != null)
        {
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (var c in canvases)
            {
                if (c && c.gameObject.scene.isLoaded && c.renderMode == RenderMode.WorldSpace)
                {
                    if (c.worldCamera == null)
                        c.worldCamera = cam;
                }
            }
        }

        // 3. Configure all NearFarInteractors, casters, and visual controllers
        var interactors = Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        foreach (var nf in interactors)
        {
            nf.allowHoveredActivate = true;
            nf.allowActivate = true;
            nf.enableUIInteraction = true;

            var caster = nf.GetComponent<CurveInteractionCaster>();
            if (caster)
            {
                caster.enableStabilization = false;
                caster.positionStabilization = 0f;
                caster.angleStabilization = 0f;
                caster.hitDetectionType = CurveInteractionCaster.HitDetectionType.Raycast;
                caster.coneCastAngle = 0f;
            }

            var cvc = nf.GetComponentInChildren<CurveVisualController>(true);
            if (cvc)
            {
                cvc.extendLineToEmptyHit = true;
                cvc.extensionRate = 0f;
                cvc.linePropertyAnimationSpeed = 0f;
                cvc.maxVisualCurveDistance = 10f;
                cvc.restingVisualLineLength = 10f;
                cvc.snapToSelectedAttachIfAvailable = false;
                cvc.snapToSnapVolumeIfAvailable = false;
                cvc.computeMidPointWithComplexCurves = false;
                cvc.overrideLineOrigin = true;
                cvc.lineOriginTransform = nf.transform;
                cvc.lineDynamicsMode = LineDynamicsMode.Traditional;
                cvc.retractDelay = 0f;
                cvc.retractDuration = 0.05f;

                if (cvc.noValidHitProperties != null) { cvc.noValidHitProperties.smoothlyCurveLine = false; cvc.noValidHitProperties.lineBendRatio = 1f; }
                if (cvc.uiHitProperties != null) { cvc.uiHitProperties.smoothlyCurveLine = false; cvc.uiHitProperties.lineBendRatio = 1f; }
                if (cvc.uiPressHitProperties != null) { cvc.uiPressHitProperties.smoothlyCurveLine = false; cvc.uiPressHitProperties.lineBendRatio = 1f; }
                if (cvc.selectHitProperties != null) { cvc.selectHitProperties.smoothlyCurveLine = false; cvc.selectHitProperties.lineBendRatio = 1f; }
                if (cvc.hoverHitProperties != null) { cvc.hoverHitProperties.smoothlyCurveLine = false; cvc.hoverHitProperties.lineBendRatio = 1f; }
            }
        }
    }
}
