using System.Collections;
using UnityEngine;

namespace PotLeakage.VFX
{
    /// <summary>
    /// Controls the multi-axis mechanical drilling oscillation and crust-breaking vibration
    /// on Cube.013/Cylinder.003 without moving Cube.013 or the crane itself.
    /// Combines sideways (left/right) oscillation + forward/back movement + subtle rotational vibration (10–18 Hz).
    /// </summary>
    [ExecuteAlways]
    public class DrillVibrationController : MonoBehaviour
    {
        private static DrillVibrationController _instance;
        public static DrillVibrationController Instance => _instance;

        [Header("Vibration State")]
        public bool isDrilling = false;

        [Header("Frequency Settings (5–8 Hz)")]
        [Tooltip("Reciprocating mechanical drilling frequency (5–8 Hz)")]
        [Range(5f, 8f)] public float vibrationFrequency = 6.5f;

        [Header("Sideways Oscillation (0.01–0.025 World/Local Units)")]
        [Tooltip("Sideways left-to-right amplitude (0.01–0.025)")]
        [Range(0.01f, 0.025f)] public float sidewaysAmplitude = 0.018f;

        [Header("Forward/Back Movement (0.01–0.025 World/Local Units)")]
        [Tooltip("Forward and back movement amplitude (0.01–0.025)")]
        [Range(0.01f, 0.025f)] public float forwardBackAmplitude = 0.018f;

        [Header("Rotational Vibration (0.5–1.5 Degrees)")]
        [Tooltip("Subtle rotational shake amplitude in degrees (0.5–1.5 deg)")]
        [Range(0.5f, 1.5f)] public float rotAmplitudeDegrees = 1.0f;

        [Header("Cached Original Transforms")]
        public Vector3 originalLocalPosition = new Vector3(0.011275199f, -0.009150002f, -0.17289487f);
        public Quaternion originalLocalRotation = Quaternion.identity;
        public Vector3 originalLocalScale = Vector3.one;
        private bool hasCachedOriginals = false;

        private void Awake()
        {
            _instance = this;
            CacheOriginalTransforms();
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            CacheOriginalTransforms();
        }

        public void CacheOriginalTransforms()
        {
            if (!hasCachedOriginals)
            {
                originalLocalPosition = transform.localPosition;
                originalLocalRotation = transform.localRotation;
                originalLocalScale = transform.localScale;
                hasCachedOriginals = true;
            }
        }

        private bool hasEmittedThisCycle = false;

        private void Update()
        {
            if (!isDrilling) return;

            float t = Time.time;
            float omega = vibrationFrequency * Mathf.PI * 2f;

            // 1. SIDEWAYS OSCILLATION (LEFT -> CENTER -> RIGHT -> CENTER)
            float sidewaysSin = Mathf.Sin(t * omega);
            float sidewaysOffset = sidewaysSin * sidewaysAmplitude;

            // 2. FORWARD/BACK MOVEMENT (FORWARD -> BACK -> FORWARD)
            float forwardBackCos = Mathf.Cos(t * omega * 0.95f + 0.35f);
            float forwardBackOffset = forwardBackCos * forwardBackAmplitude;

            // Apply deterministic local offset relative to baseline
            // (X = local sideways, Z = local forward/back)
            Vector3 localOffset = new Vector3(sidewaysOffset, 0f, forwardBackOffset);
            transform.localPosition = originalLocalPosition + localOffset;

            // 3. SUBTLE ROTATIONAL SHAKE (1–4 degrees)
            float rx = Mathf.Sin(t * omega * 1.1f + 0.4f) * rotAmplitudeDegrees;
            float ry = Mathf.Cos(t * omega * 0.9f + 0.8f) * (rotAmplitudeDegrees * 0.7f);
            float rz = Mathf.Sin(t * omega * 1.3f) * (rotAmplitudeDegrees * 0.5f);

            transform.localRotation = originalLocalRotation * Quaternion.Euler(rx, ry, rz);

            // Trigger synchronized brown dust burst during engagement cycle
            if (sidewaysSin < -0.65f)
            {
                if (!hasEmittedThisCycle)
                {
                    hasEmittedThisCycle = true;
                    var dust = DustParticleController.Instance ?? UnityEngine.Object.FindFirstObjectByType<DustParticleController>();
                    if (dust != null)
                    {
                        dust.TriggerBurst(Random.Range(6, 14));
                    }
                }
            }
            else if (sidewaysSin > 0.1f)
            {
                hasEmittedThisCycle = false;
            }
        }

        /// <summary>
        /// Starts the high-frequency mechanical drilling oscillation.
        /// </summary>
        public void StartDrilling()
        {
            CacheOriginalTransforms();
            isDrilling = true;
            Debug.Log("[DrillVibration] Cylinder.003 mechanical drill motion started (sideways + forward/back + rotational oscillation).");
        }

        /// <summary>
        /// Halts vibration and restores exact original localPosition, localRotation, localScale.
        /// </summary>
        public void StopDrilling()
        {
            isDrilling = false;
            RestoreOriginalTransforms();
            Debug.Log("[DrillVibration] Cylinder.003 drilling vibration stopped and transforms restored.");
        }

        public void RestoreOriginalTransforms()
        {
            if (hasCachedOriginals)
            {
                transform.localPosition = originalLocalPosition;
                transform.localRotation = originalLocalRotation;
                transform.localScale = originalLocalScale;
            }
        }

        private void OnDisable()
        {
            StopDrilling();
        }
    }
}
