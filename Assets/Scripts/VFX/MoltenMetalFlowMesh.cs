using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotLeakage.VFX
{
    /// <summary>
    /// Procedural molten aluminium body generator and growth animator.
    /// Produces a thick, viscous, organic molten tongue protruding from the pot shell opening.
    /// Implements normalized growth animation (0..1) over 3 seconds matching reference images:
    /// - 0.00s: No visible leakage
    /// - 0.15-0.40s: Small thick molten blob appears directly at opening
    /// - 0.40-0.90s: Blob slowly grows outward
    /// - 0.90-1.50s: Molten tongue extends farther outward
    /// - 1.50-2.20s: Tongue becomes longer and slightly thicker
    /// - 2.20-3.00s: Final molten shape settles into slow viscous movement
    /// - 3.00s+: Stable molten tongue with subtle thermal pulsation and organic breathing
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MoltenMetalFlowMesh : MonoBehaviour
    {
        [Header("Transforms (Authoritative Read-Only Scene References)")]
        public Transform drop;
        public Transform drop1;
        public Transform drop2;
        public Transform sourceTransform;
        public Transform dropTransform;

        [Header("Material & UV Parameters")]
        public Material flowMaterial;
        public float vTiling = 2.0f;
        public bool reverseUVDirection = false;

        [Header("Radius Settings (Meters)")]
        [Tooltip("Current base radius at widest point")]
        public float radius = 0.050f;

        [Tooltip("Full leakage wide radius (meters) — thick molten tongue")]
        public float defaultRadius = 0.050f;

        [Tooltip("Thinned controlled flow radius (meters) — after stopper applied")]
        public float controlledRadius = 0.032f;

        [Header("Stream Taper Settings (Compatibility)")]
        public float startRadius = 0.050f;
        public float endRadius = 0.025f;

        [Header("Mesh Complexity (Quest & PC Optimized)")]
        [Range(10, 32)] public int longitudinalSegments = 16;
        [Range(8, 20)] public int radialSegments = 12;

        [Header("Growth Animation")]
        [Tooltip("Normalized growth along the leakage path [0..1]")]
        [Range(0f, 1f)] public float growth = 0f;

        [Tooltip("Total duration in seconds for initial growth animation")]
        public float animationDuration = 3.0f;

        [Tooltip("Whether growth or steady-state viscous surface motion is currently active")]
        public bool isAnimating = false;

        [Header("Controlled Leakage State")]
        [Tooltip("Whether the flow is currently in controlled leakage mode")]
        public bool isControlledLeakage = false;

        [Tooltip("When true, mesh renderer is kept inactive so only particle stream is visible")]
        public bool isRenderingDisabled = true;

        // Authoritative Design Coordinates (Kept for backward compatibility and test validation)
        public static readonly Vector3 P0_Source = new Vector3(-4.32000017f, 0.262073308f, -68.1200027f);
        public static readonly Vector3 P1_Rise = new Vector3(-4.32000017f, 0.633f, -68.1200027f);
        public static readonly Vector3 P2_Curve1 = new Vector3(-4.20f, 0.633f, -67.95f);
        public static readonly Vector3 P3_Curve2 = new Vector3(-4.05f, 0.633f, -67.75f);
        public static readonly Vector3 P4_Curve3 = new Vector3(-3.90f, 0.633f, -67.55f);
        public static readonly Vector3 P5_Crest = new Vector3(-3.76804996f, 0.633f, -67.387001f);
        public static readonly Vector3 P6_Drop1 = new Vector3(-3.76804996f, 0.40f, -67.387001f);
        public static readonly Vector3 P7_Drop2 = new Vector3(-3.76804996f, 0.10f, -67.387001f);
        public static readonly Vector3 P8_DropTarget = new Vector3(-3.76804996f, -0.156000003f, -67.387001f);

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh generatedMesh;

        private Coroutine growthCoroutine;
        private Coroutine transitionCoroutine;

        private void Awake()
        {
            InitializeComponents();
            ResolveDropTransforms();
        }

        private void OnEnable()
        {
            InitializeComponents();
            ResolveDropTransforms();
            if (growth > 0.001f)
            {
                BuildStreamMesh();
            }
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        public void InitializeComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            if (flowMaterial == null)
            {
#if UNITY_EDITOR
                flowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/Lava/Lavafall.mat");
#endif
            }

            if (flowMaterial != null && meshRenderer != null && meshRenderer.sharedMaterial != flowMaterial)
            {
                meshRenderer.sharedMaterial = flowMaterial;
            }

            if (isRenderingDisabled && meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (isRenderingDisabled)
            {
                if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer != null && meshRenderer.enabled)
                {
                    meshRenderer.enabled = false;
                }
            }
        }

        public void ResolveDropTransforms()
        {
            if (drop == null)
            {
                if (dropTransform != null) drop = dropTransform;
                else
                {
                    var d0 = GameObject.Find("CameraSystem/TransformPoints/drop")
                          ?? GameObject.Find("TransformPoints/drop")
                          ?? GameObject.Find("drop");
                    if (d0 != null) drop = d0.transform;
                }
            }
            if (drop1 == null)
            {
                var d1 = GameObject.Find("CameraSystem/TransformPoints/drop (1)")
                      ?? GameObject.Find("TransformPoints/drop (1)")
                      ?? GameObject.Find("drop (1)");
                if (d1 != null) drop1 = d1.transform;
            }
            if (drop2 == null)
            {
                var d2 = GameObject.Find("CameraSystem/TransformPoints/drop (2)")
                      ?? GameObject.Find("TransformPoints/drop (2)")
                      ?? GameObject.Find("drop (2)");
                if (d2 != null) drop2 = d2.transform;
            }
            if (dropTransform == null && drop != null)
            {
                dropTransform = drop;
            }
        }

        // =========================================================================
        // GROWTH ANIMATION & CONTROLLED LEAKAGE
        // =========================================================================

        /// <summary>
        /// Starts the 0 to 3 second molten leakage growth animation from scratch.
        /// </summary>
        public void PlayGrowthAnimation()
        {
            InitializeComponents();
            ResolveDropTransforms();

            StopAnimation();

            isControlledLeakage = false;
            radius = defaultRadius;
            growth = 0f;

            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                growthCoroutine = StartCoroutine(GrowthAnimationRoutine());
            }
            else
            {
                growth = 1f;
                BuildStreamMesh();
            }
        }

        /// <summary>
        /// Coroutine driving the 0s to 3s growth timeline, settling into steady viscous pulsation.
        /// </summary>
        private IEnumerator GrowthAnimationRoutine()
        {
            isAnimating = true;
            float elapsed = 0f;

            // Phase 1: Growth progression (0.0s to 3.0s)
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                growth = EvaluateGrowthTimeline(elapsed);

                if (growth > 0.001f)
                {
                    BuildStreamMesh();
                }
                else
                {
                    ClearMesh();
                }

                yield return null;
            }

            growth = 1.0f;
            BuildStreamMesh();

            // Phase 2: Steady viscous motion (Section 11: slow viscous surface motion)
            while (isAnimating)
            {
                BuildStreamMesh();
                yield return null;
            }
        }

        /// <summary>
        /// Evaluates the normalized growth parameter (0..1) given elapsed time matching Section 4.
        /// </summary>
        public static float EvaluateGrowthTimeline(float time)
        {
            if (time <= 0.15f)
            {
                return 0f;
            }
            if (time <= 0.40f)
            {
                float t = (time - 0.15f) / 0.25f;
                return Mathf.Lerp(0.02f, 0.12f, Mathf.SmoothStep(0f, 1f, t));
            }
            if (time <= 0.90f)
            {
                float t = (time - 0.40f) / 0.50f;
                return Mathf.Lerp(0.12f, 0.40f, Mathf.SmoothStep(0f, 1f, t));
            }
            if (time <= 1.50f)
            {
                float t = (time - 0.90f) / 0.60f;
                return Mathf.Lerp(0.40f, 0.70f, Mathf.SmoothStep(0f, 1f, t));
            }
            if (time <= 2.20f)
            {
                float t = (time - 1.50f) / 0.70f;
                return Mathf.Lerp(0.70f, 0.90f, Mathf.SmoothStep(0f, 1f, t));
            }
            if (time <= 3.00f)
            {
                float t = (time - 2.20f) / 0.80f;
                return Mathf.Lerp(0.90f, 1.00f, Mathf.SmoothStep(0f, 1f, t));
            }
            return 1.0f;
        }

        /// <summary>
        /// Transitions the molten tongue between active wide leakage and thinner, shorter controlled leakage.
        /// </summary>
        public void SetControlled(bool controlled, float duration = 1.0f)
        {
            isControlledLeakage = controlled;
            float targetR = controlled ? controlledRadius : defaultRadius;
            float targetG = controlled ? 0.42f : 1.0f;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (Application.isPlaying && duration > 0.01f && gameObject.activeInHierarchy)
            {
                transitionCoroutine = StartCoroutine(TransitionControlledRoutine(targetR, targetG, duration));
            }
            else
            {
                radius = targetR;
                growth = targetG;
                BuildStreamMesh();
            }
        }

        private IEnumerator TransitionControlledRoutine(float targetR, float targetG, float duration)
        {
            float startR = radius;
            float startG = growth;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                radius = Mathf.Lerp(startR, targetR, t);
                growth = Mathf.Lerp(startG, targetG, t);
                BuildStreamMesh();
                yield return null;
            }

            radius = targetR;
            growth = targetG;
            BuildStreamMesh();
            transitionCoroutine = null;
        }

        /// <summary>
        /// Stops growth / motion and clears the generated mesh.
        /// </summary>
        public void StopAnimation()
        {
            isAnimating = false;
            if (growthCoroutine != null)
            {
                StopCoroutine(growthCoroutine);
                growthCoroutine = null;
            }
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
            growth = 0f;
            ClearMesh();
        }

        // =========================================================================
        // PROCEDURAL MESH GENERATION
        // =========================================================================

        /// <summary>
        /// Evaluates the normalized radius profile along the molten body matching Section 7.
        /// </summary>
        public static float EvaluateRadiusProfile(float u)
        {
            u = Mathf.Clamp01(u);
            if (u <= 0.10f) return Mathf.Lerp(0.95f, 1.00f, u / 0.10f);
            if (u <= 0.20f) return Mathf.Lerp(1.00f, 0.95f, (u - 0.10f) / 0.10f);
            if (u <= 0.35f) return Mathf.Lerp(0.95f, 0.90f, (u - 0.20f) / 0.15f);
            if (u <= 0.50f) return Mathf.Lerp(0.90f, 0.82f, (u - 0.35f) / 0.15f);
            if (u <= 0.65f) return Mathf.Lerp(0.82f, 0.75f, (u - 0.50f) / 0.15f);
            if (u <= 0.80f) return Mathf.Lerp(0.75f, 0.70f, (u - 0.65f) / 0.15f);
            if (u <= 0.90f) return Mathf.Lerp(0.70f, 0.62f, (u - 0.80f) / 0.10f);
            return Mathf.Lerp(0.62f, 0.50f, (u - 0.90f) / 0.10f);
        }

        /// <summary>
        /// Generates the organic, irregular, double-sided 3D molten metal body with rounded tip cap.
        /// </summary>
        public void BuildStreamMesh()
        {
            InitializeComponents();
            ResolveDropTransforms();

            float effGrowth = isAnimating ? growth : (growth > 0.001f ? growth : 1.0f);
            if (effGrowth <= 0.001f)
            {
                ClearMesh();
                return;
            }

            bool isLegacyDesignCoordinates = sourceTransform != null && sourceTransform.name == "Cube.010";
            if (!isLegacyDesignCoordinates)
            {
                var machine = GameObject.Find("Machine");
                if (machine != null && transform.IsChildOf(machine.transform))
                {
                    isLegacyDesignCoordinates = true;
                }
            }

            int ringCount = Mathf.Clamp(longitudinalSegments, 10, 32);
            int radialCount = Mathf.Clamp(radialSegments, 8, 20);
            int capRings = 3;

            int vertsPerRing = radialCount + 1;
            int totalVerts = (ringCount + capRings) * vertsPerRing + 2;

            Vector3[] vertices = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector4[] tangents = new Vector4[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];

            Vector3[] centerlines = new Vector3[ringCount];
            Vector3[] spineTangents = new Vector3[ringCount];

            if (isLegacyDesignCoordinates)
            {
                // Legacy verification coordinates for Machine/MoltenMetalFlow
                Vector3[] designPoints = new Vector3[] {
                    P0_Source, P1_Rise, P2_Curve1, P3_Curve2, P4_Curve3, P5_Crest, P6_Drop1, P7_Drop2, P8_DropTarget
                };
                for (int r = 0; r < ringCount; r++)
                {
                    float u = (float)r / (ringCount - 1);
                    float idxF = u * (designPoints.Length - 1);
                    int i0 = Mathf.Clamp(Mathf.FloorToInt(idxF), 0, designPoints.Length - 1);
                    int i1 = Mathf.Clamp(i0 + 1, 0, designPoints.Length - 1);
                    float f = idxF - i0;
                    centerlines[r] = Vector3.Lerp(designPoints[i0], designPoints[i1], f);
                    Vector3 diff = (i1 < designPoints.Length ? (designPoints[i1] - designPoints[i0]) : Vector3.down);
                    spineTangents[r] = diff.sqrMagnitude > 0.0001f ? diff.normalized : Vector3.down;
                }
            }
            else
            {
                // Authoritative runtime path: drop -> drop (1) -> drop (2)
                Vector3 p0 = drop != null ? drop.position : (dropTransform != null ? dropTransform.position : transform.position);
                Vector3 p1 = drop1 != null ? drop1.position : p0 + Vector3.forward * 0.3f;
                Vector3 p2 = drop2 != null ? drop2.position : p1 + Vector3.down * 0.2f;

                Vector3 dirOut = (p1 - p0).normalized;
                float distOut = Vector3.Distance(p0, p1);
                Vector3 dirSag = (p2 - p1).normalized;

                Vector3 bez0 = p0;
                Vector3 bez1 = p0 + dirOut * (distOut * 0.5f);
                Vector3 bez2 = p1 - dirOut * (distOut * 0.2f);
                Vector3 bez3 = p1 + dirSag * 0.06f;

                for (int r = 0; r < ringCount; r++)
                {
                    float u = (float)r / (ringCount - 1);
                    float t = u * effGrowth;
                    float invT = 1f - t;

                    centerlines[r] = invT * invT * invT * bez0
                                   + 3f * invT * invT * t * bez1
                                   + 3f * invT * t * t * bez2
                                   + t * t * t * bez3;

                    Vector3 deriv = -3f * invT * invT * bez0
                                  + (3f * invT * invT - 6f * invT * t) * bez1
                                  + (6f * invT * t - 3f * t * t) * bez2
                                  + 3f * t * t * bez3;

                    spineTangents[r] = deriv.sqrMagnitude > 0.0001f ? deriv.normalized : dirOut;
                }
            }

            // Parallel transport (Bishop Frame) along spine for zero twisting
            Vector3[] bishopNormals = new Vector3[ringCount];
            Vector3[] bishopBinormals = new Vector3[ringCount];

            Vector3 initTan = spineTangents[0];
            Vector3 initNorm = Vector3.Cross(initTan, Vector3.up).normalized;
            if (initNorm.sqrMagnitude < 0.001f) initNorm = Vector3.Cross(initTan, Vector3.right).normalized;
            bishopNormals[0] = initNorm;
            bishopBinormals[0] = Vector3.Cross(initTan, initNorm).normalized;

            for (int r = 1; r < ringCount; r++)
            {
                Vector3 prevTan = spineTangents[r - 1];
                Vector3 curTan = spineTangents[r];
                Vector3 v = Vector3.Cross(prevTan, curTan);
                Vector3 curNorm;
                if (v.sqrMagnitude < 0.00001f)
                {
                    curNorm = bishopNormals[r - 1];
                }
                else
                {
                    float angle = Vector3.Angle(prevTan, curTan);
                    Quaternion rot = Quaternion.AngleAxis(angle, v.normalized);
                    curNorm = (rot * bishopNormals[r - 1]).normalized;
                }
                bishopNormals[r] = curNorm;
                bishopBinormals[r] = Vector3.Cross(curTan, curNorm).normalized;
            }

            Matrix4x4 w2l = transform.worldToLocalMatrix;
            float baseR = radius;
            float blobFactor = effGrowth < 0.4f ? Mathf.Lerp(0.72f, 1.0f, effGrowth / 0.4f) : 1.0f;

            float pulseTime = Application.isPlaying ? Time.time : 0f;
            float slowMotionSpeed = isControlledLeakage ? 0.6f : 1.4f;

            // Build tube rings
            for (int r = 0; r < ringCount; r++)
            {
                float u = (float)r / (ringCount - 1);
                float profile = EvaluateRadiusProfile(u);
                float ringRadius = baseR * profile * blobFactor;

                Vector3 center = centerlines[r];
                Vector3 norm = bishopNormals[r];
                Vector3 binorm = bishopBinormals[r];
                Vector3 tan = spineTangents[r];

                float vCoord = reverseUVDirection ? (1f - u) * vTiling : u * vTiling;

                for (int s = 0; s <= radialCount; s++)
                {
                    float uCoord = (float)s / radialCount;
                    float angle = uCoord * Mathf.PI * 2f;
                    float cosA = Mathf.Cos(angle);
                    float sinA = Mathf.Sin(angle);

                    // Organic cross-section
                    float horizLobe = 1.05f + 0.05f * Mathf.Cos(2f * angle);
                    float wave = 1.0f + 0.035f * Mathf.Sin(u * 14f + angle * 2f);
                    float ripple = 1.0f + 0.02f * Mathf.Cos(u * 22f - angle * 3f + 1.2f);
                    float breath = 1.0f + 0.02f * Mathf.Sin(pulseTime * slowMotionSpeed + u * 4f);

                    float rEff = ringRadius * horizLobe * wave * ripple * breath;

                    Vector3 radialDir = (cosA * norm + sinA * binorm).normalized;
                    Vector3 vertPos = center + radialDir * rEff;

                    int idx = r * vertsPerRing + s;
                    vertices[idx] = w2l.MultiplyPoint3x4(vertPos);
                    normals[idx] = w2l.MultiplyVector(radialDir).normalized;
                    tangents[idx] = new Vector4(tan.x, tan.y, tan.z, 1.0f);
                    uvs[idx] = new Vector2(uCoord, vCoord);
                }
            }

            // Build hemispherical rounded cap at outer end
            int capBaseIdx = ringCount * vertsPerRing;
            Vector3 lastCenter = centerlines[ringCount - 1];
            Vector3 lastTan = spineTangents[ringCount - 1];
            Vector3 lastNorm = bishopNormals[ringCount - 1];
            Vector3 lastBinorm = bishopBinormals[ringCount - 1];
            float lastRadius = baseR * EvaluateRadiusProfile(1.0f) * blobFactor * 1.05f;

            for (int c = 1; c <= capRings; c++)
            {
                float phi = ((float)c / (capRings + 1)) * (Mathf.PI * 0.5f);
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);

                Vector3 capCenter = lastCenter + lastTan * (lastRadius * sinPhi);
                float capR = lastRadius * cosPhi;

                for (int s = 0; s <= radialCount; s++)
                {
                    float uCoord = (float)s / radialCount;
                    float angle = uCoord * Mathf.PI * 2f;
                    float cosA = Mathf.Cos(angle);
                    float sinA = Mathf.Sin(angle);

                    Vector3 radialDir = (cosA * lastNorm + sinA * lastBinorm).normalized;
                    Vector3 vertPos = capCenter + radialDir * capR;
                    Vector3 n = (radialDir * cosPhi + lastTan * sinPhi).normalized;

                    int idx = capBaseIdx + (c - 1) * vertsPerRing + s;
                    vertices[idx] = w2l.MultiplyPoint3x4(vertPos);
                    normals[idx] = w2l.MultiplyVector(n).normalized;
                    tangents[idx] = new Vector4(lastTan.x, lastTan.y, lastTan.z, 1.0f);
                    uvs[idx] = new Vector2(uCoord, (reverseUVDirection ? 0f : vTiling) + ((float)c / capRings) * 0.4f);
                }
            }

            // Rounded cap tip pole
            int tipPoleIdx = totalVerts - 2;
            Vector3 polePos = lastCenter + lastTan * (lastRadius * 1.05f);
            vertices[tipPoleIdx] = w2l.MultiplyPoint3x4(polePos);
            normals[tipPoleIdx] = w2l.MultiplyVector(lastTan).normalized;
            tangents[tipPoleIdx] = new Vector4(lastTan.x, lastTan.y, lastTan.z, 1.0f);
            uvs[tipPoleIdx] = new Vector2(0.5f, (reverseUVDirection ? 0f : vTiling) + 0.45f);

            // Base seal center vertex (closes base against shell)
            int baseCenterIdx = totalVerts - 1;
            vertices[baseCenterIdx] = w2l.MultiplyPoint3x4(centerlines[0]);
            normals[baseCenterIdx] = w2l.MultiplyVector(-spineTangents[0]).normalized;
            tangents[baseCenterIdx] = new Vector4(spineTangents[0].x, spineTangents[0].y, spineTangents[0].z, 1.0f);
            uvs[baseCenterIdx] = new Vector2(0.5f, 0f);

            // Assemble double-sided triangles
            List<int> triList = new List<int>();

            // Tube quads
            for (int r = 0; r < ringCount - 1; r++)
            {
                int r0 = r * vertsPerRing;
                int r1 = (r + 1) * vertsPerRing;
                for (int s = 0; s < radialCount; s++)
                {
                    int i0 = r0 + s;
                    int i1 = r1 + s;
                    int i2 = r1 + s + 1;
                    int i3 = r0 + s + 1;

                    // Front
                    triList.Add(i0); triList.Add(i1); triList.Add(i2);
                    triList.Add(i0); triList.Add(i2); triList.Add(i3);
                    // Back
                    triList.Add(i0); triList.Add(i2); triList.Add(i1);
                    triList.Add(i0); triList.Add(i3); triList.Add(i2);
                }
            }

            // Connection: last tube ring to first cap ring
            int lastTubeRing = (ringCount - 1) * vertsPerRing;
            int firstCapRing = capBaseIdx;
            for (int s = 0; s < radialCount; s++)
            {
                int i0 = lastTubeRing + s;
                int i1 = firstCapRing + s;
                int i2 = firstCapRing + s + 1;
                int i3 = lastTubeRing + s + 1;

                triList.Add(i0); triList.Add(i1); triList.Add(i2);
                triList.Add(i0); triList.Add(i2); triList.Add(i3);
                triList.Add(i0); triList.Add(i2); triList.Add(i1);
                triList.Add(i0); triList.Add(i3); triList.Add(i2);
            }

            // Cap ring quads
            for (int c = 0; c < capRings - 1; c++)
            {
                int ringA = capBaseIdx + c * vertsPerRing;
                int ringB = capBaseIdx + (c + 1) * vertsPerRing;
                for (int s = 0; s < radialCount; s++)
                {
                    int i0 = ringA + s;
                    int i1 = ringB + s;
                    int i2 = ringB + s + 1;
                    int i3 = ringA + s + 1;

                    triList.Add(i0); triList.Add(i1); triList.Add(i2);
                    triList.Add(i0); triList.Add(i2); triList.Add(i3);
                    triList.Add(i0); triList.Add(i2); triList.Add(i1);
                    triList.Add(i0); triList.Add(i3); triList.Add(i2);
                }
            }

            // Cap tip fan
            int lastCapRing = capBaseIdx + (capRings - 1) * vertsPerRing;
            for (int s = 0; s < radialCount; s++)
            {
                int i0 = tipPoleIdx;
                int i1 = lastCapRing + s;
                int i2 = lastCapRing + s + 1;

                triList.Add(i0); triList.Add(i1); triList.Add(i2);
                triList.Add(i0); triList.Add(i2); triList.Add(i1);
            }

            // Base seal fan
            for (int s = 0; s < radialCount; s++)
            {
                int i0 = baseCenterIdx;
                int i1 = s + 1;
                int i2 = s;

                triList.Add(i0); triList.Add(i1); triList.Add(i2);
                triList.Add(i0); triList.Add(i2); triList.Add(i1);
            }

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh();
                generatedMesh.name = "MoltenMetalFlow_Mesh";
            }
            else
            {
                generatedMesh.Clear();
            }

            generatedMesh.vertices = vertices;
            generatedMesh.normals = normals;
            generatedMesh.tangents = tangents;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triList.ToArray();
            generatedMesh.RecalculateBounds();

            if (meshFilter != null)
            {
                meshFilter.sharedMesh = generatedMesh;
            }

            if (meshRenderer != null)
            {
                if (flowMaterial != null && meshRenderer.sharedMaterial != flowMaterial)
                {
                    meshRenderer.sharedMaterial = flowMaterial;
                }
                meshRenderer.enabled = true;
            }
        }

        public void ClearMesh()
        {
            if (generatedMesh != null)
            {
                generatedMesh.Clear();
            }
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = null;
            }
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }
    }
}
