using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Loads training.json (display / speech text per task key, in English, Hindi and Odia) and exposes it by key or by the English
/// text. Reads persistentDataPath/TrainingData/training.json first (an updated copy can be dropped there on a PC without rebuilding) and
/// otherwise StreamingAssets/TrainingData/training.json. The language comes from LanguageService (chosen on the PPE scene).
/// The sequence helper calls GetDisplay(key) when a task starts so description/title stay in sync with the source project.</summary>
public class TrainingDataProvider : MonoBehaviour
{
    public static TrainingDataProvider instance;
    [Tooltip("Not used any more: the language is read from LanguageService (PlayerPrefs PotRedressing.Language set on the PPE scene).")]
    public string language = "en";

    [Serializable] class Loc { public string en, hi, or; }
    [Serializable] class Entry { public string key, voiceProfile; public Loc display; public Loc speech; }
    [Serializable] class File { public List<Entry> entries = new List<Entry>(); }

    readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Entry> byText = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
    public bool Ready { get; private set; }

    void Awake() { instance = this; LanguageService.Refresh(); StartCoroutine(Load()); }

    IEnumerator Load()
    {
        string json = null;
        var persistent = Path.Combine(Application.persistentDataPath, "TrainingData", "training.json");
        if (System.IO.File.Exists(persistent)) json = System.IO.File.ReadAllText(persistent);
        if (string.IsNullOrEmpty(json))
        {
            var path = Path.Combine(Application.streamingAssetsPath, "TrainingData", "training.json");
            if (path.Contains("://") || path.StartsWith("jar:"))
            {
                using var req = UnityWebRequest.Get(path);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) json = req.downloadHandler.text;
            }
            else if (System.IO.File.Exists(path)) json = System.IO.File.ReadAllText(path);
        }
        if (string.IsNullOrEmpty(json)) { Debug.LogWarning("TrainingData: training.json not found"); yield break; }
        var data = JsonUtility.FromJson<File>(json);
        if (data?.entries != null)
            foreach (var e in data.entries)
            {
                if (string.IsNullOrEmpty(e.key)) continue;
                byKey[e.key] = e;
                if (e.display != null && !string.IsNullOrEmpty(e.display.en)) byText[NormText(e.display.en)] = e;
                if (e.speech != null && !string.IsNullOrEmpty(e.speech.en) && !byText.ContainsKey(NormText(e.speech.en))) byText[NormText(e.speech.en)] = e;
            }
        Ready = true;
        LanguageService.Refresh();
    }

    static readonly Regex Tags = new Regex("<[^>]+>");
    static readonly Regex Ws = new Regex(@"\s+");
    static string NormText(string s) { return Ws.Replace(Tags.Replace(s.Replace("\\n", " "), ""), " ").Trim(); }

    static string Pick(Loc l)
    {
        if (l == null) return null;
        string s = LanguageService.Index == 1 ? l.hi : LanguageService.Index == 2 ? l.or : l.en;
        return string.IsNullOrEmpty(s) ? l.en : s;      // a missing translation falls back to English
    }

    public string GetDisplay(string key)
    {
        if (string.IsNullOrEmpty(key) || !byKey.TryGetValue(key, out var e) || e.display == null) return null;
        return Pick(e.display);
    }

    public string GetSpeech(string key)
    {
        if (string.IsNullOrEmpty(key) || !byKey.TryGetValue(key, out var e) || e.speech == null) return null;
        return Pick(e.speech);
    }

    /// <summary>Display text for an English text (tags, case and white space are ignored in the lookup). existsOnly: true when an entry exists,
    /// whatever the language.</summary>
    public bool TryDisplay(string english, out string result, bool existsOnly = false)
    {
        result = english;
        if (string.IsNullOrEmpty(english)) return false;
        Entry e;
        if (!byText.TryGetValue(NormText(english), out e) || e.display == null) return false;   // callers try prefix / tag variants next, so no log here
        if (existsOnly) return true;
        string s = Pick(e.display);
        if (string.IsNullOrEmpty(s)) return false;
        // the lookup ignores tags, so a text with its own tags keeps them (they are re-applied by LanguageService.Localize)
        result = s;
        return true;
    }

    /// <summary>Speech text for an English text (what the voice-over says), or null.</summary>
    public string SpeechForText(string english)
    {
        Entry e;
        if (string.IsNullOrEmpty(english) || !byText.TryGetValue(NormText(english), out e) || e.speech == null) return null;
        return Pick(e.speech);
    }
}
