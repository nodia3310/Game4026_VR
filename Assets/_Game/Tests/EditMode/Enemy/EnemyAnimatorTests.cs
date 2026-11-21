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

        [TestCase(0.1f, 1.5f)]  // before the hold
        [TestCase(0.35f, 0f)]   // holding the wind-up pose (telegraph)
        [TestCase(0.9f, 1.5f)]  // swinging
        public void StepPlaybackAt_FreezesDuringTelegraphHold(float elapsed, float expected) =>
            Assert.AreEqual(expected, EnemyAnimator.StepPlaybackAt(elapsed, 0.3f, 0.8f, 1.5f));

        [Test]
        public void StepPlaybackAt_Creep_SlowsTheWindUp_AndReachesThePoseOnTime()
        {
            const float holdStart = 0.4f, holdEnd = 1.2f, playback = 1.5f, creep = 0.15f;
            Assert.AreEqual(playback, EnemyAnimator.StepPlaybackAt(0.05f, holdStart, holdEnd, playback, creep), "Normal start.");
            float slow = EnemyAnimator.StepPlaybackAt(1f, holdStart, holdEnd, playback, creep);
            Assert.Greater(slow, 0f, "Slow motion, not a freeze.");
            Assert.Less(slow, playback * 0.3f);
            Assert.AreEqual(playback, EnemyAnimator.StepPlaybackAt(1.25f, holdStart, holdEnd, playback, creep), "Swing at normal speed.");

            // Clip time at the hold's end equals the pose time (holdStart × playback), as with a freeze.
            float clip = 0f;
            for (float t = 0f; t < holdEnd - 1e-6f; t += 0.001f)
                clip += EnemyAnimator.StepPlaybackAt(t + 0.0005f, holdStart, holdEnd, playback, creep) * 0.001f;
            Assert.AreEqual(holdStart * playback, clip, 0.01f);
        }

        [Test]
        public void StepPlaybackAt_NoTelegraph_NeverFreezes() =>
            Assert.AreEqual(1.2f, EnemyAnimator.StepPlaybackAt(0.3f, 0.3f, 0.3f, 1.2f));

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
        public void EnemyAttack_RecoveryOverride_NaNMeansNone_ResetClears()
        {
            var go = new GameObject("Attack");
            try
            {
                var attack = go.AddComponent<EnemyAttack>();
                attack.RecoveryOverride = 1.25f;
                Assert.AreEqual(1.25f, attack.RecoveryOverride);
                attack.RecoveryOverride = float.NaN;
                Assert.AreEqual(-1f, attack.RecoveryOverride);
                attack.RecoveryOverride = 2f;
                attack.ResetState();
                Assert.AreEqual(-1f, attack.RecoveryOverride);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
