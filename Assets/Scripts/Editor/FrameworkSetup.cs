using PotLeakage.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>One-time setup of the event-driven framework in the working copy scene (PotLeakage-(VR)_Events.unity): comfort-fade quad on the
/// camera, world-space Collected Tools panel, haptics switch (off), Resources materials, and the Sequence.autoAdvanceAfterVoice flags.
/// It refuses to run on the original scene.</summary>
public static class FrameworkSetup
{
    const string FadeMat = "Assets/Resources/VRFadeQuad.mat";
    const string XRayMat = "Assets/Resources/VRXRayHighlight.mat";

    [MenuItem("Vedanta VR/6 Framework Setup (Events scene)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity"))
        {
            Debug.LogError("Framework setup only runs on the working copy PotLeakage-(VR)_Events.unity (active: " + scene.path + ").");
            return;
        }
        EnsureMaterials();
        SetupFader();
        SetupCollectedToolsPanel();
        SetupHaptics();
        SetupSequenceFlags();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: framework set up in " + scene.path);
    }

    static void EnsureMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.LoadAssetAtPath<Material>(FadeMat))
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            AssetDatabase.CreateAsset(m, FadeMat);
        }
        if (!AssetDatabase.LoadAssetAtPath<Material>(XRayMat))
        {
            var sh = Shader.Find("Vedanta/PotLeakage_XRayHighlight");
            if (sh) { var m = new Material(sh); AssetDatabase.CreateAsset(m, XRayMat); }
            else Debug.LogWarning("XRay shader not found; VRXRayHighlight.mat not created.");
        }
        AssetDatabase.SaveAssets();
    }

    static void SetupFader()
    {
        var cam = Camera.main;
        if (!cam) { Debug.LogWarning("No main camera for the fader."); return; }
        if (!cam.GetComponent<CameraFader>()) cam.gameObject.AddComponent<CameraFader>();
        EditorUtility.SetDirty(cam.gameObject);
    }

    static void SetupHaptics()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<HapticsSettings>()) if (t && t.gameObject.scene.isLoaded) return;
        new GameObject("HapticsSettings", typeof(HapticsSettings));
    }

    static void SetupSequenceFlags()
    {
        var informational = new[] { "TASK_01_WELCOME", "TASK_02_NORMAL_POT_OPERATION", "TASK_03_LEAKAGE_OCCURRED" };
        foreach (var s in Object.FindObjectsByType<Sequence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            s.autoAdvanceAfterVoice = System.Array.IndexOf(informational, s.name) >= 0;
            foreach (var t in s.TaskList) t.AutoAdvanceAfterVoiceOver = false;   // the flag lives on the Sequence now
            EditorUtility.SetDirty(s);
        }
    }

    // ------------------------------------------------------------------ collected tools panel
    static void SetupCollectedToolsPanel()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "CollectedToolsCanvas" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = new GameObject("CollectedToolsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster), typeof(CollectedToolsPanelController));
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(420f, 400f);
        rt.localScale = Vector3.one * 0.0012f;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 6f;
        var ctl = root.GetComponent<CollectedToolsPanelController>();

        var bg = Child(root.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bgImg = bg.gameObject.AddComponent<Image>(); bgImg.sprite = uiSprite; bgImg.type = Image.Type.Sliced; bgImg.color = new Color(0.06f, 0.1f, 0.2f, 0.96f);
        var bar = Child(bg, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), Vector2.zero);
        bar.gameObject.AddComponent<Image>().color = new Color(0.24f, 0.78f, 0.29f, 1f);
        bar.GetComponent<Image>().raycastTarget = false;

        ctl.headingText = Label(bg, "Heading", "COLLECTED TOOLS", font, 26f, new Vector2(0, 1), new Vector2(0.75f, 1), new Vector2(20, -60), new Vector2(0, -14), TextAlignmentOptions.MidlineLeft);
        ctl.countText = Label(bg, "CountBadge", "0/3", font, 26f, new Vector2(0.75f, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(-20, -14), TextAlignmentOptions.MidlineRight);

        var list = Child(bg, "List", Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -72));
        var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f; vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        ctl.listContainer = list;
        ctl.panelRoot = bg.gameObject;

        ctl.totalTools = 3;
        ctl.toolDefinitions.Add(new CollectedToolsPanelController.ToolDefinition { key = "SST_PIPE_ani", displayName = "SST Cooling Connector" });
        ctl.toolDefinitions.Add(new CollectedToolsPanelController.ToolDefinition { key = "round_PIPES", displayName = "Hose Pipe" });
        ctl.toolDefinitions.Add(new CollectedToolsPanelController.ToolDefinition { key = "Fused Alumina Bag", displayName = "Fused Alumina Bag 10 KG" });
        ctl.rowEvents.Add(new CollectedToolsPanelController.RowEvent { keyContains = "SST_PIPE_ani" });
        ctl.rowEvents.Add(new CollectedToolsPanelController.RowEvent { keyContains = "round_PIPES" });
        ctl.rowEvents.Add(new CollectedToolsPanelController.RowEvent { keyContains = "Fused Alumina Bag" });

        root.SetActive(false);
        EditorUtility.SetDirty(root);
    }

    static RectTransform Child(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
        return r;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, TextAlignmentOptions align)
    {
        var r = Child(parent, name, aMin, aMax, oMin, oMax);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = Color.white; t.alignment = align; t.raycastTarget = false;
        return t;
    }
}
