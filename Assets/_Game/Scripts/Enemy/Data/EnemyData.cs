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
        [Tooltip("Moves this enemy picks from (spec 4.5). Empty = one basic melee swing.")]
        [SerializeField] private AttackDefinition[] attacks = new AttackDefinition[0];
        [Tooltip("Minimum seconds after any move ends before the next one starts.")]
        [SerializeField, Min(0f)] private float globalAttackCooldown = 1.2f;

        [Header("Group combat (attack slots)")]
        [Tooltip("Attack slots this enemy takes while attacking (see AttackCoordinator). Elites take more, so fewer others attack with them.")]
        [SerializeField, Min(1)] private int attackSlotCost = 1;
        [Tooltip("Without a free slot (or out of the player's view) the enemy waits about this far from the player and circles.")]
        [SerializeField, Min(0.5f)] private float waitDistance = 3.5f;

        [Header("Parry")]
        [Tooltip("Stagger seconds after the player deflects an attack with their weapon.")]
        [SerializeField, Min(0f)] private float parryStaggerDuration = 1.5f;
        [Tooltip("Damage taken is multiplied by this during the parry stagger (counter-attack window).")]
        [SerializeField, Min(0f)] private float parriedDamageMultiplier = 1.5f;

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
            if (attacks != null)
                foreach (AttackDefinition a in attacks)
                    if (a != null && a.MaxRange > detectionRange)
                        Debug.LogWarning($"[{name}] attack '{a.name}' maxRange ({a.MaxRange}) is larger than detectionRange ({detectionRange}).", this);
        }

        public int AttackSlotCost => attackSlotCost;
        public float WaitDistance => waitDistance;
        public float MaxHealth => maxHealth;
        public float WeakPointMultiplier => weakPointMultiplier;
        public float HitCooldown => hitCooldown;
        public float StaggerThreshold => staggerThreshold;
        public float StaggerDuration => staggerDuration;
        public float MoveSpeed => moveSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float DetectionRange => detectionRange;
        public float LoseTargetRange => loseTargetRange;
        /// <summary>The moves to pick from; never empty (falls back to <see cref="DefaultAttacks"/>).</summary>
        public AttackDefinition[] Attacks => HasOwnAttacks ? attacks : DefaultAttacks;
        public float GlobalAttackCooldown => globalAttackCooldown;
        public float ParryStaggerDuration => parryStaggerDuration;
        public float ParriedDamageMultiplier => parriedDamageMultiplier;

        private bool HasOwnAttacks
        {
            get
            {
                if (attacks == null)
                    return false;
                foreach (AttackDefinition a in attacks)
                    if (a != null)
                        return true;
                return false;
            }
        }

        private static AttackDefinition[] defaultAttacks;

        /// <summary>One basic melee swing (range 1.6, hit at 0.7 s, 10 damage, cooldown 2) for data without moves.</summary>
        public static AttackDefinition[] DefaultAttacks
        {
            get
            {
                if (defaultAttacks == null || defaultAttacks[0] == null)
                {
                    AttackDefinition melee = AttackDefinition.Create(name: "DefaultMelee");
                    melee.hideFlags = HideFlags.HideAndDontSave;
                    defaultAttacks = new[] { melee };
                }
                return defaultAttacks;
            }
        }

        /// <summary>Code setup (tests).</summary>
        internal void SetAttacks(AttackDefinition[] moves, float globalCooldown)
        {
            attacks = moves ?? new AttackDefinition[0];
            globalAttackCooldown = globalCooldown;
        }
        public float SpawnDuration => spawnDuration;
        public float DespawnDuration => despawnDuration;
    }
}
