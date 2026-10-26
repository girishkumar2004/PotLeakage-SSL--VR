using System.Collections;
using UnityEngine;

namespace PotLeakage.VFX
{
    /// <summary>
    /// Generates an emergent, vigorous, particle-driven molten aluminium leakage.
    /// Matched to reference animation target:
    /// 1. Small molten blob appears directly at leakage opening (MoltenLeak/drop).
    /// 2. Blob slowly grows outward.
    /// 3. Molten tongue extends farther outward along ledge.
    /// 4. Tongue becomes longer and slightly thicker.
    /// 5. Tongue continues extending with vigorous molten movement and subtle downward sag over lip.
    /// 6. Final state is a thick, irregular, actively moving molten tongue formed of overlapping blobs.
    /// </summary>
    [ExecuteAlways]
    public class MoltenMetalStreamParticles : MonoBehaviour
    {
        [Header("Authoritative Path Transforms (READ-ONLY)")]
        [Tooltip("Source transform: CameraSystem/TransformPoints/drop")]
        public Transform drop;

        [Tooltip("Mid-curve transform: CameraSystem/TransformPoints/drop (1)")]
        public Transform drop1;

        [Tooltip("Downstream falling-tip transform: CameraSystem/TransformPoints/drop (2)")]
        public Transform drop2;

        [Tooltip("Optional reference source: MoltenLeak")]
        public Transform moltenLeak;

        [Header("Particle System & Material")]
        public ParticleSystem particleSys;
        public ParticleSystemRenderer particleRenderer;
        public Material particleMaterial;

        [Header("Master Speed & Emission Controls")]
        [SerializeField]
        [Range(0.02f, 1.0f)]
        [Tooltip("Global speed multiplier for MoltenMetalStreamParticles. Lower values make the molten leakage significantly slower.")]
        public float moltenFlowSpeed = 0.15f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Controls the width of the molten particle emission at the leakage opening.")]
        public float moltenEmissionWidth = 0.15f;

