using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Builds the WalkieCommunicationCanvas (ported from the desktop project, UGUI + TMP, XR UI raycaster) and wires the
/// Crust Bathing and Slide Hood tasks in the open scene.</summary>
public static class TaskSetupBuilder
{
    const string ArrowPath = "Assets/Images/T_WalkieSlider_HandleArrow.png";
    const string HoodKey = "TASK_03_SLIDE_HOOD"; // "Slide the hood." voice-over, present in the VR TTS cache

    // ------------------------------------------------------------------ Walkie canvas
    [MenuItem("Vedanta VR/3 Build Walkie Communication Canvas")]
    public static void BuildWalkieCanvas()
    {
        var wt = FindAny("walkietalkie");
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "WalkieCommunicationCanvas" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);

        var importer = AssetImporter.GetAtPath(ArrowPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var shiftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Shift.png");
        var techSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Technical.png");
        var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = new GameObject("WalkieCommunicationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster), typeof(AudioSource), typeof(WalkieController));
        if (wt) root.transform.SetParent(wt.transform, false);
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(520f, 380f);
        rt.localScale = Vector3.one * (wt ? 0.002f / Mathf.Max(0.0001f, wt.transform.lossyScale.x) : 0.002f);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        root.GetComponent<AudioSource>().playOnAwake = false;

        var ctrl = root.GetComponent<WalkieController>();
        ctrl.callSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/call.mp3");
        ctrl.messageSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/click.mp3");

        // ---------------- call panel
        var call = Panel(root.transform, "WalkieCommunicationPanel", uiSprite);
        Strip(call.transform, new Color(0f, 0.85f, 1f, 1f));
        Label(call.transform, "HeaderTitle", "EMERGENCY RADIO LINK", font, 20f, FontStyles.Bold, new Color(0f, 0.9f, 1f), new Vector2(0, 142), new Vector2(480, 36));
        Label(call.transform, "HeaderSubtitle", "CONNECT TO POT SUPERINTENDENT & PROCESS CONTROL", font, 11f, FontStyles.Normal, new Color(0.68f, 0.78f, 0.9f), new Vector2(0, 115), new Vector2(480, 24));
        Card(call.transform, "ShiftCard", new Vector2(-120, 25), uiSprite, shiftSprite, font, "Shift In-charge", "Operational Safety", new Color(1f, 0.85f, 0.3f));
        Card(call.transform, "TechnicalCard", new Vector2(120, 25), uiSprite, techSprite, font, "Technical In-charge", "Process Control", new Color(0f, 0.9f, 1f));

        var sliderGO = new GameObject("CallSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderGO.transform.SetParent(call.transform, false);
        var srt = (RectTransform)sliderGO.transform;
        srt.anchoredPosition = new Vector2(0, -85); srt.sizeDelta = new Vector2(420, 54);
        var sbg = sliderGO.GetComponent<Image>();
        sbg.sprite = uiSprite; sbg.type = Image.Type.Sliced; sbg.color = new Color(0.08f, 0.12f, 0.22f, 1f);
        var slider = sliderGO.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0f; slider.wholeNumbers = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        var fillArea = Rect(sliderGO.transform, "Fill Area", Vector2.zero, Vector2.one, new Vector2(-20, -8));
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea, false);
        ((RectTransform)fill.transform).sizeDelta = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.sprite = uiSprite; fillImg.type = Image.Type.Sliced; fillImg.color = new Color(0.05f, 0.68f, 0.92f, 0.75f);
        slider.fillRect = (RectTransform)fill.transform;

        var handleArea = Rect(sliderGO.transform, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(-54, 0));
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea, false);
        ((RectTransform)handle.transform).sizeDelta = new Vector2(52, 52);
        var hImg = handle.GetComponent<Image>();
        hImg.sprite = arrowSprite ? arrowSprite : uiSprite; hImg.preserveAspect = true;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = hImg;

        var sliderLabel = Label(sliderGO.transform, "SliderLabel", "SLIDE TO CALL >>>", font, 15f, FontStyles.Bold, Color.white, Vector2.zero, Vector2.zero);
        var lrt = (RectTransform)sliderLabel.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;

        var rel = sliderGO.AddComponent<WalkieSliderRelease>();
        rel.owner = ctrl;
        ctrl.callPanel = call;
        ctrl.callSlider = slider;
        ctrl.sliderLabel = sliderLabel;

        // ---------------- conversation panel
        var conv = Panel(root.transform, "WalkieConversationPanel", uiSprite);
        Strip(conv.transform, new Color(1f, 0.75f, 0.2f, 1f));
        Label(conv.transform, "ConversationTitle", "RADIO TRANSMISSION", font, 18f, FontStyles.Bold, new Color(1f, 0.8f, 0.2f), new Vector2(0, 150), new Vector2(480, 32));
        Label(conv.transform, "ChannelStatusText", "ACTIVE CHANNEL | CEILING: ≤ 4.5 V | PERSISTENT: < 1.2 V", font, 11f, FontStyles.Bold, new Color(0f, 0.9f, 1f), new Vector2(0, 126), new Vector2(480, 22));
        ctrl.shiftBubble = Bubble(conv.transform, "ShiftBubble", new Vector2(0, 55), uiSprite, shiftSprite, font, "SHIFT IN-CHARGE",
            "Take proper safety measures before proceeding with the leakage response.", new Color(0.1f, 0.18f, 0.32f, 0.9f), new Color(1f, 0.85f, 0.3f));
        ctrl.technicalBubble = Bubble(conv.transform, "TechnicalBubble", new Vector2(0, -48), uiSprite, techSprite, font, "TECHNICAL IN-CHARGE",
            "Check the pot voltage in the pot control system and reduce the voltage below 1.2 V if the leakage does not stop.", new Color(0.06f, 0.24f, 0.26f, 0.9f), new Color(0f, 0.9f, 1f));

        var btnGO = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(conv.transform, false);
        var brt = (RectTransform)btnGO.transform;
        brt.anchoredPosition = new Vector2(0, -145); brt.sizeDelta = new Vector2(210, 42);
        var bImg = btnGO.GetComponent<Image>();
        bImg.sprite = uiSprite; bImg.type = Image.Type.Sliced; bImg.color = new Color(0.06f, 0.72f, 0.5f, 1f);
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = bImg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.1f, 0.85f, 0.6f, 1f); colors.pressedColor = new Color(0.04f, 0.55f, 0.38f, 1f);
        btn.colors = colors;
        var bt = Label(btnGO.transform, "Text", "CONTINUE >>", font, 15f, FontStyles.Bold, new Color(0.03f, 0.1f, 0.08f), Vector2.zero, Vector2.zero);
        var btrt = (RectTransform)bt.transform;
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.sizeDelta = Vector2.zero;
        ctrl.continueButton = btn;
        ctrl.conversationPanel = conv;

