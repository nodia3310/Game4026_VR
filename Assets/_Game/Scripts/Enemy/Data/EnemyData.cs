using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>Tunable values for one enemy type (spec 4.1). Initial values are placeholders for balancing.</summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Game4026/Enemy/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(1f)] private float weakPointMultiplier = 2.5f;
        [Tooltip("Minimum seconds between two accepted hits (from any source).")]
        [SerializeField, Min(0f)] private float hitCooldown = 0.3f;
        [Tooltip("A single hit dealing at least this much damage, or any weak-point hit, staggers the enemy.")]
        [SerializeField, Min(0f)] private float staggerThreshold = 25f;
        [SerializeField, Min(0f)] private float staggerDuration = 0.5f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 1.0f;
        [SerializeField, Min(0f)] private float chaseSpeed = 2.2f;

        [Header("Perception")]
        [SerializeField, Min(0f)] private float detectionRange = 12f;
        [SerializeField, Min(0f)] private float loseTargetRange = 18f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float attackRange = 1.6f;
        [SerializeField, Min(0f)] private float attackWindup = 0.7f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0f)] private float attackCooldown = 2.0f;
        [SerializeField, Range(0f, 360f)] private float attackAngle = 70f;

        [Header("Lifecycle")]
        [SerializeField, Min(0f)] private float spawnDuration = 1.5f;
        [SerializeField, Min(0f)] private float despawnDuration = 1.5f;

        private void OnValidate()
        {
            // Inspector safety: an enemy must not lose the player inside its own detection range.
            if (loseTargetRange < detectionRange)
            {
                Debug.LogWarning($"[{name}] loseTargetRange ({loseTargetRange}) < detectionRange ({detectionRange}); raised to match.", this);
                loseTargetRange = detectionRange;
            }
            if (attackRange > detectionRange)
                Debug.LogWarning($"[{name}] attackRange ({attackRange}) is larger than detectionRange ({detectionRange}).", this);
        }

        public float MaxHealth => maxHealth;
        public float WeakPointMultiplier => weakPointMultiplier;
        public float HitCooldown => hitCooldown;
        public float StaggerThreshold => staggerThreshold;
        public float StaggerDuration => staggerDuration;
        public float MoveSpeed => moveSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float DetectionRange => detectionRange;
        public float LoseTargetRange => loseTargetRange;
        public float AttackRange => attackRange;
        public float AttackWindup => attackWindup;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public float AttackAngle => attackAngle;
        public float SpawnDuration => spawnDuration;
        public float DespawnDuration => despawnDuration;
    }
}