        [Header("Stopper Stage Reduction")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Intensity multiplier after stopper is applied (approx 70-80%).")]
        public float stopperFlowMultiplier = 0.75f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Additional speed multiplier after stopper is applied.")]
        public float stopperSpeedMultiplier = 0.80f;

        [Header("Cooling Pipes Stage Reduction")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Intensity multiplier after cooling pipes activate (approx 25-40%).")]
        public float pipesFlowMultiplier = 0.35f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Additional speed multiplier after cooling pipes activate.")]
        public float pipesSpeedMultiplier = 0.55f;

        [Header("Full Leakage Settings (Vigorous Active Molten Tongue)")]
        [Tooltip("Particles spawned per second for continuous overlapping molten tongue")]
        [Range(15f, 60f)] public float fullEmissionRate = 38f;

        [Tooltip("Base lifetime in seconds for molten blobs")]
        [Range(4.0f, 16.0f)] public float baseLifetime = 8.0f;

        [Header("Particle Size Classes (Mixture: small, medium, large)")]
        [Tooltip("Small particle base size (surface bubbling & irregularity)")]
        public float smallParticleSize = 0.045f;
        [Tooltip("Medium particle base size (molten tongue body)")]
        public float mediumParticleSize = 0.075f;
        [Tooltip("Large particle base size (thick molten core)")]
        public float largeParticleSize = 0.110f;

        [Header("Turbulence & Dynamic Wobble")]
        [Range(0.001f, 0.02f)] public float turbulenceStrength = 0.005f;
        [Range(0.5f, 5.0f)] public float turbulenceFrequency = 1.5f;

        public enum LeakageStage
        {
            Full,
            StopperApplied,
            PipesCooling
        }

        [Header("Current Runtime State")]
        public bool isStreamActive = false;
        public bool isControlled = false;
        public LeakageStage currentStage = LeakageStage.Full;

        // Current interpolated runtime multipliers
        public float curFlowMultiplier = 1.0f;
        public float curSpeedMultiplier = 1.0f;
        public float curControlledFactor = 0f;
        public float curEmissionRate;
        public float curSizeMultiplier = 1.0f;
        public float curSpreadMultiplier = 1.0f;

        private Coroutine transitionCoroutine;
        private ParticleSystem.Particle[] particleBuffer;
        private const int MAX_PARTICLES = 600;

        // Hot molten aluminium color palette (glowing yellow core -> bright orange -> dark orange/red edge)
        private static readonly Color ColorCoreHot    = new Color(1.00f, 0.95f, 0.65f, 0.98f); // Hot glowing core
        private static readonly Color ColorYellowHot  = new Color(1.00f, 0.78f, 0.20f, 0.96f); // Bright yellow-orange
        private static readonly Color ColorOrangeBody = new Color(1.00f, 0.45f, 0.00f, 0.94f); // Vivid molten orange
        private static readonly Color ColorDarkOrange = new Color(0.95f, 0.30f, 0.00f, 0.90f); // Deep molten orange
        private static readonly Color ColorEdgeRed    = new Color(0.75f, 0.18f, 0.02f, 0.75f); // Dark cooling edge

        private void Awake()
        {
            InitializeComponents();
            ResetParametersToFull();
        }

        private void OnEnable()
        {
            InitializeComponents();
            if (particleBuffer == null || particleBuffer.Length < MAX_PARTICLES)
            {
                particleBuffer = new ParticleSystem.Particle[MAX_PARTICLES];
            }
        }

        public void InitializeComponents()
        {
            ResolveDropTransforms();

            if (particleSys == null)
            {
                particleSys = GetComponent<ParticleSystem>();
            }

            if (particleRenderer == null && particleSys != null)
            {
                particleRenderer = particleSys.GetComponent<ParticleSystemRenderer>();
            }

            if (particleMaterial == null)
            {
#if UNITY_EDITOR
                particleMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/PotLeakage/Materials/M_MoltenStream_Particle.mat");
#endif
            }

            SetupParticleSystem();
        }

        public void ResolveDropTransforms()
        {
            if (drop == null)
            {
                var g = GameObject.Find("CameraSystem/TransformPoints/drop")
                     ?? GameObject.Find("TransformPoints/drop")
                     ?? GameObject.Find("drop");
                if (g != null) drop = g.transform;
            }

            if (drop1 == null)
            {
                var g = GameObject.Find("CameraSystem/TransformPoints/drop (1)")
                     ?? GameObject.Find("TransformPoints/drop (1)")
                     ?? GameObject.Find("drop (1)");
                if (g != null) drop1 = g.transform;
            }

            if (drop2 == null)
            {
                var g = GameObject.Find("CameraSystem/TransformPoints/drop (2)")
                     ?? GameObject.Find("TransformPoints/drop (2)")
                     ?? GameObject.Find("drop (2)");
                if (g != null) drop2 = g.transform;
            }

            if (moltenLeak == null)
            {
                var g = GameObject.Find("VFX/MoltenLeak") ?? GameObject.Find("MoltenLeak");
                if (g != null) moltenLeak = g.transform;
            }
        }

        public void SetupParticleSystem()
        {
            if (particleSys == null) return;

            float effectiveSpeed = moltenFlowSpeed * curSpeedMultiplier;
            float speedFactor = effectiveSpeed * 0.8889f;

            var main = particleSys.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = effectiveSpeed * 0.1f;
            main.startLifetime = Mathf.Clamp(1.08f / speedFactor, 7.5f, 16.0f);
            main.maxParticles = MAX_PARTICLES;
            main.startSize = mediumParticleSize;
            main.startColor = ColorCoreHot;

            var emission = particleSys.emission;
            emission.enabled = true;
            emission.rateOverTime = curEmissionRate > 0f ? curEmissionRate : fullEmissionRate;

            var shape = particleSys.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(moltenEmissionWidth, 0.035f, 0.015f);

            if (drop != null)
            {
                transform.position = drop.position;
            }

            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.alignment = ParticleSystemRenderSpace.View;
                particleRenderer.sortMode = ParticleSystemSortMode.Distance;
                particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                particleRenderer.receiveShadows = false;

                if (particleMaterial != null && particleRenderer.sharedMaterial != particleMaterial)
                {
                    particleRenderer.sharedMaterial = particleMaterial;
                }
            }
        }

        public void ResetParametersToFull()
        {
            curFlowMultiplier = 1.0f;
            curSpeedMultiplier = 1.0f;
            curControlledFactor = 0f;
            isControlled = false;
            currentStage = LeakageStage.Full;

            curEmissionRate = fullEmissionRate;
            curSizeMultiplier = 1.0f;
            curSpreadMultiplier = 1.0f;

            if (particleSys != null)
            {
                var main = particleSys.main;
                float effectiveSpeed = moltenFlowSpeed * curSpeedMultiplier;
                float speedFactor = effectiveSpeed * 0.8889f;
                main.startLifetime = Mathf.Clamp(1.08f / speedFactor, 7.5f, 16.0f);
                main.startSpeed = effectiveSpeed * 0.1f;
                var em = particleSys.emission;
                em.rateOverTime = curEmissionRate;
            }
        }

        public void PlayStream()
        {
            InitializeComponents();
            ResetParametersToFull();
            isStreamActive = true;

            if (particleSys != null)
            {
                var em = particleSys.emission;
                em.enabled = true;
                em.rateOverTime = curEmissionRate;
                if (!particleSys.isPlaying)
                {
                    particleSys.Play();
                }
            }
        }

        public void StopStream()
        {
            isStreamActive = false;
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (particleSys != null)
            {
                particleSys.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void ApplyStopperReduction(float duration = 1.2f)
        {
            currentStage = LeakageStage.StopperApplied;
            isControlled = true;
            StartStageTransition(stopperFlowMultiplier, stopperSpeedMultiplier, 0.50f, duration);
        }

        public void ApplyPipesReduction(float duration = 2.0f)
        {
            currentStage = LeakageStage.PipesCooling;
            isControlled = true;
            StartStageTransition(pipesFlowMultiplier, pipesSpeedMultiplier, 1.00f, duration);
        }

        public void ApplyCoolingVoiceFinishedReduction(float duration = 1.5f)
        {
            currentStage = LeakageStage.PipesCooling;
            isControlled = true;
            StartStageTransition(pipesFlowMultiplier * 0.90f, pipesSpeedMultiplier, 1.25f, duration);
        }

        public void ResetToFull(float duration = 0f)
        {
            currentStage = LeakageStage.Full;
            isControlled = false;
            if (duration > 0.05f && Application.isPlaying && gameObject.activeInHierarchy)
            {
                StartStageTransition(1.0f, 1.0f, 0.0f, duration);
            }
            else
            {
                if (transitionCoroutine != null)
                {
                    StopCoroutine(transitionCoroutine);
                    transitionCoroutine = null;
                }
                ResetParametersToFull();
            }
        }

        public void SetControlled(bool controlled, float duration = 1.5f)
        {
            if (controlled)
            {
                ApplyStopperReduction(duration);
            }
            else
            {
                ResetToFull(duration);
            }
        }

        private void StartStageTransition(float targetFlow, float targetSpeed, float targetControlled, float duration)
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (gameObject.activeInHierarchy && Application.isPlaying && duration > 0.05f)
            {
                transitionCoroutine = StartCoroutine(TransitionToStageRoutine(targetFlow, targetSpeed, targetControlled, duration));
            }
            else
            {
                ApplyStageStateInstant(targetFlow, targetSpeed, targetControlled);
            }
        }

        private void ApplyStageStateInstant(float targetFlow, float targetSpeed, float targetControlled)
        {
            curFlowMultiplier = targetFlow;
            curSpeedMultiplier = targetSpeed;
            curControlledFactor = targetControlled;
            curEmissionRate = fullEmissionRate * targetFlow;
            if (curControlledFactor <= 1.0f)
            {
                curSizeMultiplier = Mathf.Lerp(1.0f, 0.85f, curControlledFactor);
                curSpreadMultiplier = Mathf.Lerp(1.0f, 0.78f, curControlledFactor);
            }
            else
            {
                float extra = Mathf.Clamp01((curControlledFactor - 1.0f) / 0.25f);
                curSizeMultiplier = Mathf.Lerp(0.85f, 0.75f, extra);
                curSpreadMultiplier = Mathf.Lerp(0.78f, 0.65f, extra);
            }

            if (particleSys != null)
            {
                var em = particleSys.emission;
                em.rateOverTime = curEmissionRate;

                var main = particleSys.main;
                float effectiveSpeed = moltenFlowSpeed * curSpeedMultiplier;
                float speedFactor = effectiveSpeed * 0.8889f;
                main.startLifetime = Mathf.Clamp(1.08f / speedFactor, 7.5f, 16.0f);
                main.startSpeed = effectiveSpeed * 0.1f;
            }
        }

        private IEnumerator TransitionToStageRoutine(float targetFlow, float targetSpeed, float targetControlled, float duration)
        {
            float fromFlow = curFlowMultiplier;
            float fromSpeed = curSpeedMultiplier;
            float fromControlled = curControlledFactor;
            float fromEmission = curEmissionRate;
            float targetEmission = fullEmissionRate * targetFlow;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                curFlowMultiplier = Mathf.Lerp(fromFlow, targetFlow, smooth);
                curSpeedMultiplier = Mathf.Lerp(fromSpeed, targetSpeed, smooth);
                curControlledFactor = Mathf.Lerp(fromControlled, targetControlled, smooth);
                curEmissionRate = Mathf.Lerp(fromEmission, targetEmission, smooth);
                if (curControlledFactor <= 1.0f)
                {
                    curSizeMultiplier = Mathf.Lerp(1.0f, 0.85f, curControlledFactor);
                    curSpreadMultiplier = Mathf.Lerp(1.0f, 0.78f, curControlledFactor);
                }
                else
                {
                    float extra = Mathf.Clamp01((curControlledFactor - 1.0f) / 0.25f);
                    curSizeMultiplier = Mathf.Lerp(0.85f, 0.75f, extra);
                    curSpreadMultiplier = Mathf.Lerp(0.78f, 0.65f, extra);
                }

                if (particleSys != null)
                {
                    var em = particleSys.emission;
                    em.rateOverTime = curEmissionRate;

                    var main = particleSys.main;
                    float effectiveSpeed = moltenFlowSpeed * curSpeedMultiplier;
                    float speedFactor = effectiveSpeed * 0.8889f;
                    main.startLifetime = Mathf.Clamp(1.08f / speedFactor, 7.5f, 16.0f);
                    main.startSpeed = effectiveSpeed * 0.1f;
                }

                yield return null;
            }

            ApplyStageStateInstant(targetFlow, targetSpeed, targetControlled);
            transitionCoroutine = null;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                UpdateParticles();
            }
        }

