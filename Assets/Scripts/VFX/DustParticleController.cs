using System.Collections;
using UnityEngine;

namespace PotLeakage.VFX
{
    /// <summary>
    /// Controls the localized brown drilling dust particle effect on Cube.013/DustParticle.
    /// Emits short, compact brown dust bursts synchronized with the drill's downward impact cycles.
    /// </summary>
    [ExecuteAlways]
    public class DustParticleController : MonoBehaviour
    {
        private static DustParticleController _instance;
        public static DustParticleController Instance => _instance;

        [Header("Components")]
        public ParticleSystem dustParticleSystem;
        public ParticleSystemRenderer particleRenderer;
        public Material dustMaterial;
        public Transform drillCylinderTarget;

        [Header("Dust Palette Colors")]
        public Color darkBrown = new Color(0.18f, 0.10f, 0.05f, 1.0f);
        public Color mediumBrown = new Color(0.32f, 0.18f, 0.08f, 1.0f);
        public Color lightBrown = new Color(0.48f, 0.30f, 0.14f, 1.0f);

        [Header("Particle Tuning")]
        [Range(0.01f, 0.06f)] public float minStartSize = 0.015f;
        [Range(0.02f, 0.08f)] public float maxStartSize = 0.045f;
        [Range(0.3f, 1.5f)] public float minLifetime = 0.5f;
        [Range(0.5f, 2.0f)] public float maxLifetime = 1.1f;
        public Vector3 drillTipLocalOffset = new Vector3(0f, 0f, 1.42f);

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
            if (dustParticleSystem == null) dustParticleSystem = GetComponent<ParticleSystem>();
            if (dustParticleSystem == null) dustParticleSystem = gameObject.AddComponent<ParticleSystem>();

            if (particleRenderer == null) particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null) particleRenderer = gameObject.AddComponent<ParticleSystemRenderer>();

            if (dustMaterial == null)
            {
#if UNITY_EDITOR
                dustMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/PotLeakage/Materials/M_DustParticle.mat");
#endif
            }

            if (dustMaterial != null && particleRenderer.sharedMaterial != dustMaterial)
            {
                particleRenderer.sharedMaterial = dustMaterial;
            }

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;

            ResolveDrillTarget();
            ConfigureParticleSystem();
        }

        public void ResolveDrillTarget()
        {
            if (drillCylinderTarget != null) return;
            var c13 = transform.parent;
            if (c13 != null)
            {
                var cyl003 = c13.Find("Cylinder.003");
                if (cyl003 != null)
                {
                    drillCylinderTarget = cyl003;
                    return;
                }
                var cyl = c13.Find("Cylinder");
                if (cyl != null)
                {
                    drillCylinderTarget = cyl;
                }
            }
        }

        public void ConfigureParticleSystem()
        {
            if (dustParticleSystem == null) return;

            // 1. Main Module
            var main = dustParticleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startSize = new ParticleSystem.MinMaxCurve(minStartSize, maxStartSize);
            main.gravityModifier = 0.25f; // Brown dust rises/splashes slightly and falls naturally
            main.maxParticles = 80;

            // Warm earthy brown gradient (Dark 0.18, 0.10, 0.05 to Light 0.48, 0.30, 0.14)
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(darkBrown, 0.0f),
                    new GradientColorKey(mediumBrown, 0.45f),
                    new GradientColorKey(lightBrown, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(darkBrown, lightBrown);

            // 2. Emission Module (Continuous + burst-driven)
            var emission = dustParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;

            // 3. Shape Module (Compact cone around drill impact contact)
            var shape = dustParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.025f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            // 4. Velocity Over Lifetime (Upward 0.05-0.15, Horizontal +-0.02-0.06)
            var vel = dustParticleSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            vel.y = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

            // 5. Size Over Lifetime Curve: 0%=0.55, 20%=0.80, 50%=1.00, 100%=0.65
            var size = dustParticleSystem.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0.0f, 0.55f));
            sizeCurve.AddKey(new Keyframe(0.20f, 0.80f));
            sizeCurve.AddKey(new Keyframe(0.50f, 1.00f));
            sizeCurve.AddKey(new Keyframe(1.0f, 0.65f));
            size.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // 6. Color Over Lifetime (Soft opacity curve: 0%=0, 5%=0.35, 20%=0.65, 50%=0.45, 80%=0.18, 100%=0)
            var col = dustParticleSystem.colorOverLifetime;
            col.enabled = true;
            var alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(Color.white, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.35f, 0.05f),
                    new GradientAlphaKey(0.65f, 0.20f),
                    new GradientAlphaKey(0.45f, 0.50f),
                    new GradientAlphaKey(0.18f, 0.80f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(alphaGrad);
        }

        [Header("Target Local Position")]
        public Vector3 targetLocalPosition = new Vector3(0.00079999998f, -0.00490000006f, -0.050999999f);

        private void LateUpdate()
        {
            // Maintain exact required localPosition under Cube.013
            transform.localPosition = targetLocalPosition;
        }

        /// <summary>
        /// Explicitly starts the dust particle system.
        /// </summary>
        public void PlayDust()
        {
            InitializeComponents();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (dustParticleSystem != null)
            {
                if (!dustParticleSystem.isPlaying) dustParticleSystem.Play(true);
            }
            Debug.Log("[DustParticle] Brown dust particle system Play() started on Cube.013.");
        }

        /// <summary>
        /// Stops the dust particle emission.
        /// </summary>
        public void StopDust()
        {
            if (dustParticleSystem != null)
            {
                dustParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            Debug.Log("[DustParticle] Brown dust particle system Stop() called.");
        }

        /// <summary>
        /// Triggers a localized brown dust burst (6–14 particles) at the drill tip.
        /// </summary>
        public void TriggerBurst(int count = 10)
        {
            if (dustParticleSystem == null) InitializeComponents();
            if (dustParticleSystem == null) return;

            transform.localPosition = targetLocalPosition;
            int burstCount = Mathf.Clamp(count, 6, 14);
            dustParticleSystem.Emit(burstCount);
        }

        /// <summary>
        /// Resets the dust particle system and clears active particles.
        /// </summary>
        public void ResetDust()
        {
            if (dustParticleSystem != null)
            {
                dustParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
