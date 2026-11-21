using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// One attack move (spec 4.5). New moves are new assets, not new code.
    /// Timeline of one step (real seconds, before the enemy's attack-speed multiplier):
    /// the clip plays from <see cref="ClipStartTime"/>, freezes on <see cref="TelegraphPoseTime"/> for
    /// <see cref="TelegraphTime"/> (the dodge cue), resumes and connects at <see cref="HitTime"/>,
    /// then <see cref="RecoveryTime"/> before the enemy moves again. Clip times are divided by <see cref="AnimSpeed"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Attack", menuName = "Game4026/Enemy/Attack Definition")]
    public sealed class AttackDefinition : ScriptableObject
    {
        [SerializeField] private AttackType attackType = AttackType.Melee;

        [Header("Animation (ignored without an Animator)")]
        [Tooltip("Animator state to cross-fade into. With Mirror on, '<state>_Mirror' is used when the controller has it.")]
        [SerializeField] private string animatorState = "Attack";
        [Tooltip("Optional: the clip of that state. Its length keeps the enemy in recovery until the clip has finished.")]
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0.05f)] private float animSpeed = 1f;
        [SerializeField] private bool mirror;
        [Tooltip("Seconds into the clip where playback starts (skip the wind-up, e.g. for combo follow-ups).")]
        [SerializeField, Min(0f)] private float clipStartTime;

        [Header("Timing")]
        [Tooltip("Extra warning before the swing: the clip holds its wind-up pose this long.")]
        [SerializeField, Min(0f)] private float telegraphTime;
        [Tooltip("Seconds into the clip (at speed 1) of the wind-up pose held during the telegraph. Negative = 70% of Hit Time.")]
        [SerializeField] private float telegraphPoseTime = -1f;
        [Tooltip("Seconds into the clip (at speed 1) where the strike connects.")]
        [SerializeField, Min(0f)] private float hitTime = 0.7f;
        [Tooltip("Seconds after the hit before moving again (a counter-attack window). In a combo step: the gap before the next step.")]
        [SerializeField, Min(0f)] private float recoveryTime = 0.5f;
        [Tooltip("Seconds before the hit in which the player's weapon can deflect the blade (parry). 0 = cannot be parried.")]
        [SerializeField, Min(0f)] private float parryWindow = 0.35f;

        [Header("Slow motion (e.g. at the top of a jump)")]
        [Tooltip("Seconds into the clip (at speed 1) where the whole enemy drops into slow motion (animation, lunge, timeline), then eases back. Negative = off.")]
        [SerializeField] private float slowMotionAt = -1f;
        [Tooltip("Real seconds of slow motion (the last 40% eases back to normal).")]
        [SerializeField, Min(0f)] private float slowMotionDuration = 0.6f;
        [Tooltip("Speed during the slow motion (0.1 = ten times slower).")]
        [SerializeField, Range(0.01f, 1f)] private float slowMotionScale = 0.12f;

        [Header("Selection")]
        [SerializeField, Min(0f)] private float minRange;
        [SerializeField, Min(0f)] private float maxRange = 1.6f;
        [SerializeField, Min(0f)] private float cooldown = 2f;
        [SerializeField, Min(0f)] private float weight = 1f;

        [Header("Hit")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0f)] private float hitRange = 1.9f;
        [SerializeField, Range(0f, 360f)] private float hitAngle = 70f;

        [Header("Lunge")]
        [Tooltip("Maximum forward distance. The lunge stops short of the player instead of passing through.")]
        [SerializeField, Min(0f)] private float lungeDistance = 2f;
        [Tooltip("Seconds of forward push after the telegraph. 0 = push until the hit.")]
        [SerializeField, Min(0f)] private float lungeDuration;
        [Tooltip("Stop this far from the player. Negative = EnemyAttack's default (0.9 m).")]
        [SerializeField] private float lungeStopDistance = -1f;
        [Tooltip("Statues: jump height of the arc while moving forward (visual only).")]
        [SerializeField, Min(0f)] private float lungeArcHeight;

        [Header("Combo")]
        [Tooltip("Steps played in order without returning to Chase. This asset's telegraph, ranges, cooldown, weight and final recovery apply to the whole combo.")]
        [SerializeField] private AttackDefinition[] comboSteps = new AttackDefinition[0];

        [Header("Area Slam")]
        [SerializeField, Min(0f)] private float slamRadius = 2f;
        [Tooltip("Leap toward the player before slamming (uses the Lunge fields for distance/stop/arc).")]
        [SerializeField] private bool leap;

        [Header("Projectile")]
        [SerializeField] private EnemyProjectile projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 7f;

        [Header("Statue (no rig)")]
        [Tooltip("Procedural body motion for statue enemies (StatueLocomotion). Auto: Lunge=Hop, AreaSlam=Slam, Projectile=Shoot.")]
        [SerializeField] private StatueMotion statueMotion = StatueMotion.Auto;

        private int stateHash, mirrorStateHash;
        private string hashedState;

        public AttackType Type => attackType;
        public string AnimatorState => animatorState;
        public AnimationClip Clip => clip;
        public float AnimSpeed => animSpeed;
        public bool Mirror => mirror;
        public float ClipStartTime => clipStartTime;
        public float TelegraphTime => telegraphTime;
        public float TelegraphPoseTime => telegraphPoseTime >= 0f ? telegraphPoseTime : hitTime * 0.7f;
        public float HitTime => hitTime;
        public float RecoveryTime => recoveryTime;
        public bool HasSlowMotion => slowMotionAt >= 0f && slowMotionDuration > 0f && slowMotionScale < 1f;
        public float SlowMotionAt => slowMotionAt;
        public float SlowMotionDuration => slowMotionDuration;
        public float SlowMotionScale => slowMotionScale;
        public float ParryWindow => parryWindow;
        public bool IsParryable => parryWindow > 0f;
        public float MinRange => minRange;
        public float MaxRange => maxRange;
        public float Cooldown => cooldown;
        public float Weight => weight;
        public float Damage => damage;
        public float HitRange => hitRange;
        public float HitAngle => hitAngle;
        public float LungeDistance => lungeDistance;
        public float LungeDuration => lungeDuration;
        public AttackDefinition[] ComboSteps => comboSteps;
        public float SlamRadius => slamRadius;
        public bool Leap => leap;
        public float LungeStopDistance => lungeStopDistance;
        public float LungeArcHeight => lungeArcHeight;
        public EnemyProjectile ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;

        /// <summary>True when the step moves the enemy toward the player (Lunge, or a leaping AreaSlam).</summary>
        public bool MovesForward => attackType == AttackType.Lunge || (attackType == AttackType.AreaSlam && leap);

        public StatueMotion Motion
        {
            get
            {
                if (statueMotion != StatueMotion.Auto)
                    return statueMotion;
                switch (attackType)
                {
                    case AttackType.AreaSlam: return StatueMotion.Slam;
                    case AttackType.Projectile: return StatueMotion.Shoot;
                    default: return StatueMotion.Hop;
                }
            }
        }

        /// <summary>Types the runner can execute today. Others are never selected.</summary>
        public bool IsSupported =>
            attackType == AttackType.Melee || attackType == AttackType.Lunge ||
            attackType == AttackType.AreaSlam || attackType == AttackType.Guard || (attackType == AttackType.Combo && HasComboSteps) ||
            (attackType == AttackType.Projectile && projectilePrefab != null);

        public bool HasComboSteps
        {
            get
            {
                if (comboSteps == null)
                    return false;
                foreach (AttackDefinition step in comboSteps)
                    if (step != null)
                        return true;
                return false;
            }
        }

        /// <summary>Animator.StringToHash of the state, cached (no per-attack string work).</summary>
        public int StateHash { get { EnsureHashes(); return stateHash; } }
        /// <summary>Hash of "<state>_Mirror".</summary>
        public int MirrorStateHash { get { EnsureHashes(); return mirrorStateHash; } }

        private void EnsureHashes()
        {
            if (ReferenceEquals(hashedState, animatorState))
                return;
            hashedState = animatorState;
            string state = animatorState ?? string.Empty;
            stateHash = Animator.StringToHash(state);
            mirrorStateHash = Animator.StringToHash(state + "_Mirror");
        }

        private void OnValidate()
        {
            hashedState = null;
            if (maxRange < minRange)
                maxRange = minRange;
            if (attackType == AttackType.Combo && !HasComboSteps)
                Debug.LogWarning($"[{name}] Combo attack has no steps; it will never be used.", this);
            if (comboSteps != null)
                foreach (AttackDefinition step in comboSteps)
                    if (step != null && step.attackType == AttackType.Combo)
                        Debug.LogWarning($"[{name}] Combo step '{step.name}' is itself a Combo; it runs as a single Melee step.", this);
            if (attackType == AttackType.Charge)
                Debug.LogWarning($"[{name}] Charge is not implemented yet (M4); this attack will never be used.", this);
            if (attackType == AttackType.Projectile && projectilePrefab == null)
                Debug.LogWarning($"[{name}] Projectile attack has no projectile prefab; it will never be used.", this);
        }

        /// <summary>Code-built attack (fallback melee, tests). Not saved as an asset.</summary>
        internal static AttackDefinition Create(
            AttackType type = AttackType.Melee, float telegraph = 0f, float hitTime = 0.7f, float animSpeed = 1f,
            float recovery = 0.5f, float minRange = 0f, float maxRange = 1.6f, float hitRange = 1.9f,
            float hitAngle = 70f, float damage = 10f, float cooldown = 2f, float weight = 1f,
            float lungeDistance = 2f, float lungeDuration = 0f, float clipStart = 0f, float poseTime = -1f,
            AttackDefinition[] combo = null, float slamRadius = 2f, string state = "Attack", bool mirror = false,
            string name = null, float parryWindow = 0.35f, bool leap = false, float stopDistance = -1f,
            EnemyProjectile projectile = null, float projectileSpeed = 7f, StatueMotion motion = StatueMotion.Auto,
            float slowAt = -1f)
        {
            var a = CreateInstance<AttackDefinition>();
            a.name = name ?? type.ToString();
            a.attackType = type;
            a.telegraphTime = telegraph;
            a.hitTime = hitTime;
            a.animSpeed = Mathf.Max(0.05f, animSpeed);
            a.recoveryTime = recovery;
            a.minRange = minRange;
            a.maxRange = Mathf.Max(minRange, maxRange);
            a.hitRange = hitRange;
            a.hitAngle = hitAngle;
            a.damage = damage;
            a.cooldown = cooldown;
            a.weight = weight;
            a.lungeDistance = lungeDistance;
            a.lungeDuration = lungeDuration;
            a.clipStartTime = clipStart;
            a.telegraphPoseTime = poseTime;
            a.comboSteps = combo ?? new AttackDefinition[0];
            a.slamRadius = slamRadius;
            a.animatorState = state;
            a.mirror = mirror;
            a.parryWindow = parryWindow;
            a.leap = leap;
            a.lungeStopDistance = stopDistance;
            a.projectilePrefab = projectile;
            a.projectileSpeed = projectileSpeed;
            a.statueMotion = motion;
            a.slowMotionAt = slowAt;
            return a;
        }
    }
}
