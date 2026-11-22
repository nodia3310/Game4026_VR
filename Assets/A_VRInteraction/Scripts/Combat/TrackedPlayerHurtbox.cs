using System;
using System.Collections.Generic;
using Game4026.Contracts;
using Game4026.Enemy;
using Game4026.Interaction;
using Game4026.Interaction.TestScene;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.PlayerCombat
{
    /// <summary>Tracks the player's damage volumes and adapts the enemy's existing attack events.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1)] // After XRI locomotion, before enemy Update methods.
    [RequireComponent(typeof(PlayerTarget))]
    public sealed class TrackedPlayerHurtbox : MonoBehaviour, IPlayerDamageReceiver
    {
        [SerializeField, Min(.3f)] private float standingEyeHeight = 1.65f;
        [SerializeField, Min(0f)] private float meleeMinHeight = 1.36f;
        [SerializeField, Min(0f)] private float meleeMaxHeight = 1.64f;
        [SerializeField] private XROrigin rigOverride;
        [SerializeField] private PlayerHealth healthOverride;
        [SerializeField] private InteractionDesktopMode desktop;
        private const float TrackingGrace = .20f;

        private readonly HashSet<EnemyAttack> attacks = new();
        private readonly Dictionary<EnemyPoolManager, int> pools = new();
        private readonly List<float> receivedDamage = new();
        private XROrigin rig;
        private Transform trackingSpace, eye;
        private TrackedPoseDriver headPose, leftPose, rightPose;
        private PlayerHealth health;
        private PlayerTarget target;
        private Vector3 lastHeadPosition;
        private Quaternion lastHeadRotation;
        private float lastTrackedAt;
        private PlayerHitVolume body;

        // DemoFlow discovers these members through PlayerVitals on the current damage receiver.
        public float CurrentHealth
        {
            [Preserve] get => health ? health.CurrentHealth : 0f;
        }
        public event Action ResetForRestart;

        [Preserve]
        public void ResetHealth()
        {
            receivedDamage.Clear();
            if (health) health.ResetHealth();
            ResetForRestart?.Invoke();
        }

        private void Awake()
        {
            target = GetComponent<PlayerTarget>();
            health = healthOverride ? healthOverride : GetComponentInParent<PlayerHealth>();
            rig = rigOverride ? rigOverride : GetComponentInParent<XROrigin>();
            if (!rig || !rig.Camera || !rig.CameraFloorOffsetObject || !health)
            {
                Debug.LogError("TrackedPlayerHurtbox requires a PlayerHealth parent and an XR Origin with a camera and floor offset.", this);
                enabled = false;
                return;
            }
            trackingSpace = rig.CameraFloorOffsetObject.transform;
            eye = rig.Camera.transform;
            headPose = eye.GetComponent<TrackedPoseDriver>();

            foreach (var hand in rig.GetComponentsInChildren<NearFarInteractor>(true))
            {
                if (hand.handedness == InteractorHandedness.Left && !leftPose)
                    leftPose = hand.GetComponentInParent<TrackedPoseDriver>(true);
                if (hand.handedness == InteractorHandedness.Right && !rightPose)
                    rightPose = hand.GetComponentInParent<TrackedPoseDriver>(true);
            }
        }

        private void OnEnable()
        {
            lastHeadPosition = Vector3.zero;
            lastHeadRotation = Quaternion.identity;
            lastTrackedAt = float.NegativeInfinity;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshSceneSources();
            UpdatePose();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (var attack in attacks)
                if (attack) attack.AttackResolved -= OnAttackResolved;
            attacks.Clear();
            pools.Clear();
            ApplyRemainingDamage();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshSceneSources();

        private void Update()
        {
            // Pooled enemies we already know keep their subscriptions as they are activated and parked.
            foreach (var entry in pools)
            {
                if (entry.Key && entry.Value == entry.Key.CreatedCount) continue;
                RefreshSceneSources();
                break;
            }
            UpdatePose();
        }

        private void LateUpdate() => ApplyRemainingDamage();

        private void RefreshSceneSources()
        {
            attacks.RemoveWhere(attack => !attack);
            pools.Clear();
            foreach (var pool in FindObjectsByType<EnemyPoolManager>(FindObjectsInactive.Include))
                pools.Add(pool, pool.CreatedCount);
            foreach (var attack in FindObjectsByType<EnemyAttack>(FindObjectsInactive.Include))
                TrackEnemy(attack);
        }

        /// <summary>Connect a scripted enemy that is instantiated outside the scene or enemy pool.</summary>
        public void TrackEnemy(EnemyAttack attack)
        {
            if (isActiveAndEnabled && attack && attacks.Add(attack))
                attack.AttackResolved += OnAttackResolved;
        }

        // B sends melee damage immediately before AttackResolved(true). Keep the receiver calls until
        // that event can replace the point test. Projectile and other damage is flushed in LateUpdate.
        void IPlayerDamageReceiver.ApplyPlayerDamage(float damage)
        {
            if (isActiveAndEnabled)
                receivedDamage.Add(damage);
            else if (health)
                health.ApplyPlayerDamage(damage);
        }

        private void OnAttackResolved(EnemyAttack enemy, AttackDefinition step, bool legacyHit)
        {
            if (PlayerTarget.Current != target || step.Type is AttackType.Guard or AttackType.Projectile)
                return;
            int last = receivedDamage.Count - 1;
            if (legacyHit && last >= 0 && receivedDamage[last] == step.Damage)
                receivedDamage.RemoveAt(last);

            UpdatePose();
            var volume = step.Type == AttackType.AreaSlam
                ? new AttackVolume(enemy.transform, step.SlamRadius, 360f,
                    float.NegativeInfinity, float.PositiveInfinity)
                : new AttackVolume(enemy.transform, step.HitRange, step.HitAngle,
                    meleeMinHeight, Mathf.Max(meleeMinHeight, meleeMaxHeight));
            if (body.Overlaps(volume))
                health.ApplyPlayerDamage(step.Damage);
        }

        private void ApplyRemainingDamage()
        {
            if (health)
                foreach (float damage in receivedDamage)
                    health.ApplyPlayerDamage(damage);
            receivedDamage.Clear();
        }

        private void UpdatePose()
        {
            float now = Time.unscaledTime;
            bool desktopActive = desktop && desktop.IsDesktopActive;
            var activeEye = desktopActive ? desktop.DesktopCamera.transform : eye;
            bool tracked = desktopActive || HasPosition(headPose);
            if (tracked)
            {
                Vector3 localEye = trackingSpace.InverseTransformPoint(activeEye.position);
                tracked = float.IsFinite(localEye.x) && float.IsFinite(localEye.y) && float.IsFinite(localEye.z);
                if (tracked)
                {
                    lastHeadPosition = localEye;
                    lastHeadRotation = Quaternion.Inverse(trackingSpace.rotation) * activeEye.rotation;
                    lastTrackedAt = now;
                }
            }

            // Hold the latest room-space sample through a brief dropout, then model an upright
            // player at the same room-space XZ. Both follow stick motion and snap turns.
            Vector3 position = trackingSpace.TransformPoint(lastHeadPosition);
            if (now - lastTrackedAt > TrackingGrace)
            {
                var local = rig.transform.InverseTransformPoint(position);
                local.y = standingEyeHeight;
                position = rig.transform.TransformPoint(local);
            }
            Quaternion rotation = trackingSpace.rotation * lastHeadRotation;
            Vector3 up = rig.transform.up;
            Vector3 facing = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
            if (facing.sqrMagnitude < .01f)
                facing = Vector3.Cross(rotation * Vector3.right, up);
            facing.Normalize();
            Vector3 right = Vector3.Cross(up, facing);

            // The controllers can turn the shoulder line. They cannot move the head, chest or hips.
            if (tracked && HasPosition(leftPose) && HasPosition(rightPose))
                right = EstimateShoulderRight(position, up, right);

            body = new PlayerHitVolume(position, rotation, up, facing, right);
            target.Head.SetPositionAndRotation(position, rotation);
            target.Body.SetPositionAndRotation(body.Chest, Quaternion.LookRotation(Vector3.Cross(right, up), up));
        }

        private Vector3 EstimateShoulderRight(Vector3 head, Vector3 up, Vector3 right)
        {
            Vector3 left = leftPose.transform.position, rightHand = rightPose.transform.position;
            Vector3 span = rightHand - left;
            Vector3 planar = Vector3.ProjectOnPlane(span, up);
            if ((left - head).sqrMagnitude > .85f * .85f || (rightHand - head).sqrMagnitude > .85f * .85f ||
                Mathf.Abs(Vector3.Dot(span, up)) > .30f || planar.sqrMagnitude < .22f * .22f ||
                planar.sqrMagnitude > .85f * .85f)
                return right;

            Vector3 direction = planar.normalized;
            return Vector3.Dot(right, direction) >= .5f
                ? Vector3.Lerp(right, direction, .6f).normalized
                : right;
        }

        private static bool HasPosition(TrackedPoseDriver pose) =>
            pose && pose.isActiveAndEnabled && pose.trackingStateInput.action is { enabled: true } action &&
            ((InputTrackingState)action.ReadValue<int>() & InputTrackingState.Position) != 0;

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            Gizmos.color = Color.cyan;
            body.Draw();
        }
    }
}
