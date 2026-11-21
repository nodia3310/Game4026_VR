using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Procedural motion for statue enemies without a rig (spec 4.9). Moves only the model child's local pose;
    /// the root stays with the NavMeshAgent. Hops while walking, and plays each attack step's motion
    /// (<see cref="AttackDefinition.Motion"/>) on the step's timeline: tilt back / crouch / spin up during the
    /// wind-up and telegraph hold (with a shake), then hop, roll, slam or recoil until the hit.
    /// Also: rise on spawn, wobble when staggered, topple and sink on death. No allocation per frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class StatueLocomotion : MonoBehaviour
    {
        [Tooltip("The visual child to move. Its pivot should be on the ground under the statue.")]
        [SerializeField] private Transform model;
        [Tooltip("Height of the statue's centre (roll pivot).")]
        [SerializeField, Min(0f)] private float centerHeight = 0.6f;

        [Header("Hopping walk")]
        [SerializeField, Min(0f)] private float hopHeight = 0.15f;
        [Tooltip("Hops per metre travelled.")]
        [SerializeField, Min(0.01f)] private float hopsPerMeter = 1.4f;
        [SerializeField, Range(0f, 0.5f)] private float landSquash = 0.08f;
        [SerializeField, Range(0f, 30f)] private float moveLean = 6f;

        [Header("Attack wind-up")]
        [SerializeField, Range(0f, 60f)] private float tiltBackAngle = 22f;
        [SerializeField, Min(0f)] private float shakeAmount = 0.015f;
        [SerializeField, Min(0f)] private float shakeSpeed = 45f;
        [SerializeField, Range(0f, 0.5f)] private float slamCrouch = 0.15f;
        [SerializeField, Min(0f)] private float slamDefaultArc = 0.8f;
        [SerializeField, Range(0f, 60f)] private float shootRecoil = 18f;
        [Tooltip("Radius used to turn rolled distance into rotation.")]
        [SerializeField, Min(0.05f)] private float rollRadius = 0.45f;

        [Header("Stagger / death / spawn")]
        [SerializeField, Range(0f, 45f)] private float wobbleAngle = 14f;
        [SerializeField, Range(0f, 120f)] private float toppleAngle = 85f;
        [SerializeField, Min(0f)] private float sinkDepth = 1.4f;
        [SerializeField, Min(0f)] private float riseDepth = 1.4f;

        private EnemyController controller;
        private EnemyAttack attack;
        private NavMeshAgent agent;
        private Vector3 restPosition, restScale;
        private Quaternion restRotation;

        private float stateTime;
        private bool stepActive;
        private AttackStepTiming step;
        private float stepTime;
        private float hopPhase;
        private float rollAngle;
        private float afterHitTilt, impactSquash;
        private Vector3 lastRootPosition;

        public Transform Model => model;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            attack = GetComponent<EnemyAttack>();
            agent = GetComponent<NavMeshAgent>();
            if (model == null && transform.childCount > 0)
                model = transform.GetChild(0);
            if (model != null)
            {
                restPosition = model.localPosition;
                restRotation = model.localRotation;
                restScale = model.localScale;
            }
        }

        private void OnEnable()
        {
            controller.StateChanged += HandleStateChanged;
            if (attack != null)
                attack.StepStarted += HandleStepStarted;
            lastRootPosition = transform.position;
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
                rollAngle = 0f;
                hopPhase = 0f;
                afterHitTilt = 0f;
                impactSquash = 0f;
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
            if (model == null)
                return;
            float dt = controller.DeltaTime; // 0 during a hit stop: the pose holds
            stateTime += dt;
            Vector3 rootDelta = transform.position - lastRootPosition;
            lastRootPosition = transform.position;

            Vector3 offset = Vector3.zero;
            float tilt = 0f;   // + leans forward, about the base
            float side = 0f;   // sideways wobble/topple, about the base
            float squash = 0f; // + flattens
            bool rolling = false;
            EnemyData data = controller.Data;

            switch (controller.CurrentState)
            {
                case EnemyStateId.Spawn:
                    float rise = data != null && data.SpawnDuration > 0f ? Mathf.Clamp01(stateTime / data.SpawnDuration) : 1f;
                    offset.y = -riseDepth * (1f - Smooth(rise));
                    offset += Shake(1f - rise);
                    break;

                case EnemyStateId.Dead:
                    side = toppleAngle * Smooth(Mathf.Clamp01(stateTime / 0.5f));
                    float sinkTime = data != null ? Mathf.Max(0.1f, data.DespawnDuration - 0.6f) : 1f;
                    offset.y = -sinkDepth * Smooth(Mathf.Clamp01((stateTime - 0.6f) / sinkTime));
                    break;

                case EnemyStateId.Hit:
                    float decay = Mathf.Exp(-4f * stateTime);
                    side = wobbleAngle * decay * Mathf.Sin(stateTime * 14f);
                    tilt = -0.5f * wobbleAngle * decay;
                    break;

                case EnemyStateId.Attack:
                    if (stepActive)
                    {
                        stepTime += dt;
                        rolling = AttackPose(dt, rootDelta, ref offset, ref tilt, ref squash);
                    }
                    break;

                default:
                    Walk(dt, rootDelta, ref offset, ref tilt, ref squash);
                    break;
            }

            // After-hit follow-through and impact squash fade out in any state.
            tilt += afterHitTilt;
            squash += impactSquash;
            afterHitTilt = Mathf.MoveTowards(afterHitTilt, 0f, 90f * dt);
            impactSquash = Mathf.MoveTowards(impactSquash, 0f, 1.2f * dt);
            if (!rolling)
                rollAngle = Mathf.MoveTowards(rollAngle, Mathf.Round(rollAngle / 360f) * 360f, 540f * dt);

            Apply(offset, tilt, side, squash);
        }

        private void Walk(float dt, Vector3 rootDelta, ref Vector3 offset, ref float tilt, ref float squash)
        {
            float speed = agent != null && agent.enabled ? new Vector2(agent.velocity.x, agent.velocity.z).magnitude : 0f;
            float travelled = new Vector2(rootDelta.x, rootDelta.z).magnitude;
            if (speed > 0.05f)
                hopPhase += travelled * hopsPerMeter;
            else
                hopPhase = Mathf.MoveTowards(hopPhase, Mathf.Ceil(hopPhase), dt * 3f); // finish the hop and land
            float h = HopHeight(hopPhase, hopHeight);
            offset.y = h;
            squash = hopHeight > 0f ? landSquash * Mathf.Pow(1f - Mathf.Clamp01(h / hopHeight), 6f) * Mathf.Clamp01(speed) : 0f;
            tilt = moveLean * Mathf.Clamp01(speed / 2f);
        }

        /// <summary>Returns true while rolling.</summary>
        private bool AttackPose(float dt, Vector3 rootDelta, ref Vector3 offset, ref float tilt, ref float squash)
        {
            float t = stepTime;
            float windUpEnd = Mathf.Max(0f, step.HoldStart);
            float holdEnd = windUpEnd + Mathf.Max(0f, step.HoldDuration);
            float hit = Mathf.Max(holdEnd, step.HitDelay);
            float windUp = windUpEnd > 0f ? Smooth(Mathf.Clamp01(t / windUpEnd)) : 1f;
            bool holding = t >= windUpEnd && t < holdEnd;
            float swing = hit > holdEnd ? Mathf.Clamp01((t - holdEnd) / (hit - holdEnd)) : (t >= hit ? 1f : 0f);
            bool beforeHit = t < hit;
            Vector3 shake = holding ? Shake(1f) : Vector3.zero;
            AttackDefinition def = step.Step;

            switch (def.Motion)
            {
                case StatueMotion.Roll:
                    if (t < holdEnd)
                    {
                        tilt = -tiltBackAngle * 0.6f * windUp; // rock back before rolling
                        offset = shake;
                        return false;
                    }
                    if (beforeHit)
                    {
                        Vector3 forward = transform.forward;
                        rollAngle += Vector3.Dot(rootDelta, forward) / rollRadius * Mathf.Rad2Deg;
                        offset.y = 0.05f * Mathf.Abs(Mathf.Sin(rollAngle * Mathf.Deg2Rad * 2f));
                        return true;
                    }
                    return false;

                case StatueMotion.Slam:
                    if (t < holdEnd)
                    {
                        squash = slamCrouch * windUp;
                        offset = shake;
                        return false;
                    }
                    if (beforeHit)
                    {
                        float arc = def.LungeArcHeight > 0f ? def.LungeArcHeight : slamDefaultArc;
                        offset.y = ArcHeight(swing, arc);
                        squash = -0.1f * Mathf.Sin(swing * Mathf.PI); // stretch in the air
                        tilt = 10f * swing;
                        return false;
                    }
                    if (t - dt < hit) // landing frame
                        impactSquash = 0.22f;
                    return false;

                case StatueMotion.Shoot:
                    if (beforeHit)
                    {
                        tilt = -tiltBackAngle * 0.5f * windUp;
                        offset = shake;
                        return false;
                    }
                    if (t - dt < hit)
                        afterHitTilt = -shootRecoil;
                    return false;

                default: // Hop: headbutt
                    if (t < holdEnd)
                    {
                        tilt = -tiltBackAngle * windUp;
                        offset = shake;
                        return false;
                    }
                    if (beforeHit)
                    {
                        tilt = Mathf.Lerp(-tiltBackAngle, 25f, swing);
                        offset.y = ArcHeight(swing, def.LungeArcHeight);
                        return false;
                    }
                    if (t - dt < hit)
                    {
                        afterHitTilt = 25f;
                        impactSquash = 0.1f;
                    }
                    return false;
            }
        }

        private Vector3 Shake(float strength)
        {
            if (strength <= 0f || shakeAmount <= 0f)
                return Vector3.zero;
            float s = Time.time * shakeSpeed;
            return new Vector3(Mathf.Sin(s * 1.3f), 0f, Mathf.Sin(s * 1.7f + 1f)) * (shakeAmount * strength);
        }

        private void Apply(Vector3 offset, float tilt, float side, float squash)
        {
            // Roll about the centre, then lean/wobble about the base. All in the root's local space.
            Vector3 center = Vector3.up * centerHeight;
            Quaternion rollRotation = Quaternion.AngleAxis(rollAngle, Vector3.right);
            Vector3 position = rollRotation * -center + center;
            Quaternion lean = Quaternion.AngleAxis(tilt, Vector3.right) * Quaternion.AngleAxis(-side, Vector3.forward);
            position = lean * position;
            Quaternion rotation = lean * rollRotation;

            model.localPosition = restPosition + position + offset;
            model.localRotation = rotation * restRotation;
            float s = Mathf.Clamp(squash, -0.3f, 0.5f);
            model.localScale = Vector3.Scale(restScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));
        }

        /// <summary>Hop height for a phase in hops (integer = on the ground).</summary>
        public static float HopHeight(float phase, float height) => height * Mathf.Abs(Mathf.Sin(phase * Mathf.PI));

        /// <summary>Parabolic jump: 0 at u = 0 and 1, <paramref name="height"/> at u = 0.5.</summary>
        public static float ArcHeight(float u, float height) => height * 4f * u * (1f - u);

        private static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
