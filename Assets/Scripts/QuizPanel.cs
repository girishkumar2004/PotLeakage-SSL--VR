using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>End-of-training quiz shown after the completion checklist. Questions come from StreamingAssets/TrainingData/quiz.json
/// (edit that file to change the quiz). An answer turns green (correct) or red (wrong, with the right one in green) and shows
/// Next Question; after the last question the result (score, PASS/FAIL, Retry / Exit) is shown. Plain UGUI driven by the XR UI ray,
/// so it works with the controllers in a standalone/Quest build.</summary>
public class QuizPanel : MonoBehaviour
{
    [Serializable] public class QuestionData { public string question; public string[] options; public int answerIndex; }
    [Serializable] public class QuestionTr { public string question; public string[] options; }
    [Serializable] public class QuizTr { public string title, resultTitle; public QuestionTr[] questions; }
    [Serializable] public class QuizTrs { public QuizTr hi, or; }
    [Serializable] public class QuizData
    {
        public string title = "Pot Leakage Standard Operations";
        public string resultTitle = "Pot Leakage Assessment Result";
        public int passPercent = 70;
        public QuestionData[] questions;
        public QuizTrs translations;           // Hindi / Odia text of the title, result title, questions and options (same order as the questions)
    }

    public string jsonFileName = "quiz.json";

    [Header("Header")]
    public TMP_Text subtitleText, counterText;

    [Header("Question view")]
    public GameObject questionView;
    public TMP_Text questionText, feedbackText;
    public Button[] optionButtons;
    public Image[] optionImages;
    public TMP_Text[] optionTexts;
    public Button nextButton;
    public TMP_Text nextButtonText;

    [Header("Result view")]
    public GameObject resultView;
    public TMP_Text resultTitleText, scoreText, statusText, resultNoteText;
    public Button retryButton, exitButton;

    [Header("Placement")]
    public float distance = 1.7f, heightOffset = -0.05f, worldScale = 0.0013f;

    public Color optionColor = new Color(0.79f, 0.81f, 0.86f, 1f);
    public Color correctColor = new Color(0.27f, 0.79f, 0.36f, 1f);
    public Color wrongColor = new Color(0.88f, 0.25f, 0.29f, 1f);

    QuizData data;
    int index, score;
    bool answered, wired;

    public void Show()
    {
        gameObject.SetActive(true);
        Place();
        if (!wired) Wire();
        StopAllCoroutines();
        StartCoroutine(LoadAndStart());
    }

    public void Hide() { gameObject.SetActive(false); }

