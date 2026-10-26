using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>The language of the training (English / Hindi / Odia). The player picks it on the PPE scene; the PPE package keeps it in
/// PlayerPrefs "PotRedressing.Language" (0 = English, 1 = Hindi, 2 = Odia) and in its static Loc.Current, so this class only reads the
/// PlayerPrefs value (no assembly reference to the package) and, when the language is changed here, writes the same value and tells Loc
/// through reflection, keeping both in sync. Texts are looked up by their English wording in training.json (see TrainingDataProvider).</summary>
public static class LanguageService
{
    public const string PrefKey = "PotRedressing.Language";
    public static event Action Changed;

    static int cached = -1;

    public static int Index { get { if (cached < 0) cached = Read(); return cached; } }
    /// <summary>"en", "hi" or "or" - the field names used by training.json.</summary>
    public static string Code { get { return Index == 1 ? "hi" : Index == 2 ? "or" : "en"; } }
    /// <summary>"English", "Hindi" or "Odia" - the language names used by cache_manifest.json.</summary>
    public static string VoiceLanguage { get { return Index == 1 ? "Hindi" : Index == 2 ? "Odia" : "English"; } }
    public static bool IsEnglish { get { return Index == 0; } }

    static int Read()
    {
        try { return Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2); } catch { return 0; }
    }

    /// <summary>Re-reads the language chosen on the PPE scene (call when a scene starts).</summary>
    public static void Refresh()
    {
        int now = Read();
        if (now == cached) return;
        cached = now;
        if (Changed != null) Changed();
    }

    /// <summary>Changes the language: stores it where the PPE scene keeps it and keeps Loc.Current in sync.</summary>
    public static void SetLanguage(int index)
    {
        index = Mathf.Clamp(index, 0, 2);
        try { PlayerPrefs.SetInt(PrefKey, index); PlayerPrefs.Save(); } catch { }
        SyncLoc(index);
        cached = index;
        if (Changed != null) Changed();
    }

    public static void SetLanguage(string code)
    {
        SetLanguage(code == "hi" ? 1 : (code == "or" || code == "od") ? 2 : 0);
    }

    static void SyncLoc(int index)
    {
        try
        {
            // Loc / AppLang live in the PPE package assembly, which this project does not reference at compile time
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var loc = asm.GetType("Loc"); var lang = asm.GetType("AppLang");
                if (loc == null || lang == null) continue;
                var prop = loc.GetProperty("Current");
                if (prop != null) prop.SetValue(null, Enum.ToObject(lang, index), null);
                return;
            }
        }
        catch (Exception e) { Debug.LogWarning("[i18n] could not sync Loc.Current: " + e.Message); }
    }

    // ---------------------------------------------------------------- texts
    static readonly Regex ListPrefix = new Regex(@"^(\[[^\]]*\]\s+)(.*)$", RegexOptions.Singleline);
    static readonly Regex KeyedRow = new Regex(@"^(<color=[^>]*>[^<]*</color>\s+)(.*)$", RegexOptions.Singleline);
    static readonly Regex TaskNumber = new Regex(@"^\d+\s*[–-]\s*(.*)$", RegexOptions.Singleline);
    static readonly Regex TagWrap =new Regex(@"^((?:<[^>]+>)*)(.*?)((?:</[^>]+>)*)$", RegexOptions.Singleline);

    /// <summary>Text in the current language for an English text; the English text itself when there is no translation. Rich-text
    /// tags around the text, a "[ ]  " list prefix and a "<color>KEY</color>  description" row layout are kept.</summary>
    public static string Localize(string english)
    {
        if (string.IsNullOrEmpty(english) || IsEnglish) return english;
        var p = TrainingDataProvider.instance;
        if (p == null || !p.Ready) return english;
        string s;
        var w = TagWrap.Match(english);                       // tags around the whole text are kept: <color=red><b>text</b></color>
        string pre = w.Success ? w.Groups[1].Value : "", core = w.Success ? w.Groups[2].Value : english, post = w.Success ? w.Groups[3].Value : "";
        if (p.TryDisplay(core, out s)) return pre + s + post;
        var m = ListPrefix.Match(english);
        if (m.Success) return m.Groups[1].Value + Localize(m.Groups[2].Value);
        m = KeyedRow.Match(english);
        if (m.Success) return m.Groups[1].Value + Localize(m.Groups[2].Value);
        m = TaskNumber.Match(english);                        // "01 – Welcome" (a task name used as the title): shown without the number, as on the desktop
        if (m.Success && p.TryDisplay(m.Groups[1].Value, out s)) return s;
        return english;
    }

    /// <summary>True when Localize would change this text (used to decide which scene labels are translatable).</summary>
    public static bool HasTranslation(string english)
    {
        var p = TrainingDataProvider.instance;
        if (p == null || !p.Ready || string.IsNullOrEmpty(english)) return false;
        string s;
        var w = TagWrap.Match(english);
        if (p.TryDisplay(w.Success ? w.Groups[2].Value : english, out s, true)) return true;
        var m = ListPrefix.Match(english);
        if (m.Success) return HasTranslation(m.Groups[2].Value);
        m = KeyedRow.Match(english);
        return m.Success && HasTranslation(m.Groups[2].Value);
    }

    /// <summary>Localized format string ("STEP {0} / {1}") filled with the arguments.</summary>
    public static string Format(string englishFormat, params object[] args)
    {
        return string.Format(Localize(englishFormat), args);
    }
}
