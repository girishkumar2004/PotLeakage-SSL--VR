#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

/// <summary>Editor menu: builds the Welcome / Instruction Panel prefab and a reusable grab-drop snap-zone prefab.
/// Open menu "Tools > PotLeakage VR > ..." in Unity to generate them.</summary>
public static class WelcomePanelBuilder
{
    const string PrefabFolder = "Assets/Prefabs";
    const string WelcomePath = PrefabFolder + "/WelcomePanel.prefab";
    const string SnapZonePath = PrefabFolder + "/DropSnapZone.prefab";
    const string ControlsGuidePath = PrefabFolder + "/ControlsGuidePanel.prefab";
    const string XRRigPath = "Assets/Samples/XR Interaction Toolkit/3.1.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string LogoPath = "Assets/Images/Final Logo.png";
    const string DeviceSimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.1.2/XR Device Simulator/XR Device Simulator.prefab";

    // Palette (image 4)
    static readonly Color BrandGreen  = new Color(0.55f, 0.78f, 0.22f, 1f); // vedanta green
    static readonly Color BrandBlue   = new Color(0.13f, 0.58f, 0.88f, 1f);
    static readonly Color PanelBg     = new Color(0.08f, 0.11f, 0.16f, 0.88f);
    static readonly Color AccentYellow= new Color(1f, 0.84f, 0.00f, 1f);
    static readonly Color SoftWhite   = new Color(0.92f, 0.95f, 0.98f, 1f);
    static readonly Color MutedBlue   = new Color(0.35f, 0.60f, 0.85f, 0.8f);

    [MenuItem("Tools/PotLeakage VR/Build Welcome Panel Prefab")]
    public static void BuildWelcomePanel()
    {
        if (!Directory.Exists(PrefabFolder)) Directory.CreateDirectory(PrefabFolder);

        // Root — world-space canvas, 720x380 like the mock.
        var root = new GameObject("WelcomePanel");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(720, 380);
        root.transform.localScale = Vector3.one * 0.0012f;

        // Dark semi-transparent background
        var bg = AddUI(root.transform, "Background");
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = PanelBg;
        Stretch(bg);

        // Top green accent bar
        var topBar = AddUI(root.transform, "TopBar");
        var topBarImg = topBar.AddComponent<Image>();
        topBarImg.color = BrandGreen;
        var tbRT = topBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.pivot = new Vector2(0.5f, 1); tbRT.anchoredPosition = Vector2.zero;
        tbRT.sizeDelta = new Vector2(0, 6);

        // Logo (top-left)
        var logoGO = AddUI(root.transform, "Logo");
        var logo = logoGO.AddComponent<Image>();
        var logoSprite = LoadSprite(LogoPath);
        if (logoSprite) { logo.sprite = logoSprite; logo.preserveAspect = true; } else logo.color = BrandGreen;
        var lRT = logoGO.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0, 1); lRT.anchorMax = new Vector2(0, 1);
        lRT.pivot = new Vector2(0, 1); lRT.anchoredPosition = new Vector2(24, -22);
        lRT.sizeDelta = new Vector2(170, 44);

        // Step label (top-right)
        var step = AddText(root.transform, "StepText", "STEP 01 / 28", 22, TextAnchor.MiddleRight, FontStyle.Bold, SoftWhite);
        var sRT = step.rectTransform;
        sRT.anchorMin = new Vector2(1, 1); sRT.anchorMax = new Vector2(1, 1);
        sRT.pivot = new Vector2(1, 1); sRT.anchoredPosition = new Vector2(-26, -24);
        sRT.sizeDelta = new Vector2(260, 36);

