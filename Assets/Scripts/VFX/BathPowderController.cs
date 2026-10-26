using System.Collections;
using UnityEngine;

namespace PotLeakage.VFX
{
    /// <summary>
    /// Controls the ash-coloured bath powder particle effect on Cube.051/piper/release.
    /// Emits a dense, slow-falling continuous stream of small granular ash/bath particles
    /// that lands in a tight, controlled, localized spill on Plane.045.
    /// </summary>
    [ExecuteAlways]
    public class BathPowderController : MonoBehaviour
    {
        private static BathPowderController _instance;
        public static BathPowderController Instance => _instance;

        [Header("Components")]
        public ParticleSystem powderParticleSystem;
        public ParticleSystemRenderer particleRenderer;
        public ParticleSystem impactParticleSystem;
        public ParticleSystemRenderer impactRenderer;
        public Material powderMaterial;

        [Header("Audio")]
        [Tooltip("SFX played when BathPowder_Impact begins (powder.mp3)")]
        public AudioClip powderAudioClip;
        private bool hasPlayedPowderSFX = false;

        [Header("Powder Colors (Ash / Grey)")]
        public Color ashDark = new Color(0.48f, 0.50f, 0.52f, 0.95f);
        public Color ashMedium = new Color(0.64f, 0.66f, 0.68f, 0.95f);
        public Color ashLight = new Color(0.78f, 0.80f, 0.82f, 0.95f);

        // Backward-compatibility aliases
        public Color powderOffWhite { get => ashDark; set => ashDark = value; }
        public Color powderWhite { get => ashMedium; set => ashMedium = value; }
        public Color powderSoftWhite { get => ashLight; set => ashLight = value; }

        [Header("Particle Tuning — Small Particles, High Quantity, Slow Motion")]
        [Range(0.010f, 0.040f)] public float minStartSize = 0.018f;
        [Range(0.015f, 0.060f)] public float maxStartSize = 0.032f;
        [Range(1.0f, 4.0f)] public float fallDuration = 2.4f;
        [Range(100f, 1000f)] public float emissionRate = 450f;

        [Header("Impact Position Offset")]
        public Vector3 impactWorldPosition = new Vector3(-7.3323f, 1.4069f, -231.6030f);

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
            // Remove any oversized cloud child if present
            Transform cloudChild = transform.Find("BathPowder_CloudMesh");
            if (cloudChild != null)
            {
                if (Application.isPlaying) Destroy(cloudChild.gameObject);
                else DestroyImmediate(cloudChild.gameObject);
            }

            if (powderParticleSystem == null) powderParticleSystem = GetComponent<ParticleSystem>();
            if (powderParticleSystem == null) powderParticleSystem = gameObject.AddComponent<ParticleSystem>();

            if (particleRenderer == null) particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null) particleRenderer = gameObject.AddComponent<ParticleSystemRenderer>();

            if (powderMaterial == null)
            {
#if UNITY_EDITOR
                powderMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/PotLeakage/Materials/M_DustParticle.mat");
#endif
            }

            if (powderAudioClip == null)
            {
#if UNITY_EDITOR
                powderAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/powder.mp3");
#endif
            }

            if (powderMaterial != null && particleRenderer.sharedMaterial != powderMaterial)
            {
                particleRenderer.sharedMaterial = powderMaterial;
            }

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;

            // Ensure secondary localized impact particle system on Plane.045
            Transform impactChild = transform.Find("BathPowder_Impact");
            if (impactChild == null)
            {
                var impactGo = new GameObject("BathPowder_Impact");
                impactGo.transform.SetParent(transform, false);
                impactChild = impactGo.transform;
            }

            if (impactChild != null)
            {
                if (transform.name.IndexOf("drop point", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    transform.name.IndexOf("Drop Point", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    impactChild.localPosition = new Vector3(0.00899999961f, -0.0353999995f, 0.0027999999f);
                    impactChild.localEulerAngles = new Vector3(4.26886828e-07f, 353.050018f, 60.9000015f);
                }

                impactParticleSystem = impactChild.GetComponent<ParticleSystem>();
                if (impactParticleSystem == null) impactParticleSystem = impactChild.gameObject.AddComponent<ParticleSystem>();

                impactRenderer = impactChild.GetComponent<ParticleSystemRenderer>();
                if (impactRenderer == null) impactRenderer = impactChild.gameObject.AddComponent<ParticleSystemRenderer>();

                if (powderMaterial != null && impactRenderer.sharedMaterial != powderMaterial)
                {
                    impactRenderer.sharedMaterial = powderMaterial;
                }
                impactRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                impactRenderer.alignment = ParticleSystemRenderSpace.View;
            }

            ConfigureParticleSystem();
            ConfigureImpactParticleSystem();
        }

        public Transform ResolvePlane045()
        {
            var p045 = GameObject.Find("LINE/line (6)/Plane.045") ?? GameObject.Find("line (6)/Plane.045");
            if (p045 != null) return p045.transform;

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var g in all)
            {
                if (g.name == "Plane.045" && g.transform.parent != null && g.transform.parent.name.Contains("(6)"))
                {
                    return g.transform;
                }
            }
            return null;
        }

