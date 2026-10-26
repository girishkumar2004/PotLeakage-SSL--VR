using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Plays TTS clips cached under StreamingAssets/TTSCache. Keys are TASK_NN
/// (matches cache_manifest.json from the source project). Clips are loaded on demand;
/// on Android/Quest it reads via UnityWebRequest since StreamingAssets lives inside the APK.
/// The language (English / Hindi / Odia) comes from LanguageService; a key without a clip in that language falls back to the English clip.
/// Every Play logs "[TTS RESOLVED] key=... lang=... status=VALID|FALLBACK_EN|MISSING".</summary>
public class VoiceOverPlayer : MonoBehaviour
{
    public static VoiceOverPlayer instance;

    public AudioSource audioSource;
    [Tooltip("Not used any more: the language is read from LanguageService (chosen on the PPE scene).")]
    public string language = "English";
    public bool preloadOnStart = false;

    [Serializable] class Entry
    {
        public string key;
        public string language;
        public string hash;
        public string fileName;
        public string text;
    }
    [Serializable] class Manifest
    {
        public int audioPaddingVersion;
        public List<Entry> entries = new List<Entry>();
    }

    // language ("English" / "Hindi" / "Odia") -> key -> file / text
    readonly Dictionary<string, Dictionary<string, string>> keyToFile = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Dictionary<string, string>> keyToText = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>();
    bool manifestReady;
    Coroutine activePlayRoutine;
    Coroutine activeAdvanceRoutine;

