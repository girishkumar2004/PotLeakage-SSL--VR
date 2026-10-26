using PotLeakage.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Writes Inspector events (persistent UnityEvent calls) for task flows in the working copy scene PotLeakage-(VR)_Events.unity.
/// Safe to run again: the calls it adds are removed and re-added.</summary>
public static class TaskEventsWiring
{
    static Transform FindDeep(string path)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!t.gameObject.scene.isLoaded) continue;
            string p = t.name; var c = t;
            while (c.parent) { c = c.parent; p = c.name + "/" + p; }
            if (p == path) return t;
        }
        return null;
    }

    static Sequence SequenceNamed(string name)
    {
        var t = FindDeep("Sequence/ListOfSequence/" + name);
        return t ? t.GetComponent<Sequence>() : null;
    }

    static UnityEventBase TaskEvents(Sequence s) { return s.TaskList[0].EventsToFollow; }

    /// <summary>Removes persistent calls whose target is one of the given objects (so a re-run does not duplicate them).</summary>
    static void RemoveCallsTo(UnityEvent ev, params Object[] targets)
    {
        for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--)
            if (System.Array.IndexOf(targets, ev.GetPersistentTarget(i)) >= 0) UnityEventTools.RemovePersistentListener(ev, i);
    }

    [MenuItem("Vedanta VR/7 Wire Welcome + Bath Powder events")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        WireWelcome();
        WireBathPowder();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Welcome + Bath Powder events wired.");
    }

    // ------------------------------------------------------------------ TASK_TOOL_SELECTION
    // Three tasks like the desktop scene: 1 SST Cooling Connector, 2 Hose pipe, 3 Fused alumina bag. Selecting the highlighted tool with the
    // controller ray adds it to the Collected Tools panel and completes the task; any other tool of the box shows the "incorrect tool" text.
    [MenuItem("Vedanta VR/8 Wire Tool Selection events")]
    public static void RunToolSelection()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        WireToolSelection();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Tool Selection events wired.");
    }

    const string WrongTool = "<color=#FF3333><b>Incorrect tool selected! This tool is not used for this step. Please select the {0}.</b></color>";

    static XRInteractionEvents MakeSelectable(GameObject go, bool armed, bool oneShot)
    {
        if (!go.GetComponent<Collider>()) go.AddComponent<BoxCollider>();
        if (!go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>())
            go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        var ev = Ensure<XRInteractionEvents>(go);
        ev.armed = armed; ev.oneShot = oneShot; ev.hapticOnSelect = false;
        WireUtil.Clear(ev.onSelect); WireUtil.Clear(ev.onHoverEnter); WireUtil.Clear(ev.onHoverExit); WireUtil.Clear(ev.onActivate);
        EditorUtility.SetDirty(go);
        return ev;
    }

    static void WireToolSelection()
    {
        var seq = SequenceNamed("TASK_TOOL_SELECTION");
        var box = FindDeep("PotLeakage Environment/ToolBox");
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var panel = Object.FindFirstObjectByType<PotLeakage.UI.CollectedToolsPanelController>(FindObjectsInactive.Include);
        var voice = Object.FindFirstObjectByType<VoiceOverPlayer>(FindObjectsInactive.Include);
        var welcome = FindDeep("WelcomePanel") ?? FindDeep("Canvas/WelcomePanel");
        if (welcome == null) foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.name == "WelcomePanel" && t.gameObject.scene.isLoaded) welcome = t;

        var sst = box.Find("SST PIPE").gameObject; var hose = box.Find("round_PIPES").gameObject; var bag = box.Find("Bag").gameObject;
        string[] distractorNames = { "Shovel", "hammer", "wrench", "L_TOOL", "Spanner", "SHOVEL", "Iron Rod", "Crowbar", "bamboo" };
        var distractors = new System.Collections.Generic.List<XRInteractionEvents>();
        foreach (var n in distractorNames) { var tr = box.Find(n); if (tr) distractors.Add(MakeSelectable(tr.gameObject, false, false)); }

        var steps = new[]
        {
            new { go = sst,  key = "SST_PIPE_ani",        title = "Step 1: Select SST Cooling Connector", desc = "Select SST Cooling Connector in order to reduce the intensity of the leakage.", name = "SST Cooling Connector" },
            new { go = hose, key = "round_PIPES",         title = "Step 2: Select Hose pipe",             desc = "Select Hose pipe in order to reduce the intensity of the leakage.",             name = "Hose pipe" },
            new { go = bag,  key = "Fused Alumina Bag",   title = "Step 3: Select Bag",                   desc = "Pick up fused alumina bag of 10 KG.",                                          name = "Fused Alumina Bag" },
        };

        // make sure there are exactly three tasks
        while (seq.TaskList.Count > 3) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        while (seq.TaskList.Count < 3) seq.TaskList.Add(new Task());

        for (int i = 0; i < 3; i++)
        {
            var s = steps[i]; var t = seq.TaskList[i];
            t.TaskName = "Tool Selection"; t.instructionText = s.desc; t.ttsTextOrKey = ""; t.useTTS = false;
            t.AutoAdvanceAfterVoiceOver = false; t.TaskCompleted = false; t.TriggerCompleted = false;
            if (t.EventsToFollow == null) t.EventsToFollow = new UnityEvent();
            var ev = t.EventsToFollow; WireUtil.Clear(ev);

            if (i == 0)
            {
                if (welcome) WireUtil.SetActive(ev, welcome.gameObject, false);
                WireUtil.Tr(ev, helper, "SnapCameraToTransform", box);
                WireUtil.Str(ev, helper, "SetTaskContent", "TASK_06");
                WireUtil.Void(ev, panel, "ClearPanel");
            }
            WireUtil.Str(ev, helper, "SetTitle", s.title);
            WireUtil.Str(ev, helper, "SetDescription", s.desc);
            if (i == 0) foreach (var d in distractors) WireUtil.Void(ev, d, "Arm");
            WireUtil.SetActive(ev, s.go, true);
            WireUtil.Obj(ev, helper, "HighlightObject", s.go);
            WireUtil.Void(ev, panel, "ShowPanel");
            WireUtil.Void(ev, Ensure<XRInteractionEvents>(s.go), "Arm");
            if (i > 0 && voice) WireUtil.Str(ev, voice, "PlayByText", s.desc);
        }

        // select reactions
        for (int i = 0; i < 3; i++)
        {
            var s = steps[i];
            var x = MakeSelectable(s.go, false, true);
            var ev = x.onSelect;
            WireUtil.Obj(ev, helper, "RemoveHighlight", s.go);
            WireUtil.SetActive(ev, s.go, false);
            WireUtil.Str(ev, panel, "AddCollected", s.key);
            WireUtil.Void(ev, helper, "HapticCorrect");
            if (i == 2) foreach (var d in distractors) WireUtil.Void(ev, d, "Disarm");
            WireUtil.Flt(ev, helper, "CompleteTaskWithDelay", 0.6f);
            WireUtil.FixCallStates(x);
        }
        foreach (var d in distractors)
        {
            WireUtil.Void(d.onSelect, helper, "HapticWrong");
            // the text names the tool of the step in progress: the first step is the default, later steps overwrite it by their own SetDescription
            WireUtil.Str(d.onSelect, helper, "ShowTemporaryDescription", string.Format(WrongTool, "highlighted tool"));
            WireUtil.FixCallStates(d);
        }
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq);
    }

    // ------------------------------------------------------------------ TASK_4_SST_CONNECTION
    // Like the desktop scene: the SST Cooling Connector and the hose pipe are clicked in the Collected Tools panel (controller ray); the tool
    // flies out of its row, turns in front of the player and lands on the connection point (ToolFlyTo). Two tasks: SST connector, hose pipe.
    [MenuItem("Vedanta VR/9 Wire SST Connection events")]
    public static void RunSstConnection()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        WireSstConnection();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: SST Connection events wired.");
    }

    // `??` does not work on Unity objects (a missing component is a fake-null object), so test with the Unity operator
    static T Ensure<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c ? c : go.AddComponent<T>(); }

    static ToolFlyTo FlyOf(GameObject go) { return Ensure<ToolFlyTo>(go); }

    static void WireSstConnection()
    {
        var seq = SequenceNamed("TASK_4_SST_CONNECTION");
        var sq = Object.FindFirstObjectByType<SstConnectionSequence>(FindObjectsInactive.Include);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var panel = Object.FindFirstObjectByType<PotLeakage.UI.CollectedToolsPanelController>(FindObjectsInactive.Include);
        var camPoint = FindDeep("SST");
        if (camPoint == null) foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.name == "SST" && t.gameObject.scene.isLoaded) camPoint = t;

        // --- the flying tools
        var sstFly = FlyOf(sq.sstPipe);
        sstFly.mode = ToolFlyTo.LandingMode.SettleOnTarget; sstFly.target = sq.sstPipe1.transform; sstFly.rowKey = "SST_PIPE_ani"; sstFly.panel = panel;
        var hoseFly = FlyOf(sq.roundPipes1);
        hoseFly.mode = ToolFlyTo.LandingMode.LandOnRenderer; hoseFly.landingRenderer = sq.sstPipe1.GetComponent<MeshRenderer>(); hoseFly.rowKey = "round_PIPES"; hoseFly.panel = panel;

        WireUtil.Clear(sstFly.onArrived);
        WireUtil.SetActive(sstFly.onArrived, sq.sstPipe, false);
        WireUtil.SetActive(sstFly.onArrived, sq.sstPipe1, true);
        WireUtil.Str(sstFly.onArrived, panel, "RemoveCollected", "SST_PIPE_ani");
        WireUtil.Flt(sstFly.onArrived, helper, "CompleteTaskWithDelay", 0.5f);

        WireUtil.Clear(hoseFly.onArrived);
        WireUtil.SetActive(hoseFly.onArrived, sq.roundPipes1, false);
        if (sq.roundPipes) WireUtil.SetActive(hoseFly.onArrived, sq.roundPipes, false);
        WireUtil.SetActive(hoseFly.onArrived, sq.sstPipe1, true);
        WireUtil.SetActive(hoseFly.onArrived, sq.sstPipe1Bezier, true);
        WireUtil.Str(hoseFly.onArrived, panel, "RemoveCollected", "round_PIPES");
        WireUtil.Flt(hoseFly.onArrived, helper, "CompleteTaskWithDelay", sq.gasPlaySeconds);

        // --- panel row clicks
        foreach (var r in panel.rowEvents)
        {
            if (r.keyContains == "SST_PIPE_ani") { WireUtil.Clear(r.onClicked); WireUtil.Void(r.onClicked, sstFly, "Play"); }
            if (r.keyContains == "round_PIPES") { WireUtil.Clear(r.onClicked); WireUtil.Void(r.onClicked, hoseFly, "Play"); }
        }

        // --- tasks
        while (seq.TaskList.Count > 2) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        while (seq.TaskList.Count < 2) seq.TaskList.Add(new Task());
        for (int i = 0; i < 2; i++)
        {
            var t = seq.TaskList[i];
            t.TaskName = "SST Connection"; t.useTTS = false; t.ttsTextOrKey = ""; t.AutoAdvanceAfterVoiceOver = false; t.TaskCompleted = false; t.TriggerCompleted = false;
            t.instructionText = i == 0 ? sq.description : sq.description2;
            if (t.EventsToFollow == null) t.EventsToFollow = new UnityEvent();
            var ev = t.EventsToFollow; WireUtil.Clear(ev);
            WireUtil.Str(ev, helper, "SetTitle", "SST Connection");
            WireUtil.Str(ev, helper, "SetDescription", i == 0 ? sq.description : sq.description2);
            if (i == 0)
            {
                if (camPoint) WireUtil.Tr(ev, helper, "SnapCameraToTransform", camPoint);
                foreach (var g in new[] { sq.roundPipes1, sq.roundPipes, sq.sstPipe1, sq.sstPipe1Bezier, sq.sstPipeBezier, sq.sstPipe })
                    if (g) WireUtil.SetActive(ev, g, false);
                WireUtil.Str(ev, panel, "AddCollected", "SST_PIPE_ani");
                WireUtil.Str(ev, panel, "AddCollected", "round_PIPES");
                WireUtil.Void(ev, panel, "ShowPanel");
                WireUtil.Str(ev, panel, "DisarmRow", "round_PIPES");
                WireUtil.Str(ev, panel, "ArmRow", "SST_PIPE_ani");
            }
            else
            {
                WireUtil.SetActive(ev, sq.sstPipe1, true);
                WireUtil.Void(ev, panel, "ShowPanel");
                WireUtil.Str(ev, panel, "ArmRow", "round_PIPES");
            }
            WireUtil.Str(ev, helper, "PlayVoiceOver", i == 0 ? sq.voiceKey : sq.voiceKey2);
        }
        WireUtil.FixCallStates(seq); WireUtil.FixCallStates(sstFly); WireUtil.FixCallStates(hoseFly); WireUtil.FixCallStates(panel);
        EditorUtility.SetDirty(seq); EditorUtility.SetDirty(sstFly); EditorUtility.SetDirty(hoseFly); EditorUtility.SetDirty(panel);
    }

    // ------------------------------------------------------------------ TASK_5_AIR_Unlock
    // Valve handle turns with an Animator clip (z -90 to -180 like the old script); selecting it starts the clip, a second task moves the
    // player to the SST point and plays the GasPoint particles under SST_PIPE_ani_1.
    [MenuItem("Vedanta VR/10 Wire Air Unlock events")]
    public static void RunAirUnlock()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        WireAirUnlock();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Air Unlock events wired.");
    }

    static AnimationClip MakeEulerClip(string path, string name, Vector3 baseEuler, float fromZ, float toZ, float length)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();
        clip.name = name; clip.frameRate = 30f;
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", AnimationCurve.Constant(0, length, baseEuler.x));
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Constant(0, length, baseEuler.y));
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.EaseInOut(0, fromZ, length, toZ));
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void WireAirUnlock()
    {
        var seq = SequenceNamed("TASK_5_AIR_Unlock");
        var aq = Object.FindFirstObjectByType<AirUnlockSequence>(FindObjectsInactive.Include);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var valve = aq.valveHandle.gameObject;
        var airPoint = FindDeep("AirUnlock");
        if (airPoint == null) foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.name == "AirUnlock" && t.gameObject.scene.isLoaded) airPoint = t;

        // animator + clips (valve handle turns about its local z)
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/Animations/AirUnlock")) AssetDatabase.CreateFolder("Assets/Animations", "AirUnlock");
        var baseEuler = valve.transform.localEulerAngles;
        var reset = MakeEulerClip("Assets/Animations/AirUnlock/ValveReset.anim", "ValveReset", baseEuler, aq.fromZ, aq.fromZ, 0.034f);
        var turn = MakeEulerClip("Assets/Animations/AirUnlock/ValveTurn.anim", "ValveTurn", baseEuler, aq.fromZ, aq.toZ, aq.turnDuration);
        string ctrlPath = "Assets/Animations/AirUnlock/ValveHandle.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); sm.defaultState = idle;
        var sReset = sm.AddState("ValveReset"); sReset.motion = reset; sReset.writeDefaultValues = false;
        var sTurn = sm.AddState("ValveTurn"); sTurn.motion = turn; sTurn.writeDefaultValues = false;
        idle.writeDefaultValues = false;
        var anim = Ensure<Animator>(valve);
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        valve.transform.localRotation = Quaternion.Euler(baseEuler.x, baseEuler.y, aq.fromZ);

        // select reaction
        var x = MakeSelectable(valve, false, true);
        WireUtil.Obj(x.onSelect, helper, "RemoveHighlight", valve);
        WireUtil.Play(x.onSelect, anim, "ValveTurn");
        WireUtil.Flt(x.onSelect, helper, "AdvanceTaskWithDelay", aq.turnDuration + aq.waitAfterTurn);
        WireUtil.FixCallStates(x);

        // gas particles (GasPoint systems under SST_PIPE_ani_1)
        var gas = new System.Collections.Generic.List<ParticleSystem>();
        foreach (var ps in aq.sstPipe1.GetComponentsInChildren<ParticleSystem>(true)) if (ps.gameObject.name == "GasPoint") gas.Add(ps);

        while (seq.TaskList.Count > 2) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        while (seq.TaskList.Count < 2) seq.TaskList.Add(new Task());
        string desc = seq.TaskList[0].instructionText;
        for (int i = 0; i < 2; i++)
        {
            var t = seq.TaskList[i];
            t.TaskName = "Air Unlock Valve"; t.useTTS = false; t.ttsTextOrKey = ""; t.AutoAdvanceAfterVoiceOver = false; t.TaskCompleted = false; t.TriggerCompleted = false;
            t.instructionText = desc;
            if (t.EventsToFollow == null) t.EventsToFollow = new UnityEvent();
            var ev = t.EventsToFollow; WireUtil.Clear(ev);
            WireUtil.Str(ev, helper, "SetTitle", "Air Unlock Valve");
            WireUtil.Str(ev, helper, "SetDescription", desc);
            if (i == 0)
            {
                if (airPoint) WireUtil.Tr(ev, helper, "SnapCameraToTransform", airPoint);
                WireUtil.Play(ev, anim, "ValveReset");
                WireUtil.Obj(ev, helper, "StopParticlesOf", aq.sstPipe1);   // gas is off until the valve is opened (was GasEffectRange)
                WireUtil.Obj(ev, helper, "HighlightObject", valve);
                WireUtil.Void(ev, x, "Arm");
            }
            else
            {
                if (aq.sstPoint) WireUtil.Tr(ev, helper, "SnapCameraToTransform", aq.sstPoint);
                WireUtil.Enabled(ev, anim, false);      // the valve keeps its open pose; later tasks may turn it again
                foreach (var ps in gas) WireUtil.PS(ev, ps, "Play");
                WireUtil.Flt(ev, helper, "AdvanceTaskWithDelay", aq.endWait);
            }
        }
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq); EditorUtility.SetDirty(valve);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ TASK_6_SLIDE_RIGHT_HOOD / TASK_05_SLIDE_LEFT_HOOD
    // Each hood plane gets an Animator with the slide clip (start -> intermediate -> final, same local positions and timings as the old
    // script), plus the matching close clip for CLOSE_HOOD later. Selecting the highlighted plane plays the clip and completes the task.
    [MenuItem("Vedanta VR/11 Wire Hood Slide events")]
    public static void RunHoodSlides()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/Animations/Hoods")) AssetDatabase.CreateFolder("Assets/Animations", "Hoods");
        WireHood("TASK_6_SLIDE_RIGHT_HOOD", "HoodSlide_Right", "HoodClose_Right", "Right");
        WireHood("TASK_05_SLIDE_LEFT_HOOD", "HoodSlide_Left", "HoodClose_Left", "Left");
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Hood slide events wired.");
    }

    static AnimationClip MakeHoodClip(string path, string name, Vector3 a, Vector3 mid, Vector3 b, float durA, float durB)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves(); clip.name = name; clip.frameRate = 30f;
        float total = durA + durB;
        for (int axis = 0; axis < 3; axis++)
        {
            var c = new AnimationCurve(
                new Keyframe(0f, a[axis], 0f, 0f),
                new Keyframe(durA, mid[axis], 0f, 0f),
                new Keyframe(total, b[axis], 0f, 0f));
            clip.SetCurve("", typeof(Transform), "localPosition." + "xyz"[axis], c);
        }
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void WireHood(string taskName, string slideName, string closeName, string side)
    {
        var seq = SequenceNamed(taskName);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        HoodSlideSequence slide = null, close = null;
        foreach (var h in Resources.FindObjectsOfTypeAll<HoodSlideSequence>())
        {
            if (!h.gameObject.scene.isLoaded) continue;
            if (h.name == slideName) slide = h;
            if (h.name == closeName) close = h;
        }
        var plane = slide.plane.gameObject;
        var camPoint = FindDeep("SST");
        if (camPoint == null) foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.name == "SST" && t.gameObject.scene.isLoaded) camPoint = t;

        string dir = "Assets/Animations/Hoods/";
        var reset = MakeHoodClip(dir + "Hood" + side + "_Start.anim", "Hood" + side + "_Start", slide.startLocalPos, slide.startLocalPos, slide.startLocalPos, 0.017f, 0.017f);
        var slideClip = MakeHoodClip(dir + "Hood" + side + "_Slide.anim", "Hood" + side + "_Slide", slide.startLocalPos, slide.intermediateLocalPos, slide.finalLocalPos, slide.phaseADuration, slide.phaseBDuration);
        var closeClip = MakeHoodClip(dir + "Hood" + side + "_Close.anim", "Hood" + side + "_Close", close.startLocalPos, close.intermediateLocalPos, close.finalLocalPos, close.phaseADuration, close.phaseBDuration);
        string ctrlPath = dir + "Hood" + side + ".controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.writeDefaultValues = false; sm.defaultState = idle;
        var closeStartClip = MakeHoodClip(dir + "Hood" + side + "_CloseStart.anim", "Hood" + side + "_CloseStart", close.startLocalPos, close.startLocalPos, close.startLocalPos, 0.017f, 0.017f);
        foreach (var (n, m) in new[] { ("Start", (Motion)reset), ("Slide", slideClip), ("CloseStart", closeStartClip), ("Close", closeClip) })
        { var st = sm.AddState(n); st.motion = m; st.writeDefaultValues = false; }
        var anim = Ensure<Animator>(plane);
        anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;

        // collider with at least 15 cm of depth so the ray cannot slip past the thin plane (same rule as the old script)
        var x = MakeSelectable(plane, false, true);
        var box = plane.GetComponent<BoxCollider>();
        var s = plane.transform.lossyScale; var size = box.size;
        for (int i = 0; i < 3; i++) { float wl = Mathf.Abs(size[i] * s[i]); if (wl < 0.15f && Mathf.Abs(s[i]) > 0.0001f) size[i] = 0.15f / Mathf.Abs(s[i]); }
        box.size = size;

        WireUtil.Obj(x.onSelect, helper, "RemoveHighlight", plane);
        WireUtil.Play(x.onSelect, anim, "Slide");
        WireUtil.Flt(x.onSelect, helper, "AdvanceTaskWithDelay", slide.phaseADuration + slide.phaseBDuration + slide.finalWait);
        WireUtil.FixCallStates(x);

        var t0 = seq.TaskList[0];
        while (seq.TaskList.Count > 1) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        string title = null, desc = t0.instructionText;
        var so = new SerializedObject(seq);
        var calls = so.FindProperty("TaskList.Array.data[0].EventsToFollow.m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
            if (calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_MethodName").stringValue == "SetTitle")
                title = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
        var ev = t0.EventsToFollow; WireUtil.Clear(ev);
        WireUtil.Str(ev, helper, "SetTitle", title);
        WireUtil.Str(ev, helper, "SetDescription", desc);
        if (camPoint) WireUtil.Tr(ev, helper, "SnapCameraToTransform", camPoint);
        WireUtil.SetActive(ev, plane, true);
        WireUtil.Play(ev, anim, "Start");
        // the slide reaction is the plane's own interactable (CLOSE_HOOD switches it off and uses the child instead; this makes a replay work)
        WireUtil.Enabled(ev, plane.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(), true);
        var childSel = plane.transform.Find("CloseSelect");
        if (childSel) WireUtil.Enabled(ev, childSel.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(), false);
        WireUtil.Obj(ev, helper, "HighlightObject", plane);
        WireUtil.Void(ev, x, "Arm");
        t0.TaskCompleted = false; t0.TriggerCompleted = false;
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq); EditorUtility.SetDirty(plane);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ TASK_CRUST_BATHING
    // Automatic task: a TimedEvents timeline (Inspector) starts Animator clips of the crane, the cylinder part and the driller connector and
    // switches the three holes on as the driller reaches them. Same positions and timings as the old script.
    [MenuItem("Vedanta VR/12 Wire Crust Bathing events")]
    public static void RunCrustBathing()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/Animations/CrustBathing")) AssetDatabase.CreateFolder("Assets/Animations", "CrustBathing");
        WireCrustBathing();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Crust Bathing events wired.");
    }

    static AnimationClip MakePathClip(string path, string name, params (float t, Vector3 p)[] keys)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves(); clip.name = name; clip.frameRate = 30f;
        for (int axis = 0; axis < 3; axis++)
        {
            var kf = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++) kf[i] = new Keyframe(keys[i].t, keys[i].p[axis], 0f, 0f);
            clip.SetCurve("", typeof(Transform), "localPosition." + "xyz"[axis], new AnimationCurve(kf));
        }
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static Animator MakeAnimator(GameObject go, string ctrlPath, params (string state, AnimationClip clip)[] states)
    {
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.writeDefaultValues = false; sm.defaultState = idle;
        foreach (var (n, c) in states) { var st = sm.AddState(n); st.motion = c; st.writeDefaultValues = false; }
        var a = Ensure<Animator>(go);
        a.runtimeAnimatorController = ctrl; a.applyRootMotion = false;
        EditorUtility.SetDirty(go);
        return a;
    }

    static void WireCrustBathing()
    {
        var seq = SequenceNamed("TASK_CRUST_BATHING");
        var cs = Object.FindFirstObjectByType<CrustBathingSequence>(FindObjectsInactive.Include);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var camPtm = FindDeep("PTM Crane");
        if (camPtm == null) foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.name == "PTM Crane" && t.gameObject.scene.isLoaded) camPtm = t;
        string dir = "Assets/Animations/CrustBathing/";

        // hole targets in the connector's parent space, measured with the crane and cylinder at their final poses
        Vector3 craneKeep = cs.crane.localPosition, cylKeep = cs.cylinder.localPosition, conKeep = cs.connector.localPosition;
        cs.crane.localPosition = cs.craneTarget; cs.cylinder.localPosition = cs.cylinderTarget;
        var holes = new[] { cs.hole1, cs.hole2, cs.hole3 };
        var targets = new Vector3[3];
        for (int i = 0; i < 3; i++) targets[i] = cs.connector.parent.InverseTransformPoint(holes[i].transform.position);
        cs.crane.localPosition = craneKeep; cs.cylinder.localPosition = cylKeep;
        Vector3 conStart = conKeep;

        // clips
        var craneRoll = MakePathClip(dir + "CraneRoll.anim", "CraneRoll", (0f, cs.craneStart), (cs.craneDuration, cs.craneTarget));
        var craneStartC = MakePathClip(dir + "CraneStart.anim", "CraneStart", (0f, cs.craneStart), (0.034f, cs.craneStart));
        var cylSlide = MakePathClip(dir + "CylinderSlide.anim", "CylinderSlide", (0f, cs.cylinderStart), (cs.cylinderDuration, cs.cylinderTarget));
        var cylStartC = MakePathClip(dir + "CylinderStart.anim", "CylinderStart", (0f, cs.cylinderStart), (0.034f, cs.cylinderStart));
        var keys = new System.Collections.Generic.List<(float, Vector3)> { (0f, conStart) };
        float tt = 0f; Vector3 prev = conStart;
        var arrive = new float[3];
        for (int i = 0; i < 3; i++)
        {
            tt += i == 0 ? cs.moveToHole1 : cs.moveToHole; keys.Add((tt, targets[i])); arrive[i] = tt;
            tt += cs.riseDuration; keys.Add((tt, new Vector3(targets[i].x, targets[i].y, cs.upZ)));
        }
        float drillLen = tt;
        var drill = MakePathClip(dir + "DrillHoles.anim", "DrillHoles", keys.ToArray());
        var drillStart = MakePathClip(dir + "DrillStart.anim", "DrillStart", (0f, conStart), (0.034f, conStart));

        var aCrane = MakeAnimator(cs.crane.gameObject, dir + "Crane.controller", ("Start", craneStartC), ("Roll", craneRoll));
        var aCyl = MakeAnimator(cs.cylinder.gameObject, dir + "Cylinder.controller", ("Start", cylStartC), ("Slide", cylSlide));
        var aCon = MakeAnimator(cs.connector.gameObject, dir + "Connector.controller", ("Start", drillStart), ("Drill", drill));
        cs.crane.localPosition = craneKeep; cs.cylinder.localPosition = cylKeep; cs.connector.localPosition = conKeep;

        // timeline
        var tl = Ensure<TimedEvents>(cs.gameObject);
        tl.steps.Clear();
        float tSnap = cs.craneDuration + cs.cylinderDuration;      // rig to the SST point once the cylinder has slid across
        float tDrill = tSnap + 0.5f;
        TimedEvents.Step Add(string label, float at) { var s = new TimedEvents.Step { label = label, atSeconds = at }; tl.steps.Add(s); return s; }
        WireUtil.Play(Add("Crane rolls", 0f).onFire, aCrane, "Roll");
        WireUtil.Play(Add("Cylinder slides", cs.craneDuration).onFire, aCyl, "Slide");
        WireUtil.Tr(Add("Rig to SST", tSnap).onFire, helper, "SnapCameraToTransform", cs.sstPoint);
        WireUtil.Play(Add("Driller starts", tDrill).onFire, aCon, "Drill");
        for (int i = 0; i < 3; i++) WireUtil.SetActive(Add("Hole " + (i + 1) + " drilled", tDrill + arrive[i]).onFire, holes[i], true);
        float tEnd = tDrill + drillLen + cs.endWait;
        var endStep = Add("Finish", tEnd);
        WireUtil.Enabled(endStep.onFire, aCrane, false); WireUtil.Enabled(endStep.onFire, aCyl, false); WireUtil.Enabled(endStep.onFire, aCon, false);
        WireUtil.Flt(endStep.onFire, helper, "AdvanceTaskWithDelay", 0f);
        WireUtil.FixCallStates(tl);

        // task events
        var t0 = seq.TaskList[0];
        while (seq.TaskList.Count > 1) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        string title = null;
        var so = new SerializedObject(seq);
        var calls = so.FindProperty("TaskList.Array.data[0].EventsToFollow.m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
            if (calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_MethodName").stringValue == "SetTitle")
                title = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
        string desc = null;
        for (int i = 0; i < calls.arraySize; i++)
            if (calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_MethodName").stringValue == "SetDescription")
                desc = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
        var ev = t0.EventsToFollow; WireUtil.Clear(ev);
        WireUtil.Str(ev, helper, "SetTitle", title);
        WireUtil.Str(ev, helper, "SetDescription", desc);
        if (camPtm) WireUtil.Tr(ev, helper, "SnapCameraToTransform", camPtm);
        // hood planes are done: hand their transforms back (the Animators of the slide tasks stop driving them)
        foreach (var h in Resources.FindObjectsOfTypeAll<HoodSlideSequence>())
            if (h.gameObject.scene.isLoaded && h.name.StartsWith("HoodSlide_"))
            { var an = h.plane.GetComponent<Animator>(); if (an) WireUtil.Enabled(ev, an, false); }
        foreach (var hh in holes) WireUtil.SetActive(ev, hh, false);
        WireUtil.SetActive(ev, cs.crane.gameObject, true);
        WireUtil.SetActive(ev, cs.cylinder.gameObject, true);
        WireUtil.SetActive(ev, cs.driller.gameObject, true);
        WireUtil.SetActive(ev, cs.connector.gameObject, true);
        WireUtil.Enabled(ev, aCrane, true); WireUtil.Enabled(ev, aCyl, true); WireUtil.Enabled(ev, aCon, true);
        WireUtil.Play(ev, aCrane, "Start"); WireUtil.Play(ev, aCyl, "Start"); WireUtil.Play(ev, aCon, "Start");
        WireUtil.Void(ev, tl, "Run");
        t0.TaskCompleted = false; t0.TriggerCompleted = false;
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq); EditorUtility.SetDirty(cs);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ TASK_CONTROL_MACHINE
    // Four tasks: the first shows the start values; each press of the highlighted AD button (Cube.016) completes the task, and the next
    // task sets the display (TMP text), the description and its voice-over. The last task waits endWait seconds and finishes.
    [MenuItem("Vedanta VR/13 Wire Control Machine events")]
    public static void RunControlMachine()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        WireControlMachine();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Control Machine events wired.");
    }

    static void WireControlMachine()
    {
        const string VoltageSuffix = "<size=70%> <color=#FFFFFF>V</color></size>";
        var seq = SequenceNamed("TASK_CONTROL_MACHINE");
        var cm = Object.FindFirstObjectByType<ControlMachineSequence>(FindObjectsInactive.Include);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var cube = cm.cube.gameObject;

        var so = new SerializedObject(seq);
        var calls = so.FindProperty("TaskList.Array.data[0].EventsToFollow.m_PersistentCalls.m_Calls");
        string title = null; Transform cam = null;
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i);
            string m = c.FindPropertyRelative("m_MethodName").stringValue;
            if (m == "SetTitle") title = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "SnapCameraToTransform") cam = c.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue as Transform;
        }

        var x = MakeSelectable(cube, false, true);
        WireUtil.Void(x.onSelect, helper, "CompleteTask");
        WireUtil.FixCallStates(x);

        int n = cm.steps.Length + 1;
        while (seq.TaskList.Count > n) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        while (seq.TaskList.Count < n) seq.TaskList.Add(new Task());
        for (int i = 0; i < n; i++)
        {
            var t = seq.TaskList[i];
            bool first = i == 0, last = i == n - 1;
            string desc = first ? cm.startDescription : cm.steps[i - 1].description;
            t.TaskName = "Pot Control Machine"; t.useTTS = false; t.ttsTextOrKey = ""; t.AutoAdvanceAfterVoiceOver = false; t.TaskCompleted = false; t.TriggerCompleted = false;
            t.instructionText = desc;
            if (t.EventsToFollow == null) t.EventsToFollow = new UnityEvent();
            var ev = t.EventsToFollow; WireUtil.Clear(ev);
            WireUtil.Str(ev, helper, "SetTitle", title);
            WireUtil.Str(ev, helper, "SetDescription", desc);
            if (first)
            {
                if (cam) WireUtil.Tr(ev, helper, "SnapCameraToTransform", cam);
                WireUtil.Text(ev, cm.voltageText, cm.startVoltage + VoltageSuffix);
                WireUtil.Text(ev, cm.beamText, cm.startBeam);
            }
            else
            {
                var s = cm.steps[i - 1];
                WireUtil.Text(ev, cm.voltageText, s.voltage + VoltageSuffix);
                WireUtil.Text(ev, cm.beamText, s.beam);
                WireUtil.Str(ev, helper, "PlayVoiceOver", s.voiceKey);
            }
            if (!last) { if (first) WireUtil.Obj(ev, helper, "HighlightObject", cube); WireUtil.Void(ev, x, "Arm"); }
            else { WireUtil.Obj(ev, helper, "RemoveHighlight", cube); WireUtil.Flt(ev, helper, "AdvanceTaskWithDelay", cm.endWait); }
        }
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq);
    }

    // ------------------------------------------------------------------ TASK_BATH_PIPE_POWDER
    // Timeline: the powder bin (Cube.006) lowers with an Animator clip; then its Animator stops, TransformPlacer puts it between Plane.020 and
    // Plane.023, and the drop point particle effect plays for 7 seconds before the task completes.
    [MenuItem("Vedanta VR/14 Wire Bath Powder timeline")]
    public static void RunBathPowderTimeline()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/Animations/BathPowder")) AssetDatabase.CreateFolder("Assets/Animations", "BathPowder");
        WireWelcome(); WireBathPowder();      // planes/holes state and the Cube.006 anchors (idempotent)
        WireBathPowderTimeline();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Bath Powder timeline wired.");
    }

    static void WireBathPowderTimeline()
    {
        var seq = SequenceNamed("TASK_BATH_PIPE_POWDER");
        var bp = Object.FindFirstObjectByType<BathPowderSequence>(FindObjectsInactive.Include);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var placer = bp.cube.GetComponent<TransformPlacer>();
        string dir = "Assets/Animations/BathPowder/";
        var startC = MakePathClip(dir + "CubeStart.anim", "CubeStart", (0f, bp.cubeStart), (0.034f, bp.cubeStart));
        var lowerC = MakePathClip(dir + "CubeLower.anim", "CubeLower", (0f, bp.cubeStart), (bp.lowerDuration, bp.cubeTarget));
        var anim = MakeAnimator(bp.cube.gameObject, dir + "Cube006.controller", ("Start", startC), ("Lower", lowerC));
        bp.cube.localPosition = bp.cubeStart;

        var tl = Ensure<TimedEvents>(bp.gameObject);
        tl.steps.Clear();
        TimedEvents.Step Add(string label, float at) { var s = new TimedEvents.Step { label = label, atSeconds = at }; tl.steps.Add(s); return s; }
        WireUtil.Play(Add("Bin lowers", 0f).onFire, anim, "Lower");
        float tl0 = bp.lowerDuration;
        WireUtil.Enabled(Add("Bin animation stops", tl0).onFire, anim, false);
        WireUtil.Void(Add("Bin between Plane.020 and Plane.023", tl0 + 0.01f).onFire, placer, "PlaceBetweenAnchors");
        WireUtil.PS(Add("Particles play", tl0 + 0.02f).onFire, bp.dropPoint, "Play");
        WireUtil.PS(Add("Particles stop", tl0 + 0.02f + bp.playSeconds).onFire, bp.dropPoint, "Stop");
        WireUtil.Flt(Add("Finish", tl0 + 0.03f + bp.playSeconds).onFire, helper, "AdvanceTaskWithDelay", 0f);
        WireUtil.FixCallStates(tl);

        var t0 = seq.TaskList[0];
        while (seq.TaskList.Count > 1) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        var so = new SerializedObject(seq);
        var calls = so.FindProperty("TaskList.Array.data[0].EventsToFollow.m_PersistentCalls.m_Calls");
        string title = null, desc = null; Transform cam = null;
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i); string m = c.FindPropertyRelative("m_MethodName").stringValue;
            if (m == "SetTitle") title = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "SetDescription") desc = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "SnapCameraToTransform") cam = c.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue as Transform;
        }
        var ev = t0.EventsToFollow; WireUtil.Clear(ev);
        WireUtil.Str(ev, helper, "SetTitle", title);
        WireUtil.Str(ev, helper, "SetDescription", desc);
        if (cam) WireUtil.Tr(ev, helper, "SnapCameraToTransform", cam);
        WireUtil.SetActive(ev, bp.connector.gameObject, false);    // the driller connector (and driller.001 below it) goes away
        WireUtil.SetActive(ev, bp.driller.gameObject, false);
        // the chain Cube.006 > piper.001 > drop point, and its parents, must be on
        var chain = new System.Collections.Generic.List<Transform>();
        for (var tr = bp.dropPoint.transform; tr != null; tr = tr.parent) { chain.Insert(0, tr); if (tr == bp.cube.parent.parent) break; }
        foreach (var tr in chain) WireUtil.SetActive(ev, tr.gameObject, true);
        WireUtil.PS(ev, bp.dropPoint, "Clear");
        WireUtil.Enabled(ev, anim, true);
        WireUtil.Play(ev, anim, "Start");
        WireUtil.Void(ev, tl, "Run");
        t0.TaskCompleted = false; t0.TriggerCompleted = false;
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq); EditorUtility.SetDirty(bp);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ TASK_THROW_FUSED_ALUMINA
    // Like the desktop scene: the Fused Alumina Bag is clicked in the Collected Tools panel (ray); it flies out of its row, turns in front of
    // the player and lands on the cement slab (ToolFlyTo). On arrival the alumina_smoke plays and MaterialBurnFade chars and dissolves the bag.
    [MenuItem("Vedanta VR/15 Wire Throw Fused Alumina events")]
    public static void RunFusedAlumina()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        WireFusedAlumina();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Throw Fused Alumina events wired.");
    }

    static Transform EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t;
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        return g.transform;
    }

    static void WireFusedAlumina()
    {
        var seq = SequenceNamed("TASK_THROW_FUSED_ALUMINA");
        var fa = Object.FindFirstObjectByType<FusedAluminaSequence>(FindObjectsInactive.Include);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var panel = Object.FindFirstObjectByType<PotLeakage.UI.CollectedToolsPanelController>(FindObjectsInactive.Include);
        var bag = fa.bag.gameObject;

        // landing pose of the bag (same point and rotation as the old throw) and the smoke point, as scene objects
        var r = fa.target.GetComponent<Renderer>();
        Vector3 land = new Vector3(fa.target.position.x, r ? r.bounds.max.y : fa.target.position.y, fa.target.position.z);
        var landPt = EnsureChild(fa.target, "FusedAlumina_LandPoint");
        landPt.SetPositionAndRotation(land, Quaternion.Euler(fa.landEuler));
        var smokePt = EnsureChild(fa.target, "FusedAlumina_SmokePoint");
        smokePt.SetPositionAndRotation(land + Vector3.up * 0.05f, Quaternion.Euler(-90f, 0f, 0f));
        fa.smoke.transform.SetParent(smokePt, false);
        fa.smoke.transform.localPosition = Vector3.zero; fa.smoke.transform.localRotation = Quaternion.identity;

        var fly = FlyOf(bag);
        fly.mode = ToolFlyTo.LandingMode.SettleOnTarget; fly.target = landPt; fly.rowKey = "Fused Alumina Bag"; fly.panel = panel;
        var burn = Ensure<MaterialBurnFade>(bag);
        burn.duration = fa.burnDuration; burn.deactivateWhenDone = true;

        WireUtil.Clear(fly.onArrived);
        WireUtil.Str(fly.onArrived, panel, "RemoveCollected", "Fused Alumina Bag");
        WireUtil.SetActive(fly.onArrived, fa.smoke.gameObject, true);
        WireUtil.PS(fly.onArrived, fa.smoke, "Play");
        WireUtil.Void(fly.onArrived, burn, "Play");
        WireUtil.Flt(fly.onArrived, helper, "AdvanceTaskWithDelay", fa.burnDuration + fa.smokeTail);
        WireUtil.FixCallStates(fly);

        foreach (var row in panel.rowEvents)
            if (row.keyContains == "Fused Alumina Bag") { WireUtil.Clear(row.onClicked); WireUtil.Void(row.onClicked, fly, "Play"); }

        var t0 = seq.TaskList[0];
        while (seq.TaskList.Count > 1) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        var so = new SerializedObject(seq);
        var calls = so.FindProperty("TaskList.Array.data[0].EventsToFollow.m_PersistentCalls.m_Calls");
        string title = null, desc = null; Transform cam = null;
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i); string m = c.FindPropertyRelative("m_MethodName").stringValue;
            if (m == "SetTitle") title = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "SetDescription") desc = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "SnapCameraToTransform") cam = c.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue as Transform;
        }
        var ev = t0.EventsToFollow; WireUtil.Clear(ev);
        WireUtil.Str(ev, helper, "SetTitle", title);
        WireUtil.Str(ev, helper, "SetDescription", desc);
        if (cam) WireUtil.Tr(ev, helper, "SnapCameraToTransform", cam);
        WireUtil.SetActive(ev, bag, false);
        WireUtil.PS(ev, fa.smoke, "Clear");
        WireUtil.Str(ev, panel, "AddCollected", "Fused Alumina Bag");
        WireUtil.Void(ev, panel, "ShowPanel");
        WireUtil.Str(ev, panel, "ArmRow", "Fused Alumina Bag");
        t0.TaskCompleted = false; t0.TriggerCompleted = false; t0.instructionText = desc;
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq); EditorUtility.SetDirty(bag); EditorUtility.SetDirty(panel);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ TASK_LEAKAGE_STOP
    // Informational task: 8 s on the SST view, then the gas particles are stopped and the task completes (a TimedEvents timeline). The gas
    // lifecycle that GasEffectRange used to drive from task names is now events: start in Air Unlock, stop here.
    [MenuItem("Vedanta VR/16 Wire Leakage Stop events")]
    public static void RunLeakageStop()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        var seq = SequenceNamed("TASK_LEAKAGE_STOP");
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var aq = Object.FindFirstObjectByType<AirUnlockSequence>(FindObjectsInactive.Include);
        var sst = FindDeep("CameraSystem/TransformPoints/SST");

        var t0 = seq.TaskList[0];
        while (seq.TaskList.Count > 1) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        var so = new SerializedObject(seq);
        var calls = so.FindProperty("TaskList.Array.data[0].EventsToFollow.m_PersistentCalls.m_Calls");
        string title = null, desc = null; float wait = 8f;
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i); string m = c.FindPropertyRelative("m_MethodName").stringValue;
            if (m == "SetTitle") title = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "SetDescription") desc = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "CompleteAfterSeconds") wait = c.FindPropertyRelative("m_Arguments.m_FloatArgument").floatValue;
            if (m == "AdvanceTaskWithDelay" && wait == 8f) wait = 8f;
        }
        var tl = Ensure<TimedEvents>(seq.gameObject);
        tl.steps.Clear();
        var s1 = new TimedEvents.Step { label = "Gas off, task done", atSeconds = wait }; tl.steps.Add(s1);
        WireUtil.Obj(s1.onFire, helper, "StopParticlesOf", aq.sstPipe1);
        WireUtil.Flt(s1.onFire, helper, "AdvanceTaskWithDelay", 0f);
        WireUtil.FixCallStates(tl);

        var ev = t0.EventsToFollow; WireUtil.Clear(ev);
        WireUtil.Str(ev, helper, "SetTitle", title);
        WireUtil.Str(ev, helper, "SetDescription", desc);
        if (sst) WireUtil.Tr(ev, helper, "SnapCameraToTransform", sst);
        WireUtil.Void(ev, tl, "Run");
        t0.TaskCompleted = false; t0.TriggerCompleted = false; t0.instructionText = desc;
        WireUtil.FixCallStates(seq);

        var gas = Object.FindFirstObjectByType<GasEffectRange>(FindObjectsInactive.Include);
        if (gas) { gas.enabled = false; EditorUtility.SetDirty(gas); }
        EditorUtility.SetDirty(seq);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Leakage Stop events wired (GasEffectRange disabled).");
    }

    // ------------------------------------------------------------------ CLOSE_HOOD
    // Two tasks: right hood (Plane.023) then left hood (Plane.020). Each plane's Animator already has the Close clip (menu 11). A child
    // object "CloseSelect" carries the select events so the slide reaction on the plane itself is left alone.
    [MenuItem("Vedanta VR/17 Wire Close Hood events")]
    public static void RunCloseHood()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Open PotLeakage-(VR)_Events.unity first."); return; }
        RunHoodSlides();                       // rebuilds the plane Animators with the CloseStart state
        WireCloseHood();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: Close Hood events wired.");
    }

    static XRInteractionEvents MakeCloseSelect(GameObject plane)
    {
        var ch = plane.transform.Find("CloseSelect");
        GameObject go = ch ? ch.gameObject : new GameObject("CloseSelect");
        go.transform.SetParent(plane.transform, false);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        var src = plane.GetComponent<BoxCollider>();
        var box = Ensure<BoxCollider>(go);
        box.center = src.center; box.size = src.size;
        var x = MakeSelectable(go, false, true);
        var xi = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        xi.colliders.Clear(); xi.colliders.Add(box);          // each interactable owns exactly its own collider (the parent would otherwise also collect this child collider)
        var planeXi = plane.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (planeXi) { planeXi.colliders.Clear(); planeXi.colliders.Add(src); EditorUtility.SetDirty(planeXi); }
        xi.enabled = false;   // switched on by the close task
        EditorUtility.SetDirty(xi);
        return x;
    }

    static void WireCloseHood()
    {
        var seq = SequenceNamed("CLOSE_HOOD");
        var ch = Object.FindFirstObjectByType<CloseHoodSequence>(FindObjectsInactive.Include);
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var sst = FindDeep("CameraSystem/TransformPoints/SST");

        var so = new SerializedObject(seq);
        var calls = so.FindProperty("TaskList.Array.data[0].EventsToFollow.m_PersistentCalls.m_Calls");
        string title = null, desc = null;
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i); string m = c.FindPropertyRelative("m_MethodName").stringValue;
            if (m == "SetTitle") title = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (m == "SetDescription") desc = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
        }

        var hoods = new[] { ch.rightHood, ch.leftHood };
        while (seq.TaskList.Count > 2) seq.TaskList.RemoveAt(seq.TaskList.Count - 1);
        while (seq.TaskList.Count < 2) seq.TaskList.Add(new Task());
        for (int i = 0; i < 2; i++)
        {
            var hood = hoods[i];
            var plane = hood.plane.gameObject;
            var anim = plane.GetComponent<Animator>();
            var x = MakeCloseSelect(plane);
            WireUtil.Obj(x.onSelect, helper, "RemoveHighlight", plane);
            WireUtil.Play(x.onSelect, anim, "Close");
            WireUtil.Flt(x.onSelect, helper, "AdvanceTaskWithDelay", hood.phaseADuration + hood.phaseBDuration + hood.finalWait);
            WireUtil.FixCallStates(x);

            var t = seq.TaskList[i];
            string d = i == 0 ? desc : ch.secondDescription;
            t.TaskName = "Close Hood"; t.useTTS = false; t.ttsTextOrKey = ""; t.AutoAdvanceAfterVoiceOver = false; t.TaskCompleted = false; t.TriggerCompleted = false;
            t.instructionText = d;
            if (t.EventsToFollow == null) t.EventsToFollow = new UnityEvent();
            var ev = t.EventsToFollow; WireUtil.Clear(ev);
            WireUtil.Str(ev, helper, "SetTitle", title);
            WireUtil.Str(ev, helper, "SetDescription", d);
            if (i == 0 && sst) WireUtil.Tr(ev, helper, "SnapCameraToTransform", sst);
            if (i == 1) WireUtil.Str(ev, helper, "PlayVoiceOver", ch.secondVoiceKey);
            WireUtil.SetActive(ev, plane, true);
            WireUtil.Enabled(ev, anim, true);
            WireUtil.Play(ev, anim, "CloseStart");
            // the slide reaction on the plane gives way to the close reaction on its child (their colliders coincide)
            var slideInteractable = plane.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (slideInteractable) WireUtil.Enabled(ev, slideInteractable, false);
            WireUtil.Enabled(ev, x.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(), true);
            WireUtil.Obj(ev, helper, "HighlightObject", plane);
            WireUtil.Void(ev, x, "Arm");
        }
        WireUtil.FixCallStates(seq);
        EditorUtility.SetDirty(seq);
        AssetDatabase.SaveAssets();
    }

    static void WireWelcome()
    {
        var seq = SequenceNamed("TASK_01_WELCOME");
        var potLine = FindDeep("POTLINE/PotLine");
        var helperT = FindDeep("Sequence Handler") ?? FindDeep("Sequence/Sequence Handler");
        var helper = helperT ? helperT.GetComponent<SequenceHelperFunctions>() : Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var p20 = potLine.Find("Plane.020").gameObject; var p23 = potLine.Find("Plane.023").gameObject;
        var holes = new[] { potLine.Find("cement/HOLE_1").gameObject, potLine.Find("cement/HOLE_2").gameObject, potLine.Find("cement/HOLE_3").gameObject };

        var ev = seq.TaskList[0].EventsToFollow;
        RemoveCallsTo(ev, p20, p23, holes[0], holes[1], holes[2]);
        // order: the planes are shown and the holes hidden before the welcome panel and camera are set up
        var all = new System.Collections.Generic.List<(GameObject, bool)> { (p20, true), (p23, true), (holes[0], false), (holes[1], false), (holes[2], false) };
        foreach (var (go, on) in all) WireUtil.SetActive(ev, go, on);
        WireUtil.FixCallStates(seq);
        // also apply the state right now so the scene starts in it
        p20.SetActive(true); p23.SetActive(true); foreach (var h in holes) h.SetActive(false);
        EditorUtility.SetDirty(seq);
    }

    static void WireBathPowder()
    {
        var bp = Object.FindFirstObjectByType<BathPowderSequence>(FindObjectsInactive.Include);
        var potLine = FindDeep("POTLINE/PotLine");
        var cube = bp.cube;
        var placer = Ensure<TransformPlacer>(cube.gameObject);
        placer.anchorA = potLine.Find("Plane.020");
        placer.anchorB = potLine.Find("Plane.023");
        EditorUtility.SetDirty(placer);

        WireUtil.Clear(bp.beforeParticle);
        WireUtil.Void(bp.beforeParticle, placer, "PlaceBetweenAnchors");
        WireUtil.FixCallStates(bp);
        EditorUtility.SetDirty(bp);
    }
}