        public void ConfigureParticleSystem()
        {
            if (powderParticleSystem == null) return;

            // 1. Main Module — Small granular ash particles, slow descent, substantial continuous quantity
            var main = powderParticleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(fallDuration * 0.95f, fallDuration * 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.02f); // No local cone dispersion
            main.startSize = new ParticleSystem.MinMaxCurve(minStartSize, maxStartSize);
            main.gravityModifier = 0.0f; // Controlled by direct velocity vector
            main.maxParticles = 1800;
            main.startColor = new ParticleSystem.MinMaxGradient(ashDark, ashMedium);

            // 2. Emission Module — Dense, continuous spillage
            var emission = powderParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 60),
                new ParticleSystem.Burst(0.3f, 80)
            });

            // 3. Shape Module — Narrow emission aperture at release nozzle
            var shape = powderParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.005f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            // 4. Velocity directly targeting Plane.045 with tight horizontal containment
            Vector3 releasePos = transform.position;
            var p045 = ResolvePlane045();
            Vector3 targetPos = p045 != null ? p045.position + new Vector3(0f, 0.03f, 0f) : impactWorldPosition;
            Vector3 delta = targetPos - releasePos;
            Vector3 baseVelocity = delta / Mathf.Max(0.5f, fallDuration);

            var vel = powderParticleSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(baseVelocity.x - 0.018f, baseVelocity.x + 0.018f);
            vel.y = new ParticleSystem.MinMaxCurve(baseVelocity.y - 0.015f, baseVelocity.y + 0.015f);
            vel.z = new ParticleSystem.MinMaxCurve(baseVelocity.z - 0.018f, baseVelocity.z + 0.018f);

            // 5. Size Over Lifetime — Natural subtle settling
            var size = powderParticleSystem.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0.0f, 0.88f));
            sizeCurve.AddKey(new Keyframe(0.50f, 1.00f));
            sizeCurve.AddKey(new Keyframe(1.0f, 1.05f));
            size.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // 6. Color Over Lifetime — Ash grey palette
            var col = powderParticleSystem.colorOverLifetime;
            col.enabled = true;
            var alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(ashLight, 0.0f),
                    new GradientColorKey(ashMedium, 0.40f),
                    new GradientColorKey(ashDark, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.06f),
                    new GradientAlphaKey(0.90f, 0.88f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(alphaGrad);

            var noise = powderParticleSystem.noise;
            noise.enabled = false;
        }

        public void ConfigureImpactParticleSystem()
        {
            if (impactParticleSystem == null) return;

            // Preserve and enforce exact fixed local transform if not under drop point
            if (transform.name.IndexOf("drop point", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                transform.name.IndexOf("Drop Point", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                impactParticleSystem.transform.localPosition = new Vector3(0.00899999961f, -0.0353999995f, 0.0027999999f);
                impactParticleSystem.transform.localEulerAngles = new Vector3(4.26886828e-07f, 353.050018f, 60.9000015f);
            }

            var main = impactParticleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.015f); // Non-explosive, gentle settling
            main.startSize = new ParticleSystem.MinMaxCurve(minStartSize * 0.9f, maxStartSize * 1.05f);
            main.gravityModifier = 0.05f;
            main.maxParticles = 800;
            main.startColor = new ParticleSystem.MinMaxGradient(ashDark, ashMedium);

            var emission = impactParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 180f;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.2f, 30)
            });

            // Localized circle on Plane.045 crust
            var shape = impactParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.035f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            // Very gentle localized settling
            var vel = impactParticleSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);
            vel.y = new ParticleSystem.MinMaxCurve(0.003f, 0.010f); // Slight cushion, no fountain
            vel.z = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);

            var col = impactParticleSystem.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(ashMedium, 0.0f),
                    new GradientColorKey(ashDark, 0.6f),
                    new GradientColorKey(ashDark, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.92f, 0.10f),
                    new GradientAlphaKey(0.85f, 0.70f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = impactParticleSystem.noise;
            noise.enabled = false;
        }

        public void PlayPowderSFX()
        {
            if (hasPlayedPowderSFX) return;
            hasPlayedPowderSFX = true;

            if (powderAudioClip == null)
            {
#if UNITY_EDITOR
                powderAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/powder.mp3");
#endif
            }

            if (powderAudioClip != null)
            {
                if (SequenceHelperFunctions.instance != null)
                {
                    SequenceHelperFunctions.instance.PlaySFX(powderAudioClip);
                }
            }
        }

        public void PlayPowder()
        {
            InitializeComponents();
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // Dynamically recalculate trajectory to guarantee exact landing on Plane.045
            ConfigureParticleSystem();
            ConfigureImpactParticleSystem();

            if (powderParticleSystem != null)
            {
                powderParticleSystem.Play(true);
            }
            if (impactParticleSystem != null)
            {
                if (!impactParticleSystem.gameObject.activeSelf) impactParticleSystem.gameObject.SetActive(true);
                impactParticleSystem.Play(true);
            }

            PlayPowderSFX();
            Debug.Log("[BathPowder] Small granular ash bath powder stream & localized spill started on Plane.045.");
        }

        public void PlayBathPowder()
        {
            PlayPowder();
        }

        public void StopPowder()
        {
            hasPlayedPowderSFX = false;
            if (powderParticleSystem != null)
            {
                powderParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (impactParticleSystem != null)
            {
                impactParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            Debug.Log("[BathPowder] Ash bath powder particle effect stopped.");
        }

        public void StopBathPowder()
        {
            StopPowder();
        }

        public void ResetPowder()
        {
            hasPlayedPowderSFX = false;
            if (powderParticleSystem != null)
            {
                powderParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (impactParticleSystem != null)
            {
                impactParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
