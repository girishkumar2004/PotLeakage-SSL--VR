using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using PotLeakage.UI;

namespace PotLeakage.Interaction
{
    /// <summary>Reusable "tool flies out of its Collected Tools row, turns in front of the player's head, lands on a target" animation.
    /// Put it on the flying prop, fill in the Inspector fields and call Play() from Events To Follow / a row event; onArrived runs
    /// when the prop has landed. Scale is never changed. Runs in front of the XR head (Camera.main).</summary>
    public class ToolFlyTo : MonoBehaviour
    {
        public enum LandingMode
        {
            [Tooltip("Takes the exact local pose of 'target' (same parent) and settles on it.")] SettleOnTarget,
            [Tooltip("Keeps its authored pose when it already sits on 'landingRenderer', otherwise lands on the closest point of it.")] LandOnRenderer
        }

        public LandingMode mode = LandingMode.SettleOnTarget;
        public Transform target;
        public Renderer landingRenderer;
        public float landingTolerance = 0.6f;
        [Tooltip("Where the prop starts: a world-space transform (e.g. the collected-tools row). Empty = low and right of the player's view.")]
        public Transform startAnchor;
        public string rowKey = "";
        public CollectedToolsPanelController panel;

        public float flyToHeadDuration = 1.2f;
        public float spinDuration = 2.4f;
        public float settleDuration = 1.3f;

        public UnityEvent onArrived = new UnityEvent();

        public bool IsPlaying { get; private set; }

        public void Play()
        {
            if (IsPlaying) return;
            if (panel == null) panel = FindAnyObjectByType<CollectedToolsPanelController>(FindObjectsInactive.Include);
            gameObject.SetActive(true);
            StartCoroutine(mode == LandingMode.SettleOnTarget ? SettleRoutine() : LandRoutine());
        }

        // Props carry hover/click components meant for the tool box; keep them inert while flying.
        void MakeInert()
        {
            var vr = GetComponent<VRHighlight>(); if (vr != null) vr.SetHighlight(false);
            var ev = GetComponent<XRInteractionEvents>(); if (ev != null) ev.Disarm();
            var xr = GetComponent<XRBaseInteractable>(); if (xr != null) xr.enabled = false;
            var animator = GetComponent<Animator>(); if (animator != null) animator.enabled = false;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
        }

        Vector3 StartPoint(Transform head, float depth)
        {
            if (startAnchor) return startAnchor.position;
            var row = panel != null && !string.IsNullOrEmpty(rowKey) ? panel.GetRow(rowKey) : null;
            if (row) return row.transform.position;
            return ToolShowcase.DefaultStart(head, depth);
        }

        IEnumerator SettleRoutine()
        {
            IsPlaying = true;
            MakeInert();
            Transform a = transform; Transform parent = a.parent;
            Vector3 finalLocalPos = target.localPosition; Quaternion finalLocalRot = target.localRotation; Vector3 finalLocalScale = target.localScale;
            Vector3 finalPos = target.position; Quaternion finalRot = target.rotation;

            bool sameParent = target.parent == parent;
            if (sameParent) { a.localPosition = finalLocalPos; a.localRotation = finalLocalRot; a.localScale = finalLocalScale; }
            a.SetParent(null, true); // rigid flight (ToolBox scale is non-uniform)

            var head = ToolShowcase.Head();
            if (head != null)
            {
                float size = ToolShowcase.MaxSize(a);
                float depth = ToolShowcase.ShowDistance(size);
                yield return ToolShowcase.FlyAndSpin(a, StartPoint(head, depth), finalRot, head, size, flyToHeadDuration, spinDuration);
            }
            Vector3 from = a.position; Quaternion fromRot = a.rotation;
            yield return ToolShowcase.Lerp(settleDuration, k => { a.position = Vector3.Lerp(from, finalPos, k); a.rotation = Quaternion.Slerp(fromRot, finalRot, k); });

            if (sameParent)
            {
                a.SetParent(parent, false);
                a.localPosition = finalLocalPos; a.localRotation = finalLocalRot; a.localScale = finalLocalScale;
            }
            else
            {
                a.SetParent(parent, true);
                a.SetPositionAndRotation(finalPos, finalRot);
            }
            IsPlaying = false;
            onArrived.Invoke();
        }

        IEnumerator LandRoutine()
        {
            IsPlaying = true;
            MakeInert();
            Transform a = transform; Transform parent = a.parent;
            Vector3 authoredLocalPos = a.localPosition; Quaternion authoredLocalRot = a.localRotation; Vector3 authoredLocalScale = a.localScale;
            Vector3 landPos = a.position; bool useAuthored = true;
            if (landingRenderer != null)
            {
                Bounds b = landingRenderer.bounds; b.Expand(landingTolerance);
                if (!b.Contains(landPos)) { landPos = landingRenderer.bounds.ClosestPoint(landPos); useAuthored = false; }
            }

            var head = ToolShowcase.Head();
            if (head != null)
            {
                float depth = Mathf.Clamp(Vector3.Distance(head.position, landPos), 0.7f, 2f);
                Vector3 startPos = StartPoint(head, depth);
                Vector3 showPos = head.position + head.forward * depth;
                Vector3 axis = parent != null ? parent.TransformDirection(Vector3.up).normalized : head.up;

                a.position = startPos;
                yield return ToolShowcase.Lerp(flyToHeadDuration, k => a.position = Vector3.Lerp(startPos, showPos, k));
                Quaternion baseRot = a.rotation;
                yield return ToolShowcase.Lerp(spinDuration, k => a.rotation = Quaternion.AngleAxis(360f * k, axis) * baseRot);
                a.rotation = baseRot;
                yield return new WaitForSeconds(0.3f);
                Vector3 from = a.position;
                yield return ToolShowcase.Lerp(settleDuration, k => a.position = Vector3.Lerp(from, landPos, k));
            }
            a.localRotation = authoredLocalRot; a.localScale = authoredLocalScale;
            if (useAuthored) a.localPosition = authoredLocalPos; else a.position = landPos;
            IsPlaying = false;
            onArrived.Invoke();
        }
    }
}
