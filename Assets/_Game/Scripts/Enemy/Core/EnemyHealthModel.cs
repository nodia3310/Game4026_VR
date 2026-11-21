using System;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Engine-independent hit rules for one enemy: weak-point scaling, hit cooldown, stagger and death.
    /// Time is passed in by the caller so the rules can be unit tested without Play Mode.
    /// </summary>
    public sealed class EnemyHealthModel
    {
        private readonly float maxHealth;
        private readonly float weakPointMultiplier;
        private readonly float hitCooldown;
        private readonly float staggerThreshold;

        private float lastAcceptedHitTime;
        private bool hasAcceptedHit;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        /// <summary>While true (e.g. during spawn), all damage is ignored.</summary>
        public bool IsInvulnerable { get; set; }

        private float damageMultiplier = 1f;

        /// <summary>Runtime multiplier on all damage (e.g. 1.5 while staggered by a parry). Reset to 1 by <see cref="Reset"/>.</summary>
        public float DamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(0f, value);
        }

        public EnemyHealthModel(float maxHealth, float weakPointMultiplier, float hitCooldown, float staggerThreshold)
        {
            if (!(maxHealth > 0f) || float.IsInfinity(maxHealth))
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Max health must be positive and finite.");

            this.maxHealth = maxHealth;
            this.weakPointMultiplier = Mathf.Max(0f, weakPointMultiplier);
            this.hitCooldown = Mathf.Max(0f, hitCooldown);
            this.staggerThreshold = Mathf.Max(0f, staggerThreshold);
            Reset();
        }

        public EnemyHealthModel(EnemyData data)
            : this(data.MaxHealth, data.WeakPointMultiplier, data.HitCooldown, data.StaggerThreshold)
        {
        }

        /// <summary>
        /// Applies one hit. Returns false (and leaves state unchanged) when the hit is ignored:
        /// dead, invulnerable, within the hit cooldown, or damage that is not positive and finite.
        /// </summary>
        public bool TryApplyDamage(float baseDamage, Vector3 hitPoint, bool isWeakPoint, float time, out EnemyDamageResult result)
        {
            result = default;

            if (IsDead || IsInvulnerable)
                return false;
            if (!(baseDamage > 0f) || float.IsInfinity(baseDamage))
                return false;
            // Compare against the end time (not the elapsed difference) so a hit exactly at the end is accepted without float drift.
            if (hasAcceptedHit && time < lastAcceptedHitTime + hitCooldown)
                return false;

            float scaled = (isWeakPoint ? baseDamage * weakPointMultiplier : baseDamage) * damageMultiplier;
            float applied = Mathf.Min(scaled, CurrentHealth);

            CurrentHealth -= applied;
            hasAcceptedHit = true;
            lastAcceptedHitTime = time;

            bool killed = CurrentHealth <= 0f;
            if (killed)
            {
                CurrentHealth = 0f;
                IsDead = true;
            }

            bool stagger = !killed && (isWeakPoint || scaled >= staggerThreshold);
            result = new EnemyDamageResult(applied, hitPoint, isWeakPoint, stagger, killed);
            return true;
        }

        /// <summary>Restores full health and clears death, cooldown and invulnerability (object pool reuse).</summary>
        public void Reset()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
            IsInvulnerable = false;
            damageMultiplier = 1f;
            hasAcceptedHit = false;
            lastAcceptedHitTime = 0f;
        }
    }
}