        call.SetActive(false); conv.SetActive(false);
        ctrl.shiftBubble.SetActive(false); ctrl.technicalBubble.SetActive(false); btnGO.SetActive(false);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Vedanta VR: WalkieCommunicationCanvas built.");
    }

    // ------------------------------------------------------------------ Task wiring
    [MenuItem("Vedanta VR/4 Wire Crust Bathing + Slide Hood Tasks")]
    public static void WireTasks()
    {
        var handler = Object.FindFirstObjectByType<SequenceHandler>();
        var helper = handler ? handler.GetComponent<SequenceHelperFunctions>() : null;
        if (!handler || !helper) { Debug.LogError("SequenceHandler/SequenceHelperFunctions not found."); return; }

        var sst = handler.GetComponent<SequenceHelperFunctions>().transformPoints.Find("SST");
        var ptm = helper.transformPoints.Find("PTM Crane");

        foreach (var n in new[] { "HoodSlide_Right", "HoodSlide_Left", "CrustBathingSequence" })
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t && t.name == n && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var host = handler.transform.parent ? handler.transform.parent : handler.transform;

        // --- hoods
        var potLine = GameObject.Find("POTLINE").transform.Find("PotLine");
        var right = MakeHood(host, "HoodSlide_Right", potLine.Find("Plane.023"),
            new Vector3(-3.71499991f, 1.29938674f, 107.44593f), new Vector3(-4.62900019f, 1.52600002f, 108.085999f), new Vector3(-4.62900019f, 1.29938674f, 107.44593f));
        var left = MakeHood(host, "HoodSlide_Left", potLine.Find("Plane.020"),
            new Vector3(-2.8900001f, 1.29938674f, 107.44593f), new Vector3(-2.046f, 1.51100004f, 108.149002f), new Vector3(-2.046f, 1.29938674f, 107.44593f));

        // --- crust bathing
        var crustGO = new GameObject("CrustBathingSequence");
        crustGO.transform.SetParent(host, false);
        var crust = crustGO.AddComponent<CrustBathingSequence>();
        // updated crane: crane_upd replaces crane_roller
        var oldCrane = FindAny("crane_roller");
        if (oldCrane) oldCrane.SetActive(false);
        var crane = FindRoot("crane_upd").transform;
        var cylinder = crane.Find("Machine big cylinedr part.002");
        crust.crane = crane;
        crust.cylinder = cylinder;
        crust.driller = FindDeep(cylinder, "driller.001");
        crust.connector = FindDeep(cylinder, "driller part coneector.001");
        var cement = potLine.Find("cement");
        crust.hole1 = cement.Find("HOLE_1").gameObject;
        crust.hole2 = cement.Find("HOLE_2").gameObject;
        crust.hole3 = cement.Find("HOLE_3").gameObject;
        crust.sstPoint = helper.transformPoints.Find("SST");   // the sequence snaps to SST after the cylinder part has moved
        crane.gameObject.SetActive(false);                      // hidden until the crust task brings it in

        // --- bath powder
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "BathPowderSequence" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var powderGO = new GameObject("BathPowderSequence");
        powderGO.transform.SetParent(host, false);
        var powder = powderGO.AddComponent<BathPowderSequence>();
        powder.driller = crust.driller;
        powder.connector = crust.connector;
        var cube006 = FindDeep(cylinder, "Cube.006");
        powder.cube = cube006;
        powder.piper = FindDeep(cube006, "piper.001");
        var drop = powder.piper.Find("drop point");
        powder.dropPoint = drop ? drop.GetComponent<ParticleSystem>() : null;

        Wire(handler, "TASK_BATH_PIPE_POWDER", "TASK_BATH_PIPE_POWDER", e =>
        {
            Str(e, helper.SetTitle, "Bath Powder");
            Str(e, helper.SetDescription, "Now let us use bath powder from PTM crane.");
            Obj(e, helper.SnapCameraToTransform, sst);
            Obj(e, helper.StartBathPowder, powder);
        });

        // --- molten stream plays from TASK_03_LEAKAGE_OCCURRED to TASK_THROW_FUSED_ALUMINA
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "MoltenStreamRange" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var moltenGO = new GameObject("MoltenStreamRange");
        moltenGO.transform.SetParent(host, false);
        var molten = moltenGO.AddComponent<MoltenStreamRange>();
        molten.stream = Object.FindFirstObjectByType<PotLeakage.VFX.MoltenMetalStreamParticles>(FindObjectsInactive.Include);

        // --- SST connection + air unlock
        foreach (var n in new[] { "SstConnectionSequence", "AirUnlockSequence" })
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t && t.name == n && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);

        var sstGO = new GameObject("SstConnectionSequence");
        sstGO.transform.SetParent(host, false);
        var sstSeq = sstGO.AddComponent<SstConnectionSequence>();
        sstSeq.sstPipe = FindAny("SST_PIPE_ani");
        sstSeq.sstPipeBezier = sstSeq.sstPipe.transform.Find("BézierCurve.002").gameObject;
        sstSeq.sstPipe1 = FindAny("SST_PIPE_ani_1");
        sstSeq.sstPipe1Bezier = sstSeq.sstPipe1.transform.Find("BézierCurve.002").gameObject;
        sstSeq.roundPipes = FindAny("round_PIPES");
        sstSeq.roundPipes1 = FindAny("round_PIPES (1)");

        var airGO = new GameObject("AirUnlockSequence");
        airGO.transform.SetParent(host, false);
        var air = airGO.AddComponent<AirUnlockSequence>();
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "valve handle.001" && t.gameObject.scene.isLoaded && t.GetComponent<BoxCollider>()) air.valveHandle = t;
        air.sstPoint = sst;
        air.sstPipe = sstSeq.sstPipe;
        air.sstPipe1 = sstSeq.sstPipe1;

        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "GasEffectRange" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var gasGO = new GameObject("GasEffectRange");
        gasGO.transform.SetParent(host, false);
        gasGO.AddComponent<GasEffectRange>().roots = new[] { sstSeq.sstPipe, sstSeq.sstPipe1 };

        Wire(handler, "TASK_4_SST_CONNECTION", "SELECT_SST_CONNECTOR", e =>
        {
            Str(e, helper.SetTitle, "SST Connection");
            Str(e, helper.SetDescription, sstSeq.description);
            Obj(e, helper.SnapCameraToTransform, sst);
            Obj(e, helper.StartSstConnection, sstSeq);
        });
        var airPoint = helper.transformPoints.Find("AirUnlock");
        Wire(handler, "TASK_5_AIR_Unlock", "TASK_5_AIR_UNLOCK", e =>
        {
            Str(e, helper.SetTitle, "Air Unlock Valve");
            Str(e, helper.SetDescription, "Let us now open the valve to release compressed air in the affected area.");
            Obj(e, helper.SnapCameraToTransform, airPoint);
            Obj(e, helper.StartAirUnlock, air);
        });

        Wire(handler, "TASK_CRUST_BATHING", "TASK_CRUST_BATHING", e =>
        {
            Str(e, helper.SetTitle, "Crust Breaking");
            Str(e, helper.SetDescription, "Let us start Crust Breaking Process by bring PTM crane to the leaked potline");
            Obj(e, helper.SnapCameraToTransform, ptm);
            Obj(e, helper.StartCrustBathing, crust);
        });
        Wire(handler, "TASK_6_SLIDE_RIGHT_HOOD", HoodKey, e =>
        {
            Str(e, helper.SetTitle, "Slide Right Hood");
            Str(e, helper.SetDescription, "Slide the right hood.");
            Obj(e, helper.SnapCameraToTransform, sst);
            Obj(e, helper.StartHoodSlide, right);
        });
        Wire(handler, "TASK_05_SLIDE_LEFT_HOOD", HoodKey, e =>
        {
            Str(e, helper.SetTitle, "Slide Left Hood");
            Str(e, helper.SetDescription, "Slide the left hood panel outward.");
            Obj(e, helper.SnapCameraToTransform, sst);
            Obj(e, helper.StartHoodSlide, left);
        });

        // --- throw fused alumina
        foreach (var n in new[] { "FusedAluminaSequence", "CloseHoodSequence", "HoodClose_Right", "HoodClose_Left" })
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t && t.name == n && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);

        var bagTr = helper.transformPoints.Find("Fused Alumina Bag");
        var bagMesh = bagTr.GetComponent<MeshFilter>();
        if (!bagMesh) bagMesh = bagTr.gameObject.AddComponent<MeshFilter>();
        var bagRend = bagTr.GetComponent<MeshRenderer>();
        if (!bagRend) bagRend = bagTr.gameObject.AddComponent<MeshRenderer>();
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Model/Bag.fbx"))
        {
            if (a is Mesh m && bagMesh.sharedMesh == null) bagMesh.sharedMesh = m;
            if (a is Material mat && bagRend.sharedMaterial == null) bagRend.sharedMaterial = mat;
        }
        bagTr.localScale = new Vector3(0.22490f, 0.31327f, 0.31327f);
        var smokeTr = bagTr.Find("alumina_smoke");
        if (!smokeTr) { var sg = new GameObject("alumina_smoke", typeof(ParticleSystem)); sg.transform.SetParent(bagTr, false); smokeTr = sg.transform; }
        var smokePs = smokeTr.GetComponent<ParticleSystem>();
        ConfigureSmoke(smokePs);
        bagTr.gameObject.SetActive(false);

        var fusedGO = new GameObject("FusedAluminaSequence");
        fusedGO.transform.SetParent(host, false);
        var fused = fusedGO.AddComponent<FusedAluminaSequence>();
        fused.bag = bagTr; fused.smoke = smokePs; fused.target = potLine.Find("cement/cement_1.001");

        Wire(handler, "TASK_THROW_FUSED_ALUMINA", "THROW_FUSED_ALUMINA_SAFE_DISTANCE", e =>
        {
            Str(e, helper.SetTitle, "Throw Fused Alumina");
            Str(e, helper.SetDescription, "Throw fused alumina to seal the bath leakage area. Make sure you are maintaining 2 to 3 meter safe distance.");
            Obj(e, helper.SnapCameraToTransform, sst);
            Obj(e, helper.StartFusedAlumina, fused);
        });

        // --- close hood
        var closeGO = new GameObject("CloseHoodSequence");
        closeGO.transform.SetParent(host, false);
        var close = closeGO.AddComponent<CloseHoodSequence>();
        close.rightHood = MakeHood(host, "HoodClose_Right", potLine.Find("Plane.023"),
            new Vector3(-4.62900019f, 1.29938674f, 107.44593f), new Vector3(-4.62900019f, 1.52600002f, 108.085999f), new Vector3(-3.71499991f, 1.29938674f, 107.44593f));
        close.leftHood = MakeHood(host, "HoodClose_Left", potLine.Find("Plane.020"),
            new Vector3(-2.046f, 1.29938674f, 107.44593f), new Vector3(-2.046f, 1.51100004f, 108.149002f), new Vector3(-2.8900001f, 1.29938674f, 107.44593f));
        Wire(handler, "CLOSE_HOOD", "CLOSE_HOOD", e =>
        {
            Str(e, helper.SetTitle, "Close Hood");
            Str(e, helper.SetDescription, "Pot leakage is completely stopped. Let us close the hood.");
            Obj(e, helper.SnapCameraToTransform, sst);
            Obj(e, helper.StartCloseHood, close);
        });

        // --- training completed checklist + cleanup after tasks
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && (t.name == "CompletionChecklistCanvas" || t.name == "QuizCanvas" || t.name == "TaskAfterCleanup") && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var checklist = BuildCompletionPanel();
        checklist.quizPanel = BuildQuizPanel();
        Wire(handler, "TASK_TRAINING_COMPLETED", "TASK_27_TRAINING_COMPLETED", e =>
        {
            Str(e, helper.SetTitle, "Training Complete");
            Str(e, helper.SetDescription, "Pot Leakage Emergency Response Training Completed.");
            Obj(e, helper.SnapCameraToTransform, helper.transformPoints.Find("Welcome"));
            Obj(e, helper.ShowCompletionPanel, checklist);
        });

        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "WalkieOnlyActive" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var walkieGO = new GameObject("WalkieOnlyActive");
        walkieGO.transform.SetParent(host, false);
        var walkieOnly = walkieGO.AddComponent<TaskOnlyActive>();
        walkieOnly.target = FindAny("walkietalkie");
        walkieOnly.sequenceName = "TASK_06_EMERGENCY_COMMUNICATION";
        if (walkieOnly.target) walkieOnly.target.SetActive(false);   // off in the scene; the emergency task turns it on

        var cleanGO = new GameObject("TaskAfterCleanup");
        cleanGO.transform.SetParent(host, false);
        var cleanup = cleanGO.AddComponent<TaskAfterCleanup>();
        cleanup.rules = new[]
        {
            new TaskAfterCleanup.Rule { afterSequence = "TASK_06_EMERGENCY_COMMUNICATION", disable = new[] { FindRoot("character_1 (1)"), FindRoot("character_2 (1)") } },
            new TaskAfterCleanup.Rule { afterSequence = "TASK_CRUST_BATHING", disable = new[] { crust.connector.gameObject } },
            new TaskAfterCleanup.Rule { afterSequence = "TASK_BATH_PIPE_POWDER", disable = new[] { crust.hole1, crust.hole2, crust.hole3, cube006.gameObject } },
        };

        // --- emergency communication: walkie press -> Talk point, characters run in, two voice clips
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "EmergencyCallSequence" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var emGO = new GameObject("EmergencyCallSequence");
        emGO.transform.SetParent(host, false);
        var em = emGO.AddComponent<EmergencyCallSequence>();
        em.walkie = FindAny("walkietalkie");
        em.talkPoint = helper.transformPoints.Find("Talk");
        em.characters = new[] { FindRoot("character_1 (1)"), FindRoot("character_2 (1)") };
        em.leakageClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Pot leakage has occurred.mp3");
        em.sstToolClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Let me START with SST Tool....mp3");
        foreach (var ch in em.characters) if (ch) ch.SetActive(false);
        var walkieCanvas = FindAny("WalkieCommunicationCanvas");
        if (walkieCanvas) walkieCanvas.SetActive(false);   // the radio canvas is no longer used
        Wire(handler, "TASK_06_EMERGENCY_COMMUNICATION", "TASK_04", e =>
        {
            Str(e, helper.SetTitle, "Emergency Communication");
            Str(e, helper.SetDescription, "Inform the Shift Superintendent and Technical In-charge immediately through the walkie-talkie.");
            Obj(e, helper.SnapCameraToTransform, sst);
            Obj(e, helper.StartEmergencyCall, em);
        });

        // --- the NEXT button is gone everywhere; tasks only finish through their interaction
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!t || !t.gameObject.scene.isLoaded || t.name != "NextButton") continue;
            var b = t.GetComponent<Button>();
            if (b)
            {
                while (b.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(b.onClick, 0);
                b.onClick.RemoveAllListeners();
            }
            t.gameObject.SetActive(false);
        }

        // --- control machine
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == "ControlMachineSequence" && t.gameObject.scene.isLoaded) Object.DestroyImmediate(t.gameObject);
        var cmGO = new GameObject("ControlMachineSequence");
        cmGO.transform.SetParent(host, false);
        var cm = cmGO.AddComponent<ControlMachineSequence>();
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!t || !t.gameObject.scene.isLoaded) continue;
            if (t.name == "Cube.016" && t.parent && t.parent.name == "machine") cm.cube = t;
            if (t.name == "ActiveDigits" && t.parent)
            {
                if (t.parent.name == "VoltageDisplay") cm.voltageText = t.GetComponent<TMPro.TMP_Text>();
                if (t.parent.name == "KADataDisplay") cm.beamText = t.GetComponent<TMPro.TMP_Text>();
            }
        }
        var machinePoint = helper.transformPoints.Find("PotControlMachine");
        Wire(handler, "TASK_CONTROL_MACHINE", "TASK_CONTROL_MACHINE", e =>
        {
            Str(e, helper.SetTitle, "Pot Control Machine");
            Str(e, helper.SetDescription, cm.startDescription);
            Obj(e, helper.SnapCameraToTransform, machinePoint);
            Obj(e, helper.StartControlMachine, cm);
        });

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Vedanta VR: Crust Bathing + Slide Hood tasks wired.");
    }

    /// <summary>Replaces the SetDescription string of an already wired task and sets its voice-over key.</summary>
    static void UpdateTexts(SequenceHandler handler, SequenceHelperFunctions helper, string seqName, string ttsKey, string description)
    {
        foreach (var s in handler.sequenceList)
        {
            if (s.name != seqName) continue;
            foreach (var task in s.TaskList)
            {
                task.ttsTextOrKey = ttsKey;
                task.AutoAdvanceAfterVoiceOver = false;
                task.completionMode = CompletionMode.Manual;
                task.instructionText = description;
                var e = task.EventsToFollow;
                for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
                    if (e.GetPersistentMethodName(i) == "SetDescription") UnityEventTools.RemovePersistentListener(e, i);
                Str(e, helper.SetDescription, description);
                for (int i = 0; i < e.GetPersistentEventCount(); i++) e.SetPersistentListenerState(i, UnityEventCallState.EditorAndRuntime);
            }
            EditorUtility.SetDirty(s);
        }
    }

    // ------------------------------------------------------------------ quiz panel
    static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        var i = g.GetComponent<Image>(); i.color = color; i.raycastTarget = raycast;
        return r;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, FontStyles style, Color color,
        float x, float y, float w, float h, TextAlignmentOptions align)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        var t = g.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static Button YellowButton(Transform parent, string name, string label, TMP_FontAsset font, float x, float y, float w, float h, out TMP_Text text)
    {
        var r = Box(parent, name, x, y, w, h, new Color(0.94f, 0.78f, 0.08f, 1f), true);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = r.GetComponent<Image>();
        var cb = b.colors; cb.highlightedColor = new Color(1f, 0.9f, 0.4f, 1f); cb.pressedColor = new Color(0.8f, 0.65f, 0.05f, 1f); b.colors = cb;
        text = Txt(r, "Text", label, font, 30f, FontStyles.Bold, new Color(0.12f, 0.12f, 0.12f), 0, 0, w, h, TextAlignmentOptions.Center);
        text.characterSpacing = 4f;
        return b;
    }

    /// <summary>Builds the quiz canvas (header, question view, result view). Hidden until the checklist's MOVE TO QUIZ is pressed.</summary>
    static QuizPanel BuildQuizPanel()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var logo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Final Logo.png");
        var root = new GameObject("QuizCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster), typeof(QuizPanel));
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(1100f, 700f);
        rt.localScale = Vector3.one * 0.0013f;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 6f;
        var q = root.GetComponent<QuizPanel>();

        var bg = Box(root.transform, "Background", 0, 0, 1100, 700, new Color(0.12f, 0.14f, 0.22f, 0.98f), true);
        Box(bg, "TopBar", 0, 0, 1100, 7, new Color(0.24f, 0.78f, 0.29f, 1f));

        // header
        var logoR = Box(bg, "Logo", 24, 24, 200, 60, Color.white);
        var logoImg = logoR.GetComponent<Image>(); logoImg.sprite = logo; logoImg.preserveAspect = true;
        Txt(bg, "Brand", "VEDANTA ALUMINIUM", font, 28f, FontStyles.Bold, new Color(0.27f, 0.79f, 0.31f), 244, 22, 560, 36, TextAlignmentOptions.Left);
        q.subtitleText = Txt(bg, "Subtitle", "Pot Leakage Standard Operations", font, 20f, FontStyles.Normal, Color.white, 244, 58, 560, 28, TextAlignmentOptions.Left);
        q.counterText = Txt(bg, "Counter", "Question 1 / 10", font, 26f, FontStyles.Normal, new Color(0.85f, 0.88f, 0.95f), 746, 30, 330, 36, TextAlignmentOptions.Right);
        Box(bg, "HeaderLine", 24, 100, 1052, 3, new Color(0.22f, 0.5f, 0.94f, 1f));

        // question view
        var qv = new GameObject("QuestionView", typeof(RectTransform));
        qv.transform.SetParent(bg, false);
        var qvr = (RectTransform)qv.transform; qvr.anchorMin = Vector2.zero; qvr.anchorMax = Vector2.one; qvr.offsetMin = qvr.offsetMax = Vector2.zero;
        q.questionView = qv;
        Box(qvr, "QuestionBox", 24, 118, 1052, 130, new Color(0.09f, 0.1f, 0.24f, 1f));
        q.questionText = Txt(qvr, "QuestionText", "Q1.  Question text", font, 36f, FontStyles.Bold, Color.white, 48, 128, 1004, 110, TextAlignmentOptions.MidlineLeft);
        Box(qvr, "OptionsBox", 24, 262, 1052, 310, new Color(0.05f, 0.09f, 0.16f, 1f));
        q.optionButtons = new Button[4]; q.optionImages = new Image[4]; q.optionTexts = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? 56f : 570f, y = i < 2 ? 296f : 428f;
            var r = Box(qvr, "Option_" + (char)('A' + i), x, y, 474, 104, new Color(0.79f, 0.81f, 0.86f, 1f), true);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            var cb = b.colors; cb.highlightedColor = new Color(0.93f, 0.96f, 1f, 1f); cb.pressedColor = new Color(0.75f, 0.8f, 0.9f, 1f); b.colors = cb;
            var tx = Txt(r, "Label", (char)('A' + i) + ".", font, 30f, FontStyles.Normal, new Color(0.22f, 0.25f, 0.32f), 24, 0, 430, 104, TextAlignmentOptions.MidlineLeft);
            q.optionButtons[i] = b; q.optionImages[i] = r.GetComponent<Image>(); q.optionTexts[i] = tx;
        }
        q.feedbackText = Txt(qvr, "Feedback", "Select the correct answer.", font, 30f, FontStyles.Normal, Color.white, 28, 596, 680, 70, TextAlignmentOptions.MidlineLeft);
        q.nextButton = YellowButton(qvr, "NextButton", "NEXT QUESTION", font, 724, 592, 352, 72, out q.nextButtonText);

        // result view
        var rv = new GameObject("ResultView", typeof(RectTransform));
        rv.transform.SetParent(bg, false);
        var rvr = (RectTransform)rv.transform; rvr.anchorMin = Vector2.zero; rvr.anchorMax = Vector2.one; rvr.offsetMin = rvr.offsetMax = Vector2.zero;
        q.resultView = rv;
        Box(rvr, "ResultBox", 24, 118, 1052, 300, new Color(0.09f, 0.1f, 0.24f, 1f));
        q.resultTitleText = Txt(rvr, "ResultTitle", "Pot Leakage Assessment Result", font, 36f, FontStyles.Bold, Color.white, 48, 134, 1004, 50, TextAlignmentOptions.Left);
        q.scoreText = Txt(rvr, "Score", "Score: 0%", font, 64f, FontStyles.Bold, Color.white, 24, 440, 1052, 80, TextAlignmentOptions.Center);
        q.statusText = Txt(rvr, "Status", "Status: FAIL", font, 64f, FontStyles.Bold, Color.white, 24, 520, 1052, 80, TextAlignmentOptions.Center);
        q.resultNoteText = Txt(rvr, "Note", "", font, 24f, FontStyles.Normal, Color.white, 28, 612, 500, 70, TextAlignmentOptions.MidlineLeft);
        TMP_Text rt1, rt2;
        q.retryButton = YellowButton(rvr, "RetryButton", "RETRY QUIZ", font, 546, 608, 260, 72, out rt1);
        q.exitButton = YellowButton(rvr, "ExitButton", "EXIT TRAINING", font, 816, 608, 260, 72, out rt2);
        rv.SetActive(false);

        root.SetActive(false);
        EditorUtility.SetDirty(root);
        return q;
    }

    /// <summary>Builds the 10-item "POT LEAKAGE RESPONSE COMPLETED" checklist canvas (hidden until the task shows it).</summary>
    static CompletionChecklistPanel BuildCompletionPanel()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        const string logoPath = "Assets/Images/Final Logo.png";
        var imp = AssetImporter.GetAtPath(logoPath) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; imp.SaveAndReimport(); }
        var logo = AssetDatabase.LoadAssetAtPath<Sprite>(logoPath);

        var root = new GameObject("CompletionChecklistCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster), typeof(CompletionChecklistPanel));
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(1078f, 782f);
        rt.localScale = Vector3.one * 0.0013f;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 6f;

        var bg = Panel(root.transform, "Background", uiSprite);
        bg.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.19f, 0.97f);
        var green = new Color(0.24f, 0.86f, 0.35f, 1f);
        Strip(bg.transform, green);

        var logoGO = new GameObject("Logo", typeof(RectTransform), typeof(Image));
        logoGO.transform.SetParent(bg.transform, false);
        var lrt = (RectTransform)logoGO.transform; lrt.anchoredPosition = new Vector2(0, 306); lrt.sizeDelta = new Vector2(320, 100);
        var li = logoGO.GetComponent<Image>(); li.sprite = logo; li.preserveAspect = true; li.raycastTarget = false;

        Label(bg.transform, "Subtitle", "VEDANTA ALUMINIUM \u2014 POTLINE OPERATIONS", font, 22f, FontStyles.Bold, green, new Vector2(0, 253), new Vector2(900, 30));
        Label(bg.transform, "Title", "POT LEAKAGE RESPONSE COMPLETED", font, 44f, FontStyles.Bold, green, new Vector2(0, 205), new Vector2(1000, 60));
        Label(bg.transform, "Instruction", "Before completion, the operator must verify every item below. Click an item to confirm it.", font, 21f, FontStyles.Normal, Color.white, new Vector2(0, 163), new Vector2(1000, 32));

        var box = new GameObject("ChecklistBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(bg.transform, false);
        var brt = (RectTransform)box.transform; brt.anchoredPosition = new Vector2(0, -36); brt.sizeDelta = new Vector2(960, 311);
        var bi = box.GetComponent<Image>(); bi.color = new Color(0.08f, 0.13f, 0.26f, 1f); bi.raycastTarget = false;
        var line = new GameObject("TopLine", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(box.transform, false);
        var lnrt = (RectTransform)line.transform;
        lnrt.anchorMin = new Vector2(0, 1); lnrt.anchorMax = new Vector2(1, 1); lnrt.pivot = new Vector2(0.5f, 1); lnrt.sizeDelta = new Vector2(0, 4);
        var lni = line.GetComponent<Image>(); lni.color = green; lni.raycastTarget = false;

        string[] labels =
        {
            "Right and left hoods closed properly", "All required tools collected and accounted for",
            "Voltage maintained below 4.1 V", "Cooling pipes checked and cooling air released",
            "Beam level maintained above 350", "FLR valve opened correctly",
            "Bath powder applied using PTM crane", "No leakage visible after treatment",
            "Fused alumina (10 kg) added successfully", "Final pot voltage and beam level verified",
        };
        var panel = root.GetComponent<CompletionChecklistPanel>();
        panel.itemLabels = labels;
        panel.itemButtons = new Button[10]; panel.itemTexts = new TMP_Text[10]; panel.itemBackgrounds = new Image[10];
        for (int i = 0; i < 10; i++)
        {
            int col = i % 2, row = i / 2;
            var item = new GameObject("Item_" + (i + 1), typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(box.transform, false);
            var irt = (RectTransform)item.transform;
            irt.anchoredPosition = new Vector2(col == 0 ? -240f : 223.5f, 111f - row * 52f);
            irt.sizeDelta = new Vector2(447f, 42f);
            var img = item.GetComponent<Image>(); img.color = panel.pendingColor;
            var btn = item.GetComponent<Button>(); btn.targetGraphic = img;
            var cols = btn.colors; cols.highlightedColor = new Color(0.8f, 0.9f, 1f, 1f); cols.pressedColor = new Color(0.6f, 0.75f, 0.9f, 1f); btn.colors = cols;
            var txt = Label(item.transform, "Text", "[ ]  " + labels[i], font, 19f, FontStyles.Bold, Color.white, Vector2.zero, Vector2.zero);
            var trt = (RectTransform)txt.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(14, 0); trt.offsetMax = new Vector2(-8, 0);
            txt.alignment = TextAlignmentOptions.Left;
            txt.enableAutoSizing = true; txt.fontSizeMin = 11f; txt.fontSizeMax = 19f;
            panel.itemButtons[i] = btn; panel.itemTexts[i] = txt; panel.itemBackgrounds[i] = img;
        }

        var quit = new GameObject("QuitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        quit.transform.SetParent(bg.transform, false);
        var qrt = (RectTransform)quit.transform; qrt.anchoredPosition = new Vector2(0, -305); qrt.sizeDelta = new Vector2(384, 55);
        var qi = quit.GetComponent<Image>(); qi.color = panel.quitOffColor;
        var qb = quit.GetComponent<Button>(); qb.targetGraphic = qi; qb.interactable = false;
        var qt = Label(quit.transform, "Text", "MOVE TO QUIZ", font, 28f, FontStyles.Bold, Color.black, Vector2.zero, Vector2.zero);
        var qtr = (RectTransform)qt.transform; qtr.anchorMin = Vector2.zero; qtr.anchorMax = Vector2.one; qtr.sizeDelta = Vector2.zero;
        panel.quitButton = qb; panel.quitBackground = qi; panel.quitText = qt;

        root.SetActive(false);
        EditorUtility.SetDirty(root);
        return panel;
    }

    /// <summary>alumina_smoke dust settings (see the reference screenshot).</summary>
    static void ConfigureSmoke(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 2.5f; main.loop = false; main.playOnAwake = false;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.simulationSpeed = 1f;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 120;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Rigidbody;
        var em = ps.emission;
        em.enabled = true; em.rateOverTime = 33f; em.rateOverDistance = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 25, 40, 1, 0.01f) });
        var sh = ps.shape;
        sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f; sh.radiusThickness = 1f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1f)));
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.maxParticleSize = 0.5f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var dust = AssetDatabase.LoadAssetAtPath<Material>("Assets/PotLeakage/Materials/M_DustParticle.mat");
        if (dust) r.sharedMaterial = dust;
        EditorUtility.SetDirty(ps);
    }

    static HoodSlideSequence MakeHood(Transform parent, string name, Transform plane, Vector3 start, Vector3 mid, Vector3 end)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var h = go.AddComponent<HoodSlideSequence>();
        h.plane = plane; h.startLocalPos = start; h.intermediateLocalPos = mid; h.finalLocalPos = end;
        return h;
    }

    static void Wire(SequenceHandler handler, string seqName, string ttsKey, System.Action<UnityEvent> add)
    {
        foreach (var s in handler.sequenceList)
        {
            if (s.name != seqName) continue;
            foreach (var task in s.TaskList)
            {
                if (ttsKey != null) task.ttsTextOrKey = ttsKey;
                // These tasks finish by interaction/animation, never when the voice-over ends.
                task.AutoAdvanceAfterVoiceOver = false;
                task.completionMode = CompletionMode.Manual;
                var e = task.EventsToFollow;
                while (e.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(e, 0);
                add(e);
                for (int i = 0; i < e.GetPersistentEventCount(); i++) e.SetPersistentListenerState(i, UnityEventCallState.EditorAndRuntime);
            }
            EditorUtility.SetDirty(s);
        }
    }

    static void Str(UnityEvent e, UnityAction<string> a, string v) { UnityEventTools.AddStringPersistentListener(e, a, v); }
    static void Obj<T>(UnityEvent e, UnityAction<T> a, T v) where T : Object { UnityEventTools.AddObjectPersistentListener(e, a, v); }

    // ------------------------------------------------------------------ UI helpers
    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static GameObject FindRoot(string n)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == n && t.gameObject.scene.isLoaded && t.parent == null) return t.gameObject;
        return FindAny(n);
    }

    static GameObject FindAny(string n)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t && t.name == n && t.gameObject.scene.isLoaded) return t.gameObject;
        return null;
    }

    static GameObject Panel(Transform parent, string name, Sprite sprite)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
        var i = g.GetComponent<Image>();
        i.sprite = sprite; i.type = Image.Type.Sliced; i.color = new Color(0.06f, 0.09f, 0.16f, 0.96f);
        return g;
    }

    static void Strip(Transform parent, Color c)
    {
        var g = new GameObject("TopStrip", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, 4);
        var i = g.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
    }

    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = min; r.anchorMax = max; r.sizeDelta = size;
        return r;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size, FontStyles style, Color color, Vector2 pos, Vector2 sz)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchoredPosition = pos; r.sizeDelta = sz;
        var t = g.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; // never block the slider / button behind it
        return t;
    }

    static void Card(Transform parent, string name, Vector2 pos, Sprite bg, Sprite avatar, TMP_FontAsset font, string title, string role, Color titleColor)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.anchoredPosition = pos; r.sizeDelta = new Vector2(220, 105);
        var i = g.GetComponent<Image>(); i.sprite = bg; i.type = Image.Type.Sliced; i.color = new Color(0.12f, 0.16f, 0.26f, 0.85f); i.raycastTarget = false;
        Avatar(g.transform, new Vector2(-65, 0), 62, avatar);
        var t = Label(g.transform, "Title", title, font, 13f, FontStyles.Bold, titleColor, new Vector2(35, 14), new Vector2(130, 26));
        t.alignment = TextAlignmentOptions.Left;
        var ro = Label(g.transform, "Role", role, font, 11f, FontStyles.Normal, new Color(0.75f, 0.82f, 0.9f), new Vector2(35, -12), new Vector2(130, 24));
        ro.alignment = TextAlignmentOptions.Left;
    }

    static void Avatar(Transform parent, Vector2 pos, float size, Sprite sprite)
    {
        var a = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        a.transform.SetParent(parent, false);
        var r = (RectTransform)a.transform; r.anchoredPosition = pos; r.sizeDelta = new Vector2(size, size);
        var i = a.GetComponent<Image>(); i.sprite = sprite; i.preserveAspect = true; i.raycastTarget = false;
    }

    static GameObject Bubble(Transform parent, string name, Vector2 pos, Sprite bg, Sprite avatar, TMP_FontAsset font, string sender, string message, Color bubbleColor, Color nameColor)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.anchoredPosition = pos; r.sizeDelta = new Vector2(460, 90);
        var i = g.GetComponent<Image>(); i.sprite = bg; i.type = Image.Type.Sliced; i.color = bubbleColor; i.raycastTarget = false;
        Avatar(g.transform, new Vector2(-195, 0), 50, avatar);
        var n = Label(g.transform, "SenderName", sender, font, 13f, FontStyles.Bold, nameColor, new Vector2(25, 24), new Vector2(370, 22));
        n.alignment = TextAlignmentOptions.Left;
        var m = Label(g.transform, "MessageText", message, font, 11.5f, FontStyles.Normal, Color.white, new Vector2(25, -10), new Vector2(370, 44));
        m.alignment = TextAlignmentOptions.TopLeft;
        m.textWrappingMode = TextWrappingModes.Normal;
        return g;
    }
}