    void Awake()
    {
        instance = this;
        LanguageService.Refresh();
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (!audioSource) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; }
        StartCoroutine(LoadManifest());
    }

    void OnEnable()  { SequenceHandler.OnTaskStarted += AutoPlayForTask; LanguageService.Changed += OnLanguageChanged; }
    void OnDisable() { SequenceHandler.OnTaskStarted -= AutoPlayForTask; LanguageService.Changed -= OnLanguageChanged; }

    void OnLanguageChanged() { StopCurrentPlayback(); }      // clips are cached per language, nothing else to reload

    /// <summary>Language of the clip that would play for this key: the chosen language, else English, else null.</summary>
    string ResolveLanguage(string key)
    {
        string lang = LanguageService.VoiceLanguage;
        if (HasFile(lang, key)) return lang;
        if (HasFile("English", key)) return "English";
        return null;
    }

    bool HasFile(string lang, string key)
    {
        Dictionary<string, string> d;
        return keyToFile.TryGetValue(lang, out d) && d.ContainsKey(key);
    }

    bool HasKey(string key) { return ResolveLanguage(key) != null; }

    void AutoPlayForTask(int sequenceIdx, Task task)
    {
        StopCurrentPlayback();
        if (task == null) return;

        string key = task.ttsTextOrKey;
        string seqName = "";
        var h = SequenceHandler.instance;
        Sequence seq = null;
        if (h != null && h.sequenceList != null && sequenceIdx >= 0 && sequenceIdx < h.sequenceList.Count)
        {
            seq = h.sequenceList[sequenceIdx];            // the running order, not the hierarchy child order
            if (seq) seqName = seq.name;
        }

        // Special handling for emergency communication: must use TASK_04 narration clip
        if (seqName.IndexOf("EMERGENCY_COMMUNICATION", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (task.TaskName != null && task.TaskName.IndexOf("Emergency Communication", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            key = "TASK_04";
        }

        if (string.IsNullOrEmpty(key))
        {
            if (!string.IsNullOrEmpty(seqName))
            {
                var m = System.Text.RegularExpressions.Regex.Match(seqName, @"TASK_(\d{1,2})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) key = "TASK_" + int.Parse(m.Groups[1].Value).ToString("D2");
            }
            if (string.IsNullOrEmpty(key)) key = task.TaskName;
        }

        if (!string.IsNullOrEmpty(key) && HasKey(key))
        {
            Play(key);
            // Auto-advance only when the task explicitly opts in. Tasks that need interaction leave AutoAdvanceAfterVoiceOver = false.
            if ((task.IsAutoAdvance || (seq != null && seq.autoAdvanceAfterVoice)) && SequenceHandler.instance != null)
            {
                activeAdvanceRoutine = StartCoroutine(AdvanceWhenDone(SequenceHandler.instance.TaskToken, key));
            }
        }
    }

    /// <summary>Advances the sequence when the current VO finishes, if that same task is still the one playing.</summary>
    System.Collections.IEnumerator AdvanceWhenDone(int token, string expectedKey)
    {
        // Wait until manifest is ready and the expected clip actually starts playing
        while (activePlayRoutine != null) yield return null;
        while (audioSource != null && audioSource.isPlaying && audioSource.clip != null && audioSource.clip.name == expectedKey)
        {
            yield return null;
        }

        var h = SequenceHandler.instance;
        if (h != null && h.TaskToken == token)
        {
            h.CompleteCurrentTask();
        }
    }

    IEnumerator LoadManifest()
    {
        var path = Path.Combine(Application.streamingAssetsPath, "TTSCache", "cache_manifest.json");
        string json = null;
        if (path.Contains("://") || path.StartsWith("jar:"))
        {
            using var req = UnityWebRequest.Get(path);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) json = req.downloadHandler.text;
        }
        else if (File.Exists(path)) json = File.ReadAllText(path);

        if (string.IsNullOrEmpty(json)) { Debug.LogWarning("VoiceOverPlayer: cache_manifest.json missing"); yield break; }
        var m = JsonUtility.FromJson<Manifest>(json);
        if (m?.entries != null)
            foreach (var e in m.entries)
            {
                if (string.IsNullOrEmpty(e.language) || string.IsNullOrEmpty(e.key)) continue;
                Dictionary<string, string> files, texts;
                if (!keyToFile.TryGetValue(e.language, out files)) { files = new Dictionary<string, string>(); keyToFile[e.language] = files; }
                if (!keyToText.TryGetValue(e.language, out texts)) { texts = new Dictionary<string, string>(); keyToText[e.language] = texts; }
                files[e.key] = e.fileName; texts[e.key] = e.text;
            }
        manifestReady = true;
        if (preloadOnStart)
        {
            Dictionary<string, string> cur;
            if (keyToFile.TryGetValue(LanguageService.VoiceLanguage, out cur)) foreach (var k in cur.Keys) StartCoroutine(LoadClip(k, null));
        }
    }

    /// <summary>Plays the clip for a task key (e.g. "TASK_01") in the chosen language. No-op if not found.</summary>
    public void Play(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (activePlayRoutine != null) { StopCoroutine(activePlayRoutine); activePlayRoutine = null; }
        activePlayRoutine = StartCoroutine(PlayRoutine(key));
    }

    /// <summary>True while a clip is loading or playing.</summary>
    public bool IsPlaying { get { return activePlayRoutine != null || (audioSource != null && audioSource.isPlaying); } }

    /// <summary>Plays the recorded clip whose English manifest text equals `text` (case-insensitive), in the chosen language.</summary>
    public void PlayByText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        Dictionary<string, string> english;
        if (keyToText.TryGetValue("English", out english))
            foreach (var kv in english)
                if (string.Equals(kv.Value, text, StringComparison.OrdinalIgnoreCase)) { Play(kv.Key); return; }
        Debug.LogWarning("VoiceOverPlayer: no recorded clip with text '" + text + "'");
    }

    public void Stop()
    {
        StopCurrentPlayback();
    }

    public void StopCurrentPlayback()
    {
        if (activePlayRoutine != null) { StopCoroutine(activePlayRoutine); activePlayRoutine = null; }
        if (activeAdvanceRoutine != null) { StopCoroutine(activeAdvanceRoutine); activeAdvanceRoutine = null; }
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
    }

    string Resolve(string key, out string lang)
    {
        lang = ResolveLanguage(key);
        string status = lang == null ? "MISSING" : (lang == LanguageService.VoiceLanguage ? "VALID" : "FALLBACK_EN");
        Debug.Log("[TTS RESOLVED] key=" + key + " lang=" + LanguageService.VoiceLanguage + " status=" + status + (lang != null ? " file=" + keyToFile[lang][key] : ""));
        return lang == null ? null : keyToFile[lang][key];
    }

    public IEnumerator PlayRoutineAndWait(string key)
    {
        if (string.IsNullOrEmpty(key)) yield break;
        while (!manifestReady) yield return null;
        string lang;
        if (Resolve(key, out lang) == null) { Debug.LogWarning($"VoiceOverPlayer: no clip for key '{key}'"); yield break; }
        string cacheKey = lang + ":" + key;
        if (!loaded.TryGetValue(cacheKey, out var clip))
        {
            yield return LoadClip(key, c => clip = c, lang);
            if (clip) loaded[cacheKey] = clip;
        }
        if (clip && audioSource)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
            while (audioSource != null && audioSource.isPlaying) yield return null;
        }
    }

    IEnumerator PlayRoutine(string key)
    {
        while (!manifestReady) yield return null;
        string lang;
        if (Resolve(key, out lang) == null) { Debug.LogWarning($"VoiceOverPlayer: no clip for key '{key}'"); activePlayRoutine = null; yield break; }
        string cacheKey = lang + ":" + key;
        if (!loaded.TryGetValue(cacheKey, out var clip))
        {
            yield return LoadClip(key, c => clip = c, lang);
            if (clip) loaded[cacheKey] = clip;
        }
        if (clip && audioSource)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }
        activePlayRoutine = null;
    }

    IEnumerator LoadClip(string key, Action<AudioClip> onDone, string lang = null)
    {
        if (lang == null) lang = ResolveLanguage(key);
        if (lang == null) { onDone?.Invoke(null); yield break; }
        var file = keyToFile[lang][key];
        var path = Path.Combine(Application.streamingAssetsPath, "TTSCache", file);
        string uri = (path.Contains("://") || path.StartsWith("jar:")) ? path : "file://" + path;
        using var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"VoiceOverPlayer: failed to load {uri} ({req.error})"); onDone?.Invoke(null); yield break; }
        var c = DownloadHandlerAudioClip.GetContent(req);
        if (c) c.name = key;
        onDone?.Invoke(c);
    }
}
