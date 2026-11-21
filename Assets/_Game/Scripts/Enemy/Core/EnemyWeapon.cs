using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Put on the enemy's weapon (with a non-trigger collider on the blade). A's melee weapons reach it through
    /// <c>GetComponentInParent&lt;IDamageable&gt;()</c> like any target, so no contract change is needed:
    /// a hit inside the attack's parry window deflects the attack (<see cref="EnemyAttack.TryParry"/>);
    /// outside it the hit is blocked by the blade and does nothing. The blade never passes damage to the body.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyWeapon : MonoBehaviour, IDamageable, IParryTarget
    {
        [SerializeField] private EnemyAttack attack;
        [Tooltip("Which hand holds it. Mirrored moves (humanoid mirror) swing with the left hand.")]
        [SerializeField] private bool leftHand;
        [Tooltip("The body itself is the weapon (statues that ram/roll): outside a parry window, hits damage the enemy normally.")]
        [SerializeField] private bool passDamageWhenNotParrying;
        [Tooltip("Blade tip: where the attack indicator glints. Defaults to this transform.")]
        [SerializeField] private Transform tip;

        /// <summary>The hit deflected an attack. Vector3 = hit point (sparks, clang).</summary>
        public event Action<EnemyWeapon, Vector3> Deflected;
        /// <summary>The blade was hit outside a parry window. Vector3 = hit point.</summary>
        public event Action<EnemyWeapon, Vector3> Blocked;

        public Transform Tip => tip != null ? tip : transform;
        public bool IsLeftHand => leftHand;
        public bool PassesDamage => passDamageWhenNotParrying;
        public EnemyAttack Attack
        {
            get
            {
                if (attack == null)
                    attack = GetComponentInParent<EnemyAttack>(); // once; cached afterwards
                return attack;
            }
        }

        private void Awake()
        {
            if (Attack == null)
                Debug.LogWarning($"{nameof(EnemyWeapon)} on '{name}' has no {nameof(EnemyAttack)} in its parents.", this);
        }

        public void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint) => TryDeflect(hitPoint, Time.time, damage);

        /// <summary>
        /// Returns true when the hit parried an attack. Otherwise the blade blocks it, or, for a body weapon,
        /// the damage goes to the enemy's health. Explicit time for tests.
        /// </summary>
        public bool TryDeflect(Vector3 hitPoint, float now, float damage = 0f)
        {
            EnemyAttack owner = Attack;
            if (owner != null && owner.TryParry(now))
            {
                Deflected?.Invoke(this, hitPoint);
                return true;
            }
            if (passDamageWhenNotParrying)
                PassToBody(damage, hitPoint);
            else
                Blocked?.Invoke(this, hitPoint);
            return false;
        }

        /// <summary>Arrows: never a parry; a body weapon still takes the damage.</summary>
        public bool TakeUnparryableHit(float damage, Vector3 hitPoint)
        {
            if (!passDamageWhenNotParrying)
                return false;
            return PassToBody(damage, hitPoint);
        }

        private bool PassToBody(float damage, Vector3 hitPoint)
        {
            if (!(damage > 0f))
                return false;
            EnemyHealth health = GetComponentInParent<EnemyHealth>(); // per hit, not per frame
            if (health == null)
                return false;
            health.TakeDamage(damage, hitPoint, false);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(Tip.position, 0.04f);
        }
    }
}
