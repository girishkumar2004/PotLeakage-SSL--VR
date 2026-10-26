using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Translates the labels of the scene (TMP and legacy UI Text on every canvas, active or not) into the chosen language, using
/// the English text as the key into training.json, and gives legacy Text a font that can show Hindi / Odia (TMP labels get the Hindi / Odia
/// fallback fonts from the TMP font asset). The control-panel display codes (PotControlDisplay, the machine button labels) stay English.
/// Put one on any object of the scene; labels that code sets later translate themselves through LanguageService.Localize.</summary>
public class UiLocalizer : MonoBehaviour
{
    public static UiLocalizer Instance { get; private set; }

    [Tooltip("Resources path of a font that has Latin, Devanagari and Odia glyphs (used by the legacy UI Text).")]
    public string indicFontResource = "Fonts/VR_NotoSansIndic";
    [Tooltip("Objects with these names (and everything below them) are never translated.")]
    public string[] excludeNames = { "PotControlDisplay" };

    readonly Dictionary<int, string> original = new Dictionary<int, string>();
    readonly Dictionary<int, string> written = new Dictionary<int, string>();
    readonly Dictionary<int, Font> originalFont = new Dictionary<int, Font>();
    Font indicFont;

    void Awake() { Instance = this; }
    void OnEnable() { Instance = this; LanguageService.Changed += ApplyAll; }
    void OnDisable() { LanguageService.Changed -= ApplyAll; }

    IEnumerator Start()
    {
        LanguageService.Refresh();
        float end = Time.realtimeSinceStartup + 10f;
        while ((TrainingDataProvider.instance == null || !TrainingDataProvider.instance.Ready) && Time.realtimeSinceStartup < end) yield return null;
        ApplyAll();
    }

    public void ApplyAll() { Apply(null); }

    /// <summary>Translates every text under `root` (the whole scene when null).</summary>
    public void Apply(Transform root)
    {
        if (indicFont == null) indicFont = Resources.Load<Font>(indicFontResource);
        foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            if (Eligible(t.gameObject, root, true)) ApplyText(t.GetInstanceID(), t.text, s => t.text = s);
        foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (!Eligible(t.gameObject, root, false)) continue;
            ApplyText(t.GetInstanceID(), t.text, s => t.text = s);
            ApplyFont(t);
        }
    }

    bool Eligible(GameObject go, Transform root, bool needsCanvas)
    {
        if (!go.scene.isLoaded) return false;                          // assets / prefabs
        if (root != null && !go.transform.IsChildOf(root)) return false;
        if (needsCanvas && go.GetComponentInParent<Canvas>(true) == null) return false;   // world-space machine labels (TextMeshPro) stay English
        for (var t = go.transform; t != null; t = t.parent)
            foreach (var n in excludeNames) if (t.name == n) return false;
        return true;
    }

    void ApplyText(int id, string current, System.Action<string> set)
    {
        string baseText, last;
        if (written.TryGetValue(id, out last) && last == current && original.ContainsKey(id)) baseText = original[id];
        else baseText = current;                                        // new text, or code has changed it since we wrote it
        if (!LanguageService.HasTranslation(baseText)) { original.Remove(id); written.Remove(id); return; }
        original[id] = baseText;
        string loc = LanguageService.Localize(baseText);
        if (loc != current) set(loc);
        written[id] = loc;
    }

    void ApplyFont(Text t)
    {
        int id = t.GetInstanceID();
        if (LanguageService.IsEnglish)
        {
            Font f;
            if (originalFont.TryGetValue(id, out f)) { t.font = f; originalFont.Remove(id); }
        }
        else if (indicFont != null && t.font != indicFont)
        {
            if (!originalFont.ContainsKey(id)) originalFont[id] = t.font;
            t.font = indicFont;
        }
    }
}
