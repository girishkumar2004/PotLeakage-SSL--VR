using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class VRSceneBuilder
{
    const string Prefabs = "Assets/Prefabs/";

    // Root transforms of the prefab instances in PotLeakage_Main.unity (position, rotation x,y,z,w).
    struct Placement
    {
        public string prefab; public Vector3 pos; public Quaternion rot; public bool active;
        public Placement(string p, Vector3 pos, Quaternion rot, bool active = true) { prefab = p; this.pos = pos; this.rot = rot; this.active = active; }
    }

    static readonly Placement[] Placements =
    {
        new Placement("machine",                      new Vector3(-17.321f, 0f, 40.062f),    new Quaternion(0f, 0.0076f, 0f, 1f)),
        new Placement("crane_roller",                 new Vector3(0f, 0f, 17.16f),           Quaternion.identity),
        new Placement("blue pipe with ball valve ",   new Vector3(-99.1811f, 0f, 17.2665f),  new Quaternion(0f, 0.738f, 0f, 0.6749f)),
        new Placement("SST_PIPE_ani",                 new Vector3(-4.3862f, 1.812f, 38.9558f), new Quaternion(-0.5f, -0.5f, -0.5f, 0.5f)),
        new Placement("ToolBox",                      new Vector3(15.09f, 0.506f, 29.95f),   new Quaternion(0.7071f, 0.7071f, -0.0037f, -0.0037f)),
        new Placement("walkietalkie",                 new Vector3(-2.445f, 2.238f, 39.676f), new Quaternion(0.9917f, -0.0015f, 0.0308f, 0.1245f), false),
    };

    [MenuItem("Vedanta VR/1 Configure Quest 3S Android Settings")]
    public static void ConfigureAndroid()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.Build.NamedBuildTarget.Android, BuildTarget.Android);
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        Debug.Log("Android settings applied. Enable OpenXR + Meta Quest Support in Project Settings > XR Plug-in Management (Android tab).");
    }

    [MenuItem("Vedanta VR/2 Build XR Architecture")]
    public static void Build()
    {
        CleanPrevious();

        // ---- XR rig (created by XRI, then renamed to the required architecture)
        EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
        var origin = Object.FindFirstObjectByType<XROrigin>();
        if (!origin) { Debug.LogError("XR Origin creation failed - check the XRI package imported."); return; }
        origin.name = "XR Origin";
        var mgr = Object.FindFirstObjectByType<XRInteractionManager>();
        if (mgr) mgr.name = "XR Interaction Manager";
        origin.CameraFloorOffsetObject.name = "Camera Floor Offset";
        origin.Camera.name = "Main Camera";
        origin.transform.SetAsLastSibling();

        Transform left = null, right = null;
        foreach (var t in origin.GetComponentsInChildren<Transform>(true))
        {
            if (!left && t.name.StartsWith("Left Controller")) left = t;
            if (!right && t.name.StartsWith("Right Controller")) right = t;
        }
        if (left) { left.name = "Left Controller"; Tidy(left); }
        if (right) { right.name = "Right Controller"; Tidy(right); }

        // ---- VR Interaction UI (event system for world-space canvases)
        var ui = new GameObject("VR Interaction UI");
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
        es.transform.SetParent(ui.transform);

        // ---- Environment: prefabs with the same root transforms as the desktop scene
        var env = new GameObject("PotLeakage Environment");
        var spawned = new Dictionary<string, GameObject>();
        foreach (var p in Placements)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + p.prefab + ".prefab");
            if (!prefab) { Debug.LogWarning("Missing prefab " + p.prefab); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, env.transform);
            inst.transform.SetPositionAndRotation(p.pos, p.rot);
            inst.SetActive(p.active);
            spawned[p.prefab] = inst;
        }
        // Hood panels (POTLINE/PotLine/Plane.023 & Plane.020): placeholders at the original local transforms until the real model is imported.
        var potline = new GameObject("POTLINE"); potline.transform.SetParent(env.transform);
        potline.transform.position = new Vector3(-7.52f, 0f, -7.71f);
        var potLine = new GameObject("PotLine"); potLine.transform.SetParent(potline.transform, false);
        potLine.transform.localPosition = new Vector3(7.604f, 0f, -62.11f);
        spawned["Plane.023"] = Hood(potLine.transform, "Plane.023", new Vector3(-3.715f, 1.299f, 107.446f));
        spawned["Plane.020"] = Hood(potLine.transform, "Plane.020", new Vector3(-2.89f, 1.299f, 107.446f));
        Floor(env.transform);

        // ---- CameraSystem/TransformPoints (same names + transforms as the desktop scene)
        var camSys = new GameObject("CameraSystem");
        var points = new GameObject("TransformPoints"); points.transform.SetParent(camSys.transform);
        var pointByName = new Dictionary<string, Transform>();
        foreach (var d in VRSceneData.Points)
        {
            var g = new GameObject(d.name); g.transform.SetParent(points.transform, false);
            g.transform.localPosition = d.pos; g.transform.localRotation = d.rot; g.transform.localScale = d.scale;
            g.SetActive(d.active);
            pointByName[d.name] = g.transform;
        }

        // ---- Sequence / Sequence Handler / ListOfSequence
        var seqRoot = new GameObject("Sequence");
        var handlerGo = new GameObject("Sequence Handler"); handlerGo.transform.SetParent(seqRoot.transform);
        var listGo = new GameObject("ListOfSequence"); listGo.transform.SetParent(seqRoot.transform);

        var handler = handlerGo.AddComponent<SequenceHandler>();
        var helper = handlerGo.AddComponent<SequenceHelperFunctions>();
        handler.helper = helper; handler.listOfSequence = listGo.transform;
        helper.handler = handler; helper.xrOrigin = origin; helper.transformPoints = points.transform;
        if (left) { var panel = BuildPanel(left); helper.panel = panel; }

        foreach (var def in VRSceneData.Tasks)
        {
            var go = new GameObject(def.objectName); go.transform.SetParent(listGo.transform, false);
            var seq = go.AddComponent<Sequence>();
            seq.SequenceName = def.sequenceName;
            var task = new Task { TaskName = def.taskName, instructionText = def.instruction, typeOfInteraction = Task.TypeOfInteraction.None };
            seq.TaskList.Add(task);
            handler.sequenceList.Add(seq);
            WireTask(def, task.EventsToFollow, helper, pointByName, spawned);
            EditorUtility.SetDirty(seq);
        }

        EditorUtility.SetDirty(handler); EditorUtility.SetDirty(helper);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Vedanta VR: architecture built - " + VRSceneData.Tasks.Length + " tasks wired.");
    }

    // ------------------------------------------------------------------ task wiring
    // Event order matches the desktop tasks: SetTitle, SetDescription, SnapCamera..., SetActive, Highlight, then a completion trigger.
    static void WireTask(VRSceneData.TaskDef def, UnityEvent e, SequenceHelperFunctions h,
        Dictionary<string, Transform> points, Dictionary<string, GameObject> obj)
    {
        UnityEventTools.AddStringPersistentListener(e, h.SetTitle, def.title);
        UnityEventTools.AddStringPersistentListener(e, h.SetDescription, def.description);
        if (def.snapKind == "T" && points.TryGetValue(def.snapValue, out var pt))
            UnityEventTools.AddObjectPersistentListener<Transform>(e, h.SnapCameraToTransform, pt);
        else if (def.snapKind == "N")
            UnityEventTools.AddStringPersistentListener(e, h.SnapCameraToNamedPoint, def.snapValue);

        GameObject O(string key) { obj.TryGetValue(key, out var g); return g; }
        void Active(string key, bool on) { var g = O(key); if (g) UnityEventTools.AddBoolPersistentListener(e, g.SetActive, on); }
        void Highlight(string key) { var g = O(key); if (g) UnityEventTools.AddObjectPersistentListener<GameObject>(e, h.HighlightObjectGreen, g); }
        void OnGrab(string key) { var g = O(key); if (g) UnityEventTools.AddObjectPersistentListener<GameObject>(e, h.CompleteOnGrab, g); }
        void OnSelect(string key) { var g = O(key); if (g) UnityEventTools.AddObjectPersistentListener<GameObject>(e, h.CompleteOnSelect, g); }
        void After(float s) { UnityEventTools.AddFloatPersistentListener(e, h.CompleteAfterSeconds, s); }

        switch (def.objectName)
        {
            case "TASK_01_WELCOME":                Active("SST_PIPE_ani", false); After(8f); break;
            case "TASK_02_NORMAL_POT_OPERATION":   Active("SST_PIPE_ani", false); Highlight("machine"); After(8f); break;
            case "TASK_03_LEAKAGE_OCCURRED":       Active("SST_PIPE_ani", false); After(8f); break;
            case "TASK_06_EMERGENCY_COMMUNICATION": Active("walkietalkie", true); Highlight("walkietalkie"); OnGrab("walkietalkie"); break;
            case "TASK_TOOL_SELECTION":            Highlight("ToolBox"); OnSelect("ToolBox"); break;
            case "TASK_4_SST_CONNECTION":          Active("SST_PIPE_ani", true); Highlight("SST_PIPE_ani"); OnGrab("SST_PIPE_ani"); break;
            case "TASK_5_AIR_Unlock":              Highlight("blue pipe with ball valve "); OnSelect("blue pipe with ball valve "); break;
            case "TASK_6_SLIDE_RIGHT_HOOD":        Active("Plane.023", true); Highlight("Plane.023"); OnGrab("Plane.023"); break;
            case "TASK_05_SLIDE_LEFT_HOOD":        Active("Plane.020", true); Highlight("Plane.020"); OnGrab("Plane.020"); break;
            case "TASK_CRUST_BATHING":             Highlight("crane_roller"); OnSelect("crane_roller"); break;
            case "TASK_CONTROL_MACHINE":           Highlight("machine"); OnSelect("machine"); break;
            case "TASK_BATH_PIPE_POWDER":          After(8f); break;
            case "TASK_THROW_FUSED_ALUMINA":       After(8f); break;
            case "TASK_LEAKAGE_STOP":              After(8f); break;
            case "CLOSE_HOOD":                     Highlight("Plane.023"); OnGrab("Plane.023"); break;
            case "TASK_TRAINING_COMPLETED":        break;
        }

        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            e.SetPersistentListenerState(i, UnityEventCallState.EditorAndRuntime);
    }

    // ------------------------------------------------------------------ helpers
    static void CleanPrevious()
    {
        foreach (var n in new[] { "XR Origin", "XR Interaction Manager", "VR Interaction UI", "Sequence", "PotLeakage Environment", "CameraSystem", "POTLINE", "EventSystem" })
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go && go.transform.parent == null && go.name == n) Object.DestroyImmediate(go);
        var oldCam = Camera.main; if (oldCam) Object.DestroyImmediate(oldCam.gameObject);
    }

    static GameObject Hood(Transform parent, string name, Vector3 localPos)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = new Quaternion(-0.7071f, 0f, 0f, 0.7071f);
        g.transform.localScale = Vector3.one * 0.08f;
        g.SetActive(false);
        return g;
    }

    static void Floor(Transform parent)
    {
        var f = GameObject.CreatePrimitive(PrimitiveType.Plane);
        f.name = "Floor (placeholder)"; f.transform.SetParent(parent);
        f.transform.position = new Vector3(0f, -0.01f, 30f); f.transform.localScale = new Vector3(20f, 1f, 20f);
    }

    static void Tidy(Transform ctrl)
    {
        var ray = ctrl.GetComponentInChildren<XRRayInteractor>(true);
        if (ray) ray.gameObject.name = "Ray Interactor";
        if (!ctrl.Find("Controller Model"))
            foreach (Transform c in ctrl)
                if (c.name.Contains("Model")) { c.name = "Controller Model"; break; }
        if (!ctrl.Find("Controller Model")) new GameObject("Controller Model").transform.SetParent(ctrl, false);
    }

    // Card proportions match the reference: 640x360 px canvas at 0.0005 scale = 0.32 x 0.18 m.
    static InstructionPanel BuildPanel(Transform controller)
    {
        var go = new GameObject("Instruction Panel", typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), typeof(InstructionPanel));
        go.transform.SetParent(controller, false);
        go.transform.localPosition = new Vector3(0f, 0.08f, 0.12f);
        go.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.0005f;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(640, 360);

        Img(go.transform, "Background", new Color32(18, 24, 48, 255), new Vector2(640, 360), Vector2.zero);
        Img(go.transform, "Green Accent", new Color32(60, 190, 80, 255), new Vector2(640, 5), new Vector2(0, 177.5f));
        var logo = Img(go.transform, "Logo", Color.white, new Vector2(180, 56), new Vector2(0, 135));
        logo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Vedanta-Logo.png");
        logo.preserveAspect = true;
        Txt(go.transform, "Subtitle", "VEDANTA ALUMINIUM — POTLINE OPERATIONS", 16, new Color32(110, 200, 120, 255), new Vector2(620, 24), new Vector2(0, 92));
        var title = Txt(go.transform, "Title", "", 28, Color.white, new Vector2(620, 40), new Vector2(0, 58));
        Img(go.transform, "Body Box", new Color32(32, 52, 118, 255), new Vector2(580, 190), new Vector2(0, -60));
        var body = Txt(go.transform, "Body", "", 24, Color.white, new Vector2(540, 170), new Vector2(0, -60));

        var p = go.GetComponent<InstructionPanel>();
        p.titleText = title; p.bodyText = body;
        return p;
    }

    static Image Img(Transform parent, string n, Color c, Vector2 size, Vector2 pos)
    {
        var g = new GameObject(n, typeof(Image));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.sizeDelta = size; r.anchoredPosition = pos;
        var i = g.GetComponent<Image>(); i.color = c;
        return i;
    }

    static Text Txt(Transform parent, string n, string s, int size, Color c, Vector2 sz, Vector2 pos)
    {
        var g = new GameObject(n, typeof(Text));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.sizeDelta = sz; r.anchoredPosition = pos;
        var t = g.GetComponent<Text>();
        t.text = s; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }
}
