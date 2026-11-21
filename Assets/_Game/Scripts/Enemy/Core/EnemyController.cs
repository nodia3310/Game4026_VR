using System;
using Game4026.Contracts;
using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Enemy AI host (spec 4.4): runs the state machine, NavMesh movement and throttled perception.
    /// Works without an Animator; when one is assigned, "Speed" (float) and "State" (int) are set if present.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth), typeof(EnemyAttack))]
    public sealed class EnemyController : MonoBehaviour, IEnemyContext
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int StateParam = Animator.StringToHash("State");

        [SerializeField] private EnemyData data;
        [Tooltip("Line-of-sight origin. Defaults to 1.5 m above the pivot.")]
        [SerializeField] private Transform eye;
        [SerializeField] private Transform[] patrolPoints = new Transform[0];

        [Header("Perception")]
        [Tooltip("Layers that block sight. Default only for now (no Environment layer yet).")]
        [SerializeField] private LayerMask sightBlockers = 1;
        [SerializeField, Min(0.05f)] private float perceptionInterval = 0.2f;
        [SerializeField, Min(0f)] private float loseSightSeconds = 5f;

        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
        [SerializeField, Min(0f)] private float repathDistance = 0.5f;
        [SerializeField, Min(0f)] private float turnSpeed = 360f;

        [Header("Lifecycle")]
        [Tooltip("Deactivate the GameObject after the death delay. The M3 pool will take over this.")]
        [SerializeField] private bool deactivateOnDespawn = true;

        [Header("Optional")]
        [SerializeField] private Animator animator;

        private readonly RaycastHit[] sightHits = new RaycastHit[8];

        private NavMeshAgent agent;
        private EnemyHealth health;
        private EnemyAttack attack;
        private EnemyStateMachine machine;

        private PlayerTarget target;
        private float distanceToTarget = float.PositiveInfinity;
        private bool canSee;
        private float lastSeenTime = float.NegativeInfinity;
        private float nextPerceptionTime;
        private float nextRepathTime;
        private float nextAttackSelectTime;
        private float staggerDuration, staggerEndTime;
        private Vector3 lastDestination;
        private int patrolIndex = -1;
        private float currentTime;
        private bool animatorHasSpeed;
        private bool animatorHasState;

        /// <summary>(controller, previous, next)</summary>
        public event Action<EnemyController, EnemyStateId, EnemyStateId> StateChanged;
        /// <summary>Raised once after death + despawnDuration (also after <see cref="ForceClear"/>).</summary>
        public event Action<EnemyController> Despawned;
        /// <summary>Raised when the player kills this enemy (not for <see cref="ForceClear"/>).</summary>
        public event Action<EnemyController> Killed;
        /// <summary>The player's weapon deflected an attack (SFX, sparks, haptics). (controller, step)</summary>
        public event Action<EnemyController, AttackDefinition> Parried;

        public EnemyStateId CurrentState => machine != null ? machine.CurrentId : EnemyStateId.None;
        /// <summary>True when this life ended through <see cref="ForceClear"/> instead of a kill.</summary>
        public bool WasForceCleared { get; private set; }
        public EnemyHealth Health => health;
        public EnemyAttack Attack => attack;

        // ---- Unity ----

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            attack = GetComponent<EnemyAttack>();

            if (data == null)
                data = health.Data;
            if (data == null)
            {
                Debug.LogWarning($"{nameof(EnemyController)} on '{name}' has no {nameof(EnemyData)}; using default values.", this);
                data = ScriptableObject.CreateInstance<EnemyData>();
            }
            if (health.Data != data)
                health.Initialize(data);

            machine = EnemyStateMachine.CreateDefault(this);
            machine.Changed += HandleStateChanged;

            if (animator != null)
            {
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.nameHash == SpeedParam && p.type == AnimatorControllerParameterType.Float) animatorHasSpeed = true;
                    if (p.nameHash == StateParam && p.type == AnimatorControllerParameterType.Int) animatorHasState = true;
                }
            }
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
            attack.Parried += HandleParried;
            Respawn();
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
            attack.Parried -= HandleParried;
        }

        private void Update() => Tick(Time.time, Time.deltaTime);

        /// <summary>
        /// One AI frame. Every time-based rule reads <paramref name="now"/> (never Time.time directly),
        /// so tests can drive frames manually, e.g. to measure per-frame GC allocation.
        /// </summary>
        internal void Tick(float now, float deltaTime)
        {
            currentTime = now;
            UpdateTarget();

            if (now >= nextPerceptionTime)
            {
                nextPerceptionTime = now + perceptionInterval;
                canSee = CheckLineOfSight();
                if (canSee)
                    lastSeenTime = now;
            }

            machine.Tick(deltaTime);
            UpdateAnimator();
        }

        /// <summary>Full reset and restart from Spawn (also used when taken from the pool).</summary>
        public void Respawn()
        {
            health.ResetState();
            attack.ResetState();
            WasForceCleared = false;
            patrolIndex = -1;
            canSee = false;
            lastSeenTime = float.NegativeInfinity;
            nextPerceptionTime = 0f;
            nextRepathTime = 0f;
            nextAttackSelectTime = 0f;
            staggerDuration = 0f;
            staggerEndTime = 0f;
            currentTime = Time.time;
            if (agent != null)
                agent.enabled = true;
            machine.ChangeState(EnemyStateId.Spawn);
        }

        /// <summary>
        /// Encounter clear (e.g. artifact restored): plays the death/despawn delay, then despawns.
        /// Does not count as a kill and does not raise <see cref="EnemyHealth.Died"/> or <see cref="Killed"/>.
        /// </summary>
        public void ForceClear()
        {
            if (CurrentState == EnemyStateId.Dead || CurrentState == EnemyStateId.None)
                return;
            WasForceCleared = true;
            machine.ChangeState(EnemyStateId.Dead);
            // After the switch: leaving Spawn turns invulnerability off, and a cleared enemy must not be "killed" again.
            health.IsInvulnerable = true;
        }

        /// <summary>Sets patrol points for this life (used by encounters for pooled enemies).</summary>
        public void SetPatrolPoints(Transform[] points)
        {
            patrolPoints = points ?? new Transform[0];
            patrolIndex = -1;
        }

        // ---- Perception ----

        private void UpdateTarget()
        {
            target = PlayerTarget.Current;
            distanceToTarget = target != null
                ? EnemyGeometry.HorizontalDistance(transform.position, target.Body.position)
                : float.PositiveInfinity;
        }

        private Vector3 EyePosition => eye != null ? eye.position : transform.position + Vector3.up * 1.5f;

        private bool CheckLineOfSight()
        {
            if (target == null || distanceToTarget > data.LoseTargetRange)
                return false;

            Vector3 from = EyePosition;
            Vector3 delta = target.Head.position - from;
            float distance = delta.magnitude;
            if (distance < 1e-4f)
                return true;

            int count = Physics.RaycastNonAlloc(from, delta / distance, sightHits, distance, sightBlockers, QueryTriggerInteraction.Ignore);
            if (count >= sightHits.Length)
                return false; // too many hits to be sure; treat as blocked

            Transform self = transform;
            Transform player = target.transform;
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (hit.IsChildOf(self) || hit.IsChildOf(player))
                    continue;
                return false;
            }
            return true;
        }

        // ---- Damage events ----

        private void HandleDamaged(EnemyHealth _, EnemyDamageResult result)
        {
            if (!result.Killed && result.CausedStagger)
                Stagger(data.StaggerDuration);
        }

        private void HandleParried(EnemyAttack _, AttackDefinition move, AttackDefinition step)
        {
            if (CurrentState == EnemyStateId.Dead)
                return;
            // Before entering Hit, so the counter window is active from the first frame of the stagger.
            health.DamageMultiplier = data.ParriedDamageMultiplier; // cleared when the stagger ends
            Stagger(data.ParryStaggerDuration);
            Parried?.Invoke(this, step);
        }

        /// <summary>
        /// Enters Hit for <paramref name="seconds"/>. A shorter stagger during a longer one (e.g. a normal hit during the
        /// parry stagger) restarts the Hit reaction but keeps the remaining time.
        /// </summary>
        public void Stagger(float seconds)
        {
            if (CurrentState == EnemyStateId.Dead || CurrentState == EnemyStateId.None)
                return;
            if (CurrentState == EnemyStateId.Hit)
                seconds = Mathf.Max(seconds, staggerEndTime - currentTime);
            staggerDuration = seconds;
            staggerEndTime = currentTime + seconds;
            machine.ChangeState(EnemyStateId.Hit);
        }

        private void HandleDied(EnemyHealth _)
        {
            machine.ChangeState(EnemyStateId.Dead);
            Killed?.Invoke(this);
        }

        private void HandleStateChanged(EnemyStateId previous, EnemyStateId next)
        {
            if (previous == EnemyStateId.Hit && next != EnemyStateId.Hit)
                health.DamageMultiplier = 1f; // parry counter window over
            StateChanged?.Invoke(this, previous, next);
        }

        private void UpdateAnimator()
        {
            if (animator == null)
                return;
            if (animatorHasSpeed)
                animator.SetFloat(SpeedParam, agent.enabled ? agent.velocity.magnitude : 0f);
            if (animatorHasState)
                animator.SetInteger(StateParam, (int)CurrentState);
        }

        // ---- IEnemyContext ----

        public EnemyData Data => data;
        public bool HasTarget => target != null;
        public bool CanSeeTarget => canSee && target != null;
        public float DistanceToTarget => distanceToTarget;
        public float TimeSinceTargetSeen => currentTime - lastSeenTime;
        public float LoseSightSeconds => loseSightSeconds;

        public bool HasPatrolPoints => patrolPoints != null && patrolPoints.Length > 0;

        private bool AgentUsable => agent != null && agent.enabled && agent.isOnNavMesh;

        public bool ReachedDestination =>
            AgentUsable && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;

        public void SetMoveSpeed(float speed)
        {
            if (agent != null)
                agent.speed = speed;
        }

        public void StopMoving()
        {
            if (!AgentUsable)
                return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        public void MoveToNextPatrolPoint()
        {
            if (!HasPatrolPoints)
                return;
            for (int tries = 0; tries < patrolPoints.Length; tries++)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                if (patrolPoints[patrolIndex] != null)
                {
                    SetDestination(patrolPoints[patrolIndex].position);
                    return;
                }
            }
        }

        public void ChaseTarget()
        {
            if (target == null)
                return;
            Vector3 destination = target.Body.position;
            float now = currentTime;
            bool moved = (destination - lastDestination).sqrMagnitude > repathDistance * repathDistance;
            bool idle = AgentUsable && !agent.hasPath && !agent.pathPending;
            if (now >= nextRepathTime || moved || idle)
            {
                SetDestination(destination);
                nextRepathTime = now + repathInterval;
            }
        }

        private void SetDestination(Vector3 destination)
        {
            if (!AgentUsable)
                return;
            agent.isStopped = false;
            agent.SetDestination(destination);
            lastDestination = destination;
        }

        public void FaceTarget(float deltaTime)
        {
            if (target == null)
                return;
            Vector3 direction = target.Body.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(direction), turnSpeed * deltaTime);
        }

        public bool TrySelectAttack()
        {
            // Spec 4.5: the selector runs on the perception interval, not every frame.
            if (target == null || currentTime < nextAttackSelectTime)
                return false;
            nextAttackSelectTime = currentTime + perceptionInterval;
            return attack.TrySelect(data, distanceToTarget, currentTime);
        }

        public float AttackHoldRange => attack.HoldRange(data);
        public void BeginAttack() => attack.Begin(data, currentTime);
        public bool TickAttack(float deltaTime) => attack.Tick(currentTime, deltaTime, target);
        public void CancelAttack() => attack.Cancel(currentTime);

        public float CurrentStaggerDuration => staggerDuration > 0f ? staggerDuration : data.StaggerDuration;

        public void SetInvulnerable(bool value) => health.IsInvulnerable = value;

        public void OnDeath()
        {
            if (agent != null && agent.enabled)
                agent.enabled = false;
        }

        public void Despawn()
        {
            Despawned?.Invoke(this);
            if (deactivateOnDespawn)
                gameObject.SetActive(false);
        }

        public void RequestState(EnemyStateId id) => machine.ChangeState(id);

        // ---- Gizmos ----

        private void OnDrawGizmosSelected()
        {
            EnemyData d = data;
            if (d == null)
                return;
            Vector3 p = transform.position;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(p, d.DetectionRange);
            Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(p, d.LoseTargetRange);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(p, AttackSelector.HoldRange(d.Attacks));
            float reach = 0f;
            foreach (AttackDefinition a in d.Attacks)
                if (a != null && a.IsSupported && a.MaxRange > reach)
                    reach = a.MaxRange;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(p, reach);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(EyePosition, 0.05f);
        }
    }
}
