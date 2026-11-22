using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// The pharaoh's barrier wall. Weapon and arrow hits on it are absorbed (they would otherwise find the boss's
    /// EnemyHealth in a parent) and play the "blocked" reaction.
    /// </summary>
    public sealed class ArenaBarrier : MonoBehaviour, IDamageable
    {
        private EnemyHitFeedback feedback;

        private void Awake() => feedback = GetComponentInParent<EnemyHitFeedback>();

        public void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint)
        {
            if (feedback != null && damage > 0f)
                feedback.Play(HitKind.Blocked, hitPoint, 0f);
        }
    }
}
