using UnityEngine;

namespace Game4026.Contracts
{
    /// <summary>Implemented by B's enemy damage receiver; called by A's weapons.</summary>
    public interface IDamageable
    {
        /// <summary>
        /// Applies one confirmed hit. Damage is finite, non-negative base damage before
        /// weak-point scaling; B applies the weak-point multiplier exactly once.
        /// Hit point is in world space. A suppresses duplicate contacts from one swing
        /// before calling this method; B owns health, invulnerability and death rules.
        /// </summary>
        void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint);
    }
}
