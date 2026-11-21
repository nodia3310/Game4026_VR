using System;
using Game4026.Contracts;
using Game4026.Interaction.Grabbing;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Interaction.Melee
{
    [DisallowMultipleComponent, RequireComponent(typeof(PhysicalGrabbable)), DefaultExecutionOrder(100)]
    public sealed class ReboundingWeapon : MonoBehaviour, IXRSelectFilter
    {
        private enum Flight { None, Released, Outbound, Returning }

        [Header("Throw")]
        [SerializeField, Min(0.1f)] private float minimumThrowSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float maximumOutboundSeconds = 2f;
        [SerializeField, Min(0f)] private float impactDamage = 20f;
        [Header("Return and catch")]
        [SerializeField, Min(0.1f)] private float returnSpeed = 6f;
        [SerializeField, Min(0f)] private float arcHeight = 0.8f;
        [SerializeField, Min(0.01f)] private float catchRadius = 0.45f;
        [SerializeField, Min(0.01f)] private float catchSeconds = 0.24f;

        public event Action<Collider> Rebounded;
        public event Action CatchWindowOpened;
        public event Action<bool> ReturnEnded;
        public bool IsInFlight => flight == Flight.Outbound || flight == Flight.Returning;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private XRBaseInteractor thrower;
        private Transform throwerRoot;
        private Flight flight;
        private float outboundUntil;
        private float returnElapsed, returnDuration;
        private Vector3 arcStart, gripOffset;
        private Quaternion initialRotation, gripRotation;
        private CollisionDetectionMode originalCollisionMode;
        private bool originallyDetectCollisions;
        private bool selectingCatch, signaledCatch;
        private Vector3? desktopLaunchVelocity;

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) =>
            flight == Flight.None || (selectingCatch && ReferenceEquals(interactor, thrower));

        private void Awake()
        {
            var physical = GetComponent<PhysicalGrabbable>();
            grab = physical.Grab;
            body = physical.Body;
        }

        private void OnEnable()
        {
            grab.lastSelectExited.AddListener(OnReleased);
            grab.selectFilters.Add(this);
        }

        private void OnDisable()
        {
            grab.lastSelectExited.RemoveListener(OnReleased);
            grab.selectFilters.Remove(this);
            if (flight == Flight.Returning) RestorePhysics();
            ClearFlight();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.isCanceled || args.interactorObject is not XRBaseInteractor hand ||
                hand.handedness == InteractorHandedness.None) return;

            thrower = hand;
            var origin = hand.GetComponentInParent<XROrigin>();
            throwerRoot = origin ? origin.transform : hand.transform.root;
            flight = Flight.Released;
        }

        internal bool QueueDesktopThrow(XRBaseInteractor hand, Vector3 velocity)
        {
            if (!isActiveAndEnabled || !grab.isSelected || !ReferenceEquals(grab.firstInteractorSelecting, hand))
                return false;

            desktopLaunchVelocity = velocity;
            return true;
        }

        private void LateUpdate()
        {
            if (flight == Flight.Released)
            {
                // The XRI manager applies its smoothed throw velocity in its earlier LateUpdate.
                if (desktopLaunchVelocity is { } velocity) body.linearVelocity = velocity;
                desktopLaunchVelocity = null;
                if (body.linearVelocity.sqrMagnitude < minimumThrowSpeed * minimumThrowSpeed)
                {
                    ClearFlight();
                    return;
                }

                flight = Flight.Outbound;
                outboundUntil = Time.time + maximumOutboundSeconds;
            }

            if (flight != Flight.Returning || !CanCatch(thrower)) return;
            var inputHand = thrower as XRBaseInputInteractor;
            if (!signaledCatch)
            {
                signaledCatch = true;
                inputHand?.SendHapticImpulse(0.3f, 0.045f);
                CatchWindowOpened?.Invoke();
            }

            if (inputHand != null && inputHand.isSelectActive && inputHand.selectInput.ReadWasPerformedThisFrame())
                TryCatch(inputHand);
        }

        private void FixedUpdate()
        {
            if (flight == Flight.Outbound && (Time.time >= outboundUntil || !HasThrower))
                ClearFlight();

            if (flight != Flight.Returning) return;
            if (!HasThrower || returnElapsed >= returnDuration)
            {
                RestorePhysics();
                ClearFlight();
                ReturnEnded?.Invoke(false);
                return;
            }

            var hand = thrower.GetAttachTransform(grab);
            returnElapsed += Time.fixedDeltaTime;
            float remaining = 1f - Mathf.Clamp01(returnElapsed / returnDuration);
            // Ease into the hand while keeping a parabolic spatial path.
            float t = 1f - remaining * remaining;
            var grip = Vector3.Lerp(arcStart, hand.position, t) + Vector3.up * (4f * arcHeight * t * (1f - t));
            var targetRotation = hand.rotation * Quaternion.Inverse(gripRotation);
            var rotation = Quaternion.Slerp(initialRotation, targetRotation, t);
            body.MovePosition(grip - rotation * gripOffset);
            body.MoveRotation(rotation);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (flight != Flight.Outbound || collision.contactCount == 0) return;
            BeginReturn(collision.collider, collision.GetContact(0).point);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (flight != Flight.Outbound || other.GetComponentInParent<IDamageable>() == null) return;
            BeginReturn(other, other.ClosestPoint(body.worldCenterOfMass));
        }

        private void BeginReturn(Collider other, Vector3 hitPoint)
        {
            if (!HasThrower || other.transform.IsChildOf(throwerRoot)) return;

            var grip = grab.GetAttachTransform(thrower);
            gripOffset = Quaternion.Inverse(transform.rotation) * (grip.position - transform.position);
            gripRotation = Quaternion.Inverse(transform.rotation) * grip.rotation;
            initialRotation = body.rotation;
            arcStart = body.position + initialRotation * gripOffset;
            returnDuration = Mathf.Clamp(Vector3.Distance(arcStart, thrower.GetAttachTransform(grab).position) / returnSpeed, 0.65f, 1.35f);
            returnElapsed = 0f;
            signaledCatch = false;

            flight = Flight.Returning;
            originalCollisionMode = body.collisionDetectionMode;
            originallyDetectCollisions = body.detectCollisions;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.detectCollisions = false;

            Rebounded?.Invoke(other);
            if (other && impactDamage > 0f)
                other.GetComponentInParent<IDamageable>()?.TakeDamage(impactDamage, hitPoint, false);
        }

        private bool HasThrower => thrower && thrower.isActiveAndEnabled;

        private bool CanCatch(XRBaseInteractor hand)
        {
            if (flight != Flight.Returning || !HasThrower || hand != thrower || hand.hasSelection ||
                returnDuration - returnElapsed > catchSeconds) return false;

            var grip = body.position + body.rotation * gripOffset;
            return (grip - hand.GetAttachTransform(grab).position).sqrMagnitude <= catchRadius * catchRadius;
        }

        internal bool TryCatch(XRBaseInteractor hand)
        {
            if (!CanCatch(hand) || !grab.isActiveAndEnabled || !hand.interactionManager) return false;

            var manager = hand.interactionManager;
            selectingCatch = true;
            try
            {
                if (!manager.IsSelectPossible(hand, grab)) return false;

                RestorePhysics();
                ClearFlight();

                // A direct catch always attaches at the controller, even if its ray is over a far target.
                var previousMode = grab.farAttachMode;
                grab.farAttachMode = InteractableFarAttachMode.Near;
                try { manager.SelectEnter((IXRSelectInteractor)hand, grab); }
                finally { grab.farAttachMode = previousMode; }

                bool caught = grab.isSelected;
                if (caught && hand is XRBaseInputInteractor inputHand) inputHand.SendHapticImpulse(0.55f, 0.07f);
                ReturnEnded?.Invoke(caught);
                return caught;
            }
            finally { selectingCatch = false; }
        }

        private void RestorePhysics()
        {
            body.isKinematic = false;
            body.collisionDetectionMode = originalCollisionMode;
            body.detectCollisions = originallyDetectCollisions;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void ClearFlight()
        {
            flight = Flight.None;
            thrower = null;
            throwerRoot = null;
            desktopLaunchVelocity = null;
        }
    }
}
