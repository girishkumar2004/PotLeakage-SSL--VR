using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Pulsing green highlight for every mesh renderer under this object (colour #33D959). While highlighted, the renderer's
/// materials are swapped for instances of Resources/VRHighlight_Material (URP/Lit, transparent, metallic, green emission) and put
/// back afterwards. The material lives in Resources, so it and its shader variants are always included in a standalone/Quest build.
/// Also reacts to XR hover.</summary>
public class VRHighlight : MonoBehaviour
{
    public const string MaterialResource = "VRHighlight_Material";

    public float pulseSpeed = 3f;
    public Vector2 alphaRange = new Vector2(0.55f, 0.85f);
    public Vector2 emissionRange = new Vector2(0.8f, 1.8f);
    public bool highlightOnHover = true;
    [Tooltip("Yellow instead of the template's green (HighlightObjectYellow).")]
    public bool yellow;

    static readonly Color YellowTint = new Color(0.98f, 0.82f, 0.12f, 1f);

    static Material template;

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Material[]> originals = new List<Material[]>();
    readonly List<Material[]> instances = new List<Material[]>();
    bool forced, hovered, applied;
    XRBaseInteractable interactable;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    static Material Template
    {
        get
        {
            if (!template) template = Resources.Load<Material>(MaterialResource);
            if (!template) Debug.LogWarning("VRHighlight: Resources/" + MaterialResource + " is missing.");
            return template;
        }
    }

    void OnEnable()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable && highlightOnHover)
        {
            interactable.hoverEntered.AddListener(OnHoverEnter);
            interactable.hoverExited.AddListener(OnHoverExit);
        }
    }

    void OnDisable()
    {
        if (interactable)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
        }
        Restore();
    }

    void OnHoverEnter(UnityEngine.XR.Interaction.Toolkit.HoverEnterEventArgs _) { hovered = true; }
    void OnHoverExit(UnityEngine.XR.Interaction.Toolkit.HoverExitEventArgs _) { hovered = false; }

    public void SetHighlight(bool on) { forced = on; }

    void Update()
    {
        bool want = forced || hovered;
        if (want && !applied) Apply();
        else if (!want && applied) Restore();
        if (!applied) return;

        float k = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);   // 0..1
        float alpha = Mathf.Lerp(alphaRange.x, alphaRange.y, k);
        float glow = Mathf.Lerp(emissionRange.x, emissionRange.y, k);
        var t = Template;
        if (!t) return;
        var baseCol = t.GetColor(BaseColor);
        var emiCol = t.GetColor(EmissionColor);
        if (yellow) { baseCol = YellowTint; emiCol = YellowTint; }
        var emiDir = new Color(emiCol.r, emiCol.g, emiCol.b, 1f);
        float emiMax = Mathf.Max(emiDir.r, Mathf.Max(emiDir.g, emiDir.b));
        if (emiMax > 0f) emiDir /= emiMax;   // direction of the template emission, strength comes from the pulse
        foreach (var mats in instances)
            foreach (var m in mats)
            {
                if (!m) continue;
                m.SetColor(BaseColor, new Color(baseCol.r, baseCol.g, baseCol.b, alpha));
                m.SetColor(EmissionColor, emiDir * glow);
            }
    }

    void Apply()
    {
        var t = Template;
        if (!t) return;
        renderers.Clear(); originals.Clear(); instances.Clear();
        GetComponentsInChildren(false, renderers);
        renderers.RemoveAll(r => r is ParticleSystemRenderer);   // never touch particle effects
        foreach (var r in renderers)
        {
            var shared = r.sharedMaterials;
            var inst = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++) inst[i] = new Material(t);
            originals.Add(shared); instances.Add(inst);
            r.sharedMaterials = inst;
        }
        applied = true;
    }

    void Restore()
    {
        if (!applied) return;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i]) renderers[i].sharedMaterials = originals[i];
            foreach (var m in instances[i]) if (m) Destroy(m);
        }
        renderers.Clear(); originals.Clear(); instances.Clear();
        applied = false;
    }
}
