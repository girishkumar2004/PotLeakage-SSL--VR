using UnityEngine;

/// <summary>Event-friendly placement: put this on a prop, fill in the Inspector fields and call PlaceBetweenAnchors() / PlaceAtAnchor()
/// from Events To Follow. Keeps the prop's own rotation and scale (world position only).</summary>
public class TransformPlacer : MonoBehaviour
{
    public Transform anchorA;
    public Transform anchorB;
    [Tooltip("Added to the placed world position.")]
    public Vector3 worldOffset;

    /// <summary>World position = midpoint of anchorA and anchorB (+ worldOffset).</summary>
    public void PlaceBetweenAnchors()
    {
        if (anchorA == null || anchorB == null) { Debug.LogWarning("[TransformPlacer] anchors missing on " + name); return; }
        transform.position = (anchorA.position + anchorB.position) * 0.5f + worldOffset;
    }

    public void PlaceAtAnchor()
    {
        if (anchorA == null) { Debug.LogWarning("[TransformPlacer] anchorA missing on " + name); return; }
        transform.position = anchorA.position + worldOffset;
    }
}
