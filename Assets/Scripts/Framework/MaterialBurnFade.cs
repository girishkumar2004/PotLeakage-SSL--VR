using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PotLeakage.Interaction
{
    /// <summary>
    /// Reusable "burn then dissolve" effect: tints every material under this object towards a burn colour,
    /// then fades it out. Call Play() from Events To Follow; originals are restored when it finishes.
    /// </summary>
    public class MaterialBurnFade : MonoBehaviour
    {
        public float duration = 4f;
        public Color burnColor = new Color(0.08f, 0.07f, 0.06f, 1f);
        [Range(0.05f, 1f)] public float burnEndsAt = 0.45f;
        [Range(0f, 0.95f)] public float fadeStartsAt = 0.35f;
        public bool deactivateWhenDone = true;
        [Tooltip("Played together with the burn and stopped at the end (optional).")]
        public ParticleSystem emitWhileBurning;
        public UnityEvent onFinished = new UnityEvent();

        private Coroutine routine;

        public void Play()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var originals = new Material[renderers.Length][];
            var instances = new List<Material>();
            var baseColors = new List<Color>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer) { originals[i] = null; continue; }
                originals[i] = renderers[i].sharedMaterials;
                foreach (var m in renderers[i].materials)
                {
                    MakeFadeable(m);
                    instances.Add(m);
                    baseColors.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white));
                }
            }

            if (emitWhileBurning != null) emitWhileBurning.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float burn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / burnEndsAt));
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - fadeStartsAt) / (1f - fadeStartsAt)));
                for (int i = 0; i < instances.Count; i++)
                {
                    var m = instances[i];
                    string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : (m.HasProperty("_Color") ? "_Color" : null);
                    if (prop == null) continue;
                    Color c = Color.Lerp(baseColors[i], burnColor, burn);
                    c.a = baseColors[i].a * (1f - fade);
                    m.SetColor(prop, c);
                }
                yield return null;
            }

            if (emitWhileBurning != null) emitWhileBurning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (deactivateWhenDone) gameObject.SetActive(false);

            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && originals[i] != null) renderers[i].sharedMaterials = originals[i];
            foreach (var m in instances) if (m != null) Destroy(m);

            routine = null;
            onFinished.Invoke();
        }

        private static void MakeFadeable(Material m)
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
}
