using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotLeakage.VFX
{
    /// <summary>
    /// Manages the cooling pipes stage and directional white cooling gas plume
    /// emitted from PIPES/GasPoint, PIPES/GasPoint1, and PIPES/GasPoint2.
    /// Matched to reference images: dense, soft white, directional pressurized gas
    /// expanding from narrow nozzle outlets toward the molten leakage / stopper area.
    /// Strictly kept inactive until STOPPER APPLIED stage, and reset on task exit.
    /// </summary>
    [ExecuteAlways]
    public class CoolingPipesController : MonoBehaviour
    {
        private static CoolingPipesController _instance;
        public static CoolingPipesController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<CoolingPipesController>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        [Header("Gas Point Transforms (Read-Only Outlets)")]
        [Tooltip("Top pipe outlet")]
        public Transform gasPoint;
        [Tooltip("Bottom pipe outlet")]
        public Transform gasPoint1;
        [Tooltip("Middle pipe outlet")]
        public Transform gasPoint2;

        [Header("Target Leakage Area")]
        [Tooltip("Target area where molten leakage and stopper are located")]
        public Vector3 targetLeakagePosition = new Vector3(-6.269f, 0.744f, -229.287f);

        [Header("Cooling Gas Intensity Parameters (Inspector-Tuned)")]
        [SerializeField] [Min(0f)] public float coolingGasEmissionRate = 600.0f;
        [SerializeField] [Min(0f)] public float coolingGasForwardSpeed = 1.90f;
        [SerializeField] [Min(0f)] public float coolingGasParticleLifetime = 0.45f;

        // Backward compatibility properties
        public float targetEmissionRate { get => coolingGasEmissionRate; set => coolingGasEmissionRate = value; }
        public float startSpeed { get => coolingGasForwardSpeed; set => coolingGasForwardSpeed = value; }
        public float startLifetime { get => coolingGasParticleLifetime; set => coolingGasParticleLifetime = value; }

        [Range(0.02f, 0.30f)] public float startSize = 0.055f;
        [Range(0.005f, 0.04f)] public float coneRadius = 0.012f;
        [Range(3f, 20f)] public float coneAngle = 6.0f;
        [Range(0.001f, 0.05f)] public float noiseStrength = 0.006f;
        [Range(0.1f, 1.0f)] public float noiseFrequency = 0.30f;
        public Color gasColor = new Color(1.0f, 1.0f, 1.0f, 0.98f);

        [Header("Secondary Density Layer (GasDensity_Fill Soft White Cloud Volume)")]
        public bool enableSecondaryDensity = true;
        [Range(10f, 1000f)] public float secondaryEmissionRate = 450.0f;
        [Range(0.02f, 0.40f)] public float secondaryStartSize = 0.075f;
        [Range(0.5f, 5.0f)] public float secondaryStartSpeed = 1.75f;
        [Range(0.2f, 2.0f)] public float secondaryStartLifetime = 0.48f;
        public Color secondaryGasColor = new Color(0.98f, 0.99f, 1.0f, 0.94f);

        [Header("Startup Buildup")]
        [Tooltip("Duration in seconds for the initial gas buildup (0.0s -> 0.2s -> 0.5s -> 1.0s)")]
        public float startupBuildupDuration = 1.0f;

        [Header("Material")]
        public Material gasMaterial;

        [Header("Runtime State")]
        public bool isCoolingActive = false;

        private readonly List<ParticleSystem> primarySystems = new List<ParticleSystem>();
        private readonly List<ParticleSystem> secondarySystems = new List<ParticleSystem>();
        private Coroutine startupCoroutine;
        private Coroutine dissipationCoroutine;

        private void Awake()
        {
            _instance = this;
            InitializeReferences();
        }

        private void OnEnable()
        {
            InitializeReferences();
        }

        public void InitializeReferences()
        {
            // Find GasPoints recursively under PIPES (BézierCurve.001 and BézierCurve.002)
            if (gasPoint == null) gasPoint = FindChildRecursive(transform, "GasPoint");
            if (gasPoint1 == null) gasPoint1 = FindChildRecursive(transform, "GasPoint1");
            if (gasPoint2 == null) gasPoint2 = FindChildRecursive(transform, "GasPoint2");

            // Auto-resolve target leakage position if stopper or drop exists
            ResolveTargetPosition();

            // Load dedicated soft white cooling gas material
            if (gasMaterial == null)
            {
#if UNITY_EDITOR
                gasMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/PotLeakage/Materials/M_CoolingPipes_Gas.mat");
                if (gasMaterial == null)
                {
                    gasMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/PotLeakage/Materials/M_MoltenLeakage_Smoke.mat");
                }
#endif
            }

            EnsureParticleSystems();
        }

        private void ResolveTargetPosition()
        {
            var stopper = GameObject.Find("SIDEREAKING TOOL (5)");
            if (stopper != null)
            {
                targetLeakagePosition = stopper.transform.position;
                return;
            }
            var drop = GameObject.Find("drop");
            if (drop != null)
            {
                targetLeakagePosition = drop.transform.position;
            }
        }

        public void EnsureParticleSystems()
        {
            primarySystems.Clear();
            secondarySystems.Clear();

            SetupGasPointSystems(gasPoint);
            SetupGasPointSystems(gasPoint1);
            SetupGasPointSystems(gasPoint2);
        }

        private void SetupGasPointSystems(Transform point)
        {
            if (point == null) return;

            // Direct parallel nozzle spray with slight downward pitch (10 degrees) to conform to the working surface
            Vector3 streamDir = (point.forward + Vector3.down * 0.18f).normalized;

            Quaternion targetWorldRot = Quaternion.LookRotation(streamDir, Vector3.up);
            Quaternion localShapeRot = Quaternion.Inverse(point.rotation) * targetWorldRot;
            Vector3 shapeEuler = localShapeRot.eulerAngles;

            // 1. Primary Directional System (attached directly to GasPoint)
            var ps = point.GetComponent<ParticleSystem>();
            if (ps == null) ps = point.gameObject.AddComponent<ParticleSystem>();

            var psr = point.GetComponent<ParticleSystemRenderer>();
            if (psr == null) psr = point.gameObject.AddComponent<ParticleSystemRenderer>();

            ConfigurePrimaryParticleSystem(ps, psr, shapeEuler);
            primarySystems.Add(ps);

            // 2. Secondary Density System (attached to child "GasDensity_Fill")
            if (enableSecondaryDensity)
            {
                Transform secChild = point.Find("GasDensity_Fill");
                if (secChild == null)
                {
                    var go = new GameObject("GasDensity_Fill");
                    go.transform.SetParent(point, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    secChild = go.transform;
                }

                var secPs = secChild.GetComponent<ParticleSystem>();
                if (secPs == null) secPs = secChild.gameObject.AddComponent<ParticleSystem>();

                var secPsr = secChild.GetComponent<ParticleSystemRenderer>();
                if (secPsr == null) secPsr = secChild.gameObject.AddComponent<ParticleSystemRenderer>();

                ConfigureSecondaryParticleSystem(secPs, secPsr, shapeEuler);
                secondarySystems.Add(secPs);
            }
        }

        private void ConfigurePrimaryParticleSystem(ParticleSystem ps, ParticleSystemRenderer psr, Vector3 shapeEuler)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(startLifetime * 0.9f, startLifetime * 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed * 0.90f, startSpeed * 1.10f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.85f, startSize * 1.20f);
            main.startColor = new ParticleSystem.MinMaxGradient(gasColor, new Color(gasColor.r * 0.96f, gasColor.g * 0.96f, gasColor.b * 0.98f, gasColor.a * 0.90f));
            main.gravityModifier = 0.02f; // Slight gravity pull to keep gas conforming to the surface
            main.maxParticles = 1500;

            // Emission (starts at 0, ramped by StartupBuildupRoutine)
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = isCoolingActive ? coolingGasEmissionRate : 0f;

            // Shape (narrow cone ~6 degrees, nozzle radius ~0.012m)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = coneRadius;
            shape.rotation = shapeEuler;

            // Size Over Lifetime: narrow at nozzle (0.9x), expanding smoothly to ~2.2x at max distance (~0.12m max particle size)
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.90f);
            sizeCurve.AddKey(0.35f, 1.40f);
            sizeCurve.AddKey(0.70f, 1.85f);
            sizeCurve.AddKey(1.0f, 2.30f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // Color Over Lifetime: dense white spray, soft gray/translucent fade
            var colOverLifetime = ps.colorOverLifetime;
            colOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(new Color(0.98f, 0.99f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.95f, 0.96f, 0.98f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.92f, 0.05f),
                    new GradientAlphaKey(0.88f, 0.50f),
                    new GradientAlphaKey(0.40f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            // Noise: subtle organic wobble without dispersing the narrow jet
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = noiseFrequency;
            noise.scrollSpeed = 0.20f;
            noise.damping = true;

            // Limit Velocity Over Lifetime: damp forward velocity to contain gas strictly within pot opening (~0.8m)
            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.limit = 0.35f;
            limitVel.dampen = 0.45f;

            // Renderer
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.alignment = ParticleSystemRenderSpace.View;
            psr.sortMode = ParticleSystemSortMode.Distance;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;

            if (gasMaterial != null && psr.sharedMaterial != gasMaterial)
            {
                psr.sharedMaterial = gasMaterial;
            }
        }

        private void ConfigureSecondaryParticleSystem(ParticleSystem ps, ParticleSystemRenderer psr, Vector3 shapeEuler)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(secondaryStartLifetime * 0.9f, secondaryStartLifetime * 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(secondaryStartSpeed * 0.90f, secondaryStartSpeed * 1.10f);
            main.startSize = new ParticleSystem.MinMaxCurve(secondaryStartSize * 0.85f, secondaryStartSize * 1.25f);
            main.startColor = secondaryGasColor;
            main.gravityModifier = 0.02f;
            main.maxParticles = 1200;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = isCoolingActive ? secondaryEmissionRate : 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle * 1.20f;
            shape.radius = coneRadius * 1.20f;
            shape.rotation = shapeEuler;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 1.00f);
            sizeCurve.AddKey(0.35f, 1.50f);
            sizeCurve.AddKey(0.70f, 1.95f);
            sizeCurve.AddKey(1.0f, 2.35f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var colOverLifetime = ps.colorOverLifetime;
            colOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(new Color(0.97f, 0.98f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.94f, 0.95f, 0.97f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.06f),
                    new GradientAlphaKey(0.80f, 0.50f),
                    new GradientAlphaKey(0.35f, 0.82f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength * 0.75f;
            noise.frequency = noiseFrequency * 0.8f;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;

            // Limit Velocity Over Lifetime: damp forward velocity to contain gas strictly within pot opening
            var secLimitVel = ps.limitVelocityOverLifetime;
            secLimitVel.enabled = true;
            secLimitVel.limit = 0.35f;
            secLimitVel.dampen = 0.45f;

            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.alignment = ParticleSystemRenderSpace.View;
            psr.sortMode = ParticleSystemSortMode.Distance;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;

            if (gasMaterial != null && psr.sharedMaterial != gasMaterial)
            {
                psr.sharedMaterial = gasMaterial;
            }
        }

        /// <summary>
        /// Activates the cooling pipes GameObject and starts white gas particle emission with startup buildup.
        /// </summary>
        public void ActivateCoolingPipes()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            InitializeReferences();
            isCoolingActive = true;

            if (startupCoroutine != null)
            {
                StopCoroutine(startupCoroutine);
            }

            if (Application.isPlaying)
            {
                startupCoroutine = StartCoroutine(StartupBuildupRoutine());
            }
            else
            {
                // In Edit Mode, immediately apply full emission rate
                ApplyEmissionRate(1.0f);
                PlayAllSystems();
            }

            Debug.Log("[CoolingPipes] PIPES GameObject activated with dense directional white cooling gas on GasPoint, GasPoint1, GasPoint2.");
        }

        /// <summary>
        /// Ensures cooling gas is active and running at full intensified rate (e.g. for PTM crane sequence).
        /// </summary>
        public void IntensifyCoolingGas()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            InitializeReferences();
            isCoolingActive = true;

            if (startupCoroutine != null)
            {
                StopCoroutine(startupCoroutine);
                startupCoroutine = null;
            }

            StopGasDissipation();

            ApplyEmissionRate(1.0f);
            PlayAllSystems();
            Debug.Log("[CoolingPipes] Intensified white cooling gas active and spraying at full density.");
        }

        /// <summary>
        /// Initiates the gradual die-off of cooling gas over the specified duration (default 5.0s):
        /// 0-1s: strong density
        /// 1-2s: high density
        /// 2-3s: gradual reduction
        /// 3-4s: noticeably weaker
        /// 4-5s: mostly dissipated
        /// 5s+: minimal / stopped
        /// </summary>
        public void StartGasDissipation(float duration = 5.0f)
        {
            StopGasDissipation();

            if (Application.isPlaying)
            {
                dissipationCoroutine = StartCoroutine(GasDissipationRoutine(duration));
            }
            else
            {
                ApplyEmissionRate(0.0f);
            }
            Debug.Log($"[CoolingPipes] Started gradual cooling gas dissipation over {duration:F1}s.");
        }

        public void StopGasDissipation()
        {
            if (dissipationCoroutine != null)
            {
                StopCoroutine(dissipationCoroutine);
                dissipationCoroutine = null;
            }
        }

        private IEnumerator GasDissipationRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Multi-stage gradual die-off curve:
                // 0.0 -> 0.2 (0-1s): 1.0 -> 0.90 (strong density)
                // 0.2 -> 0.4 (1-2s): 0.90 -> 0.65 (high density)
                // 0.4 -> 0.6 (2-3s): 0.65 -> 0.35 (gradual reduction)
                // 0.6 -> 0.8 (3-4s): 0.35 -> 0.12 (noticeably weaker)
                // 0.8 -> 1.0 (4-5s): 0.12 -> 0.00 (mostly dissipated to zero)
                float fraction;
                if (t < 0.2f)
                {
                    fraction = Mathf.Lerp(1.0f, 0.90f, t / 0.2f);
                }
                else if (t < 0.4f)
                {
                    fraction = Mathf.Lerp(0.90f, 0.65f, (t - 0.2f) / 0.2f);
                }
                else if (t < 0.6f)
                {
                    fraction = Mathf.Lerp(0.65f, 0.35f, (t - 0.4f) / 0.2f);
                }
                else if (t < 0.8f)
                {
                    fraction = Mathf.Lerp(0.35f, 0.12f, (t - 0.6f) / 0.2f);
                }
                else
                {
                    fraction = Mathf.Lerp(0.12f, 0.00f, (t - 0.8f) / 0.2f);
                }

                ApplyEmissionRate(fraction);
                yield return null;
            }

            ApplyEmissionRate(0.0f);
            dissipationCoroutine = null;
            Debug.Log("[CoolingPipes] Cooling gas dissipation complete (die-off ended).");
        }

        private IEnumerator StartupBuildupRoutine()
        {
            // Frame 1: 0.0 sec - no gas
            ApplyEmissionRate(0.0f);
            PlayAllSystems();

            // Frame 2: 0.0s -> 0.2s - small gas emission begins
            float elapsed = 0f;
            while (elapsed < 0.20f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.20f);
                ApplyEmissionRate(Mathf.Lerp(0f, 0.25f, t));
                yield return null;
            }

            // Frame 3: 0.2s -> 0.5s - continuous stream emerges and expands
            while (elapsed < 0.50f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01((elapsed - 0.20f) / 0.30f);
                ApplyEmissionRate(Mathf.Lerp(0.25f, 0.60f, t));
                yield return null;
            }

            // Frame 4 & 5: 0.5s -> 1.0s - streams merge into full dense cooling plume
            while (elapsed < startupBuildupDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01((elapsed - 0.50f) / (startupBuildupDuration - 0.50f));
                ApplyEmissionRate(Mathf.Lerp(0.60f, 1.00f, t));
                yield return null;
            }

            // Frame 6: 1.0s+ - sustained full cooling plume
            ApplyEmissionRate(1.0f);
            startupCoroutine = null;
        }

        private void PlayAllSystems()
        {
            foreach (var ps in primarySystems)
            {
                if (ps != null)
                {
                    ps.gameObject.SetActive(true);
                    if (!ps.isPlaying) ps.Play();
                }
            }
            foreach (var ps in secondarySystems)
            {
                if (ps != null)
                {
                    ps.gameObject.SetActive(true);
                    if (!ps.isPlaying) ps.Play();
                }
            }
        }

        private void ApplyEmissionRate(float normalizedFraction)
        {
            float ratePrimary = targetEmissionRate * normalizedFraction;
            foreach (var ps in primarySystems)
            {
                if (ps != null)
                {
                    var em = ps.emission;
                    em.enabled = true;
                    em.rateOverTime = ratePrimary;
                }
            }

            float rateSecondary = secondaryEmissionRate * normalizedFraction;
            foreach (var ps in secondarySystems)
            {
                if (ps != null)
                {
                    var em = ps.emission;
                    em.enabled = true;
                    em.rateOverTime = rateSecondary;
                }
            }
        }

        /// <summary>
        /// Deactivates the cooling pipes GameObject and stops particle emission cleanly.
        /// </summary>
        public void DeactivateCoolingPipes()
        {
            isCoolingActive = false;

            StopGasDissipation();

            if (startupCoroutine != null)
            {
                StopCoroutine(startupCoroutine);
                startupCoroutine = null;
            }

            foreach (var ps in primarySystems)
            {
                if (ps != null)
                {
                    var em = ps.emission;
                    em.rateOverTime = 0f;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            foreach (var ps in secondarySystems)
            {
                if (ps != null)
                {
                    var em = ps.emission;
                    em.rateOverTime = 0f;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            Debug.Log("[CoolingPipes] PIPES GameObject deactivated and cooling gas particle effects stopped.");
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName) return t;
            }
            return null;
        }
    }
}