        private void LateUpdate()
        {
            UpdateParticles();
        }

        public void UpdateParticles()
        {
            if (particleSys == null) return;

            ResolveDropTransforms();
            if (drop == null || drop1 == null || drop2 == null) return;

            // Anchor emitter origin at drop source
            transform.position = drop.position;

            if (particleBuffer == null || particleBuffer.Length < particleSys.main.maxParticles)
            {
                particleBuffer = new ParticleSystem.Particle[particleSys.main.maxParticles];
            }

            int count = particleSys.GetParticles(particleBuffer);
            if (count == 0) return;

            Vector3 p0 = drop.position;
            Vector3 p1 = drop1.position;
            Vector3 p2 = drop2.position;

            float timeNow = Application.isPlaying ? Time.time : 0f;

            // Authoritative master speed control: moltenFlowSpeed * curSpeedMultiplier
            float effectiveSpeed = moltenFlowSpeed * curSpeedMultiplier;
            float speedFactor = effectiveSpeed * 0.8889f;
            float speedScale = effectiveSpeed / 0.15f;

            for (int i = 0; i < count; i++)
            {
                float lifetime = particleBuffer[i].startLifetime;
                if (lifetime <= 0.001f) continue;

                float age = lifetime - particleBuffer[i].remainingLifetime;
                // Progress along path is strictly governed by age * speedFactor
                float progress = Mathf.Clamp01(age * speedFactor);
                uint seed = particleBuffer[i].randomSeed;

                // 1. Path spline along drop -> drop (1) -> drop (2)
                Vector3 pathPos = EvaluateSpline(p0, p1, p2, progress);

                // 2. Radial envelope: wide along X (shell opening width), organic ellipse
                float currentWidth = Mathf.Lerp(moltenEmissionWidth, moltenEmissionWidth * 0.70f, Mathf.SmoothStep(0f, 1f, progress)) * curSpreadMultiplier;
                Vector3 radialOffset = GetOrganicRadialOffset(seed, currentWidth, progress);

                // 3. Dynamic organic turbulence / viscous wobble (scaled down with master speed)
                float wobbleTime = timeNow * (turbulenceFrequency * 0.5f * speedScale) + age * (1.0f * speedScale);
                float wobbleX = Mathf.Sin(wobbleTime + (seed & 0xFF)) * (turbulenceStrength * 0.5f);
                float wobbleY = Mathf.Cos(wobbleTime * 0.75f + ((seed >> 8) & 0xFF)) * (turbulenceStrength * 0.35f);
                Vector3 wobble = new Vector3(wobbleX, wobbleY, 0f);

                particleBuffer[i].position = pathPos + radialOffset + wobble;

                // 4. Particle size over lifetime (wide initial blob, slow growth, organic tongue)
                float classBaseSize = GetParticleClassBaseSize(seed);
                float sizeGrowthCurve = EvaluateSizeGrowthCurve(progress);
                particleBuffer[i].startSize = classBaseSize * sizeGrowthCurve * curSizeMultiplier;

                // 5. Rotational variation (gentle, slow tumbling scaled with master speed)
                float baseRot = ((seed & 0x3FF) / 1023f) * 360f;
                particleBuffer[i].rotation = baseRot + age * (2.0f * speedScale);

                // 6. Molten coloration (hot core -> bright orange -> dark orange/red edge)
                particleBuffer[i].startColor = EvaluateMoltenColor(progress, seed);
            }

            particleSys.SetParticles(particleBuffer, count);
        }

