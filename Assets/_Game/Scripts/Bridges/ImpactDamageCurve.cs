using System;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>
    /// Converts an impact speed (m/s) into base damage: clamp(speed * damagePerSpeed, minDamage, maxDamage).
    /// Non-positive or non-finite speeds give 0. Weak-point scaling is applied later by the enemy.
    /// </summary>
    [Serializable]
    public struct ImpactDamageCurve
    {
        [Tooltip("Damage per 1 m/s of impact speed.")]
        [Min(0f)] public float DamagePerSpeed;
        [Tooltip("Damage of any accepted hit, however slow.")]
        [Min(0f)] public float MinDamage;
        [Min(0f)] public float MaxDamage;

        public ImpactDamageCurve(float damagePerSpeed, float minDamage, float maxDamage)
        {
            DamagePerSpeed = damagePerSpeed;
            MinDamage = minDamage;
            MaxDamage = maxDamage;
        }

        /// <summary>Melee default: 3 m/s = 24 (no stagger), 4 m/s = 32 (stagger at 25).</summary>
        public static ImpactDamageCurve MeleeDefault => new ImpactDamageCurve(8f, 5f, 40f);

        /// <summary>Arrow default: roughly 1 damage per m/s, 10–35.</summary>
        public static ImpactDamageCurve ArrowDefault => new ImpactDamageCurve(1f, 10f, 35f);

        public float Evaluate(float speed)
        {
            if (!(speed > 0f) || float.IsInfinity(speed))
                return 0f;

            float max = Mathf.Max(MinDamage, MaxDamage);
            return Mathf.Clamp(speed * DamagePerSpeed, MinDamage, max);
        }
    }
}
