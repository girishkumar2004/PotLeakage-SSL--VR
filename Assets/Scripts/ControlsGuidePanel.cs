using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

/// <summary>First-run panel showing controller button mapping. The Continue button hides it,
/// snaps the player to the Welcome spawn point, and starts the SequenceHandler.</summary>
public class ControlsGuidePanel : MonoBehaviour
{
    public Button continueButton;
    public SequenceHandler sequenceHandler;
    public Transform welcomeSpawn;
    public XROrigin xrOrigin;
    public float faceDistance = 1.8f;

    void Awake()
    {
        if (!sequenceHandler) sequenceHandler = FindFirstObjectByType<SequenceHandler>();
        if (!xrOrigin) xrOrigin = FindFirstObjectByType<XROrigin>();
        if (continueButton) continueButton.onClick.AddListener(Continue);
        if (sequenceHandler) sequenceHandler.autostart = false; // we start it after Continue
    }

    IEnumerator Start()
    {
        // Face the user on spawn
        yield return null;
        if (xrOrigin)
        {
            var head = xrOrigin.Camera.transform;
            transform.position = head.position + head.forward * faceDistance;
            transform.rotation = Quaternion.LookRotation(transform.position - head.position);
        }
    }

    public void Continue()
    {
        if (xrOrigin && welcomeSpawn)
        {
            var fwd = Vector3.ProjectOnPlane(welcomeSpawn.forward, Vector3.up);
            if (fwd.sqrMagnitude > 0.0001f) xrOrigin.MatchOriginUpCameraForward(Vector3.up, fwd.normalized);
            var cam = xrOrigin.Camera.transform.position;
            xrOrigin.MoveCameraToWorldLocation(new Vector3(welcomeSpawn.position.x, cam.y, welcomeSpawn.position.z));
        }
        gameObject.SetActive(false);
        if (sequenceHandler) sequenceHandler.StartSequence(0);
    }
}