        private float GetParticleClassBaseSize(uint seed)
        {
            // Pseudo-random class distribution:
            // ~25% small (surface bubbling/irregularity)
            // ~50% medium (main molten body)
            // ~25% large (thick molten core)
            uint roll = (seed >> 4) & 0xFF; // 0..255
            if (roll < 64)
            {
                // Small (0.035 - 0.052)
                float frac = roll / 64f;
                return Mathf.Lerp(smallParticleSize * 0.85f, smallParticleSize * 1.15f, frac);
            }
            else if (roll < 192)
            {
                // Medium (0.065 - 0.088)
                float frac = (roll - 64) / 128f;
                return Mathf.Lerp(mediumParticleSize * 0.85f, mediumParticleSize * 1.15f, frac);
            }
            else
            {
                // Large (0.095 - 0.125)
                float frac = (roll - 192) / 64f;
                return Mathf.Lerp(largeParticleSize * 0.85f, largeParticleSize * 1.15f, frac);
            }
        }

        /// <summary>
        /// Evaluates size growth over progress per Section 17:
        /// 0–1.5 sec (progress 0.00-0.20): wide initial molten blobs
        /// 1.5–3.0 sec (progress 0.20-0.40): slow outward growth
        /// 3.0–5.0 sec (progress 0.40-0.67): tongue gradually develops
        /// 5.0–7.0 sec (progress 0.67-0.93): tongue slowly becomes longer and slightly thicker
        /// 7.0 sec+ (progress 0.93-1.00): stable slow leakage, tip cooling taper & shrink
        /// </summary>
        public static float EvaluateSizeGrowthCurve(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (progress < 0.20f)
            {
                // 0.0 - 1.5s: Wide initial molten blobs (already wide from emission width, grows 0.75 -> 0.95)
                return Mathf.Lerp(0.75f, 0.95f, progress / 0.20f);
            }
            else if (progress < 0.40f)
            {
                // 1.5 - 3.0s: Slow outward growth (0.95 -> 1.10)
                return Mathf.Lerp(0.95f, 1.10f, (progress - 0.20f) / 0.20f);
            }
            else if (progress < 0.67f)
            {
                // 3.0 - 5.0s: Tongue gradually develops (1.10 -> 1.20)
                float mid = (progress - 0.40f) / 0.27f;
                return 1.10f + Mathf.Sin(mid * Mathf.PI * 0.5f) * 0.10f;
            }
            else if (progress < 0.93f)
            {
                // 5.0 - 7.0s: Tongue slowly becomes longer and slightly thicker (1.20 -> 1.10)
                float endT = (progress - 0.67f) / 0.26f;
                return Mathf.Lerp(1.20f, 1.10f, endT);
            }
            else
            {
                // 7.0s+: Tip cooling taper & shrink (1.10 -> 0.30)
                float finalT = (progress - 0.93f) / 0.07f;
                return Mathf.Lerp(1.10f, 0.30f, finalT);
            }
        }