        // Horizontal blue separator
        var sep = AddUI(root.transform, "Separator");
        var sepImg = sep.AddComponent<Image>();
        sepImg.color = MutedBlue;
        var sepRT = sep.GetComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0, 1); sepRT.anchorMax = new Vector2(1, 1);
        sepRT.pivot = new Vector2(0.5f, 1); sepRT.anchoredPosition = new Vector2(0, -78);
        sepRT.sizeDelta = new Vector2(-48, 1);

        // Title ("Welcome")
        var title = AddText(root.transform, "TitleText", "Welcome", 32, TextAnchor.UpperLeft, FontStyle.Bold, Color.white);
        var tRT = title.rectTransform;
        tRT.anchorMin = new Vector2(0, 1); tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot = new Vector2(0, 1); tRT.anchoredPosition = new Vector2(24, -94);
        tRT.sizeDelta = new Vector2(-48, 48);

        // Body text (driven by SetDescription)
        var body = AddText(root.transform, "BodyText",
            "Welcome to the Vedanta Pot Leakage Training Visualization. This module covers the Emergency Response Procedure for Side Shell Leakage at Upper Location.",
            18, TextAnchor.UpperLeft, FontStyle.Normal, SoftWhite);
        var bRT = body.rectTransform;
        bRT.anchorMin = new Vector2(0, 0); bRT.anchorMax = new Vector2(1, 1);
        bRT.pivot = new Vector2(0.5f, 0.5f); bRT.offsetMin = new Vector2(24, 90); bRT.offsetMax = new Vector2(-48, -144);

        // Progress text (hidden until used)
        var prog = AddText(root.transform, "ProgressText", "", 14, TextAnchor.MiddleLeft, FontStyle.Italic, BrandGreen);
        var pRT = prog.rectTransform;
        pRT.anchorMin = new Vector2(0, 0); pRT.anchorMax = new Vector2(0.5f, 0);
        pRT.pivot = new Vector2(0, 0); pRT.anchoredPosition = new Vector2(24, 24);
        pRT.sizeDelta = new Vector2(0, 28);

        // Yellow NEXT button (bottom-right)
        var btnGO = AddUI(root.transform, "NextButton");
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = AccentYellow;
        var btn = btnGO.AddComponent<Button>();
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1, 0); btnRT.anchorMax = new Vector2(1, 0);
        btnRT.pivot = new Vector2(1, 0); btnRT.anchoredPosition = new Vector2(-24, 20);
        btnRT.sizeDelta = new Vector2(170, 54);
        var btnLabel = AddText(btnGO.transform, "Label", "NEXT", 24, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.08f, 0.1f, 0.12f));
        Stretch(btnLabel.gameObject);

        // Optional brand / subheader fields (hidden by default — populated via SetBrand)
        var brand = AddText(root.transform, "BrandText", "", 0, TextAnchor.MiddleCenter, FontStyle.Normal, Color.clear);
        brand.gameObject.SetActive(false);
        var subH = AddText(root.transform, "SubHeaderText", "", 0, TextAnchor.MiddleCenter, FontStyle.Normal, Color.clear);
        subH.gameObject.SetActive(false);

        var ip = root.AddComponent<InstructionPanel>();
        ip.logoImage = logo;
        ip.stepText = step;
        ip.brandText = brand;
        ip.subHeaderText = subH;
        ip.titleText = title;
        ip.bodyText = body;
        ip.progressText = prog;
        ip.nextButton = btn;
        ip.nextButtonLabel = btnLabel;
        ip.totalSteps = 28;

        PrefabUtility.SaveAsPrefabAsset(root, WelcomePath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"Welcome Panel saved to {WelcomePath}.");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(WelcomePath));
    }

    static Sprite LoadSprite(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (tex) return tex;
        // Re-import as Sprite if currently imported as Texture (default)
        var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
        if (importer == null) return null;
        importer.textureType = UnityEditor.TextureImporterType.Sprite;
        importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("Tools/PotLeakage VR/Build Controls Guide Panel Prefab")]
    public static void BuildControlsGuide()
    {
        if (!Directory.Exists(PrefabFolder)) Directory.CreateDirectory(PrefabFolder);

        var root = new GameObject("ControlsGuidePanel");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(820, 540);
        root.transform.localScale = Vector3.one * 0.0018f;

        var bg = AddUI(root.transform, "Background");
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = PanelBg;
        Stretch(bg);

        // Top green bar
        var top = AddUI(root.transform, "TopBar");
        top.AddComponent<Image>().color = BrandGreen;
        var topRT = top.GetComponent<RectTransform>();
        topRT.anchorMin = new Vector2(0,1); topRT.anchorMax = new Vector2(1,1);
        topRT.pivot = new Vector2(0.5f,1); topRT.sizeDelta = new Vector2(0, 6); topRT.anchoredPosition = Vector2.zero;

        // Logo + title row
        var logoGO = AddUI(root.transform, "Logo");
        var logoImg = logoGO.AddComponent<Image>();
        var logoSprite = LoadSprite(LogoPath);
        if (logoSprite) { logoImg.sprite = logoSprite; logoImg.preserveAspect = true; } else logoImg.color = BrandGreen;
        var lRT = logoGO.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0,1); lRT.anchorMax = new Vector2(0,1); lRT.pivot = new Vector2(0,1);
        lRT.anchoredPosition = new Vector2(28, -24); lRT.sizeDelta = new Vector2(180, 48);

        var hdr = AddText(root.transform, "Header", "CONTROLS GUIDE", 26, TextAnchor.MiddleRight, FontStyle.Bold, SoftWhite);
        var hRT = hdr.rectTransform;
        hRT.anchorMin = new Vector2(1,1); hRT.anchorMax = new Vector2(1,1); hRT.pivot = new Vector2(1,1);
        hRT.anchoredPosition = new Vector2(-28, -28); hRT.sizeDelta = new Vector2(360, 36);

        var sep = AddUI(root.transform, "Separator");
        sep.AddComponent<Image>().color = MutedBlue;
        var sRT = sep.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0,1); sRT.anchorMax = new Vector2(1,1); sRT.pivot = new Vector2(0.5f,1);
        sRT.anchoredPosition = new Vector2(0, -82); sRT.sizeDelta = new Vector2(-56, 1);

        // Controls list — one line each with highlight word in brand green
        string[] rows = {
            "<color=#8BC53F>TRIGGER</color>      Grab / Pick objects you point at",
            "<color=#8BC53F>GRIP</color>              Hold to move items in your hand",
            "<color=#8BC53F>THUMBSTICK</color>  Smooth move (left) / Snap turn (right)",
            "<color=#8BC53F>TELEPORT RAY</color>  Point and release to teleport",
            "<color=#8BC53F>SOCKET DROP</color>  Release a tool over a glowing zone to use it",
            "<color=#8BC53F>MENU / A</color>     Shows this panel again"
        };
        var listGO = AddUI(root.transform, "List");
        var listRT = listGO.GetComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0,0); listRT.anchorMax = new Vector2(1,1);
        listRT.pivot = new Vector2(0.5f,0.5f); listRT.offsetMin = new Vector2(28, 110); listRT.offsetMax = new Vector2(-28, -100);
        var vlg = listGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10; vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        foreach (var r in rows)
        {
            var t = AddText(listGO.transform, "Row", r, 20, TextAnchor.MiddleLeft, FontStyle.Normal, SoftWhite);
            t.supportRichText = true;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 38;
        }

        // Continue button (bottom-right, yellow)
        var btnGO = AddUI(root.transform, "ContinueButton");
        btnGO.AddComponent<Image>().color = AccentYellow;
        var btn = btnGO.AddComponent<Button>();
        var brt = btnGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1,0); brt.anchorMax = new Vector2(1,0); brt.pivot = new Vector2(1,0);
        brt.anchoredPosition = new Vector2(-28, 24); brt.sizeDelta = new Vector2(220, 58);
        var btnLabel = AddText(btnGO.transform, "Label", "CONTINUE", 24, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.08f,0.1f,0.12f));
        Stretch(btnLabel.gameObject);

        var cg = root.AddComponent<ControlsGuidePanel>();
        cg.continueButton = btn;

        PrefabUtility.SaveAsPrefabAsset(root, ControlsGuidePath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"Controls Guide Panel saved to {ControlsGuidePath}.");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(ControlsGuidePath));
    }

    [MenuItem("Tools/PotLeakage VR/Build Grab-Drop Snap Zone Prefab")]
    public static void BuildSnapZone()
    {
        if (!Directory.Exists(PrefabFolder)) Directory.CreateDirectory(PrefabFolder);

        var root = new GameObject("DropSnapZone");
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.5f, 0.3f, 0.5f);

        var socket = root.AddComponent<XRSocketInteractor>();
        socket.showInteractableHoverMeshes = true;

        // Visual: translucent green cube to show the drop zone (toggle via VRHighlight in play).
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "ZoneVisual";
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        var r = vis.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.1f, 1f, 0.25f, 0.35f);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1); // transparent (URP)
        r.sharedMaterial = mat;

        root.AddComponent<VRHighlight>();

        PrefabUtility.SaveAsPrefabAsset(root, SnapZonePath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"Drop Snap Zone saved to {SnapZonePath}. Place one near the bin for each 'Remove X' step. Wire CompleteOnSocketed(<this socket>) in the task's events.");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(SnapZonePath));
    }

    [MenuItem("Tools/PotLeakage VR/Setup Scene (XR Rig + Prefabs + Handler)")]
    public static void SetupScene()
    {
        // 1) XR Rig
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XRRigPath);
        GameObject rig = GameObject.Find("XR Origin (XR Rig)");
        if (!rig && rigPrefab) rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        if (!rig) { Debug.LogError("XR Rig prefab not found at " + XRRigPath); return; }
        rig.transform.position = Vector3.zero;

        // 2) Parent WelcomePanel under LEFT controller
        var welcome = AssetDatabase.LoadAssetAtPath<GameObject>(WelcomePath);
        if (welcome)
        {
            var left = FindDeep(rig.transform, "LeftHand Controller") ?? FindDeep(rig.transform, "Left Controller") ?? FindDeep(rig.transform, "Left");
            if (left && !FindDeep(left, "WelcomePanel"))
            {
                var w = (GameObject)PrefabUtility.InstantiatePrefab(welcome, left);
                w.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
                w.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            }
        }
        else Debug.LogWarning("Build the Welcome Panel first.");

        // 3) World prefabs
        PlacePrefab("Assets/Prefabs/new_warehouse.prefab", "new_warehouse", Vector3.zero);
        PlacePrefab("Assets/Prefabs/POTLINE.prefab",       "POTLINE",       new Vector3(0, 0, 5));
        PlacePrefab("Assets/Prefabs/Poster.prefab",        "Poster",        new Vector3(-3, 2, 4));
        PlacePrefab("Assets/Prefabs/SST_PIPE_ani.prefab",  "SST",           new Vector3(2, 0, 5));
        var toolBox = PlacePrefab("Assets/Prefabs/ToolBox.prefab", "ToolBox", new Vector3(-2, 0, 3));
        PlacePrefab("Assets/Prefabs/walkietalkie.prefab",  "walkietalkie",  new Vector3(0.3f, 1.1f, 0.4f));

        // 4) Drop zones — right side of ToolBox and near SST
        PlaceDropZone(toolBox ? toolBox.transform : null, "ToolBox_DropPos_Right", new Vector3(0.6f, 0.4f, 0f));
        var sst = GameObject.Find("SST");
        PlaceDropZone(sst ? sst.transform : null, "SST_DropPos", new Vector3(0f, 0.5f, 0.3f));

        // 4.5) XR Device Simulator (so controllers show in Editor Play without a headset)
        var simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeviceSimulatorPath);
        if (simPrefab && !GameObject.Find("XR Device Simulator"))
            PrefabUtility.InstantiatePrefab(simPrefab);

        // 4.6) VoiceOverPlayer
        if (!GameObject.Find("VoiceOverPlayer"))
        {
            var vo = new GameObject("VoiceOverPlayer");
            var src = vo.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f;
            var vop = vo.AddComponent<VoiceOverPlayer>();
            vop.audioSource = src;
        }

        // 5) SequenceHandler GameObject
        var handlerGO = GameObject.Find("SequenceHandler");
        if (!handlerGO)
        {
            handlerGO = new GameObject("SequenceHandler");
            handlerGO.AddComponent<SequenceHandler>();
            var help = handlerGO.AddComponent<SequenceHelperFunctions>();
            var listRoot = new GameObject("ListOfSequence");
            listRoot.transform.SetParent(handlerGO.transform, false);
            handlerGO.GetComponent<SequenceHandler>().listOfSequence = listRoot.transform;

            var seqGO = new GameObject("SEQ_PotLeakage");
            seqGO.transform.SetParent(listRoot.transform, false);
            seqGO.AddComponent<Sequence>().SequenceName = "PotLeakage";

            // Wire panel + xrOrigin
            var panelGO = FindDeep(rig.transform, "WelcomePanel");
            if (panelGO) help.panel = panelGO.GetComponent<InstructionPanel>();
            help.xrOrigin = rig.GetComponent<Unity.XR.CoreUtils.XROrigin>();
        }
        Debug.Log("Scene setup complete. Add TASK_* entries in SequenceHandler > SEQ_PotLeakage via its Sequence.TaskList, and wire EventsToFollow per the source project.");
    }

    static GameObject PlacePrefab(string path, string name, Vector3 pos)
    {
        if (GameObject.Find(name)) return GameObject.Find(name);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) { Debug.LogWarning("Missing prefab: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.position = pos;
        return go;
    }

    static void PlaceDropZone(Transform parent, string name, Vector3 localPos)
    {
        if (!parent) return;
        if (FindDeep(parent, name)) return;
        var zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SnapZonePath);
        if (!zonePrefab) { Debug.LogWarning("Build Drop Snap Zone prefab first."); return; }
        var z = (GameObject)PrefabUtility.InstantiatePrefab(zonePrefab, parent);
        z.name = name;
        z.transform.localPosition = localPos;
        z.transform.localRotation = Quaternion.identity;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (!root) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++) { var r = FindDeep(root.GetChild(i), name); if (r) return r; }
        return null;
    }

    // --- helpers ---
    static GameObject AddUI(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Text AddText(Transform parent, string name, string value, int size, TextAnchor anchor, FontStyle style, Color col)
    {
        var go = AddUI(parent, name);
        var t = go.AddComponent<Text>();
        t.text = value;
        t.fontSize = size;
        t.alignment = anchor;
        t.fontStyle = style;
        t.color = col;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
#endif
