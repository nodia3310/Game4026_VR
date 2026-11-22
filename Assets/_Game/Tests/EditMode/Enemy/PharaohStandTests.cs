using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class PharaohStandTests
    {
        [Test]
        public void Phase0_NeverCombo()
        {
            for (float r = 0f; r <= 1f; r += 0.05f)
                Assert.AreNotEqual(StandMove.ComboSlam, StandMoveSelector.Pick(0, r, null));
        }

        [Test]
        public void LaterPhases_CanCombo()
        {
            Assert.AreEqual(StandMove.ComboSlam, StandMoveSelector.Pick(1, 0.99f, null));
            Assert.AreEqual(StandMove.ComboSlam, StandMoveSelector.Pick(2, 0.99f, null));
            Assert.AreEqual(StandMove.ComboSlam, StandMoveSelector.Pick(5, 0.99f, null)); // clamped to the last phase
        }

        [Test]
        public void NeverTheSameSideAttackTwiceInARow()
        {
            for (int phase = 0; phase < 3; phase++)
                for (float r = 0f; r <= 1f; r += 0.02f)
                {
                    Assert.AreNotEqual(StandMove.Sweep, StandMoveSelector.Pick(phase, r, StandMove.Sweep));
                    Assert.AreNotEqual(StandMove.HeadSlash, StandMoveSelector.Pick(phase, r, StandMove.HeadSlash));
                }
        }

        [Test]
        public void Phase0_PicksEveryBasicMove()
        {
            Assert.AreEqual(StandMove.Slam, StandMoveSelector.Pick(0, 0.1f, null));
            Assert.AreEqual(StandMove.Sweep, StandMoveSelector.Pick(0, 0.38f, null));
            Assert.AreEqual(StandMove.HeadSlash, StandMoveSelector.Pick(0, 0.52f, null));
            Assert.AreEqual(StandMove.WeaponThrow, StandMoveSelector.Pick(0, 0.7f, null));
            Assert.AreEqual(StandMove.Clap, StandMoveSelector.Pick(0, 0.9f, null));
        }

        [Test]
        public void NeverTwoClapsInARow()
        {
            for (int phase = 0; phase < 3; phase++)
                for (float r = 0f; r <= 1f; r += 0.02f)
                    Assert.AreNotEqual(StandMove.Clap, StandMoveSelector.Pick(phase, r, StandMove.Clap));
        }

        [Test]
        public void Clap_HitsBetweenTheHands_OneStepBackOrForwardAvoids()
        {
            var target = Vector3.zero;
            var towardBoss = Vector3.forward;
            Assert.IsTrue(StandGeometry.ClapHits(target, towardBoss, new Vector3(1f, 1f, 0.3f), 0.8f, 3f));
            Assert.IsFalse(StandGeometry.ClapHits(target, towardBoss, new Vector3(0f, 1f, -1f), 0.8f, 3f), "stepped back");
            Assert.IsFalse(StandGeometry.ClapHits(target, towardBoss, new Vector3(0f, 1f, 1f), 0.8f, 3f), "stepped forward");
            Assert.IsFalse(StandGeometry.ClapHits(target, towardBoss, new Vector3(3.5f, 1f, 0f), 0.8f, 3f), "outside the hands");
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(5, 3)]
        public void ThrowCount_GrowsWithPhase(int phase, int expected)
        {
            Assert.AreEqual(expected, StandMoveSelector.ThrowCount(phase));
        }

        [Test]
        public void HeadSlash_HitsStandingPlayerAcrossTheWideLane_CrouchAvoids()
        {
            var lane = Vector3.right;
            var hand = new Vector3(0f, 1.65f, 0f);
            // One step (1 m) out of the lane is not enough.
            Assert.IsTrue(StandGeometry.LaneHits(hand, lane, new Vector3(0.3f, 1f, 1f), 0.9f, 2f, 1.7f, 1.3f));
            Assert.IsFalse(StandGeometry.LaneHits(hand, lane, new Vector3(0.3f, 0.6f, 1f), 0.9f, 2f, 1.1f, 1.3f), "crouched");
            Assert.IsFalse(StandGeometry.LaneHits(hand, lane, new Vector3(2f, 1f, 0f), 0.9f, 2f, 1.7f, 1.3f), "hand not there yet");
        }

        [Test]
        public void Slam_HitsInsideRadius_OneStepOutMisses()
        {
            var c = new Vector3(5f, 0f, 5f);
            Assert.IsTrue(StandGeometry.SlamHits(c, 1.1f, new Vector3(5.5f, 1.1f, 5f)));
            Assert.IsFalse(StandGeometry.SlamHits(c, 1.1f, new Vector3(6.3f, 1.1f, 5f)));
        }

        [Test]
        public void Sweep_HitsStandingPlayer_CrouchingOrSteppingAsideAvoids()
        {
            var hand = new Vector3(0f, 1.3f, 0f);
            Assert.IsTrue(StandGeometry.SweepHits(hand, 0.8f, new Vector3(0.3f, 1f, 0f), 1.7f, 1.25f));
            Assert.IsFalse(StandGeometry.SweepHits(hand, 0.8f, new Vector3(0.3f, 0.6f, 0f), 1.1f, 1.25f), "crouched");
            Assert.IsFalse(StandGeometry.SweepHits(hand, 0.8f, new Vector3(1.2f, 1f, 0f), 1.7f, 1.25f), "stepped out");
        }

        [Test]
        public void SweepLane_IsPerpendicularToBossAndThroughTarget()
        {
            var boss = new Vector3(10f, 0f, 0f);
            var target = new Vector3(0f, 0f, 0f);
            StandGeometry.SweepLane(boss, target, 5f, true, out Vector3 a, out Vector3 b);
            Assert.AreEqual(10f, Vector3.Distance(a, b), 1e-4f);
            Assert.AreEqual(Vector3.zero, (a + b) * 0.5f);
            Assert.AreEqual(0f, Vector3.Dot((b - a).normalized, (target - boss).normalized), 1e-4f);

            StandGeometry.SweepLane(boss, target, 5f, false, out Vector3 a2, out _);
            Assert.AreEqual(b, a2, "other side");
        }

        [Test]
        public void SweepLane_Angled_StartsBetweenPlayerAndBoss_StillThroughTarget()
        {
            var boss = new Vector3(10f, 0f, 0f);
            var target = Vector3.zero;
            StandGeometry.SweepLane(boss, target, 5f, true, out Vector3 a, out Vector3 b, 50f);
            Assert.Greater(a.x, 2f, "start is toward the boss (in front of the player)");
            Assert.Less(b.x, -2f);
            Assert.AreEqual(0f, ((a + b) * 0.5f).magnitude, 1e-4f);
        }
    }
}
