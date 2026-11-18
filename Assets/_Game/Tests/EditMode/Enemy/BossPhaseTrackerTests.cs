using Game4026.Enemy;
using NUnit.Framework;

namespace Game4026.Tests.Enemy
{
    public sealed class BossPhaseTrackerTests
    {
        private static BossPhaseTracker Default() => new BossPhaseTracker(new[] { 1f, 0.6f, 0.25f });

        [TestCase(1.0f, 0)]
        [TestCase(0.61f, 0)]
        [TestCase(0.6f, 1)]
        [TestCase(0.26f, 1)]
        [TestCase(0.25f, 2)]
        [TestCase(0.0f, 2)]
        public void PhaseFor_UsesThresholds(float fraction, int expected)
        {
            Assert.AreEqual(expected, Default().PhaseFor(fraction));
        }

        [Test]
        public void TryAdvance_ReportsFromAndTo()
        {
            var t = Default();
            Assert.IsFalse(t.TryAdvance(0.8f, out _, out _));

            Assert.IsTrue(t.TryAdvance(0.5f, out int from, out int to));
            Assert.AreEqual(0, from);
            Assert.AreEqual(1, to);
            Assert.AreEqual(1, t.Current);
        }

        [Test]
        public void Phases_OnlyMoveForward()
        {
            var t = Default();
            t.TryAdvance(0.2f, out _, out _);
            Assert.IsFalse(t.TryAdvance(0.9f, out _, out _), "Healing must not go back a phase.");
            Assert.AreEqual(2, t.Current);
        }

        [Test]
        public void BigHit_SkipsStraightToLaterPhase()
        {
            var t = Default();
            Assert.IsTrue(t.TryAdvance(0.1f, out int from, out int to));
            Assert.AreEqual(0, from);
            Assert.AreEqual(2, to);
        }

        [Test]
        public void Reset_ReturnsToFirstPhase()
        {
            var t = Default();
            t.TryAdvance(0.1f, out _, out _);
            t.Reset();
            Assert.AreEqual(0, t.Current);
        }

        [Test]
        public void FourPhases_FromInspectorList_Work()
        {
            var t = new BossPhaseTracker(new[] { 1f, 0.75f, 0.5f, 0.25f });
            Assert.AreEqual(2, t.PhaseFor(0.4f));
            Assert.AreEqual(4, t.PhaseCount);
        }
    }
}
