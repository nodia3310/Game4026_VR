using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>Outcome of one accepted hit, passed to <see cref="EnemyHealth.Damaged"/> listeners (VFX, SFX, haptics, AI).</summary>
    public readonly struct EnemyDamageResult
    {
        /// <summary>Health actually removed (after weak-point scaling, clamped to remaining health).</summary>
        public readonly float AppliedDamage;
        public readonly Vector3 HitPoint;
        public readonly bool IsWeakPoint;
        /// <summary>True when the hit met the stagger condition and the enemy is still alive.</summary>
        public readonly bool CausedStagger;
        public readonly bool Killed;

        public EnemyDamageResult(float appliedDamage, Vector3 hitPoint, bool isWeakPoint, bool causedStagger, bool killed)
        {
            AppliedDamage = appliedDamage;
            HitPoint = hitPoint;
            IsWeakPoint = isWeakPoint;
            CausedStagger = causedStagger;
            Killed = killed;
        }
    }
}
