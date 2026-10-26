using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Brings the lighting of the desktop project (baked directional + area lights, freight-station skybox, baked reflection probe,
/// ACES tonemapping) into the VR scene and tunes it for Quest: baked GI for the static environment, one realtime (mixed) sun for
/// dynamic objects, light probes, MSAA, a short shadow distance and no vignette.</summary>
public static class LightingSetup
{
    const string SkyboxPath = "Assets/Model/skybox/New Material.mat";
    const string SkyboxTexPath = "Assets/Model/skybox/freight_station_4k.exr";
    const string DesktopLightingPath = "Assets/Model/light bake/ware house light bake.lighting";
    const string VrLightingPath = "Assets/Model/light bake/VR light bake.lighting";
    const string VolumeProfilePath = "Assets/Settings/VRVolumeProfile.asset";

    [MenuItem("Vedanta VR/5 Setup Lighting")]
    public static void Apply()
    {
        SetupSkyboxAndEnvironment();
        SetupLights();
        MarkStatic();
        SetupReflectionProbe();
        SetupLightProbes();
        SetupLightingSettings();
        SetupPostProcessing();
        SetupPipelineAsset();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Vedanta VR: lighting set up. Bake with Window > Rendering > Lighting > Generate Lighting.");
    }

    // ------------------------------------------------------------------ skybox / ambient
    static void SetupSkyboxAndEnvironment()
    {
        // The 4k EXR is far too heavy for Quest: cap it.
        var ti = AssetImporter.GetAtPath(SkyboxTexPath) as TextureImporter;
        if (ti != null)
        {
            bool dirty = false;
            if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
            var android = ti.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.maxTextureSize != 1024)
            {
                android.overridden = true; android.maxTextureSize = 1024;
                ti.SetPlatformTextureSettings(android); dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
        }
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        if (sky) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.reflectionBounces = 1;
        RenderSettings.fog = false;
        RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f);
    }

    // ------------------------------------------------------------------ lights
    static void SetupLights()
    {
        // 1. main sun (realtime direct + baked indirect) lights the dynamic objects: crane, bag, hoods, tools
        var sunGO = GameObject.Find("Directional Light");
        Light sun = sunGO ? sunGO.GetComponent<Light>() : null;
        if (!sun) { sunGO = new GameObject("Directional Light"); sun = sunGO.AddComponent<Light>(); }
        sun.type = LightType.Directional;
        sun.lightmapBakeType = LightmapBakeType.Mixed;
        sun.color = Color.white;
        sun.intensity = 1.6f;
        sun.transform.SetPositionAndRotation(new Vector3(0.04f, 10.66f, 66.23f), Quaternion.Euler(51.8f, 4.4f, 0f));
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
        RenderSettings.sun = sun;

        // 2. baked lights copied from the desktop project
        MakeLight("Fill Light (Baked)", LightType.Directional, LightmapBakeType.Baked, 1.2f,
            new Vector3(0.04f, 10.66f, 20.08f), Quaternion.Euler(36.2f, 184.4f, 180f), Vector2.zero);
        MakeLight("Area Light (1)", LightType.Rectangle, LightmapBakeType.Baked, 0.3f,
            new Vector3(-0.42f, 14.69f, 53.5f), Quaternion.Euler(270f, 0f, 0f), new Vector2(27.64f, 132.64f));
        MakeLight("Area Light (2)", LightType.Rectangle, LightmapBakeType.Baked, 1f,
            new Vector3(-9.84f, 11.71f, 53.5f), Quaternion.Euler(0f, 270f, 90f), new Vector2(10.37f, 119.9f));
    }

    static Light MakeLight(string name, LightType type, LightmapBakeType bake, float intensity, Vector3 pos, Quaternion rot, Vector2 area)
    {
        var go = GameObject.Find(name);
        if (!go) go = new GameObject(name);
        var l = go.GetComponent<Light>();
        if (!l) l = go.AddComponent<Light>();
        l.type = type;
        l.lightmapBakeType = bake;
        l.color = Color.white;
        l.intensity = intensity;
        l.shadows = type == LightType.Rectangle ? LightShadows.Soft : LightShadows.None;
        go.transform.SetPositionAndRotation(pos, rot);
        if (type == LightType.Rectangle) l.areaSize = area;
        return l;
    }

    // ------------------------------------------------------------------ static flags (GI contributors only)
    static void MarkStatic()
    {
        var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;
        var roots = new List<GameObject>();
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == "POTLINE" || go.name == "Poster" || go.name == "Plane.019") roots.Add(go);
            if (go.name == "PotLeakage Environment")
                foreach (Transform c in go.transform)
                    if (c.name == "Warehouse" || c.name == "machine") roots.Add(c.gameObject);
        }
        int made = 0;
        foreach (var root in roots)
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = r.gameObject;
                bool moves = go.GetComponentInParent<Animator>() != null || go.GetComponentInParent<Rigidbody>() != null
                    || go.name == "Plane.020" || go.name == "Plane.023" || go.name == "Cube.016"
                    || go.name.StartsWith("HOLE_") || go.name.StartsWith("Lava_Hole") || go.name.StartsWith("round_PIPES");
                var cur = GameObjectUtility.GetStaticEditorFlags(go);
                var want = moves ? cur & ~StaticEditorFlags.ContributeGI & ~StaticEditorFlags.ReflectionProbeStatic : cur | flags;
                if (want != cur) { GameObjectUtility.SetStaticEditorFlags(go, want); made++; }
            }
        Debug.Log("Vedanta VR: static flags updated on " + made + " renderers.");
    }

    // ------------------------------------------------------------------ reflection / light probes
    static void SetupReflectionProbe()
    {
        var go = GameObject.Find("Reflection Probe");
        if (!go) go = new GameObject("Reflection Probe");
        var p = go.GetComponent<ReflectionProbe>();
        if (!p) p = go.AddComponent<ReflectionProbe>();
        go.transform.position = new Vector3(-0.3f, 8.6f, 19.9f);
        p.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
        p.resolution = 256;                 // 2048 in the desktop project; far too large for Quest
        p.size = new Vector3(49.69f, 24.36f, 254.29f);
        p.center = new Vector3(-0.48f, 1.86f, 14.05f);   // same local centre as the desktop probe
        p.intensity = 1f;
        p.boxProjection = true;
        p.hdr = false;
        p.importance = 1;
    }

    static void SetupLightProbes()
    {
        var go = GameObject.Find("Light Probes");
        if (!go) go = new GameObject("Light Probes");
        var g = go.GetComponent<LightProbeGroup>();
        if (!g) g = go.AddComponent<LightProbeGroup>();
        go.transform.position = Vector3.zero;
        var pts = new List<Vector3>();
        for (float x = -22f; x <= 22f; x += 4f)
            for (float z = 24f; z <= 56f; z += 4f)
                foreach (var y in new[] { 1.2f, 4f })
                    pts.Add(new Vector3(x, y, z));
        g.probePositions = pts.ToArray();
    }

    // ------------------------------------------------------------------ lighting settings (bake)
    static void SetupLightingSettings()
    {
        var vr = AssetDatabase.LoadAssetAtPath<LightingSettings>(VrLightingPath);
        if (!vr)
        {
            AssetDatabase.CopyAsset(DesktopLightingPath, VrLightingPath);
            vr = AssetDatabase.LoadAssetAtPath<LightingSettings>(VrLightingPath);
        }
        if (!vr) { Debug.LogWarning("Lighting settings asset missing."); return; }
        vr.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        vr.bakedGI = true;
        vr.realtimeGI = false;
        vr.mixedBakeMode = MixedLightingMode.IndirectOnly;
        vr.lightmapResolution = 10f;
        vr.lightmapMaxSize = 2048;
        vr.directSampleCount = 32;
        vr.indirectSampleCount = 128;
        vr.environmentSampleCount = 128;
        vr.maxBounces = 2;
        vr.ao = true;
        vr.directionalityMode = LightmapsMode.NonDirectional;   // cheaper on Quest
        vr.lightmapCompression = LightmapCompression.NormalQuality;
        vr.lightmapPadding = 2;
        EditorUtility.SetDirty(vr);
        Lightmapping.lightingSettings = vr;
    }

    // ------------------------------------------------------------------ post processing
    static void SetupPostProcessing()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }
        var tone = profile.TryGet(out UnityEngine.Rendering.Universal.Tonemapping t) ? t : profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.isGlobal) { v.sharedProfile = profile; v.weight = 1f; }   // no vignette/bloom/motion blur in VR

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var d = cam.GetComponent<UniversalAdditionalCameraData>();
            if (!d || cam.name.Contains("Preview")) continue;
            d.renderPostProcessing = true;
            d.antialiasing = AntialiasingMode.None;   // MSAA on the pipeline asset instead
        }
    }

    // ------------------------------------------------------------------ URP asset
    static void SetupPipelineAsset()
    {
        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if (!urp) return;
        urp.msaaSampleCount = 4;
        urp.shadowDistance = 30f;
        EditorUtility.SetDirty(urp);
        AssetDatabase.SaveAssets();
    }
}
