using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>TASK_THROW_FUSED_ALUMINA. The Fused Alumina Bag is enabled and highlighted next to the rig; pressing it throws it on a
/// tumbling (diagonal spin) arc onto `target` (cement_1.001). On impact the alumina_smoke dust plays, the bag chars to charcoal and
/// dissolves as if burnt, and the task completes.</summary>
public class FusedAluminaSequence : MonoBehaviour
{
    public Transform bag, target;
    public ParticleSystem smoke;
    public Vector3 startPos = new Vector3(-2.40100002f, 1.55799997f, 38.4749985f);
    public Vector3 startEuler = new Vector3(275.533295f, 88.9519501f, 181.043167f);
    public Vector3 landEuler = new Vector3(20.0964565f, 318.448517f, 340.166016f);
    public float throwDuration = 1.6f, arcHeight = 1.1f, spinDegrees = 900f;
    public Vector3 spinAxis = new Vector3(1f, 1f, 0.5f);
    public float burnDuration = 3.5f, smokeTail = 2f;

    XRGrabInteractable xr;
    Action done;
    Coroutine routine;
    Material[][] originals;

    public void Arm(Action onDone)
    {
        Disarm();
        done = onDone;
        bag.gameObject.SetActive(true);
        bag.SetPositionAndRotation(startPos, Quaternion.Euler(startEuler));
        foreach (var r in bag.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;
        originals = null;
        xr = SequenceHelperFunctions.EnsureInteractable(bag.gameObject, false);
        if (xr) xr.selectEntered.AddListener(OnPressed);
        SequenceHelperFunctions.instance.HighlightObjectGreen(bag.gameObject);
    }

    public void Disarm()
    {
        if (xr) xr.selectEntered.RemoveListener(OnPressed);
        xr = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        RestoreMaterials();
        if (bag) bag.gameObject.SetActive(false);
        done = null;
    }

    void OnPressed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (routine != null) return;
        if (xr) xr.selectEntered.RemoveListener(OnPressed);
        SequenceHelperFunctions.instance.ClearHighlight(bag.gameObject);
        routine = StartCoroutine(Run());
    }

    Vector3 LandingPoint()
    {
        var r = target ? target.GetComponent<Renderer>() : null;
        if (r) return new Vector3(target.position.x, r.bounds.max.y, target.position.z);   // the mesh pivot sits on the slab; bounds only give the top
        return target ? target.position : bag.position;
    }

    IEnumerator Run()
    {
        Vector3 from = bag.position, to = LandingPoint();
        Quaternion rotEnd = Quaternion.Euler(landEuler);
        Quaternion rotStart = bag.rotation;
        Vector3 axis = spinAxis.normalized;

        // 1. throw: parabolic arc with a diagonal tumble that settles into the landing pose
        float e = 0f;
        while (e < throwDuration)
        {
            e += Time.deltaTime;
            float t = Mathf.Clamp01(e / throwDuration);
            Vector3 flat = Vector3.Lerp(from, to, Mathf.Lerp(t, Mathf.SmoothStep(0f, 1f, t), 0.35f));
            bag.position = flat + Vector3.up * (4f * t * (1f - t) * arcHeight);
            Quaternion baseRot = Quaternion.Slerp(rotStart, rotEnd, Mathf.SmoothStep(0f, 1f, t));
            bag.rotation = Quaternion.AngleAxis(spinDegrees * (1f - t), axis) * baseRot;
            yield return null;
        }
        bag.SetPositionAndRotation(to, rotEnd);

        // 2. impact: alumina_smoke dust
        if (smoke)
        {
            smoke.gameObject.SetActive(true);
            smoke.transform.SetPositionAndRotation(to + Vector3.up * 0.05f, Quaternion.Euler(-90f, 0f, 0f));
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smoke.Play(true);
        }

        // 3. the bag burns: darkens to charcoal, then dissolves
        yield return Burn(burnDuration);
        foreach (var r in bag.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
        yield return new WaitForSeconds(smokeTail);

        routine = null;
        var cb = done; done = null;
        cb?.Invoke();
    }

    IEnumerator Burn(float duration)
    {
        var rends = bag.GetComponentsInChildren<MeshRenderer>(true);
        originals = new Material[rends.Length][];
        var inst = new System.Collections.Generic.List<Material>();
        var baseCols = new System.Collections.Generic.List<Color>();
        for (int i = 0; i < rends.Length; i++)
        {
            originals[i] = rends[i].sharedMaterials;
            foreach (var m in rends[i].materials)
            {
                MakeFadeable(m);
                inst.Add(m);
                baseCols.Add(ColorOf(m));
            }
        }
        Color charcoal = new Color(0.08f, 0.07f, 0.06f, 1f);
        Vector3 baseScale = bag.localScale;
        float e = 0f;
        while (e < duration)
        {
            e += Time.deltaTime;
            float t = Mathf.Clamp01(e / duration);
            float burn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));
            for (int i = 0; i < inst.Count; i++)
            {
                var c = Color.Lerp(baseCols[i], charcoal, burn);
                c.a = baseCols[i].a * (1f - fade);
                SetColor(inst[i], c);
            }
            bag.localScale = baseScale * Mathf.Lerp(1f, 0.85f, fade);   // crumbles slightly as it dissolves
            yield return null;
        }
        bag.localScale = baseScale;
    }

    void RestoreMaterials()
    {
        if (originals == null || !bag) return;
        var rends = bag.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < rends.Length && i < originals.Length; i++) rends[i].sharedMaterials = originals[i];
        originals = null;
    }

    static string ColorProp(Material m) { return m.HasProperty("_BaseColor") ? "_BaseColor" : (m.HasProperty("_Color") ? "_Color" : null); }
    static Color ColorOf(Material m) { var p = ColorProp(m); return p != null ? m.GetColor(p) : Color.white; }
    static void SetColor(Material m, Color c) { var p = ColorProp(m); if (p != null) m.SetColor(p, c); }

    static void MakeFadeable(Material m)
    {
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
