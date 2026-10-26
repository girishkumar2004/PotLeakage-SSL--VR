using System.Collections.Generic;
using UnityEngine;

namespace PotLeakage.Interaction
{
    /// <summary>See-through highlight for a prop (stays visible even inside other geometry). Wire On() / Off() from Events To Follow or
    /// XRInteractionEvents (hover enter/exit). The original materials are restored by Off(). The material comes from the field or
    /// Resources/VRXRayHighlight, so its shader is always in the build.</summary>
    public class XRayHighlight : MonoBehaviour
    {
        public Material highlightMaterial;
        public Color color = new Color(0.2f, 1f, 0.35f, 0.9f);

        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();

        public void On()
        {
            var mat = highlightMaterial;
            if (mat == null)
            {
                var template = Resources.Load<Material>("VRXRayHighlight");
                if (template == null) { Debug.LogWarning("XRayHighlight: Resources/VRXRayHighlight is missing.", this); return; }
                mat = new Material(template);
                mat.SetColor("_HighlightColor", color);
                highlightMaterial = mat;
            }
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || originals.ContainsKey(r)) continue;
                originals[r] = r.sharedMaterials;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        public void Off()
        {
            foreach (var kv in originals) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            originals.Clear();
        }

        void OnDisable() { Off(); }
    }
}