        public static Vector3 EvaluateSpline(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            t = Mathf.Clamp01(t);

            // Cubic Bezier with outward path from p0 to p1, then sagging over lip toward p2
            Vector3 cp0 = p0;
            Vector3 cp1 = p0 + (p1 - p0) * 0.70f;
            Vector3 cp2 = p1 + (p2 - p1) * 0.35f;
            Vector3 cp3 = p2;

            float u = 1f - t;
            return (u * u * u * cp0) +
                   (3f * u * u * t * cp1) +
                   (3f * u * t * t * cp2) +
                   (t * t * t * cp3);
        }

        private Vector3 GetOrganicRadialOffset(uint seed, float currentWidth, float progress)
        {
            // Pseudo-random normalized coordinates -1..1
            float u = ((seed & 0xFFFF) / 65535f) * 2f - 1f;
            float v = (((seed >> 16) & 0xFFFF) / 65535f) * 2f - 1f;

            // X is along the pot shell width (horizontal cross-section)
            // Y is vertical height (thickness)
            // Z is flow depth
            float halfWidth = currentWidth * 0.5f;
            float halfHeight = currentWidth * 0.28f;

            // Elliptical cross section: wide along X, rounded vertically along Y
            float offsetX = u * halfWidth;
            float offsetY = v * Mathf.Sqrt(Mathf.Max(0f, 1f - (u * u))) * halfHeight;
            float offsetZ = Mathf.Sin(u * Mathf.PI * 2f) * 0.010f;

            return new Vector3(offsetX, offsetY, offsetZ);
        }

