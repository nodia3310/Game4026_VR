using Game4026.Interaction.Ranged;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>
    /// Put on A's arrow prefab next to <see cref="ArrowProjectile"/>. Turns its first impact into damage
    /// on the hit collider's <c>IDamageable</c>, scaled by the collision's relative speed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArrowDamageBridge : MonoBehaviour
    {
        [SerializeField] private ArrowProjectile arrow;
        [SerializeField] private ImpactDamageCurve damage = ImpactDamageCurve.ArrowDefault;

        public ImpactDamageCurve Damage { get => damage; set => damage = value; }

        private void Awake()
        {
            if (arrow == null)
                arrow = GetComponent<ArrowProjectile>();
            if (arrow == null)
                Debug.LogWarning($"{nameof(ArrowDamageBridge)} on '{name}' has no {nameof(ArrowProjectile)}.", this);
        }

        private void OnEnable()
        {
            if (arrow != null)
                arrow.Impact += HandleImpact;
        }

        private void OnDisable()
        {
            if (arrow != null)
                arrow.Impact -= HandleImpact;
        }

        private void HandleImpact(Collision collision)
        {
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            ApplyImpact(collision.collider, point, collision.relativeVelocity.magnitude);
        }

        /// <summary>Applies one arrow impact. Public so tests can call it without a physics collision.</summary>
        public bool ApplyImpact(Collider hitCollider, Vector3 hitPoint, float impactSpeed) =>
            DamageDispatch.TryApply(hitCollider, damage.Evaluate(impactSpeed), hitPoint);
    }
}
