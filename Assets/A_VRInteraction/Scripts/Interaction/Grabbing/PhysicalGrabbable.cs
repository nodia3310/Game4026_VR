using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Interaction.Grabbing
{
    /// <summary>
    /// Rigidbody-driven XRI grabbing for tools and artifacts. Use a tracked Direct or
    /// NearFar Interactor on each hand; this is not a finger-joint simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class PhysicalGrabbable : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float massKg = 1f;
        [SerializeField, Min(0f)] private float throwVelocityScale = 1f;
        [SerializeField, Min(0f)] private float throwAngularVelocityScale = 1f;
        [SerializeField] private Transform gripPose;
        [SerializeField, Tooltip("Handle direction in grip-pose space. Zero leaves the tracked hand pose as-is.")]
        private Vector3 handGripAxis;
        [SerializeField] private bool useDynamicGrip;
        [FormerlySerializedAs("lockToHandWhileHeld")]
        [SerializeField, Tooltip("Keep the visible hand on the weapon's physical grip during contact.")]
        private bool keepHandOnGrip;
        [SerializeField] private UnityEvent onGrabbed = new UnityEvent();
        [SerializeField] private UnityEvent onReleased = new UnityEvent();

        private XRGrabInteractable grab;
        private Rigidbody body;
        private CollisionDetectionMode? collisionModeBeforeGrab;
        public XRGrabInteractable Grab => grab != null ? grab : (grab = GetComponent<XRGrabInteractable>());
        public Rigidbody Body => body != null ? body : (body = GetComponent<Rigidbody>());
        public bool IsHeld => Grab.isSelected;
        public bool KeepsHandOnGrip => keepHandOnGrip;
        public event Action<PhysicalGrabbable> Grabbed;
        public event Action<PhysicalGrabbable> Released;

        public bool TryGetHandGrip(out Vector3 center, out Vector3 axis)
        {
            center = default;
            axis = default;
            if (useDynamicGrip || gripPose == null || handGripAxis == Vector3.zero) return false;

            center = gripPose.position;
            axis = gripPose.TransformDirection(handGripAxis.normalized);
            return true;
        }

        private void Awake() => ApplyPhysicsSettings();

        private void OnEnable()
        {
            Grab.firstSelectEntered.AddListener(HandleGrabbed);
            Grab.lastSelectExited.AddListener(HandleReleased);
        }

        private void OnDisable()
        {
            Grab.firstSelectEntered.RemoveListener(HandleGrabbed);
            Grab.lastSelectExited.RemoveListener(HandleReleased);
            RestoreCollisionMode();
        }

        /// <summary>Configure before grabbing. Never rewrites physics while held.</summary>
        public bool ApplyPhysicsSettings()
        {
            if (IsHeld) return false;
            Body.mass = Mathf.Max(0.01f, massKg);
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = Mathf.Max(Body.maxAngularVelocity, 30f);
            Grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            Grab.selectMode = InteractableSelectMode.Single;
            Grab.throwOnDetach = true;
            Grab.throwVelocityScale = Mathf.Max(0f, throwVelocityScale);
            Grab.throwAngularVelocityScale = Mathf.Max(0f, throwAngularVelocityScale);
            Grab.forceGravityOnDetach = true;
            Grab.trackPosition = true;
            Grab.trackRotation = true;
            Grab.trackScale = false;
            Grab.useDynamicAttach = useDynamicGrip;
            Grab.addDefaultGrabTransformers = true;
            if (gripPose != null) Grab.attachTransform = gripPose;
            return true;
        }

        private void HandleGrabbed(SelectEnterEventArgs args)
        {
            if (keepHandOnGrip)
            {
                collisionModeBeforeGrab = Body.collisionDetectionMode;
                // Include angular motion when a held blade or handle swings into another weapon.
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            onGrabbed.Invoke();
            Grabbed?.Invoke(this);
        }

        private void HandleReleased(SelectExitEventArgs args)
        {
            RestoreCollisionMode();
            onReleased.Invoke();
            Released?.Invoke(this);
        }

        private void RestoreCollisionMode()
        {
            if (collisionModeBeforeGrab is not { } mode) return;
            Body.collisionDetectionMode = mode;
            collisionModeBeforeGrab = null;
        }
    }
}
