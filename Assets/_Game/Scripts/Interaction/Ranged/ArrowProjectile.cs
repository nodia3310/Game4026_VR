using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Interaction.Ranged
{
    /// <summary>Optional physical arrow. First solid collision emits a hit; no enemy dependency.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class ArrowProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 10f;
        [SerializeField] private bool freezeOnImpact = true;
        [SerializeField] private UnityEvent<Vector3> onImpactPoint = new UnityEvent<Vector3>();
        public event Action<Collision> Impact;
        private Rigidbody body;
        private bool launched;
        private bool impacted;

        public void Launch(Vector3 velocity, Collider[] ownerColliders, Collider[] extraIgnoredColliders = null)
        {
            if (launched) return;
            launched = true;
            body = GetComponent<Rigidbody>();
            // Spawned arrows are projectiles, not grab interactables. Lifetime bounds orphaned arrows.
            foreach (var grab in GetComponentsInChildren<XRGrabInteractable>(true)) grab.enabled = false;
            var colliders = GetComponentsInChildren<Collider>(true);
            Ignore(colliders, ownerColliders);
            Ignore(colliders, extraIgnoredColliders);
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            Destroy(gameObject, Mathf.Max(0.1f, lifetime));
        }

        private static void Ignore(Collider[] arrows, Collider[] others)
        {
            if (others == null) return;
            foreach (var arrow in arrows)
                foreach (var other in others)
                    if (arrow != null && other != null && arrow != other) Physics.IgnoreCollision(arrow, other);
        }

        private void FixedUpdate()
        {
            if (!launched || impacted || body == null || body.linearVelocity.sqrMagnitude < 0.01f) return;
            body.MoveRotation(Quaternion.LookRotation(body.linearVelocity.normalized));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!launched || impacted) return;
            impacted = true;
            if (freezeOnImpact)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = true;
            }
            Impact?.Invoke(collision);
            onImpactPoint.Invoke(collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }
    }
}
