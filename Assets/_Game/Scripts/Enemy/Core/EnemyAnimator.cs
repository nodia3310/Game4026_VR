using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Drives a model's Animator from <see cref="EnemyController"/> state changes.
    /// Expected Animator states: a locomotion blend tree on "Locomotion" (0 idle, 1 walk, 2 run), "Attack", "Hit", "Death".
    /// The attack clip is sped up or slowed down so its impact frame lands exactly when the hit is resolved.
    /// Leave EnemyController's own Animator field empty when using this.
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
        [SerializeField] private string attackState = "Attack";
        [SerializeField] private string hitState = "Hit";
        [SerializeField] private string deathState = "Death";

        [Header("Timing")]
        [Tooltip("The clip used by the Attack state. Its length sets how long the enemy stays in attack recovery.")]
        [SerializeField] private AnimationClip attackClip;
        [Tooltip("Seconds into the attack clip (at normal speed) where the strike connects.")]
        [SerializeField, Min(0.01f)] private float attackImpactTime = 0.6f;
        [Tooltip("Keep the enemy in attack recovery until the clip has finished, so it is not cut off by chasing.")]
        [SerializeField] private bool recoveryFollowsClip = true;
        [Tooltip("Playback speed range for the attack clip, so very short or long windups do not look broken.")]
        [SerializeField] private Vector2 attackSpeedRange = new Vector2(0.5f, 2.5f);
        [SerializeField, Min(0f)] private float crossFade = 0.15f;
        [SerializeField, Min(0f)] private float hitCrossFade = 0.05f;
        [Tooltip("How fast the locomotion blend follows the agent speed.")]
        [SerializeField, Min(0f)] private float locomotionDamping = 0.1f;

        private EnemyController controller;
        private NavMeshAgent agent;
        private int locomotionHash, attackHash, hitHash, deathHash;

        public Animator Animator => animator;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            agent = GetComponent<NavMeshAgent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            locomotionHash = Animator.StringToHash(locomotionState);
            attackHash = Animator.StringToHash(attackState);
            hitHash = Animator.StringToHash(hitState);
            deathHash = Animator.StringToHash(deathState);
        }

        private void OnEnable() => controller.StateChanged += HandleStateChanged;

        private void OnDisable() => controller.StateChanged -= HandleStateChanged;

        private void LateUpdate()
        {
            if (animator == null || controller.Data == null)
                return;
            float speed = agent.enabled ? agent.velocity.magnitude : 0f;
            animator.SetFloat(LocomotionParam, LocomotionBlend(speed, controller.Data.MoveSpeed, controller.Data.ChaseSpeed),
                locomotionDamping, Time.deltaTime);
        }

        private void HandleStateChanged(EnemyController _, EnemyStateId previous, EnemyStateId next)
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return;
            switch (next)
            {
                case EnemyStateId.Spawn:
                    animator.Play(locomotionHash, 0, 0f);
                    break;
                case EnemyStateId.Attack:
                    float playback = AttackPlaybackSpeed(attackImpactTime, controller.AttackWindup, attackSpeedRange);
                    animator.SetFloat(AttackSpeedParam, playback);
                    animator.CrossFadeInFixedTime(attackHash, crossFade, 0, 0f);
                    controller.Attack.RecoveryOverride = recoveryFollowsClip && attackClip != null
                        ? RecoveryForClip(attackClip.length, attackImpactTime, playback, crossFade)
                        : -1f;
                    break;
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

        /// <summary>Playback speed that puts the clip's impact time on the windup end, clamped to a sane range.</summary>
        public static float AttackPlaybackSpeed(float impactTime, float windup, Vector2 range)
        {
            if (windup <= 0f)
                return range.y;
            return Mathf.Clamp(impactTime / windup, range.x, range.y);
        }
    }
}