    void Wire()
    {
        wired = true;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int n = i;
            optionButtons[i].onClick.AddListener(() => OnOption(n));
        }
        nextButton.onClick.AddListener(OnNext);
        retryButton.onClick.AddListener(Restart);
        exitButton.onClick.AddListener(Exit);
    }

    void Place()
    {
        var cam = Camera.main ? Camera.main.transform : null;
        if (!cam) return;
        var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.001f) fwd = cam.forward;
        fwd.Normalize();
        transform.position = cam.position + fwd * distance + Vector3.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        transform.localScale = Vector3.one * worldScale;
        var canvas = GetComponent<Canvas>();
        if (canvas) canvas.worldCamera = Camera.main;
    }

    IEnumerator LoadAndStart()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "TrainingData", jsonFileName);
        string json = null;
        if (path.Contains("://") || path.StartsWith("jar:"))
        {
            using (var req = UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) json = req.downloadHandler.text;
            }
        }
        else if (File.Exists(path)) json = File.ReadAllText(path);

        data = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { data = JsonUtility.FromJson<QuizData>(json); } catch (Exception e) { Debug.LogWarning("Quiz: bad json - " + e.Message); }
        }
        if (data == null || data.questions == null || data.questions.Length == 0)
        {
            ShowQuestionView();
            questionText.text = "Quiz data not found (StreamingAssets/TrainingData/" + jsonFileName + ").";
            foreach (var b in optionButtons) b.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            feedbackText.text = "";
            yield break;
        }
        if (subtitleText && !string.IsNullOrEmpty(data.title)) subtitleText.text = Tr() != null && !string.IsNullOrEmpty(Tr().title) ? Tr().title : data.title;
        Restart();
    }

    // ---- language: the quiz text of the chosen language, the English text when there is none
    QuizTr Tr()
    {
        if (data == null || data.translations == null) return null;
        return LanguageService.Index == 1 ? data.translations.hi : LanguageService.Index == 2 ? data.translations.or : null;
    }

    string QuestionOf(int i)
    {
        var t = Tr();
        return t != null && t.questions != null && i < t.questions.Length && !string.IsNullOrEmpty(t.questions[i].question) ? t.questions[i].question : data.questions[i].question;
    }

    string OptionOf(int q, int o)
    {
        var t = Tr();
        if (t != null && t.questions != null && q < t.questions.Length && t.questions[q].options != null && o < t.questions[q].options.Length
            && !string.IsNullOrEmpty(t.questions[q].options[o])) return t.questions[q].options[o];
        return data.questions[q].options[o];
    }

    void Restart()
    {
        index = 0; score = 0;
        ShowQuestion();
    }

    void ShowQuestionView() { questionView.SetActive(true); resultView.SetActive(false); }

    void ShowQuestion()
    {
        ShowQuestionView();
        var q = data.questions[index];
        answered = false;
        counterText.text = LanguageService.Format("Question {0} / {1}", index + 1, data.questions.Length);
        questionText.text = "Q" + (index + 1) + ".  " + QuestionOf(index);
        feedbackText.text = LanguageService.Localize("Select the correct answer.");
        nextButton.gameObject.SetActive(false);
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool has = q.options != null && i < q.options.Length;
            optionButtons[i].gameObject.SetActive(has);
            if (!has) continue;
            optionTexts[i].text = (char)('A' + i) + ".  " + OptionOf(index, i);
            optionImages[i].color = optionColor;
            optionButtons[i].transition = Selectable.Transition.ColorTint;
        }
    }

    void OnOption(int i)
    {
        if (answered || data == null) return;
        answered = true;
        var q = data.questions[index];
        bool correct = i == q.answerIndex;
        if (correct) score++;
        foreach (var b in optionButtons) b.transition = Selectable.Transition.None;
        optionImages[i].color = correct ? correctColor : wrongColor;
        if (!correct && q.answerIndex >= 0 && q.answerIndex < optionImages.Length) optionImages[q.answerIndex].color = correctColor;
        feedbackText.text = correct
            ? "<color=#45C95A><b>" + LanguageService.Localize("Correct!") + "</b></color>"
            : "<color=#E0414A><b>" + LanguageService.Localize("Incorrect.") + "</b></color>  " + LanguageService.Localize("Correct answer:") + " <b>" + (char)('A' + q.answerIndex) + "</b>";
        bool last = index >= data.questions.Length - 1;
        nextButtonText.text = LanguageService.Localize(last ? "SHOW RESULT" : "NEXT QUESTION");
        nextButton.gameObject.SetActive(true);
    }

    void OnNext()
    {
        if (!answered) return;
        if (index < data.questions.Length - 1) { index++; ShowQuestion(); }
        else ShowResult();
    }

    void ShowResult()
    {
        questionView.SetActive(false);
        resultView.SetActive(true);
        int total = data.questions.Length;
        int percent = Mathf.RoundToInt(100f * score / Mathf.Max(1, total));
        bool pass = percent >= data.passPercent;
        counterText.text = LanguageService.Format("{0} / {1} correct", score, total);
        resultTitleText.text = Tr() != null && !string.IsNullOrEmpty(Tr().resultTitle) ? Tr().resultTitle
            : (string.IsNullOrEmpty(data.resultTitle) ? LanguageService.Localize("Assessment Result") : data.resultTitle);
        scoreText.text = LanguageService.Format("Score: {0}%", percent);
        statusText.text = LanguageService.Format("Status: {0}", pass
            ? "<color=#45C95A>" + LanguageService.Localize("PASS") + "</color>"
            : "<color=#FF4D5A>" + LanguageService.Localize("FAIL") + "</color>");
        resultNoteText.text = pass
            ? LanguageService.Localize("Well done. You have completed the Pot Leakage training.")
            : LanguageService.Format("Pass mark is {0}%. Review the procedure and retry the quiz.", data.passPercent);
        retryButton.gameObject.SetActive(!pass);
    }

    void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
