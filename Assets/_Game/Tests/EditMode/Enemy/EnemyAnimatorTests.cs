using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class EnemyAnimatorTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.6f, 0.5f)]
        [TestCase(1.2f, 1f)]
        [TestCase(1.9f, 1.5f)]
        [TestCase(2.6f, 2f)]
        [TestCase(10f, 2f)]
        public void LocomotionBlend_MapsSpeedToIdleWalkRun(float speed, float expected) =>
            Assert.AreEqual(expected, EnemyAnimator.LocomotionBlend(speed, 1.2f, 2.6f), 1e-4f);

        [Test]
        public void LocomotionBlend_ChaseNotFasterThanMove_RunsAboveMoveSpeed() =>
            Assert.AreEqual(2f, EnemyAnimator.LocomotionBlend(1.5f, 1.2f, 1.2f));

        [Test]
        public void AttackPlaybackSpeed_LandsImpactOnWindupEnd() =>
            Assert.AreEqual(0.6f / 0.4f, EnemyAnimator.AttackPlaybackSpeed(0.6f, 0.4f, new Vector2(0.5f, 2.5f)), 1e-4f);

        [Test]
        public void RecoveryForClip_WaitsForRestOfClipAtPlaybackSpeed() =>
            // 1.6 s clip, impact 0.8 s, played at 2x: 0.4 s left, minus 0.15 s cross-fade.
            Assert.AreEqual(0.25f, EnemyAnimator.RecoveryForClip(1.6f, 0.8f, 2f, 0.15f), 1e-4f);

        [Test]
        public void RecoveryForClip_NeverNegative()
        {
            Assert.AreEqual(0f, EnemyAnimator.RecoveryForClip(0.5f, 0.8f, 1f, 0.15f));
            Assert.AreEqual(0f, EnemyAnimator.RecoveryForClip(1.6f, 0.8f, 0f, 0.15f));
        }

        [Test]
        public void EnemyAttack_RecoveryOverride_ReplacesInspectorValue_NegativeRestores()
        {
            var go = new GameObject("Attack");
            try
            {
                var attack = go.AddComponent<EnemyAttack>();
                float inspector = attack.RecoveryDuration;
                attack.RecoveryOverride = 1.25f;
                Assert.AreEqual(1.25f, attack.RecoveryDuration);
                attack.RecoveryOverride = -1f;
                Assert.AreEqual(inspector, attack.RecoveryDuration);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AttackPlaybackSpeed_IsClamped()
        {
            var range = new Vector2(0.5f, 2.5f);
            Assert.AreEqual(2.5f, EnemyAnimator.AttackPlaybackSpeed(0.6f, 0.1f, range));
            Assert.AreEqual(0.5f, EnemyAnimator.AttackPlaybackSpeed(0.6f, 5f, range));
            Assert.AreEqual(2.5f, EnemyAnimator.AttackPlaybackSpeed(0.6f, 0f, range));
        }
    }
}
