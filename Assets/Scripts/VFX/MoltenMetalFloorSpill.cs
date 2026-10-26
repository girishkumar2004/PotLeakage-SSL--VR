using System.Collections;
using UnityEngine;

namespace PotLeakage.VFX
{
    /// <summary>
    /// Procedural organic molten metal floor spill on Machine/Floor directly below TransformPoints/drop.
    /// Gradually expands from a small entry point (~0.03m) to a full puddle (~0.85m) over ~8 seconds.
    /// Rendered with the Lavafall shader (Assets/Shaders/Lava/Lavafall.mat).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MoltenMetalFloorSpill : MonoBehaviour
    {
        [Header("Puddle Geometry & Sizing")]
        [Tooltip("Minimum starting radius of the puddle at the drip point (meters)")]
        public float minRadius = 0.03f;

        [Tooltip("Maximum spread radius of the molten metal puddle on the floor (meters)")]
        public float maxRadius = 0.85f;

        [Tooltip("Duration of the gradual floor spill expansion (seconds)")]
        public float expandDuration = 8.0f;

        [Tooltip("Number of radial sectors around the puddle")]
        public int radialSegments = 32;

        [Tooltip("Number of concentric rings from center to perimeter")]
        public int ringSegments = 4;

        [Tooltip("Center thickness offset in Y to create a convex liquid meniscus (meters)")]
        public float centerThickness = 0.0025f;

        [Header("Material & Visuals")]
        public Material spillMaterial;

        [Header("State")]
        [SerializeField] private float currentRadius = 0.03f;
        [SerializeField] private bool isSpilling = false;

        public float CurrentRadius => currentRadius;
        public bool IsSpilling => isSpilling;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh puddleMesh;
        private Coroutine expandCoroutine;

        // Authoritative target position: directly below TransformPoints/drop, 5mm above Machine/Floor (Y = -0.0186f)
        public static readonly Vector3 SpillPosition = new Vector3(-3.7681f, -0.0136f, -67.3870f);

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            InitializeComponents();
        }

        public void InitializeComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            if (puddleMesh == null)
            {
                puddleMesh = new Mesh();
                puddleMesh.name = "MoltenMetalFloorSpill_ProceduralMesh";
                meshFilter.sharedMesh = puddleMesh;
            }

            if (spillMaterial == null)
            {
#if UNITY_EDITOR
                spillMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/Lava/Lavafall.mat");
#endif
            }

