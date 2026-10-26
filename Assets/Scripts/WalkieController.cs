using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Walkie-talkie flow on the WalkieCommunicationCanvas: a slide-to-call step (WalkieCommunicationPanel),
/// then a radio transmission (WalkieConversationPanel) with the Shift and Technical In-charge bubbles and a CONTINUE button.
/// The slider and button are plain UGUI, driven by the controllers' ray through the XR UI input module.
/// OnConversationComplete fires when CONTINUE is pressed after both voice-overs have played.</summary>
public class WalkieController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject callPanel;
    public GameObject conversationPanel;

    [Header("Call step")]
    public Slider callSlider;
    public TMP_Text sliderLabel;
    [Range(0f, 1f)] public float completeSliderAt = 0.9f;
    public AudioClip callSFX;
    public AudioClip messageSFX;

    [Header("Conversation step")]
    public GameObject shiftBubble;
    public GameObject technicalBubble;
    public Button continueButton;
    public string shiftVoiceKey = "POTLEAKAGE_WALKIE_SHIFT_INCHARGE";
    public string technicalVoiceKey = "POTLEAKAGE_WALKIE_TECHNICAL_INCHARGE";

    public System.Action OnCallComplete;
    public System.Action OnConversationComplete;

    AudioSource audioSrc;
    bool callDone;
    Coroutine resetRoutine;

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (!audioSrc) { audioSrc = gameObject.AddComponent<AudioSource>(); audioSrc.playOnAwake = false; }
    }

    public void BeginCall()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        StopAllCoroutines();
        callDone = false;
        if (conversationPanel) conversationPanel.SetActive(false);
        if (shiftBubble) shiftBubble.SetActive(false);
        if (technicalBubble) technicalBubble.SetActive(false);
        if (continueButton) continueButton.gameObject.SetActive(false);
        if (callPanel) callPanel.SetActive(true);
        if (sliderLabel) sliderLabel.text = LanguageService.Localize("SLIDE TO CALL >>>");

        if (callSlider)
        {
            callSlider.interactable = true;
            callSlider.value = 0f;
            callSlider.onValueChanged.RemoveListener(OnSliderChanged);
            callSlider.onValueChanged.AddListener(OnSliderChanged);
            var rel = callSlider.GetComponent<WalkieSliderRelease>();
            if (!rel) rel = callSlider.gameObject.AddComponent<WalkieSliderRelease>();
            rel.owner = this;
        }
    }

    void OnSliderChanged(float v)
    {
        if (callDone) return;
        if (v >= completeSliderAt) TriggerCall();
    }

    /// <summary>Called when the pointer lets go of the slider: springs back unless the call was already placed.</summary>
    public void OnSliderReleased()
    {
        if (callDone || !callSlider) return;
        if (resetRoutine != null) StopCoroutine(resetRoutine);
        resetRoutine = StartCoroutine(SpringBack());
    }

    IEnumerator SpringBack()
    {
        while (callSlider && !callDone && callSlider.value > 0.001f)
        {
            callSlider.SetValueWithoutNotify(Mathf.MoveTowards(callSlider.value, 0f, Time.unscaledDeltaTime * 2.5f));
            yield return null;
        }
        resetRoutine = null;
    }

    public void TriggerCall()
    {
        if (callDone) return;
        callDone = true;
        if (resetRoutine != null) { StopCoroutine(resetRoutine); resetRoutine = null; }
        if (callSlider) { callSlider.SetValueWithoutNotify(1f); callSlider.interactable = false; }
        if (sliderLabel) sliderLabel.text = LanguageService.Localize("CALLING...");
        if (callSFX && audioSrc) audioSrc.PlayOneShot(callSFX);
        OnCallComplete?.Invoke();
        StartCoroutine(ShowConversation());
    }

    IEnumerator ShowConversation()
    {
        yield return new WaitForSeconds(0.6f);
        if (callPanel) callPanel.SetActive(false);
        if (conversationPanel) conversationPanel.SetActive(true);

        yield return Speak(shiftBubble, shiftVoiceKey);
        yield return Speak(technicalBubble, technicalVoiceKey);

        if (continueButton)
        {
            continueButton.gameObject.SetActive(true);
            continueButton.onClick.RemoveListener(OnContinue);
            continueButton.onClick.AddListener(OnContinue);
        }
        else OnContinue();
    }

    IEnumerator Speak(GameObject bubble, string key)
    {
        if (bubble) bubble.SetActive(true);
        if (messageSFX && audioSrc) audioSrc.PlayOneShot(messageSFX);
        if (VoiceOverPlayer.instance && !string.IsNullOrEmpty(key)) yield return VoiceOverPlayer.instance.PlayRoutineAndWait(key);
        else yield return new WaitForSeconds(3f);
        yield return new WaitForSeconds(0.4f);
    }

    void OnContinue()
    {
        if (continueButton) continueButton.interactable = false;
        if (conversationPanel) conversationPanel.SetActive(false);
        OnConversationComplete?.Invoke();
    }
}