        private Color EvaluateMoltenColor(float progress, uint seed)
        {
            Color baseCol;
            if (progress < 0.18f)
            {
                baseCol = Color.Lerp(ColorCoreHot, ColorYellowHot, progress / 0.18f);
            }
            else if (progress < 0.45f)
            {
                baseCol = Color.Lerp(ColorYellowHot, ColorOrangeBody, (progress - 0.18f) / 0.27f);
            }
            else if (progress < 0.75f)
            {
                baseCol = Color.Lerp(ColorOrangeBody, ColorDarkOrange, (progress - 0.45f) / 0.30f);
            }
            else
            {
                baseCol = Color.Lerp(ColorDarkOrange, ColorEdgeRed, (progress - 0.75f) / 0.25f);
            }

            // Alpha curve: fast fade in at source opening, dense body, smooth fade out at tip
            float alpha = 1.0f;
            if (progress < 0.06f)
            {
                alpha = Mathf.Lerp(0.35f, 0.98f, progress / 0.06f);
            }
            else if (progress > 0.90f)
            {
                alpha = Mathf.Lerp(0.98f, 0.0f, (progress - 0.90f) / 0.10f);
            }
            baseCol.a = alpha;

            // When controlled (stopper or pipes), cool the core slightly toward deep orange
            if (curControlledFactor > 0.01f)
            {
                baseCol.g *= Mathf.Lerp(1.0f, 0.85f, curControlledFactor);
            }

            // Subtle thermal shimmer per particle
            float shimmer = 1f + (((seed & 0x3F) / 63f) - 0.5f) * 0.03f;
            baseCol.r = Mathf.Clamp01(baseCol.r * shimmer);
            baseCol.g = Mathf.Clamp01(baseCol.g * shimmer);

            return baseCol;
        }
    }
}
