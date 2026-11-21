using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Drives a model's Animator from <see cref="EnemyController"/> states and <see cref="EnemyAttack"/> steps.
    /// Expected Animator: a locomotion blend tree on "Locomotion" (0 idle, 1 walk, 2 run), "Hit", "Death", and one
    /// state per attack animation named by <see cref="AttackDefinition.AnimatorState"/> (Speed bound to the
    /// "AttackSpeed" float). Mirrored moves use "&lt;state&gt;_Mirror" when it exists.
    /// Each attack step plays its clip at animSpeed, freezes on the wind-up pose for the telegraph, then swings so
    /// the impact frame lands on the hit check. Leave EnemyController's own Animator field empty when using this.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyAnimator : MonoBehaviour
    {
        private static readonly int LocomotionParam = Animator.StringToHash("Locomotion");
        private static readonly int AttackSpeedParam = Animator.StringToHash("AttackSpeed");

        [SerializeField] private Animator animator;

        [Header("Animator state names")]
        [SerializeField] private string locomotionState = "Locomotion";
        [Tooltip("Fallback when a move's state is missing from the controller.")]
        [SerializeField] private string attackState = "Attack";
        [SerializeField] private string hitState = "Hit";
        [SerializeField] private string deathState = "Death";

        [Header("Timing")]
        [Tooltip("Fallback clip length for moves without their own clip. Keeps the enemy in recovery until the clip has finished.")]
        [SerializeField] private AnimationClip attackClip;
        [Tooltip("Keep the enemy in recovery after a move's last hit until its clip has finished, so chasing does not cut it off.")]
        [SerializeField] private bool recoveryFollowsClip = true;
        [SerializeField, Min(0f)] private float crossFade = 0.15f;
        [Tooltip("Cross-fade between combo steps (short, so follow-up swings start crisply).")]
        [SerializeField, Min(0f)] private float comboCrossFade = 0.08f;
        [SerializeField, Min(0f)] private float hitCrossFade = 0.05f;
        [Tooltip("How fast the locomotion blend follows the agent speed.")]
        [SerializeField, Min(0f)] private float locomotionDamping = 0.1f;

        private EnemyController controller;
        private EnemyAttack attack;
        private NavMeshAgent agent;
        private int locomotionHash, attackHash, hitHash, deathHash;

        private bool stepActive;
        private float stepElapsed, holdStart, holdEnd, playback, appliedSpeed = -1f;

        public Animator Animator => animator;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            attack = GetComponent<EnemyAttack>();
            agent = GetComponent<NavMeshAgent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            locomotionHash = Animator.StringToHash(locomotionState);
            attackHash = Animator.StringToHash(attackState);
            hitHash = Animator.StringToHash(hitState);
            deathHash = Animator.StringToHash(deathState);
        }

        private void OnEnable()
        {
            controller.StateChanged += HandleStateChanged;
            if (attack != null)
                attack.StepStarted += HandleStepStarted;
        }

        private void OnDisable()
        {
            controller.StateChanged -= HandleStateChanged;
            if (attack != null)
                attack.StepStarted -= HandleStepStarted;
        }

        private void LateUpdate()
        {
            if (animator == null || controller.Data == null)
                return;
            float speed = agent.enabled ? agent.velocity.magnitude : 0f;
            animator.SetFloat(LocomotionParam, LocomotionBlend(speed, controller.Data.MoveSpeed, controller.Data.ChaseSpeed),
                locomotionDamping, Time.deltaTime);

            if (!stepActive)
                return;
            stepElapsed += Time.deltaTime;
            SetAttackSpeed(StepPlaybackAt(stepElapsed, holdStart, holdEnd, playback));
        }

        private void HandleStepStarted(EnemyAttack _, AttackStepTiming timing)
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return;

            AttackDefinition step = timing.Step;
            int state = step.Mirror && animator.HasState(0, step.MirrorStateHash) ? step.MirrorStateHash : step.StateHash;
            if (!animator.HasState(0, state))
                state = attackHash;

            stepActive = true;
            stepElapsed = 0f;
            holdStart = timing.HoldStart;
            holdEnd = timing.HoldStart + timing.HoldDuration;
            playback = timing.Playback;
            appliedSpeed = -1f;
            SetAttackSpeed(StepPlaybackAt(0f, holdStart, holdEnd, playback));
            animator.CrossFadeInFixedTime(state, timing.Index == 0 ? crossFade : comboCrossFade, 0, timing.ClipStartTime);

            if (!timing.IsLast || !recoveryFollowsClip)
                return;
            AnimationClip clip = step.Clip != null ? step.Clip : attackClip;
            if (clip != null)
                attack.RecoveryOverride = RecoveryForClip(clip.length, step.HitTime, playback, crossFade);
        }

        private void SetAttackSpeed(float speed)
        {
            if (Mathf.Approximately(speed, appliedSpeed))
                return;
            appliedSpeed = speed;
            animator.SetFloat(AttackSpeedParam, speed);
        }

        private void HandleStateChanged(EnemyController _, EnemyStateId previous, EnemyStateId next)
        {
            if (next != EnemyStateId.Attack)
                stepActive = false;
            if (animator == null || !animator.isActiveAndEnabled)
                return;
            switch (next)
            {
                case EnemyStateId.Spawn:
                    animator.Play(locomotionHash, 0, 0f);
                    break;
                case EnemyStateId.Attack:
                    break; // each step cross-fades on EnemyAttack.StepStarted
                case EnemyStateId.Hit:
                    // Re-entering Hit restarts the clip.
                    animator.CrossFadeInFixedTime(hitHash, hitCrossFade, 0, 0f);
                    break;
                case EnemyStateId.Dead:
                    animator.CrossFadeInFixedTime(deathHash, crossFade, 0, 0f);
                    break;
                default: // Idle, Patrol, Chase: the blend tree follows the agent speed.
                    if (previous == EnemyStateId.Attack || previous == EnemyStateId.Hit)
                        animator.CrossFadeInFixedTime(locomotionHash, crossFade, 0);
                    break;
            }
        }

        /// <summary>Clip speed at this moment of a step: 0 while holding the wind-up pose (telegraph), otherwise playback.</summary>
        public static float StepPlaybackAt(float elapsed, float holdStart, float holdEnd, float playback) =>
            holdEnd > holdStart && elapsed >= holdStart && elapsed < holdEnd ? 0f : playback;

        /// <summary>0 = idle, 1 = walk at moveSpeed, 2 = run at chaseSpeed (linear in between, clamped).</summary>
        public static float LocomotionBlend(float speed, float moveSpeed, float chaseSpeed)
        {
            if (speed <= 0f || moveSpeed <= 0f)
                return 0f;
            if (speed <= moveSpeed)
                return speed / moveSpeed;
            if (chaseSpeed <= moveSpeed)
                return 2f;
            return Mathf.Min(2f, 1f + (speed - moveSpeed) / (chaseSpeed - moveSpeed));
        }

        /// <summary>
        /// Real seconds from the impact frame to the end of the clip at this playback speed,
        /// minus the cross-fade so the blend back to locomotion finishes as the clip ends.
        /// </summary>
        public static float RecoveryForClip(float clipLength, float impactTime, float playbackSpeed, float crossFade)
        {
            if (playbackSpeed <= 0f)
                return 0f;
            return Mathf.Max(0f, (clipLength - impactTime) / playbackSpeed - crossFade);
        }
    }
}
