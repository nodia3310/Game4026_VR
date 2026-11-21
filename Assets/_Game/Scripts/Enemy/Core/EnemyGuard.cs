using System;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// A shield the enemy raises with a Guard step (e.g. the birdman folding its wings). While it is up, hits from the
    /// front are blocked: no damage, a Blocked reaction instead. A hit at or above <see cref="GuardBreakDamage"/>
    /// smashes through: the enemy staggers long and takes extra damage. After enough blocked hits the guard ends early
    /// and the move goes on to its next step (the counter-attack).
    /// It can also raise the guard by itself right after being hit (<see cref="ReactChance"/>), so a flurry gets
    /// blocked after the first blow. <see cref="EnemyHealth"/> asks it first for every hit; no contract change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyGuard : MonoBehaviour
    {
        [Tooltip("Guard move to start as a reaction (usually a Combo: Guard step, then a counter). Put it in the EnemyData attacks too to also use it by distance.")]
        [SerializeField] private AttackDefinition guardMove;
        [Tooltip("Seconds into the Guard step before the shield is up (the wings closing).")]
        [SerializeField, Min(0f)] private float raiseTime = 0.15f;
        [Tooltip("Horizontal arc in front that the shield covers (degrees). Hits from outside it go through.")]
        [SerializeField, Range(0f, 360f)] private float coverAngle = 220f;

        [Header("Break")]
        [Tooltip("One hit with at least this much damage breaks the guard instead of being blocked.")]
        [SerializeField, Min(0f)] private float guardBreakDamage = 30f;
        [SerializeField, Min(0f)] private float breakStagger = 1.8f;
        [Tooltip("Damage multiplier while staggered after a guard break.")]
        [SerializeField, Min(1f)] private float breakDamageMultiplier = 1.5f;

        [Header("Counter")]
        [Tooltip("After this many blocked hits the guard ends at once and the counter-attack starts. 0 = only when the guard runs out.")]
        [SerializeField, Min(0)] private int counterAfterBlocks = 2;

        [Header("Reaction")]
        [Tooltip("Chance to raise the guard right after taking a hit that did not stagger.")]
        [SerializeField, Range(0f, 1f)] private float reactChance = 0.45f;
        [SerializeField, Min(0f)] private float reactCooldown = 5f;

        private EnemyController controller;
        private EnemyAttack attack;
        private EnemyHealth health;
        private int blocks;
        private float nextReactTime;

        /// <summary>A hit was blocked. Vector3 = hit point.</summary>
        public event Action<EnemyGuard, Vector3> Blocked;
        /// <summary>A heavy hit broke the guard. Vector3 = hit point.</summary>
        public event Action<EnemyGuard, Vector3> Broken;

        public AttackDefinition GuardMove => guardMove;
        public float GuardBreakDamage => guardBreakDamage;
        public float ReactChance => reactChance;
        /// <summary>True while the shield is up (a Guard step is playing and past its raise time).</summary>
        public bool IsGuarding => attack != null && attack.IsGuarding && attack.StepElapsed >= raiseTime;
        public int BlockCount => blocks;

        /// <summary>Code setup (tests).</summary>
        internal void Configure(AttackDefinition move, float reaction = 0f, int counterAfter = 2, float breakDamage = 30f, float raise = 0f)
        {
            guardMove = move;
            reactChance = reaction;
            counterAfterBlocks = counterAfter;
            guardBreakDamage = breakDamage;
            raiseTime = raise;
        }

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            attack = GetComponent<EnemyAttack>();
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            attack.StepStarted += HandleStepStarted;
            nextReactTime = 0f;
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            attack.StepStarted -= HandleStepStarted;
        }

        private void HandleStepStarted(EnemyAttack _, AttackStepTiming timing)
        {
            if (timing.Step.Type == AttackType.Guard)
                blocks = 0;
        }

        /// <summary>
        /// Asked by <see cref="EnemyHealth"/> before damage applies. True when the shield took the hit (blocked or broken);
        /// false lets the damage through.
        /// </summary>
        public bool TryBlock(float damage, Vector3 hitPoint)
        {
            if (!IsGuarding || !Covers(hitPoint))
                return false;
            if (guardBreakDamage > 0f && damage >= guardBreakDamage)
            {
                Break(hitPoint);
                return true;
            }
            blocks++;
            Blocked?.Invoke(this, hitPoint);
            if (counterAfterBlocks > 0 && blocks >= counterAfterBlocks)
                attack.EndGuard();
            return true;
        }

        /// <summary>True when a hit at this point comes from inside the covered arc in front.</summary>
        public bool Covers(Vector3 hitPoint) => Covers(transform, hitPoint, coverAngle);

        public static bool Covers(Transform enemy, Vector3 hitPoint, float coverAngle)
        {
            Vector3 d = hitPoint - enemy.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f)
                return true;
            return Vector3.Angle(enemy.forward, d) <= coverAngle * 0.5f;
        }

        private void Break(Vector3 hitPoint)
        {
            health.DamageMultiplier = breakDamageMultiplier; // cleared when the stagger ends
            controller.Stagger(breakStagger);                // cancels the guard move and its counter
            Broken?.Invoke(this, hitPoint);
        }

        private void HandleDamaged(EnemyHealth _, EnemyDamageResult result)
        {
            if (result.Killed || result.CausedStagger || guardMove == null)
                return;
            float now = controller.LocalTime;
            if (now < nextReactTime || UnityEngine.Random.value >= reactChance)
                return;
            if (controller.ForceAttack(guardMove))
                nextReactTime = now + reactCooldown;
        }
    }
}
