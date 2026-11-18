using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Melee attack (spec 4.5): windup event, then a distance + angle check (no physics colliders).
    /// A hit calls <see cref="IPlayerDamageReceiver.ApplyPlayerDamage"/> exactly once per attack.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttack : MonoBehaviour
    {
        [Tooltip("Seconds after the hit check before the enemy can move again.")]
        [SerializeField, Min(0f)] private float recoveryDuration = 0.5f;
        [Tooltip("Extra reach added to EnemyData.attackRange for the hit check (spec: 0.3 m).")]
        [SerializeField, Min(0f)] private float hitRangePadding = 0.3f;

        private float nextReadyTime;
        private float speedMultiplier = 1f;

        /// <summary>Windup started: play telegraph SFX/VFX here so the player can dodge.</summary>
        public event Action<EnemyAttack> AttackWindup;
        /// <summary>Hit check done; bool is true when the player was hit.</summary>
        public event Action<EnemyAttack, bool> AttackResolved;
        /// <summary>Windup interrupted (stagger or death) before the hit check.</summary>
        public event Action<EnemyAttack> AttackCancelled;

        private float recoveryOverride = -1f;

        /// <summary>Seconds after the hit check before moving again. Uses <see cref="RecoveryOverride"/> when it is set.</summary>
        public float RecoveryDuration => recoveryOverride >= 0f ? recoveryOverride : recoveryDuration;

        /// <summary>
        /// Runtime recovery time, e.g. set by <see cref="EnemyAnimator"/> so the attack clip can finish.
        /// Negative = use the Inspector value.
        /// </summary>
        public float RecoveryOverride
        {
            get => recoveryOverride;
            set => recoveryOverride = float.IsNaN(value) ? -1f : value;
        }
        public float HitRangePadding => hitRangePadding;

        /// <summary>
        /// Runtime attack speed (e.g. the boss's last phase). 2 = windup and cooldown take half as long.
        /// Reset to 1 by <see cref="ResetState"/>.
        /// </summary>
        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Max(0.1f, value);
        }

        public float WindupFor(EnemyData data) => data.AttackWindup / speedMultiplier;
        public float CooldownFor(EnemyData data) => data.AttackCooldown / speedMultiplier;

        public bool IsReady(float time) => time >= nextReadyTime;

        public void BeginWindup() => AttackWindup?.Invoke(this);

        /// <summary>Runs the hit check, applies damage on a hit and starts the cooldown.</summary>
        public bool Resolve(PlayerTarget target, EnemyData data, float time)
        {
            nextReadyTime = time + CooldownFor(data);

            bool hit = target != null && EnemyGeometry.IsInAttackCone(
                transform.position, transform.forward, target.Body.position,
                data.AttackRange + hitRangePadding, data.AttackAngle);

            if (hit)
                target.Damageable?.ApplyPlayerDamage(data.AttackDamage);

            AttackResolved?.Invoke(this, hit);
            return hit;
        }

        public void Cancel() => AttackCancelled?.Invoke(this);

        public void ResetState()
        {
            nextReadyTime = 0f;
            speedMultiplier = 1f;
        }
    }
}
