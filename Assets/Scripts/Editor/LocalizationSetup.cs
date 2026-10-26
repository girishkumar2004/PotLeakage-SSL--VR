using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>One-time setup of the Hindi / Odia support in the working copy scene (PotLeakage-(VR)_Events.unity): the Indic font (Latin +
/// Devanagari + Odia) as a TMP fallback of the default TMP font and as the legacy UI Text font, the UiLocalizer, and LocalizeOnEnable on every
/// canvas. Safe to run again. The original scene is not touched.</summary>
public static class LocalizationSetup
{
    const string SourceFont = @"E:\com.madrasmindworks.ppe-training\Content\Fonts\NotoSansDevanagari-Regular.ttf";   // has Latin, Devanagari and Odia glyphs
    const string FontPath = "Assets/Resources/Fonts/VR_NotoSansIndic.ttf";
    const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/VR_NotoSansIndic SDF.asset";
    const string DefaultTmpFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Vedanta VR/18 Localization Setup (Events scene)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("_Events.unity")) { Debug.LogError("Localization setup only runs on PotLeakage-(VR)_Events.unity (active: " + scene.path + ")."); return; }

        // 1. the font file
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Fonts")) AssetDatabase.CreateFolder("Assets/Resources", "Fonts");
        if (!File.Exists(FontPath))
        {
            if (!File.Exists(SourceFont)) { Debug.LogError("Indic font not found: " + SourceFont); return; }
            File.Copy(SourceFont, FontPath);
            AssetDatabase.ImportAsset(FontPath);
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        // 2. TMP font asset (dynamic atlas) and fallback entry of the default TMP font
        var tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        if (tmp == null)
        {
            tmp = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            tmp.name = "VR_NotoSansIndic SDF";
            AssetDatabase.CreateAsset(tmp, TmpFontPath);
            AssetDatabase.AddObjectToAsset(tmp.material, tmp);
            foreach (var tex in tmp.atlasTextures) AssetDatabase.AddObjectToAsset(tex, tmp);
            EditorUtility.SetDirty(tmp);
        }
        var def = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultTmpFont);
        if (def != null)
        {
            if (def.fallbackFontAssetTable == null) def.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!def.fallbackFontAssetTable.Contains(tmp))
            {
                string bk = "Assets/_Backup/LiberationSans_SDF_before_i18n.asset";
                if (!File.Exists(bk)) AssetDatabase.CopyAsset(DefaultTmpFont, bk);
                def.fallbackFontAssetTable.Add(tmp);
                EditorUtility.SetDirty(def);
            }
        }
        AssetDatabase.SaveAssets();

        // 3. scene: UiLocalizer next to the sequence handler, LocalizeOnEnable on each canvas
        var helper = Object.FindFirstObjectByType<SequenceHelperFunctions>();
        var host = helper ? helper.gameObject : new GameObject("UiLocalizer");
        if (!host.GetComponent<UiLocalizer>()) host.AddComponent<UiLocalizer>();
        int canvases = 0;
        foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (!c.gameObject.scene.isLoaded || !c.isRootCanvas) continue;
            if (c.name == "PotControlDisplay") continue;        // machine display codes stay English
            if (!c.GetComponent<LocalizeOnEnable>()) c.gameObject.AddComponent<LocalizeOnEnable>();
            canvases++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Vedanta VR: localization set up (UiLocalizer on " + host.name + ", LocalizeOnEnable on " + canvases + " canvases, TMP fallback + legacy Text font).");
    }
}
