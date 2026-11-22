using System;
using Game4026.Contracts;
using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Attack host (spec 4.5): picks moves from <see cref="EnemyData.Attacks"/> with an <see cref="AttackSelector"/>
    /// and plays them with an <see cref="AttackRunner"/>. Hit checks are distance + angle (no physics colliders);
    /// a hit calls <see cref="IPlayerDamageReceiver.ApplyPlayerDamage"/> once per step.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttack : MonoBehaviour, IAttackHost
    {
        [Tooltip("A lunge stops this far in front of the player instead of running through them.")]
        [SerializeField, Min(0f)] private float lungeStopDistance = 0.9f;
        [Tooltip("Where projectiles spawn (e.g. the eyes). Defaults to the controller's eye.")]
        [SerializeField] private Transform muzzle;
        [Tooltip("Base attack speed of this enemy type, multiplied with the runtime SpeedMultiplier (boss phases). " +
                 "0.7 = every telegraph, swing, recovery and cooldown takes 1/0.7 as long; the parry window (seconds) grows too.")]
        [SerializeField, Range(0.3f, 2f)] private float baseAttackSpeed = 1f;

        private readonly AttackSelector selector = new AttackSelector();
        private readonly AttackRunner runner = new AttackRunner();

        private EnemyController controller;
        private NavMeshAgent agent;
        private EnemyData data;
        private PlayerTarget target;
        private float speedMultiplier = 1f;
        private float recoveryOverride = -1f;
        private int pendingIndex = -1;
        private AttackDefinition queued;
        private bool guardEnded;
        private Vector3 lungeDirection;
        private float lungeTotal;

        /// <summary>A move started (once per attack, also for combos).</summary>
        public event Action<EnemyAttack, AttackDefinition> AttackStarted;
        /// <summary>A step's telegraph started: warning SFX/VFX (glow, growl) so the player can react.</summary>
        public event Action<EnemyAttack, AttackStepTiming> AttackTelegraph;
        /// <summary>A step started; <see cref="EnemyAnimator"/> plays its clip from this.</summary>
        public event Action<EnemyAttack, AttackStepTiming> StepStarted;
        /// <summary>A step's hit check ran; bool is true when the player was hit (for Projectile: the bolt was fired).</summary>
        public event Action<EnemyAttack, AttackDefinition, bool> AttackResolved;
        /// <summary>A Projectile step fired a bolt.</summary>
        public event Action<EnemyAttack, EnemyProjectile> ProjectileFired;
        /// <summary>The whole move finished normally.</summary>
        public event Action<EnemyAttack, AttackDefinition> AttackFinished;
        /// <summary>Interrupted (stagger or death) before it finished.</summary>
        public event Action<EnemyAttack, AttackDefinition> AttackCancelled;
        /// <summary>A step's parry window opened: the strike lands in (HitDelay − ParryOpen) seconds.</summary>
        public event Action<EnemyAttack, AttackStepTiming> ParryWindowOpened;
        /// <summary>The player's weapon deflected this step; the whole move was cancelled. (attack, move, step)</summary>
        public event Action<EnemyAttack, AttackDefinition, AttackDefinition> Parried;

        /// <summary>True while the current step can be deflected by the player's weapon.</summary>
        public bool IsParryWindowOpen => runner.IsParryWindowOpen;
        /// <summary>Frozen (hit stop) time removed from the owner's AI clock; set by <see cref="EnemyController"/>.</summary>
        internal float ClockOffset { get; set; }
        /// <summary>The owner's AI clock: use this, not Time.time, when calling <see cref="TryParry"/> from a hit.</summary>
        public float Now => Time.time - ClockOffset;

        /// <summary>True while a Guard step holds its shield (from the step start until its end).</summary>
        public bool IsGuarding => !guardEnded && runner.IsBeforeHit && runner.Step != null && runner.Step.Type == AttackType.Guard;
        /// <summary>Seconds since the current step started.</summary>
        public float StepElapsed => runner.StepElapsed;

        /// <summary>The move being played (null when idle).</summary>
        public AttackDefinition Current => runner.Attack;
        /// <summary>The combo step being played (the move itself when not a combo).</summary>
        public AttackDefinition CurrentStep => runner.Step;
        public int CurrentStepIndex => runner.StepIndex;
        public bool IsAttacking => runner.IsRunning;
        public AttackSelector Selector => selector;
        public Transform Muzzle => muzzle != null ? muzzle : controller != null ? controller.Eye : transform;

        /// <summary>
        /// Runtime attack speed (e.g. the boss's last phase). 2 = telegraphs, swings, recovery and cooldowns take half as long.
        /// Reset to 1 by <see cref="ResetState"/>.
        /// </summary>
        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Max(0.1f, value);
        }

        /// <summary>Base speed of this enemy type (Inspector).</summary>
        public float BaseAttackSpeed
        {
            get => baseAttackSpeed;
            set => baseAttackSpeed = Mathf.Clamp(value, 0.1f, 4f);
        }

        /// <summary>Speed the move timeline actually runs at: <see cref="BaseAttackSpeed"/> × <see cref="SpeedMultiplier"/>.</summary>
        public float EffectiveSpeed => Mathf.Max(0.1f, baseAttackSpeed * speedMultiplier);

        /// <summary>
        /// Recovery wanted by the animation for the current step (set by <see cref="EnemyAnimator"/> on
        /// <see cref="StepStarted"/> so the clip can finish). The longer of this and the move's recovery is used.
        /// Negative = none. Cleared at each step start.
        /// </summary>
        public float RecoveryOverride
        {
            get => recoveryOverride;
            set => recoveryOverride = float.IsNaN(value) ? -1f : value;
        }

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>Chooses a move for this distance; true when one is ready. <see cref="Begin"/> starts it.</summary>
        public bool TrySelect(EnemyData enemyData, float distance, float now)
        {
            if (runner.IsRunning || enemyData == null)
                return false;
            pendingIndex = selector.Select(enemyData.Attacks, distance, now, UnityEngine.Random.value);
            if (pendingIndex >= 0)
                data = enemyData;
            return pendingIndex >= 0;
        }

        /// <summary>Starts the move chosen by <see cref="TrySelect"/>, or forces one (tests, scripted bosses).</summary>
        public bool Begin(EnemyData enemyData, float now, AttackDefinition forced = null)
        {
            data = enemyData;
            AttackDefinition[] attacks = enemyData != null ? enemyData.Attacks : null;
            AttackDefinition move = forced != null ? forced : queued;
            queued = null;
            int index = -1;
            if (move == null && attacks != null && pendingIndex >= 0 && pendingIndex < attacks.Length)
            {
                index = pendingIndex;
                move = attacks[index];
            }
            pendingIndex = -1;
            if (move == null)
                return false;

            if (index < 0 && attacks != null)
                index = Array.IndexOf(attacks, move);
            selector.MarkStarted(index, move, now, EffectiveSpeed);
            AttackStarted?.Invoke(this, move);
            runner.Start(move, EffectiveSpeed, this);
            return true;
        }

        /// <summary>One frame of the current move. Returns false once it has finished.</summary>
        public bool Tick(float now, float deltaTime, PlayerTarget player)
        {
            target = player;
            if (!runner.IsRunning)
                return false;
            AttackDefinition move = runner.Attack;
            if (runner.Tick(deltaTime, this))
                return true;
            selector.MarkEnded(now, data != null ? data.GlobalAttackCooldown : 0f, EffectiveSpeed);
            AttackFinished?.Invoke(this, move);
            return false;
        }

        /// <summary>The next <see cref="Begin"/> without a forced move plays this one (reactions, e.g. a guard after a hit).</summary>
        public void Queue(AttackDefinition move) => queued = move;

        /// <summary>Ends a Guard step now so the move goes on to its next step (the counter). False when not guarding.</summary>
        public bool EndGuard()
        {
            if (!IsGuarding)
                return false;
            guardEnded = true; // the shield drops now; the step resolves on the next tick
            runner.SkipToHit();
            return true;
        }

        public void Cancel(float now)
        {
            pendingIndex = -1;
            queued = null;
            if (!runner.IsRunning)
                return;
            AttackDefinition move = runner.Attack;
            runner.Stop();
            selector.MarkEnded(now, data != null ? data.GlobalAttackCooldown : 0f, EffectiveSpeed);
            AttackCancelled?.Invoke(this, move);
        }

        /// <summary>
        /// The player's weapon hit this enemy's weapon. Inside the parry window the whole move (rest of a combo too)
        /// is cancelled without damaging the player and <see cref="Parried"/> is raised. Returns false otherwise.
        /// </summary>
        public bool TryParry(float now)
        {
            if (!runner.IsParryWindowOpen)
                return false;
            AttackDefinition move = runner.Attack;
            AttackDefinition step = runner.Step;
            pendingIndex = -1;
            runner.Stop();
            selector.MarkEnded(now, data != null ? data.GlobalAttackCooldown : 0f, EffectiveSpeed);
            Parried?.Invoke(this, move, step);
            return true;
        }

        public float HoldRange(EnemyData enemyData) => enemyData != null ? AttackSelector.HoldRange(enemyData.Attacks) : 0f;

        public void ResetState()
        {
            runner.Stop();
            selector.Reset();
            pendingIndex = -1;
            queued = null;
            speedMultiplier = 1f;
            recoveryOverride = -1f;
        }

        // ---- IAttackHost ----

        void IAttackHost.FaceTarget(float deltaTime)
        {
            if (controller != null)
                controller.FaceTarget(deltaTime);
        }

        void IAttackHost.BeginLunge(AttackDefinition step)
        {
            lungeDirection = transform.forward;
            lungeTotal = step.LungeDistance;
            if (target == null)
                return;
            Vector3 toPlayer = target.Body.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > 1e-3f)
            {
                lungeDirection = toPlayer / distance;
                transform.rotation = Quaternion.LookRotation(lungeDirection);
            }
            float stop = step.LungeStopDistance >= 0f ? step.LungeStopDistance : lungeStopDistance;
            lungeTotal = Mathf.Clamp(distance - stop, 0f, step.LungeDistance);
        }

        void IAttackHost.MoveLunge(float fraction)
        {
            Vector3 delta = lungeDirection * (lungeTotal * fraction);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Move(delta); // stays on the NavMesh, so it cannot go through walls
            else
                transform.position += delta;
        }

        bool IAttackHost.ResolveHit(AttackDefinition step)
        {
            if (step.Type == AttackType.Guard)
            {
                AttackResolved?.Invoke(this, step, false); // the guard simply ends
                return false;
            }
            if (step.Type == AttackType.Projectile)
            {
                bool fired = Fire(step) != null;
                AttackResolved?.Invoke(this, step, fired);
                return fired;
            }
            bool hit = target != null && IsInHitArea(step, transform, target.Body.position);
            if (hit)
                target.Damageable?.ApplyPlayerDamage(step.Damage);
            AttackResolved?.Invoke(this, step, hit);
            return hit;
        }

        /// <summary>Fires the step's projectile from the muzzle at the player's head (straight ahead without a player).</summary>
        public EnemyProjectile Fire(AttackDefinition step)
        {
            EnemyProjectile bolt = EnemyProjectilePool.Get(step.ProjectilePrefab);
            if (bolt == null)
                return null; // cap reached
            Vector3 from = Muzzle.position;
            Vector3 direction = target != null ? target.Head.position - from : transform.forward;
            bolt.Launch(controller, from, direction, step.ProjectileSpeed * speedMultiplier, step.Damage);
            ProjectileFired?.Invoke(this, bolt);
            return bolt;
        }

        float IAttackHost.RecoveryOverride => recoveryOverride;

        void IAttackHost.OnStepStarted(in AttackStepTiming timing)
        {
            recoveryOverride = -1f;
            guardEnded = false;
            StepStarted?.Invoke(this, timing);
            if (timing.HoldDuration > 0f && timing.Step.Type != AttackType.Guard) // a guard is not a warning
                AttackTelegraph?.Invoke(this, timing);
        }

        void IAttackHost.OnParryWindowOpened(in AttackStepTiming timing) => ParryWindowOpened?.Invoke(this, timing);

        void IAttackHost.OnSlowMotion(AttackDefinition step)
        {
            if (controller != null)
                controller.HitStop(step.SlowMotionDuration, step.SlowMotionScale); // the whole enemy, eased back
        }

        /// <summary>AreaSlam: anywhere within slamRadius (any height). Others: the hit cone in front.</summary>
        public static bool IsInHitArea(AttackDefinition step, Transform enemy, Vector3 playerBody)
        {
            if (step.Type == AttackType.AreaSlam)
                return EnemyGeometry.HorizontalDistance(enemy.position, playerBody) <= step.SlamRadius;
            return EnemyGeometry.IsInAttackCone(enemy.position, enemy.forward, playerBody, step.HitRange, step.HitAngle);
        }
    }
}
