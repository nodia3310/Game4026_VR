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
        public void NeverTwoSweepsInARow()
        {
            for (int phase = 0; phase < 3; phase++)
                for (float r = 0f; r <= 1f; r += 0.02f)
                    Assert.AreNotEqual(StandMove.Sweep, StandMoveSelector.Pick(phase, r, StandMove.Sweep));
        }

        [Test]
        public void Phase0_PicksBothSlamAndSweep()
        {
            Assert.AreEqual(StandMove.Slam, StandMoveSelector.Pick(0, 0.1f, null));
            Assert.AreEqual(StandMove.Sweep, StandMoveSelector.Pick(0, 0.9f, null));
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
    }
}
