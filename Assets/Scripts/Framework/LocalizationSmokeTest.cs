using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Play-mode check of the Hindi / Odia support: for each language it starts every sequence in turn, records the title and description
/// shown on the instruction panel, which script they are written in (Devanagari / Odia / Latin) and the "[TTS RESOLVED]" status of every
/// voice-over that started, then scans the scene labels for texts that stayed English although a translation exists. The summary is
/// logged as "[I18N] ..." lines and kept in `report`. Add it to any object and call Run() (or tick runOnStart).</summary>
public class LocalizationSmokeTest : MonoBehaviour
{
    public bool runOnStart;
    public float secondsPerSequence = 1.2f;
    public int[] languages = { 0, 1, 2 };

    public string report;
    public bool finished;

    static readonly Regex Deva = new Regex("[ऀ-ॣ०-ॿ]"), Odia = new Regex("[଀-୿]"), Latin = new Regex("[A-Za-z]");   // the danda (U+0964/0965) is shared by both scripts, so it is not counted as Devanagari
    static readonly Regex TtsLine = new Regex(@"\[TTS RESOLVED\] key=(\S+) lang=(\S+) status=(\S+)");

    readonly List<string> ttsLines = new List<string>();
    int errors, exceptions;

    void Start() { if (runOnStart) Run(); }
    public void Run() { finished = false; report = ""; StartCoroutine(Go()); }

    void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Exception) { exceptions++; Debug.Log("[I18N] exception: " + condition); }
        else if (type == LogType.Error || type == LogType.Assert) { errors++; Debug.Log("[I18N] error: " + condition); }
        else if (condition.StartsWith("[TTS RESOLVED]")) ttsLines.Add(condition);
    }

    static string Script(string s)
    {
        if (string.IsNullOrEmpty(s)) return "empty";
        var parts = new List<string>();
        if (Deva.IsMatch(s)) parts.Add("Devanagari");
        if (Odia.IsMatch(s)) parts.Add("Odia");
        if (Latin.IsMatch(s)) parts.Add("Latin");
        return parts.Count == 0 ? "none" : string.Join("+", parts);
    }

    IEnumerator Go()
    {
        var sb = new StringBuilder();
        Application.logMessageReceived += OnLog;
        var h = SequenceHandler.instance;
        var panel = FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
        string[] langNames = { "English", "Hindi", "Odia" };
        foreach (int li in languages)
        {
            LanguageService.SetLanguage(li);
            yield return null;
            errors = exceptions = 0; ttsLines.Clear();
            int valid = 0, fallback = 0, missing = 0, wrongScript = 0, seqs = 0;
            sb.AppendLine("[I18N] ===== " + langNames[li] + " =====");
            for (int i = 0; i < h.sequenceList.Count; i++)
            {
                int before = ttsLines.Count;
                foreach (var tl in FindObjectsByType<TimedEvents>(FindObjectsInactive.Include, FindObjectsSortMode.None)) tl.Stop();   // timelines of the sequence before must not advance this one
                h.StartSequence(i);
                seqs++;
                yield return new WaitForSeconds(secondsPerSequence);
                string title = panel && panel.titleText ? panel.titleText.text : "", body = panel && panel.bodyText ? panel.bodyText.text : "";
                string st = Script(title), sd = Script(body);
                bool bad = li == 1 ? !(Deva.IsMatch(body)) : li == 2 ? !(Odia.IsMatch(body)) : !Latin.IsMatch(body);
                if (bad) wrongScript++;
                var statuses = new List<string>();
                for (int k = before; k < ttsLines.Count; k++)
                {
                    var m = TtsLine.Match(ttsLines[k]);
                    if (m.Success) { statuses.Add(m.Groups[1].Value + "=" + m.Groups[3].Value); if (m.Groups[3].Value == "VALID") valid++; else if (m.Groups[3].Value == "FALLBACK_EN") fallback++; else missing++; }
                }
                sb.AppendLine("[I18N] " + langNames[li] + " | " + h.sequenceList[i].name + " | title(" + st + ")='" + title + "' | desc(" + sd + ")" + (bad ? " WRONG-SCRIPT" : "") + "='" + (body.Length > 70 ? body.Substring(0, 70) + "..." : body) + "' | tts " + (statuses.Count == 0 ? "-" : string.Join(",", statuses)));
            }
            // labels that stayed English although there is a translation
            int untranslated = 0; var names = new List<string>();
            if (li != 0)
            {
                UiLocalizer.Instance?.ApplyAll();
                foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (t.gameObject.scene.isLoaded && t.GetComponentInParent<Canvas>(true) != null && t.GetComponentInParent<Canvas>(true).name != "PotControlDisplay" && LanguageService.HasTranslation(t.text) && !(li == 1 ? Deva : Odia).IsMatch(t.text)) { untranslated++; names.Add(t.name + "='" + t.text + "'"); }
                foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
                    if (t.gameObject.scene.isLoaded && LanguageService.HasTranslation(t.text) && !(li == 1 ? Deva : Odia).IsMatch(t.text)) { untranslated++; names.Add(t.name + "='" + t.text + "'"); }
            }
            sb.AppendLine("[I18N] " + langNames[li] + " SUMMARY sequences=" + seqs + " wrongScriptDescriptions=" + wrongScript + " tts VALID=" + valid + " FALLBACK_EN=" + fallback + " MISSING=" + missing + " untranslatedLabels=" + untranslated + (names.Count > 0 ? " [" + string.Join("; ", names) + "]" : "") + " errors=" + errors + " exceptions=" + exceptions);
        }
        Application.logMessageReceived -= OnLog;
        LanguageService.SetLanguage(0);
        report = sb.ToString();
        finished = true;
        Debug.Log(report);
    }
}
