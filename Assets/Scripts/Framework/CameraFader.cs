using System;
using System.Collections;
using UnityEngine;

/// <summary>Short comfort fade used around rig snaps (fade to black, move, fade back; ~0.15 s total). Never a glide.
/// Lives on the head camera. The black quad sits just in front of the lens and uses Resources/VRFadeQuad (a Resources material, so its
/// shader is always in the build).</summary>
public class CameraFader : MonoBehaviour
{
    public static CameraFader instance;

    public float totalDuration = 0.15f;
    public float quadDistance = 0.12f;

    Renderer quad;
    Material mat;
    Coroutine routine;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        instance = this;
        var template = Resources.Load<Material>("VRFadeQuad");
        if (!template) { enabled = false; return; }
        mat = new Material(template);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FadeQuad";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, quadDistance);
        go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        quad = go.GetComponent<Renderer>();
        quad.sharedMaterial = mat;
        quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quad.receiveShadows = false;
        SetAlpha(0f);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void SetAlpha(float a)
    {
        if (!quad) return;
        quad.enabled = a > 0.001f;
        var c = mat.GetColor(BaseColor); c.a = a; mat.SetColor(BaseColor, c);
    }

    /// <summary>Fades out, runs `action` (the snap) while the screen is black, then fades back in.</summary>
    public void FadeThen(Action action)
    {
        if (!isActiveAndEnabled || !quad) { action?.Invoke(); return; }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(action));
    }

    IEnumerator Run(Action action)
    {
        float half = Mathf.Max(0.02f, totalDuration * 0.5f);
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime) { SetAlpha(t / half); yield return null; }
        SetAlpha(1f);
        action?.Invoke();
        yield return null;
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime) { SetAlpha(1f - t / half); yield return null; }
        SetAlpha(0f);
        routine = null;
    }
}
