using System;
using System.Collections.Generic;
using Game4026.Contracts;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
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
        [Header("References")]
        [SerializeField] private Transform bladeBase;
        [SerializeField] private Transform bladeTip;
        [Tooltip("XR Origin root moved by locomotion, not a tracked hand or Camera.")]
        [SerializeField] private Transform rigMotionReference;
        [Tooltip("Use the tracked head Camera inside the playable space for wall occlusion checks.")]
        [SerializeField] private Transform obstructionOrigin;
        [Tooltip("Optional player root whose colliders must never be hit.")]
        [SerializeField] private Transform ignoredPlayerRoot;
        [Tooltip("Fill unassigned rig references from the XR Origin of the hand that grabs this weapon.")]
        [SerializeField] private bool useSelectingXrOrigin;
        [Header("Queries")]
        [SerializeField, Min(0.005f)] private float sweepRadius = 0.035f;
        [SerializeField] private LayerMask targetMask = ~0;
        [Tooltip("Additional layers where only colliders with a parent IDamageable can emit a hit.")]
        [SerializeField] private LayerMask damageableTargetMask;
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
        private XROrigin selectingOrigin;
        private MeleeSwingWindow swing;
        private readonly Collider[] overlaps = new Collider[64];
        private readonly Collider[] occlusionOverlaps = new Collider[64];
        private readonly RaycastHit[] casts = new RaycastHit[64];
        private Vector3[] localSweepPoints;
        private Matrix4x4 previousWeaponPose;
        private Vector3 previousRigPosition;
        private Quaternion previousRigRotation;
        private bool hasSample;

        private Transform ActiveObstructionOrigin => obstructionOrigin ? obstructionOrigin :
            selectingOrigin && selectingOrigin.Camera ? selectingOrigin.Camera.transform : null;

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

        private void OnEnable() => grab.firstSelectEntered.AddListener(OnGrabbed);

        private void OnDisable()
        {
            grab.firstSelectEntered.RemoveListener(OnGrabbed);
            selectingOrigin = null;
            ResetSampling();
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!useSelectingXrOrigin) return;
            selectingOrigin = args.interactorObject.transform.GetComponentInParent<XROrigin>();
            ResetSampling();
        }

        /// <summary>Call on an explicit teleport/recenter or known tracking loss, in addition to automatic guards.</summary>
        public void ResetSampling()
        {
            hasSample = false;
            CurrentSpeed = 0;
            swing?.Reset();
        }

        private void LateUpdate()
        {
            var rig = rigMotionReference ? rigMotionReference : selectingOrigin ? selectingOrigin.transform : null;
            if (!grab.isSelected || !bladeBase || !bladeTip || !rig || !ActiveObstructionOrigin)
            {
                ResetSampling();
                return;
            }
            localSweepPoints ??= BuildSweepPoints();
            var pose = transform.localToWorldMatrix;
            var rigPosition = rig.position;
            var rigRotation = rig.rotation;
            float dt = Time.deltaTime;
            if (!MeleeMotion.IsFinite(dt) || dt <= 0 || localSweepPoints.Length == 0)
            {
                ResetSampling();
                return;
            }

            var oldPose = previousWeaponPose;
            var oldRigPosition = previousRigPosition;
            var oldRigRotation = previousRigRotation;
            previousWeaponPose = pose;
            previousRigPosition = rigPosition;
            previousRigRotation = rigRotation;
            if (!hasSample)
            {
                hasSample = true;
                return;
            }

            // Express both weapon poses in the current rig frame so locomotion cannot arm a swing.
            var rigDelta = rigRotation * Quaternion.Inverse(oldRigRotation);
            oldPose = Matrix4x4.TRS(rigPosition - rigDelta * oldRigPosition, rigDelta, Vector3.one) * oldPose;
            float maxDistanceSquared = 0f;
            foreach (var point in localSweepPoints)
            {
                var delta = pose.MultiplyPoint3x4(point) - oldPose.MultiplyPoint3x4(point);
                if (!MeleeMotion.IsFinite(delta)) { ResetSampling(); return; }
                maxDistanceSquared = Mathf.Max(maxDistanceSquared, delta.sqrMagnitude);
            }
            float movement = Mathf.Sqrt(maxDistanceSquared);
            if (dt > maximumSampleSeconds || movement > maximumSampleDistance || movement / dt > maximumTrackedSpeed ||
                Vector3.Distance(oldRigPosition, rigPosition) > rigTeleportDistance ||
                Quaternion.Angle(oldRigRotation, rigRotation) > rigTeleportAngle)
            {
                CurrentSpeed = 0;
                swing.Reset();
                return;
            }
            CurrentSpeed = movement / dt;
            if (!swing.Advance(CurrentSpeed, dt)) return;
            foreach (var point in localSweepPoints)
                Sweep(oldPose.MultiplyPoint3x4(point), pose.MultiplyPoint3x4(point), dt);
        }

        private Vector3[] BuildSweepPoints()
        {
            var start = bladeBase.position;
            var tip = bladeTip.position;
            var axis = (tip - start).normalized;
            var grip = GetComponent<XRGrabInteractable>().attachTransform;
            var points = new List<Vector3>(maximumBladeSamples);
            bool AddSegment(Vector3 from, Vector3 to, bool includeStart)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / sweepRadius));
                if (points.Count + steps + (includeStart ? 1 : 0) > maximumBladeSamples) return false;
                for (int i = includeStart ? 0 : 1; i <= steps; i++)
                    points.Add(transform.InverseTransformPoint(Vector3.Lerp(from, to, (float)i / steps)));
                return true;
            }

            if (!AddSegment(start, tip, true)) return Array.Empty<Vector3>();
            if (!grip || axis == Vector3.zero) return points.ToArray();

            // The blade markers can sit off the shaft (axe head and crowbar hook).
            // Follow the shaft through the grip instead of cutting diagonally across empty space.
            var handle = grip.position;
            float rear = 0f;
            foreach (var box in GetComponentsInChildren<BoxCollider>())
            {
                if (!box.enabled || box.isTrigger) continue;
                var extents = box.size * 0.5f;
                var boxTransform = box.transform;
                float center = Vector3.Dot(boxTransform.TransformPoint(box.center) - handle, axis);
                float halfLength = Mathf.Abs(Vector3.Dot(boxTransform.TransformVector(Vector3.right * extents.x), axis))
                    + Mathf.Abs(Vector3.Dot(boxTransform.TransformVector(Vector3.up * extents.y), axis))
                    + Mathf.Abs(Vector3.Dot(boxTransform.TransformVector(Vector3.forward * extents.z), axis));
                rear = Mathf.Min(rear, center - halfLength);
            }
            var joint = start;
            var shoulder = handle + axis * Vector3.Dot(joint - handle, axis);
            if (Vector3.Distance(shoulder, joint) > sweepRadius)
            {
                // Offset heads join the shaft inside the head, between the two striking markers.
                joint = (start + tip) * 0.5f;
                shoulder = handle + axis * Vector3.Dot(joint - handle, axis);
            }
            if ((shoulder - joint).sqrMagnitude > 0.000001f && !AddSegment(joint, shoulder, false))
                return Array.Empty<Vector3>();
            if (!AddSegment(shoulder, handle + axis * rear, false)) return Array.Empty<Vector3>();
            return points.ToArray();
        }
        private bool Ignore(Collider collider)
        {
            var playerRoot = ignoredPlayerRoot ? ignoredPlayerRoot : selectingOrigin ? selectingOrigin.transform : null;
            return !collider || collider.transform.IsChildOf(transform) ||
                (playerRoot && collider.transform.IsChildOf(playerRoot));
        }

        private static bool InMask(Collider collider, LayerMask mask) => (mask.value & (1 << collider.gameObject.layer)) != 0;

        private bool IsTarget(Collider collider) =>
            InMask(collider, targetMask) ||
            (InMask(collider, damageableTargetMask) && collider.GetComponentInParent<IDamageable>() != null);

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
            int mask = targetMask.value | damageableTargetMask.value | blockerMask.value;
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
                if (IsTarget(collider))
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
                if (IsTarget(hit.collider)) TryEmit(hit.collider, hit.point, direction, speed);
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
            Vector3 origin = ActiveObstructionOrigin.position;
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
            foreach (var point in localSweepPoints ?? BuildSweepPoints())
                Gizmos.DrawWireSphere(transform.TransformPoint(point), sweepRadius);
        }
    }
}
