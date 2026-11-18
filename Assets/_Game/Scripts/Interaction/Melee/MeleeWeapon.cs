using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Interaction.Melee
{
    /// <summary>
    /// Controller melee with rig-compensated speed and NonAlloc blade sweeps. Emits measurements;
    /// does not own enemy health or apply damage. Transform scale must remain constant and uniform.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(XRGrabInteractable)), DefaultExecutionOrder(200)]
    public sealed class MeleeWeapon : MonoBehaviour
    {
        [Header("References (required)")]
        [SerializeField] private Transform bladeBase;
        [SerializeField] private Transform bladeTip;
        [Tooltip("XR Origin root moved by locomotion, not a tracked hand or Camera.")]
        [SerializeField] private Transform rigMotionReference;
        [Tooltip("Use the tracked head Camera inside the playable space for wall occlusion checks.")]
        [SerializeField] private Transform obstructionOrigin;
        [Tooltip("Optional player root whose colliders must never be hit.")]
        [SerializeField] private Transform ignoredPlayerRoot;
        [Header("Queries")]
        [SerializeField, Min(0.005f)] private float sweepRadius = 0.035f;
        [SerializeField] private LayerMask targetMask = ~0;
        [Tooltip("Include all solid walls/props. Keep enemy-only trigger layers out if they should not block.")]
        [SerializeField] private LayerMask blockerMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Range(2, 64)] private int maximumBladeSamples = 32;
        [Header("Swing and discontinuity rejection")]
        [SerializeField, Min(0.01f)] private float startSpeed = 1.2f;
        [SerializeField, Min(0)] private float stopSpeed = 0.45f;
        [SerializeField, Min(0.01f)] private float maximumSwingSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float maximumSampleSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float maximumSampleDistance = 0.75f;
        [SerializeField, Min(1)] private float maximumTrackedSpeed = 20f;
        [SerializeField, Min(0.01f)] private float rigTeleportDistance = 0.4f;
        [SerializeField, Range(1, 180)] private float rigTeleportAngle = 25f;
        [Header("Physical estimate (not measured controller force)")]
        [SerializeField, Min(0.01f)] private float effectiveMass = 0.8f;
        [SerializeField, Min(0.001f)] private float contactSeconds = 0.04f;
        [SerializeField] private MeleeHitEvent onHit = new MeleeHitEvent();

        public event Action<MeleeHit> Hit;
        public float CurrentSpeed { get; private set; }
        public float CurrentEnergy => MeleeMotion.KineticEnergy(effectiveMass, CurrentSpeed);
        public bool IsSwinging => swing != null && swing.IsOpen;
        // A full NonAlloc buffer is handled conservatively: skip damage, never ignore possible walls.
        public int QueryOverflowCount { get; private set; }

        private XRGrabInteractable grab;
        private MeleeSwingWindow swing;
        private readonly Collider[] overlaps = new Collider[64];
        private readonly Collider[] occlusionOverlaps = new Collider[64];
        private readonly RaycastHit[] casts = new RaycastHit[64];
        private Vector3 previousBase, previousTip, previousRigPosition;
        private Quaternion previousRigRotation;
        private bool hasSample;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            ValidateParameters();
            swing = new MeleeSwingWindow(startSpeed, stopSpeed, maximumSwingSeconds);
        }

        private void OnValidate() => ValidateParameters();

        private void ValidateParameters()
        {
            sweepRadius = SafePositive(sweepRadius, 0.035f, 0.005f);
            stopSpeed = SafePositive(stopSpeed, 0.45f, 0f);
            startSpeed = SafePositive(startSpeed, 1.2f, stopSpeed + 0.01f);
            maximumSwingSeconds = SafePositive(maximumSwingSeconds, 0.6f, 0.01f);
            maximumSampleSeconds = SafePositive(maximumSampleSeconds, 0.1f, 0.01f);
            maximumSampleDistance = SafePositive(maximumSampleDistance, 0.75f, 0.01f);
            maximumTrackedSpeed = SafePositive(maximumTrackedSpeed, 20f, 1f);
            rigTeleportDistance = SafePositive(rigTeleportDistance, 0.4f, 0.01f);
            rigTeleportAngle = Mathf.Clamp(SafePositive(rigTeleportAngle, 25f, 1f), 1, 180);
            effectiveMass = SafePositive(effectiveMass, 0.8f, 0.01f);
            contactSeconds = SafePositive(contactSeconds, 0.04f, 0.001f);
            maximumBladeSamples = Mathf.Clamp(maximumBladeSamples, 2, 64);
        }

        private static float SafePositive(float value, float fallback, float minimum) =>
            MeleeMotion.IsFinite(value) ? Mathf.Max(minimum, value) : Mathf.Max(minimum, fallback);

        private void OnDisable() => ResetSampling();

        /// <summary>Call on an explicit teleport/recenter or known tracking loss, in addition to automatic guards.</summary>
        public void ResetSampling()
        {
            hasSample = false;
            CurrentSpeed = 0;
            swing?.Reset();
        }

        private void LateUpdate()
        {
            if (!grab.isSelected || !bladeBase || !bladeTip || !rigMotionReference || !obstructionOrigin)
            {
                ResetSampling();
                return;
            }
            var currentBase = bladeBase.position;
            var currentTip = bladeTip.position;
            var rigPosition = rigMotionReference.position;
            var rigRotation = rigMotionReference.rotation;
            float dt = Time.deltaTime;
            if (!MeleeMotion.IsFinite(currentBase) || !MeleeMotion.IsFinite(currentTip) ||
                !MeleeMotion.IsFinite(rigPosition) || !MeleeMotion.IsFinite(dt) || dt <= 0)
            {
                ResetSampling();
                return;
            }
            if (!hasSample)
            {
                SaveSample(currentBase, currentTip, rigPosition, rigRotation);
                return;
            }
            var oldBase = MeleeMotion.RebasePoint(previousBase, previousRigPosition, previousRigRotation, rigPosition, rigRotation);
            var oldTip = MeleeMotion.RebasePoint(previousTip, previousRigPosition, previousRigRotation, rigPosition, rigRotation);
            float movement = Mathf.Max(Vector3.Distance(oldBase, currentBase), Vector3.Distance(oldTip, currentTip));
            bool discontinuity = dt > maximumSampleSeconds || movement > maximumSampleDistance ||
                movement / dt > maximumTrackedSpeed ||
                Vector3.Distance(previousRigPosition, rigPosition) > rigTeleportDistance ||
                Quaternion.Angle(previousRigRotation, rigRotation) > rigTeleportAngle;
            SaveSample(currentBase, currentTip, rigPosition, rigRotation);
            if (discontinuity)
            {
                CurrentSpeed = 0;
                swing.Reset();
                return;
            }
            CurrentSpeed = movement / dt;
            if (!swing.Advance(CurrentSpeed, dt)) return;

            // <= radius spacing leaves overlap between neighbouring swept spheres along the blade.
            int samples = Mathf.CeilToInt(Mathf.Max(Vector3.Distance(oldBase, oldTip),
                Vector3.Distance(currentBase, currentTip)) / sweepRadius) + 1;
            if (samples > maximumBladeSamples)
            {
                // Do not silently create unswept gaps for an overlong weapon.
                swing.Reset();
                return;
            }
            samples = Mathf.Max(samples, 2);
            for (int i = 0; i < samples; ++i)
            {
                float t = (float)i / (samples - 1);
                Sweep(Vector3.Lerp(oldBase, oldTip, t), Vector3.Lerp(currentBase, currentTip, t), dt);
            }
        }

        private void SaveSample(Vector3 b, Vector3 t, Vector3 position, Quaternion rotation)
        {
            previousBase = b; previousTip = t; previousRigPosition = position;
            previousRigRotation = rotation; hasSample = true;
        }

        private bool Ignore(Collider collider) => !collider || collider.transform.IsChildOf(transform) ||
            (ignoredPlayerRoot && collider.transform.IsChildOf(ignoredPlayerRoot));

        private static bool InMask(Collider collider, LayerMask mask) => (mask.value & (1 << collider.gameObject.layer)) != 0;

        private static GameObject Identity(Collider collider)
        {
            var target = collider.GetComponentInParent<MeleeHitTarget>();
            if (target) return target.gameObject;
            return collider.attachedRigidbody ? collider.attachedRigidbody.gameObject : collider.gameObject;
        }

        private void Sweep(Vector3 start, Vector3 end, float dt)
        {
            var delta = end - start;
            float distance = delta.magnitude;
            var direction = distance > 0.00001f ? delta / distance : Vector3.zero;
            float speed = distance / dt;
            // An unmoving handle must not inherit the tip's striking speed.
            if (speed < startSpeed) return;
            int mask = targetMask.value | blockerMask.value;
            int count = Physics.OverlapSphereNonAlloc(start, sweepRadius, overlaps, mask, triggerInteraction);
            if (count >= overlaps.Length) { ++QueryOverflowCount; return; }
            // Any initial solid overlap stops this path. Candidate contacts at the starting point
            // may still be emitted after LOS validation; nothing beyond that wall is considered.
            bool blockedAtStart = false;
            for (int i = 0; i < count; ++i)
            {
                var collider = overlaps[i];
                if (Ignore(collider)) continue;
                if (InMask(collider, blockerMask)) blockedAtStart = true;
                if (InMask(collider, targetMask))
                    TryEmit(collider, collider.ClosestPoint(start), direction, speed);
            }
            if (blockedAtStart || distance <= 0.00001f) return;

            count = Physics.SphereCastNonAlloc(start, sweepRadius, direction, casts, distance, mask, triggerInteraction);
            if (count >= casts.Length) { ++QueryOverflowCount; return; }
            // Query results are unordered; insertion sort avoids allocations for these short buffers.
            for (int i = 1; i < count; ++i)
            {
                var item = casts[i]; int j = i - 1;
                while (j >= 0 && casts[j].distance > item.distance) { casts[j + 1] = casts[j]; --j; }
                casts[j + 1] = item;
            }
            for (int i = 0; i < count; ++i)
            {
                var hit = casts[i];
                if (Ignore(hit.collider)) continue;
                if (InMask(hit.collider, targetMask)) TryEmit(hit.collider, hit.point, direction, speed);
                if (InMask(hit.collider, blockerMask)) break;
            }
        }

        private void TryEmit(Collider collider, Vector3 point, Vector3 direction, float speed)
        {
            var target = Identity(collider);
            if (IsOccluded(point, target) || !swing.TryRegisterTarget(target)) return;
            var hit = new MeleeHit
            {
                Collider = collider, Target = target, Point = point, Direction = direction, Speed = speed,
                KineticEnergy = MeleeMotion.KineticEnergy(effectiveMass, speed),
                EstimatedForce = MeleeMotion.EstimatedStoppingForce(effectiveMass, speed, contactSeconds),
                SwingId = swing.SwingId
            };
            onHit.Invoke(hit);
            Hit?.Invoke(hit);
        }

        private bool IsOccluded(Vector3 point, GameObject target)
        {
            Vector3 origin = obstructionOrigin.position;
            // Raycasts alone miss a wall containing the origin. Check a tiny starting volume first.
            int count = Physics.OverlapSphereNonAlloc(origin, 0.005f, occlusionOverlaps, blockerMask, triggerInteraction);
            if (count >= occlusionOverlaps.Length) { ++QueryOverflowCount; return true; }
            for (int i = 0; i < count; ++i)
                if (!Ignore(occlusionOverlaps[i]) && Identity(occlusionOverlaps[i]) != target) return true;
            var delta = point - origin;
            float length = delta.magnitude;
            if (length < 0.00001f) return false;
            // Separate buffer prevents this occlusion query from overwriting the active sweep results.
            count = Physics.RaycastNonAlloc(origin, delta / length, occlusionHits, length, blockerMask, triggerInteraction);
            if (count >= occlusionHits.Length) { ++QueryOverflowCount; return true; }
            for (int i = 0; i < count; ++i)
                if (!Ignore(occlusionHits[i].collider) && Identity(occlusionHits[i].collider) != target) return true;
            return false;
        }

        private readonly RaycastHit[] occlusionHits = new RaycastHit[64];

        private void OnDrawGizmosSelected()
        {
            if (!bladeBase || !bladeTip) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(bladeBase.position, bladeTip.position);
            Gizmos.DrawWireSphere(bladeBase.position, sweepRadius);
            Gizmos.DrawWireSphere(bladeTip.position, sweepRadius);
        }
    }
}
