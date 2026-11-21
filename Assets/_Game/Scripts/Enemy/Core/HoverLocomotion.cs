using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Procedural flight for winged enemies whose rig has no walk cycle (the birdman kneels in the air). Moves only the
    /// pivot child's local pose; the root stays with the NavMeshAgent. Hovers with a slow bob, leans into movement and
    /// banks into turns; per attack step: leans back on the wind-up and into the swing, rises high during a Lunge's
    /// telegraph and dives down onto the player, settles back behind a Guard. Descends from the sky on spawn, reels
    /// when staggered, drops to the floor and sinks on death. Runs on the enemy clock (slows in hit stops). No allocation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class HoverLocomotion : MonoBehaviour
    {
        [Tooltip("The child to move (holds the model). Its rest pose is the pose on the ground.")]
        [SerializeField] private Transform pivot;
        [Tooltip("Height of the body's centre above the pivot (lean pivot).")]
        [SerializeField, Min(0f)] private float centerHeight = 0.8f;

        [Header("Hover")]
        [SerializeField, Min(0f)] private float hoverHeight = 0.45f;
        [SerializeField, Min(0f)] private float bobHeight = 0.06f;
        [Tooltip("Bobs per second.")]
        [SerializeField, Min(0f)] private float bobRate = 0.5f;
        [SerializeField, Range(0f, 40f)] private float moveLean = 12f;
        [Tooltip("Roll into turns, degrees per 100°/s of turning.")]
        [SerializeField, Range(0f, 40f)] private float bankAngle = 14f;
        [Tooltip("How fast the pose follows its target (1/s).")]
        [SerializeField, Min(0.1f)] private float smoothing = 8f;

        [Header("Attacks")]
        [SerializeField, Range(0f, 40f)] private float windUpLean = 10f;
        [SerializeField, Range(0f, 40f)] private float swingLean = 14f;
        [Tooltip("Lunge (dive): height gained during the telegraph when the move's Lunge Arc Height is 0.")]
        [SerializeField, Min(0f)] private float diveRise = 1.4f;
        [SerializeField, Range(0f, 60f)] private float diveLean = 30f;
        [SerializeField, Range(0f, 30f)] private float guardLean = 8f;
        [SerializeField, Min(0f)] private float guardDip = 0.12f;
        [SerializeField, Min(0f)] private float telegraphShake = 0.012f;

        [Header("Spawn / stagger / death")]
        [SerializeField, Min(0f)] private float descendHeight = 3f;
        [SerializeField, Range(0f, 45f)] private float wobbleAngle = 14f;
        [SerializeField, Min(0f)] private float staggerDip = 0.25f;
        [SerializeField, Range(0f, 120f)] private float deathTopple = 70f;
        [SerializeField, Min(0f)] private float sinkDepth = 1.4f;

        private EnemyController controller;
        private EnemyAttack attack;
        private NavMeshAgent agent;
        private Vector3 restPosition;
        private Quaternion restRotation;

        private float stateTime, bobTime, stepTime;
        private bool stepActive;
        private AttackStepTiming step;
        private float height, pitch, roll, lastYaw;
        private bool hasPose;

        public Transform Pivot => pivot;
        /// <summary>Current height of the pivot above its rest pose.</summary>
        public float Height => height;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            attack = GetComponent<EnemyAttack>();
            agent = GetComponent<NavMeshAgent>();
            if (pivot == null && transform.childCount > 0)
                pivot = transform.GetChild(0);
            if (pivot != null)
            {
                restPosition = pivot.localPosition;
                restRotation = pivot.localRotation;
            }
        }

        private void OnEnable()
        {
            controller.StateChanged += HandleStateChanged;
            if (attack != null)
                attack.StepStarted += HandleStepStarted;
            lastYaw = transform.eulerAngles.y;
        }

        private void OnDisable()
        {
            controller.StateChanged -= HandleStateChanged;
            if (attack != null)
                attack.StepStarted -= HandleStepStarted;
        }

        private void HandleStateChanged(EnemyController _, EnemyStateId previous, EnemyStateId next)
        {
            stateTime = 0f;
            if (next != EnemyStateId.Attack)
                stepActive = false;
            if (next == EnemyStateId.Spawn)
            {
                height = hoverHeight + descendHeight;
                pitch = roll = 0f;
                hasPose = true;
            }
        }

        private void HandleStepStarted(EnemyAttack _, AttackStepTiming timing)
        {
            step = timing;
            stepTime = 0f;
            stepActive = true;
        }

        private void LateUpdate()
        {
            if (pivot == null)
                return;
            float dt = controller.DeltaTime; // slowed during a hit stop
            stateTime += dt;
            bobTime += dt;

            float yaw = transform.eulerAngles.y;
            float yawRate = dt > 1e-5f ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
            lastYaw = yaw;

            float bob = bobHeight * Mathf.Sin(bobTime * bobRate * 2f * Mathf.PI);
            float targetHeight = hoverHeight + bob;
            float targetPitch = 0f;
            float targetRoll = 0f;
            float follow = smoothing;
            Vector3 shake = Vector3.zero;
            EnemyData data = controller.Data;

            switch (controller.CurrentState)
            {
                case EnemyStateId.Spawn:
                    float descend = data != null && data.SpawnDuration > 0f ? Mathf.Clamp01(stateTime / data.SpawnDuration) : 1f;
                    targetHeight = hoverHeight + descendHeight * (1f - Smooth(descend));
                    targetPitch = -12f * (1f - descend); // flare the body to land
                    follow = 30f;
                    break;

                case EnemyStateId.Dead:
                    float fall = Smooth(Mathf.Clamp01(stateTime / 0.45f));
                    float sinkTime = data != null ? Mathf.Max(0.1f, data.DespawnDuration - 0.8f) : 1f;
                    float sink = Smooth(Mathf.Clamp01((stateTime - 0.8f) / sinkTime));
                    targetHeight = hoverHeight * (1f - fall) - sinkDepth * sink;
                    targetPitch = deathTopple * fall;
                    follow = 30f;
                    break;

                case EnemyStateId.Hit:
                    float decay = Mathf.Exp(-4f * stateTime);
                    targetHeight = hoverHeight - staggerDip * Mathf.Min(1f, stateTime * 8f) * Mathf.Exp(-1.5f * stateTime);
                    targetRoll = wobbleAngle * decay * Mathf.Sin(stateTime * 12f);
                    targetPitch = -0.6f * wobbleAngle * decay;
                    follow = 20f;
                    break;

                case EnemyStateId.Attack:
                    if (stepActive)
                    {
                        stepTime += dt;
                        AttackPose(bob, ref targetHeight, ref targetPitch, ref follow, ref shake);
                    }
                    break;

                default:
                    float speed = agent != null && agent.enabled ? new Vector2(agent.velocity.x, agent.velocity.z).magnitude : 0f;
                    targetPitch = moveLean * Mathf.Clamp01(speed / 2.5f);
                    targetRoll = -bankAngle * Mathf.Clamp(yawRate / 100f, -1f, 1f);
                    break;
            }

            if (!hasPose)
            {
                height = targetHeight;
                hasPose = true;
            }
            float k = 1f - Mathf.Exp(-follow * dt);
            height = Mathf.Lerp(height, targetHeight, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            roll = Mathf.Lerp(roll, targetRoll, k);
            Apply(shake);
        }

        private void AttackPose(float bob, ref float targetHeight, ref float targetPitch, ref float follow, ref Vector3 shake)
        {
            float t = stepTime;
            float windUpEnd = Mathf.Max(0f, step.HoldStart);
            float holdEnd = windUpEnd + Mathf.Max(0f, step.HoldDuration);
            float hit = Mathf.Max(holdEnd, step.HitDelay);
            float windUp = windUpEnd > 0f ? Smooth(Mathf.Clamp01(t / windUpEnd)) : 1f;
            bool beforeHit = t < hit;
            AttackDefinition def = step.Step;

            if (def.Type == AttackType.Guard)
            {
                targetHeight = hoverHeight + bob * 0.3f - guardDip * windUp;
                targetPitch = -guardLean * windUp; // settle back behind the wings
                return;
            }

            if (t >= windUpEnd && t < holdEnd) // trembling while holding the wind-up pose
                shake = Shake(1f);

            if (def.MovesForward) // dive
            {
                float rise = def.LungeArcHeight > 0f ? def.LungeArcHeight : diveRise;
                follow = 30f;
                if (t < holdEnd)
                {
                    float up = holdEnd > 0f ? Smooth(Mathf.Clamp01(t / holdEnd)) : 1f;
                    targetHeight = hoverHeight + rise * up;
                    targetPitch = -windUpLean * up;
                }
                else if (beforeHit)
                {
                    float u = hit > holdEnd ? Mathf.Clamp01((t - holdEnd) / (hit - holdEnd)) : 1f;
                    targetHeight = hoverHeight + rise * (1f - u) * (1f - u);
                    targetPitch = Mathf.Lerp(-windUpLean, diveLean, Smooth(Mathf.Min(1f, u * 2f)));
                }
                else
                {
                    targetPitch = swingLean * Mathf.Exp(-3f * (t - hit));
                    follow = smoothing;
                }
                return;
            }

            // Melee: lean back on the wind-up, throw the body into the swing, settle.
            if (t < holdEnd)
                targetPitch = -windUpLean * windUp;
            else if (beforeHit)
                targetPitch = swingLean;
            else
                targetPitch = swingLean * Mathf.Exp(-3f * (t - hit));
            follow = beforeHit && t >= holdEnd ? 25f : smoothing;
        }

        private Vector3 Shake(float strength)
        {
            if (strength <= 0f || telegraphShake <= 0f)
                return Vector3.zero;
            float s = Time.time * 45f;
            return new Vector3(Mathf.Sin(s * 1.3f), 0f, Mathf.Sin(s * 1.7f + 1f)) * (telegraphShake * strength);
        }

        private void Apply(Vector3 shake)
        {
            // Lean and bank about the body's centre, then lift. All in the root's local space.
            Vector3 center = Vector3.up * centerHeight;
            Quaternion lean = Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.AngleAxis(-roll, Vector3.forward);
            Vector3 position = lean * -center + center;
            pivot.localPosition = restPosition + position + Vector3.up * height + shake;
            pivot.localRotation = lean * restRotation;
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
