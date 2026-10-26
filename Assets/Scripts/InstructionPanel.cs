using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>World-space Welcome / Instruction card parented to the left controller.
/// Layout matches the mock: logo top-left, "STEP x / N" top-right, title, body, yellow NEXT button bottom-right.</summary>
public class InstructionPanel : MonoBehaviour
{
    [Header("Header")]
    public Image logoImage;
    public Text stepText;
    public Text brandText;        // optional - e.g. "VEDANTA — Transforming for Good"
    public Text subHeaderText;    // optional - e.g. "VEDANTA ALUMINIUM — POTLINE OPERATIONS"

    [Header("Body")]
    public Text titleText;
    public Text bodyText;
    public Text progressText;

    [Header("Next button")]
    public Button nextButton;
    public Text nextButtonLabel;

    public event Action NextPressed;

    [Header("Step counter")]
    public int totalSteps = 28;

    void Awake()
    {
        HideNext();
    }

    void OnEnable() { HideNext(); }

    void Start() { HideNext(); }

    public void EnsureNextButtonListener() { HideNext(); }

    void HideNext()
    {
        if (nextButton)
        {
            nextButton.onClick.RemoveAllListeners();
            if (nextButton.gameObject.activeSelf) nextButton.gameObject.SetActive(false);
        }
    }

    /// <summary>The NEXT button has been removed: a task only ever finishes through its own interaction/animation.</summary>
    public void OnNextClicked() { }

    public void Show(string title, string body)
    {
        EnsureNextButtonListener();
        if (titleText) titleText.text = title;
        if (bodyText) bodyText.text = body;
    }

    public void SetStep(int step)
    {
        if (stepText) stepText.text = LanguageService.Format("STEP {0} / {1}", step.ToString("D2"), totalSteps.ToString("D2"));
    }

    public void SetTotalSteps(int total)
    {
        totalSteps = Mathf.Max(1, total);
    }

    public void SetProgress(string progress)
    {
        if (progressText) progressText.text = progress;
    }

    public void SetBrand(string brand, string subHeader)
    {
        if (brandText) brandText.text = brand;
        if (subHeaderText) subHeaderText.text = subHeader;
    }

    public void SetNextVisible(bool visible) { HideNext(); }

    public void SetNextInteractable(bool interactable)
    {
        if (nextButton) nextButton.interactable = interactable;
    }
}
