using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PotLeakage.VFX
{
    /// <summary>
    /// Controls the gradual localized red-hot shell formation effect on line (6)/Plane.045/red shell component
    /// and synchronizes the LINE/line (6)/red GameObject.
    /// Follows Plane.045's transform and smoothly transitions from 0% (normal shell) to 100% (stable bright hot red area).
    /// </summary>
    [ExecuteAlways]
    public class RedShellController : MonoBehaviour
    {
        private static RedShellController _instance;
        public static RedShellController Instance => _instance;

        [Header("Formation Progress & Timing")]
        [Range(0f, 1f)] public float formationProgress = 0.0f;
        [Tooltip("Duration in seconds for the red shell to fully form (2–4 seconds)")]
        [Range(1.5f, 5.0f)] public float formationDuration = 2.0f;

        [Header("Hot Palette Colors")]
        public Color darkHotRed = new Color(0.40f, 0.02f, 0.01f, 1.0f);
        public Color deepVibrantRed = new Color(0.85f, 0.10f, 0.04f, 1.0f);
        public Color brightRedOrangeCore = new Color(1.00f, 0.48f, 0.10f, 1.0f);

        [Header("Hotspot Dimensions (World Units)")]
        public float hotspotLength = 0.36f; // Along the ledge tangent
        public float hotspotWidth = 0.20f;  // Across the ledge
        public float surfaceOffset = 0.004f; // Prevent z-fighting with shell surface

        [Header("Components")]
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Material redShellMaterial;

        [Header("Synchronized Red GameObject")]
        public GameObject redGameObject;
        public MeshFilter redMeshFilter;
        public MeshRenderer redMeshRenderer;
        private Mesh redGeneratedQuadMesh;

        private MaterialPropertyBlock propBlock;
        private Coroutine activeFormationRoutine;
        private Mesh generatedQuadMesh;

        private void Awake()
        {
            _instance = this;
            InitializeComponents();
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            InitializeComponents();
        }

        public void InitializeComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            if (redShellMaterial == null)
            {
#if UNITY_EDITOR
                redShellMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/PotLeakage/Materials/M_RedShell_Formation.mat");
#endif
            }

            if (redShellMaterial != null && meshRenderer.sharedMaterial != redShellMaterial)
            {
                meshRenderer.sharedMaterial = redShellMaterial;
            }

            ResolveRedGameObject();
            BuildUnskewedQuadMesh();
            UpdateVisuals();
        }

        private static bool IsPersistentObject(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.IsPersistent(obj);
#else
            return false;
#endif
        }

        public void ResolveRedGameObject()
        {
            if (redGameObject == null)
            {
                if (transform.parent != null)
                {
                    Transform t = transform.parent.Find("red") ?? transform.parent.Find("Red");
                    if (t != null) redGameObject = t.gameObject;
                }
                if (redGameObject == null)
                {
                    redGameObject = GameObject.Find("LINE/line (6)/red") ?? GameObject.Find("line (6)/red") ?? GameObject.Find("red");
                }
                if (redGameObject == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<GameObject>();
                    for (int i = 0; i < all.Length; i++)
                    {
                        if ((all[i].name == "red" || all[i].name == "Red") && !IsPersistentObject(all[i]))
                        {
                            redGameObject = all[i];
                            break;
                        }
                    }
                }
            }

            if (redGameObject != null)
            {
                // Exact requested coordinates and scale for Red GameObject:
                // Position: Vector3(-6.31879997, -11.3990002, -67.3339996)
                // Scale: Vector3(1574.98523, 7523.24219, 1574.98523)
                redGameObject.transform.localPosition = new Vector3(-6.31879997f, -11.3990002f, -67.3339996f);
                redGameObject.transform.localScale = new Vector3(1574.98523f, 7523.24219f, 1574.98523f);

                if (redMeshFilter == null) redMeshFilter = redGameObject.GetComponent<MeshFilter>();
                if (redMeshFilter == null) redMeshFilter = redGameObject.AddComponent<MeshFilter>();

                if (redMeshRenderer == null) redMeshRenderer = redGameObject.GetComponent<MeshRenderer>();
                if (redMeshRenderer == null) redMeshRenderer = redGameObject.AddComponent<MeshRenderer>();

                if (redShellMaterial != null && redMeshRenderer.sharedMaterial != redShellMaterial)
                {
                    redMeshRenderer.sharedMaterial = redShellMaterial;
                }
            }
        }

        /// <summary>
        /// Generates an unskewed quad mesh whose vertices are defined directly in the local space
        /// of this transform, aligned flush to the Plane.045 slanted surface.
        /// </summary>
        public void BuildUnskewedQuadMesh()
        {
            if (meshFilter == null) return;

            // Surface normal in world space on Plane.045
            Vector3 normalWorld = new Vector3(-0.19f, 0.88f, 0.44f).normalized;
            Vector3 centerWorld = transform.position + normalWorld * surfaceOffset;

            // Tangent along the pot opening ledge
            Vector3 dirLength = new Vector3(0.0f, -0.44f, 0.88f).normalized;
            Vector3 dirWidth = Vector3.Cross(normalWorld, dirLength).normalized;

            float halfLen = hotspotLength * 0.5f;
            float halfWid = hotspotWidth * 0.5f;

            Vector3 w0 = centerWorld - dirWidth * halfWid - dirLength * halfLen;
            Vector3 w1 = centerWorld + dirWidth * halfWid - dirLength * halfLen;
            Vector3 w2 = centerWorld + dirWidth * halfWid + dirLength * halfLen;
            Vector3 w3 = centerWorld - dirWidth * halfWid + dirLength * halfLen;

            // Convert world vertices into local space of red shell component
            Vector3 l0 = transform.InverseTransformPoint(w0);
            Vector3 l1 = transform.InverseTransformPoint(w1);
            Vector3 l2 = transform.InverseTransformPoint(w2);
            Vector3 l3 = transform.InverseTransformPoint(w3);

            if (generatedQuadMesh == null)
            {
                generatedQuadMesh = new Mesh { name = "RedShell_Quad" };
            }
            else
            {
                generatedQuadMesh.Clear();
            }

            generatedQuadMesh.vertices = new Vector3[] { l0, l1, l2, l3 };
            generatedQuadMesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            generatedQuadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            generatedQuadMesh.RecalculateNormals();
            generatedQuadMesh.RecalculateBounds();

            meshFilter.sharedMesh = generatedQuadMesh;

            // Also configure quad for synchronized Red GameObject
            if (redGameObject != null && redMeshFilter != null)
            {
                Vector3 r0 = redGameObject.transform.InverseTransformPoint(w0);
                Vector3 r1 = redGameObject.transform.InverseTransformPoint(w1);
                Vector3 r2 = redGameObject.transform.InverseTransformPoint(w2);
                Vector3 r3 = redGameObject.transform.InverseTransformPoint(w3);

                if (redGeneratedQuadMesh == null)
                {
                    redGeneratedQuadMesh = new Mesh { name = "RedGO_Quad" };
                }
                else
                {
                    redGeneratedQuadMesh.Clear();
                }

                redGeneratedQuadMesh.vertices = new Vector3[] { r0, r1, r2, r3 };
                redGeneratedQuadMesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                redGeneratedQuadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
                redGeneratedQuadMesh.RecalculateNormals();
                redGeneratedQuadMesh.RecalculateBounds();

                redMeshFilter.sharedMesh = redGeneratedQuadMesh;
            }
        }

        /// <summary>
        /// Starts the gradual red shell formation animation over the specified duration.
        /// </summary>
        public void StartFormation(float duration = 2.0f)
        {
            if (gameObject != null && !gameObject.activeSelf) gameObject.SetActive(true);
            this.enabled = true;
            InitializeComponents();

            formationDuration = Mathf.Max(0.5f, duration);
            if (activeFormationRoutine != null) StopCoroutine(activeFormationRoutine);

            ResolveRedGameObject();
            if (redGameObject != null) redGameObject.SetActive(true);

            if (Application.isPlaying)
            {
                activeFormationRoutine = StartCoroutine(FormationRoutine());
            }
            else
            {
                formationProgress = 1.0f;
                UpdateVisuals();
            }

            Debug.Log("[RedShell] Red shell & Red GameObject formation initiated on Plane.045.");
        }

        public void TriggerProgressiveFormation(float duration = 2.0f) => StartFormation(duration);

        private IEnumerator FormationRoutine()
        {
            formationProgress = 0.0f;
            UpdateVisuals();

            float elapsed = 0f;
            while (elapsed < formationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / formationDuration);

                // Smooth gradual formation curve
                formationProgress = Mathf.SmoothStep(0f, 1f, t);
                UpdateVisuals();
                yield return null;
            }

            formationProgress = 1.0f;
            UpdateVisuals();
            activeFormationRoutine = null;
            Debug.Log("[RedShell] Red shell & Red GameObject formation reached stable 100% localized hot-lava state.");
        }

        /// <summary>
        /// Updates the material properties and physical quad scale based on the current formation progress.
        /// Bright hot red, localized emission, matching hot red shell visual.
        /// </summary>
        public void UpdateVisuals()
        {
            if (meshRenderer == null && redMeshRenderer == null) return;

            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            if (formationProgress <= 0.001f)
            {
                if (meshRenderer != null) meshRenderer.enabled = false;
                if (redMeshRenderer != null) redMeshRenderer.enabled = false;
                transform.localScale = Vector3.one * 0.35f;
                return;
            }

            if (meshRenderer != null) meshRenderer.enabled = true;
            if (redMeshRenderer != null) redMeshRenderer.enabled = true;

            float p = Mathf.Clamp01(formationProgress);
            float currentScale;
            Color currentColor;

            if (p < 0.25f)
            {
                float st = p / 0.25f;
                currentScale = Mathf.Lerp(0.35f, 0.55f, st);
                currentColor = Color.Lerp(new Color(0.45f, 0.02f, 0.01f, 0.60f), new Color(0.75f, 0.05f, 0.02f, 0.85f), st);
            }
            else if (p < 0.50f)
            {
                float st = (p - 0.25f) / 0.25f;
                currentScale = Mathf.Lerp(0.55f, 0.75f, st);
                currentColor = Color.Lerp(new Color(0.75f, 0.05f, 0.02f, 0.85f), new Color(0.95f, 0.08f, 0.03f, 0.95f), st);
            }
            else if (p < 0.75f)
            {
                float st = (p - 0.50f) / 0.25f;
                currentScale = Mathf.Lerp(0.75f, 0.95f, st);
                currentColor = Color.Lerp(new Color(0.95f, 0.08f, 0.03f, 0.95f), new Color(1.00f, 0.30f, 0.05f, 1.0f), st);
            }
            else
            {
                float st = (p - 0.75f) / 0.25f;
                currentScale = Mathf.Lerp(0.95f, 1.0f, st);
                currentColor = Color.Lerp(new Color(1.00f, 0.30f, 0.05f, 1.0f), new Color(1.00f, 0.50f, 0.12f, 1.0f), st);
            }

            // Slow irregular thermal breathing pulse when fully formed
            float pulse = (p >= 0.85f && Application.isPlaying)
                ? (1.0f + Mathf.Sin(Time.time * 2.0f) * 0.045f + Mathf.Sin(Time.time * 0.85f) * 0.025f)
                : 1.0f;

            Color activeColor = new Color(
                Mathf.Clamp01(currentColor.r * pulse),
                Mathf.Clamp01(currentColor.g * pulse),
                Mathf.Clamp01(currentColor.b * pulse),
                currentColor.a
            );

            // Bright hot red HDR emission
            Color emissionColor = new Color(
                currentColor.r * 4.2f * pulse,
                currentColor.g * 2.2f * pulse,
                currentColor.b * 0.5f * pulse,
                1.0f
            );

            transform.localScale = Vector3.one * currentScale;

            propBlock.SetColor("_BaseColor", activeColor);
            propBlock.SetColor("_Color", activeColor);
            propBlock.SetColor("_EmissionColor", emissionColor);

            if (meshRenderer != null)
            {
                meshRenderer.SetPropertyBlock(propBlock);
            }

            if (redMeshRenderer != null)
            {
                redMeshRenderer.SetPropertyBlock(propBlock);
            }
        }

        private void Update()
        {
            if (formationProgress >= 0.90f && Application.isPlaying)
            {
                UpdateVisuals();
            }
        }

        /// <summary>
        /// Disables the red shell component and sets Red GameObject inactive immediately.
        /// Preserves original transforms, materials, and hierarchy.
        /// </summary>
        public void DisableRedShell()
        {
            if (activeFormationRoutine != null)
            {
                StopCoroutine(activeFormationRoutine);
                activeFormationRoutine = null;
            }

            formationProgress = 0.0f;
            if (redGameObject != null)
            {
                redGameObject.SetActive(false);
            }
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
            this.enabled = false;
            if (gameObject != null && gameObject.name == "RedShell")
            {
                gameObject.SetActive(false);
            }
            Debug.Log("[RedShell] Red shell component disabled and Red GameObject set inactive.");
        }

        /// <summary>
        /// Resets the red shell to 0% (invisible normal shell).
        /// </summary>
        public void ResetRedShell()
        {
            if (activeFormationRoutine != null)
            {
                StopCoroutine(activeFormationRoutine);
                activeFormationRoutine = null;
            }

            formationProgress = 0.0f;
            transform.localScale = Vector3.one * 0.35f;
            UpdateVisuals();
            if (redGameObject != null) redGameObject.SetActive(false);
            Debug.Log("[RedShell] Red shell formation reset to 0% (normal shell).");
        }
    }
}