            if (meshRenderer != null && spillMaterial != null && meshRenderer.sharedMaterial != spillMaterial)
            {
                meshRenderer.sharedMaterial = spillMaterial;
            }
        }

        /// <summary>
        /// Starts gradual procedural floor spill expansion over ~8 seconds.
        /// </summary>
        public void StartSpill()
        {
            InitializeComponents();

            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
            gameObject.SetActive(true);

            if (expandCoroutine != null)
            {
                StopCoroutine(expandCoroutine);
            }

            isSpilling = true;

            if (Application.isPlaying)
            {
                expandCoroutine = StartCoroutine(ExpandRoutine());
            }
            else
            {
                // In edit mode or batch execution, build initial puddle
                SimulateSpill(1.0f);
            }
        }

        private IEnumerator ExpandRoutine()
        {
            float elapsed = 0f;
            currentRadius = minRadius;
            BuildPuddleMesh(currentRadius);

            while (elapsed < expandDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / expandDuration);
                // Smooth organic deceleration curve
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                currentRadius = Mathf.Lerp(minRadius, maxRadius, smoothT);
                BuildPuddleMesh(currentRadius);
                yield return null;
            }

            currentRadius = maxRadius;
            BuildPuddleMesh(currentRadius);
            expandCoroutine = null;
        }

        /// <summary>
        /// Explicitly sets spill progress (0.0 to 1.0) and generates mesh immediately.
        /// Useful for testing, editor inspection, and scrubbable sequences.
        /// </summary>
        public void SimulateSpill(float progress)
        {
            InitializeComponents();
            if (meshRenderer != null) meshRenderer.enabled = true;
            gameObject.SetActive(true);
            isSpilling = true;

            float t = Mathf.Clamp01(progress);
            currentRadius = Mathf.Lerp(minRadius, maxRadius, t);
            BuildPuddleMesh(currentRadius);
        }

        /// <summary>
        /// Stops spill expansion and keeps current visual puddle state.
        /// </summary>
        public void StopSpill()
        {
            if (expandCoroutine != null)
            {
                StopCoroutine(expandCoroutine);
                expandCoroutine = null;
            }
            isSpilling = false;
        }

        /// <summary>
        /// Settles the puddle growth when controlled leakage is reached,
        /// ensuring the existing pool remains visible and does not disappear.
        /// </summary>
        public void SettleSpill()
        {
            StopSpill();
        }

        /// <summary>
        /// Completely resets the spill: stops coroutine, resets radius, and disables renderer.
        /// </summary>
        public void ResetSpill()
        {
            StopSpill();
            currentRadius = minRadius;
            BuildPuddleMesh(0.001f);
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Generates the organic radial liquid mesh on XZ plane with harmonic wave perturbations.
        /// </summary>
        public void BuildPuddleMesh(float radius)
        {
            if (meshFilter == null || puddleMesh == null)
            {
                InitializeComponents();
            }

            int radial = Mathf.Max(8, radialSegments);
            int rings = Mathf.Max(1, ringSegments);

            int totalVerts = 1 + rings * radial;
            Vector3[] vertices = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];

            // Center vertex
            vertices[0] = new Vector3(0f, centerThickness, 0f);
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            int vertIdx = 1;
            for (int r = 1; r <= rings; r++)
            {
                float ringFactor = (float)r / rings;
                float ringThickness = centerThickness * (1.0f - ringFactor * 0.85f);

                for (int i = 0; i < radial; i++)
                {
                    float angle = (float)i / radial * Mathf.PI * 2f;

                    // Harmonic / sinusoidal shape modulation for natural liquid puddle boundary
                    float organicOffset = 1.0f
                        + 0.11f * Mathf.Sin(3f * angle)
                        + 0.07f * Mathf.Cos(5f * angle + 0.45f)
                        + 0.04f * Mathf.Sin(7f * angle - 0.25f);

                    float modulatedRadius = radius * ringFactor * organicOffset;

                    float x = Mathf.Cos(angle) * modulatedRadius;
                    float z = Mathf.Sin(angle) * modulatedRadius;
                    float y = ringThickness;

                    vertices[vertIdx] = new Vector3(x, y, z);
                    normals[vertIdx] = Vector3.up;

                    // UV mapping for continuous flowing Lava texture
                    float u = 0.5f + (x / (maxRadius * 2f));
                    float v = 0.5f + (z / (maxRadius * 2f));
                    uvs[vertIdx] = new Vector2(u, v);

                    vertIdx++;
                }
            }

            // Triangles
            int totalTriangles = (radial * 3) + ((rings - 1) * radial * 6);
            int[] triangles = new int[totalTriangles];
            int triIdx = 0;

            // Center fan (first ring)
            for (int i = 0; i < radial; i++)
            {
                int next = (i + 1) % radial;
                triangles[triIdx++] = 0;
                triangles[triIdx++] = 1 + i;
                triangles[triIdx++] = 1 + next;
            }

            // Quad rings
            for (int r = 1; r < rings; r++)
            {
                int innerRingStart = 1 + (r - 1) * radial;
                int outerRingStart = 1 + r * radial;

                for (int i = 0; i < radial; i++)
                {
                    int next = (i + 1) % radial;

                    int inA = innerRingStart + i;
                    int inB = innerRingStart + next;
                    int outA = outerRingStart + i;
                    int outB = outerRingStart + next;

                    // Quad = 2 triangles
                    triangles[triIdx++] = inA;
                    triangles[triIdx++] = outA;
                    triangles[triIdx++] = inB;

                    triangles[triIdx++] = inB;
                    triangles[triIdx++] = outA;
                    triangles[triIdx++] = outB;
                }
            }

            puddleMesh.Clear();
            puddleMesh.vertices = vertices;
            puddleMesh.normals = normals;
            puddleMesh.uv = uvs;
            puddleMesh.triangles = triangles;
            puddleMesh.RecalculateBounds();
        }

        private void OnDisable()
        {
            if (expandCoroutine != null)
            {
                StopCoroutine(expandCoroutine);
                expandCoroutine = null;
            }
        }
    }
}
