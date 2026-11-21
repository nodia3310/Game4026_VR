using Game4026.Contracts;
using Game4026.Enemy;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>Delivers damage to whatever <see cref="IDamageable"/> owns a hit collider (body or weak point).</summary>
    public static class DamageDispatch
    {
        /// <param name="allowParry">False for arrows: hitting an enemy's weapon (<see cref="EnemyWeapon"/>) does nothing.</param>
        /// <returns>True when a receiver was found and called.</returns>
        public static bool TryApply(Collider hitCollider, float damage, Vector3 hitPoint, bool allowParry = true)
        {
            if (hitCollider == null || !(damage > 0f))
                return false;

            // Called once per hit event, never per frame.
            IDamageable receiver = hitCollider.GetComponentInParent<IDamageable>();
            if (receiver == null)
                return false;
            if (!allowParry && receiver is EnemyWeapon)
                return false;

            // Weapons always report false; a WeakPoint component turns it into a weak-point hit.
            receiver.TakeDamage(damage, hitPoint, false);
            return true;
        }
    }
}
