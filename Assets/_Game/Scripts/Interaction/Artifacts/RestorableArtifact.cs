using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Interaction.Artifacts
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArtifactIdentity), typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class RestorableArtifact : MonoBehaviour
    {
        private ArtifactIdentity identity;
        private Rigidbody body;
        private XRGrabInteractable grab;
        private Transform previousParent;
        private Vector3 previousLocalPosition, previousLocalScale, previousWorldPosition;
        private Quaternion previousLocalRotation, previousWorldRotation;
        private Vector3 previousVelocity, previousAngularVelocity;
        private bool previousKinematic, previousGravity, previousGrabEnabled, previousThrowOnDetach;
        private CollisionDetectionMode previousCollisionMode;
        private int lastReleaseFrame = -1;

        public ArtifactIdentity Identity { get { EnsureReferences(); return identity; } }
        public bool IsRestored { get; private set; }
        public bool IsHeld { get { EnsureReferences(); return grab.isSelected; } }

        private void OnEnable()
        {
            EnsureReferences();
            grab.lastSelectExited.AddListener(HandleReleased);
        }

        private void OnDisable()
        {
            if (grab) grab.lastSelectExited.RemoveListener(HandleReleased);
        }

        private void HandleReleased(SelectExitEventArgs args) { lastReleaseFrame = Time.frameCount; }

        private void EnsureReferences()
        {
            if (!identity) identity = GetComponent<ArtifactIdentity>();
            if (!body) body = GetComponent<Rigidbody>();
            if (!grab) grab = GetComponent<XRGrabInteractable>();
        }

        /// <summary>Called only after the display verifies membership, identity and dwell.</summary>
        public bool TryLockTo(Transform snapPoint)
        {
            EnsureReferences();
            if (!isActiveAndEnabled || IsRestored || IsHeld || !identity.IsValid || !snapPoint
                || (Application.isPlaying && Time.frameCount <= lastReleaseFrame)
                || snapPoint == transform || snapPoint.IsChildOf(transform)) return false;

            previousParent = transform.parent;
            previousLocalPosition = transform.localPosition;
            previousLocalRotation = transform.localRotation;
            previousLocalScale = transform.localScale;
            previousWorldPosition = transform.position;
            previousWorldRotation = transform.rotation;
            previousKinematic = body.isKinematic;
            previousGravity = body.useGravity;
            previousGrabEnabled = grab.enabled;
            previousThrowOnDetach = grab.throwOnDetach;
            previousCollisionMode = body.collisionDetectionMode;
            previousVelocity = body.linearVelocity;
            previousAngularVelocity = body.angularVelocity;

            IsRestored = true;
            // XRI finishes throwing in its Late phase. The release-frame guard lets that
            // finish first; disabling throwing is a second guard while the artifact is locked.
            grab.throwOnDetach = false;
            grab.enabled = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.useGravity = false;
            transform.SetParent(snapPoint, true);
            transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
            body.position = snapPoint.position;
            body.rotation = snapPoint.rotation;
            return true;
        }

        /// <summary>Restores the pre-lock pose, physics flags, velocities and grab enabled state.</summary>
        public void ResetRestoration()
        {
            if (!IsRestored) return;
            EnsureReferences();
            transform.SetParent(previousParent, false);
            if (previousParent)
            {
                transform.localPosition = previousLocalPosition;
                transform.localRotation = previousLocalRotation;
            }
            else transform.SetPositionAndRotation(previousWorldPosition, previousWorldRotation);
            transform.localScale = previousLocalScale;
            body.position = transform.position;
            body.rotation = transform.rotation;
            body.isKinematic = previousKinematic;
            body.collisionDetectionMode = previousCollisionMode;
            body.useGravity = previousGravity;
            if (!previousKinematic)
            {
                body.linearVelocity = previousVelocity;
                body.angularVelocity = previousAngularVelocity;
            }
            IsRestored = false;
            grab.throwOnDetach = previousThrowOnDetach;
            grab.enabled = previousGrabEnabled;
        }
    }
}
