using System;
using System.Collections;
using UnityEngine;

namespace PotLeakage.Interaction
{
    /// <summary>Shared "show the tool to the player" motion: the tool glides toward the player's head, tilts so its long axis reads across
    /// the view, and turns a full circle. Everything is relative to the XR head transform (Camera.main), never a screen position. Scale is
    /// never touched, and callers detach the tool from its (non-uniformly scaled) parent first so the turn stays rigid.</summary>
    public static class ToolShowcase
    {
        public static Transform Head()
        {
            var cam = UnityEngine.Camera.main;
            return cam ? cam.transform : null;
        }

        public static float MaxSize(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return 0.5f;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.size.x, b.size.y, b.size.z, 1e-3f);
        }

        /// <summary>Comfortable viewing distance in front of the head (VR: closer than the desktop 1-4 m range).</summary>
        public static float ShowDistance(float size)
        {
            return Mathf.Clamp(size * 2.2f, 0.7f, 2f);
        }

        public static IEnumerator Lerp(float duration, Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                apply(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            apply(1f);
        }

        /// <summary>Default start point when there is no panel row: low and to the right of the player's view.</summary>
        public static Vector3 DefaultStart(Transform head, float depth)
        {
            return head.position + head.forward * depth + head.right * 0.45f - head.up * 0.3f;
        }

        /// <summary>Moves a detached tool from startPos toward the head, tilts it, and spins it one full turn.</summary>
        public static IEnumerator FlyAndSpin(Transform a, Vector3 startPos, Quaternion startRot, Transform head, float size, float flyDuration, float spinDuration)
        {
            Vector3 showPos = head.position + head.forward * ShowDistance(size);
            Quaternion showRot = Quaternion.AngleAxis(-65f, head.forward) * startRot;

            a.position = startPos;
            a.rotation = startRot;
            yield return Lerp(flyDuration, k =>
            {
                a.position = Vector3.Lerp(startPos, showPos, k);
                a.rotation = Quaternion.Slerp(startRot, showRot, k);
            });

            Quaternion baseRot = a.rotation;
            Vector3 axis = head.up;
            yield return Lerp(spinDuration, k => a.rotation = Quaternion.AngleAxis(360f * k, axis) * baseRot);
            yield return new WaitForSeconds(0.3f);
        }
    }
}
