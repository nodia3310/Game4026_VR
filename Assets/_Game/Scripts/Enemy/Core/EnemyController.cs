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

        // Hit stop (slow motion): real seconds elapsed/total, the slow speed, and the time it removed from the AI clock.
        private float hitStopElapsed, hitStopLength, hitStopScale, clockOffset, localDeltaTime, timeScale = 1f;
        private float baseMoveSpeed = -1f, baseAngularSpeed = -1f;

        // Waiting for an attack slot: circling the player.
        private bool circling, rotationBeforeCircling = true;
        private float circleSign = 1f, nextCircleFlip;

        /// <summary>(controller, previous, next)</summary>
        public event Action<EnemyController, EnemyStateId, EnemyStateId> StateChanged;
        /// <summary>Raised once after death + despawnDuration (also after <see cref="ForceClear"/>).</summary>
        public event Action<EnemyController> Despawned;
        /// <summary>Raised when the player kills this enemy (not for <see cref="ForceClear"/>).</summary>
        public event Action<EnemyController> Killed;
        /// <summary>The player's weapon deflected an attack (SFX, sparks, haptics). (controller, step)</summary>
        public event Action<EnemyController, AttackDefinition> Parried;
        /// <summary>Hit stop started (true) or ended (false). Animation drivers follow <see cref="TimeScale"/> meanwhile.</summary>
        public event Action<EnemyController, bool> HitStopChanged;

        public EnemyStateId CurrentState => machine != null ? machine.CurrentId : EnemyStateId.None;
        /// <summary>True when this life ended through <see cref="ForceClear"/> instead of a kill.</summary>
        public bool WasForceCleared { get; private set; }
        public EnemyHealth Health => health;
        /// <summary>Line-of-sight origin (also the default projectile muzzle).</summary>
        public Transform Eye => eye != null ? eye : transform;
        public EnemyAttack Attack => attack;
        /// <summary>True while slowed by <see cref="HitStop"/>.</summary>
        public bool IsHitStopped => hitStopElapsed < hitStopLength;
        /// <summary>This frame's AI delta time: slowed during a hit stop. Animation drivers use it instead of Time.deltaTime.</summary>
        public float DeltaTime => localDeltaTime;
        /// <summary>Current speed of this enemy's clock: 1 normally, the hit stop's slow speed while it lasts (eases back to 1).</summary>
        public float TimeScale => timeScale;
        /// <summary>The AI clock (Time.time minus all time removed by hit stops).</summary>
        public float LocalTime => currentTime;

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
            if (IsHitStopped)
                EndHitStop();
            AttackTokens.Release(this);
            EndCircling();
        }

        private void Update() => Step(Time.time, Time.deltaTime);

        /// <summary>
        /// One real frame. During a hit stop the AI clock runs slowed (<see cref="HitStopCurve"/>), so every AI timer
        /// (attack timeline, stagger, cooldowns) shifts by the time the slow motion removed.
        /// </summary>
        internal void Step(float realTime, float realDeltaTime)
        {
            float dt = realDeltaTime;
            if (IsHitStopped)
            {
                float from = hitStopElapsed;
                hitStopElapsed = Mathf.Min(hitStopLength, hitStopElapsed + dt);
                float local = HitStopCurve.LocalTime(from, hitStopElapsed, hitStopLength, hitStopScale);
                float real = hitStopElapsed - from;
                clockOffset += real - local;
                dt = local + (dt - real);
                attack.ClockOffset = clockOffset;
                if (IsHitStopped)
                    SetTimeScale(HitStopCurve.ScaleAt(hitStopElapsed, hitStopLength, hitStopScale));
                else
                    EndHitStop();
            }
            localDeltaTime = dt;
            Tick(realTime - clockOffset, dt);
        }

        /// <summary>
        /// Slow motion for <paramref name="seconds"/> of real time ("卡肉"): AI, attack timeline, NavMesh movement and
        /// animation drop to <paramref name="timeScale"/> (0 = full freeze), hold, then ease back to normal speed.
        /// A new hit stop during one restarts it if it is longer, and keeps the slower speed.
        /// </summary>
        public void HitStop(float seconds, float timeScale = 0f)
        {
            if (!(seconds > 0f))
                return;
            timeScale = Mathf.Clamp01(timeScale);
            bool wasSlowed = IsHitStopped;
            if (wasSlowed)
            {
                hitStopScale = Mathf.Min(hitStopScale, timeScale);
                if (seconds > hitStopLength - hitStopElapsed)
                {
                    hitStopLength = seconds;
                    hitStopElapsed = 0f;
                }
            }
            else
            {
                hitStopScale = timeScale;
                hitStopLength = seconds;
                hitStopElapsed = 0f;
            }
            SetTimeScale(HitStopCurve.ScaleAt(hitStopElapsed, hitStopLength, hitStopScale));
            if (!wasSlowed)
                HitStopChanged?.Invoke(this, true);
        }

        private void EndHitStop()
        {
            hitStopElapsed = hitStopLength = 0f;
            SetTimeScale(1f);
            HitStopChanged?.Invoke(this, false);
        }

        /// <summary>Applies this enemy's clock speed to the NavMeshAgent and the optional Animator.</summary>
        private void SetTimeScale(float scale)
        {
            if (Mathf.Approximately(scale, timeScale))
                return;
            float previous = timeScale;
            timeScale = scale;
            if (agent != null)
            {
                if (baseMoveSpeed < 0f) baseMoveSpeed = agent.speed;
                if (baseAngularSpeed < 0f) baseAngularSpeed = agent.angularSpeed;
                agent.speed = baseMoveSpeed * scale;
                agent.angularSpeed = baseAngularSpeed * scale;
                if (agent.enabled && agent.isOnNavMesh)
                    agent.velocity *= previous > 1e-4f ? scale / previous : 0f;
            }
            if (animator != null)
                animator.speed = scale;
        }

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
            AttackTokens.Release(this);
            EndCircling();
            circleSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            if (IsHitStopped)
                EndHitStop();
            currentTime = Time.time - clockOffset;
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
            if (previous == EnemyStateId.Attack)
                AttackTokens.Release(this); // someone else's turn
            if (previous == EnemyStateId.Chase)
                EndCircling();
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
            baseMoveSpeed = speed;
            if (agent != null)
                agent.speed = speed * timeScale;
        }

        public void StopMoving()
        {
            EndCircling();
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
            if (circling)
            {
                EndCircling();
                SetMoveSpeed(data.ChaseSpeed);
            }
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
            if (MustWaitForTurn)
                return false;
            return attack.TrySelect(data, distanceToTarget, currentTime);
        }

        /// <summary>True while the player is looking roughly toward this enemy (see <see cref="AttackTokens.ViewHalfAngle"/>).</summary>
        public bool IsInPlayerView =>
            target == null || AttackTokens.InView(transform.position, target.Head.position, target.Head.forward, AttackTokens.ViewHalfAngle);

        public bool MustWaitForTurn =>
            AttackTokens.Enabled && !AttackTokens.IsHolder(this) &&
            (!AttackTokens.HasRoom(this, data.AttackSlotCost) || !IsInPlayerView);

        public float WaitDistance => data.WaitDistance;

        /// <summary>Walks around the player at the wait distance, facing them; out of view, it walks toward their front.</summary>
        public void CircleTarget(float deltaTime)
        {
            if (target == null)
                return;
            if (!circling)
            {
                circling = true;
                SetMoveSpeed(data.MoveSpeed);
                if (agent != null)
                {
                    rotationBeforeCircling = agent.updateRotation;
                    agent.updateRotation = false; // strafe: keep facing the player
                }
                nextCircleFlip = currentTime + UnityEngine.Random.Range(3f, 6f);
                nextRepathTime = 0f;
            }
            FaceTarget(deltaTime);
            if (currentTime >= nextCircleFlip)
            {
                circleSign = -circleSign;
                nextCircleFlip = currentTime + UnityEngine.Random.Range(3f, 6f);
            }
            if (currentTime < nextRepathTime)
                return;
            nextRepathTime = currentTime + repathInterval;

            Vector3 center = target.Body.position;
            Vector3 from = transform.position - center;
            from.y = 0f;
            if (from.sqrMagnitude < 1e-4f)
                from = -transform.forward;
            float direction = circleSign;
            float view = AttackTokens.AngleFromView(transform.position, target.Head.position, target.Head.forward);
            if (Mathf.Abs(view) > AttackTokens.ViewHalfAngle)
                direction = view > 0f ? -1f : 1f; // come round into the player's view
            Vector3 point = center + Quaternion.AngleAxis(direction * 35f, Vector3.up) * (from.normalized * data.WaitDistance);
            SetDestination(point);
        }

        private void EndCircling()
        {
            if (!circling)
                return;
            circling = false;
            if (agent != null)
                agent.updateRotation = rotationBeforeCircling;
        }

        /// <summary>
        /// Starts <paramref name="move"/> now (a reaction, e.g. raising a guard after being hit). Only from Idle, Patrol
        /// or Chase: never interrupts an attack, a stagger, spawn or death. Its cooldown still applies if it is in the data.
        /// </summary>
        public bool ForceAttack(AttackDefinition move)
        {
            if (move == null || !move.IsSupported)
                return false;
            EnemyStateId s = CurrentState;
            if (s != EnemyStateId.Idle && s != EnemyStateId.Patrol && s != EnemyStateId.Chase)
                return false;
            attack.Queue(move);
            machine.ChangeState(EnemyStateId.Attack);
            return true;
        }

        public float AttackHoldRange => attack.HoldRange(data);
        public void BeginAttack()
        {
            AttackTokens.TryTake(this, data.AttackSlotCost); // reactions (ForceAttack) go ahead even without a slot
            attack.Begin(data, currentTime);
        }
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
