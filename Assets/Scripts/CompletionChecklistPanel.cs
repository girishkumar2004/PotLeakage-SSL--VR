using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>End-of-training checklist (TASK_TRAINING_COMPLETED). Pressing an item shows a green [√], turns the row green and adds
/// [VERIFIED]. Once all items are verified the MOVE TO QUIZ button lights up and opens the quiz. Items and buttons are plain UGUI driven by the XR UI ray.</summary>
public class CompletionChecklistPanel : MonoBehaviour
{
    public Button[] itemButtons;
    public TMP_Text[] itemTexts;
    public Image[] itemBackgrounds;
    public string[] itemLabels;
    public QuizPanel quizPanel;
    public Button quitButton;          // now the MOVE TO QUIZ button
    public Image quitBackground;
    public TMP_Text quitText;

    public float distance = 1.7f;
    public float heightOffset = -0.05f;
    public float worldScale = 0.0013f;

    public Color pendingColor = new Color(0.12f, 0.2f, 0.38f, 1f);
    public Color verifiedColor = new Color(0.08f, 0.42f, 0.2f, 1f);
    public Color quitOffColor = new Color(0.35f, 0.33f, 0.2f, 1f);
    public Color quitOnColor = new Color(0.93f, 0.78f, 0f, 1f);

    bool[] verified;
    bool wired;

    public void Show()
    {
        gameObject.SetActive(true);
        Place();
        ResetItems();
        if (!wired) Wire();
    }

    public void Hide() { gameObject.SetActive(false); }

    void Wire()
    {
        wired = true;
        for (int i = 0; i < itemButtons.Length; i++)
        {
            int index = i;
            itemButtons[i].onClick.AddListener(() => Verify(index));
        }
        quitButton.onClick.AddListener(Quit);
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

    void ResetItems()
    {
        verified = new bool[itemButtons.Length];
        for (int i = 0; i < itemButtons.Length; i++) Refresh(i);
        RefreshQuit();
    }

    void Verify(int i)
    {
        if (verified[i]) return;
        verified[i] = true;
        Refresh(i);
        RefreshQuit();
    }

    void Refresh(int i)
    {
        bool v = verified[i];
        itemTexts[i].text = v
            ? "<color=#3CDC5A>[√]</color>  " + LanguageService.Localize(itemLabels[i]) + "  <color=#3CDC5A>" + LanguageService.Localize("[VERIFIED]") + "</color>"
            : "[ ]  " + LanguageService.Localize(itemLabels[i]);
        itemBackgrounds[i].color = v ? verifiedColor : pendingColor;
    }

    bool AllVerified()
    {
        foreach (var v in verified) if (!v) return false;
        return true;
    }

    void RefreshQuit()
    {
        bool on = AllVerified();
        quitButton.interactable = on;
        quitBackground.color = on ? quitOnColor : quitOffColor;
        quitText.color = on ? Color.black : new Color(0.1f, 0.1f, 0.1f, 0.7f);
    }

    void Quit()
    {
        if (!AllVerified()) return;
        if (quizPanel)
        {
            Hide();
            quizPanel.Show();
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
