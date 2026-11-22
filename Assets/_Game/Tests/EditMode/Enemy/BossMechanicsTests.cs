using System.Collections.Generic;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class BossMechanicsTests
    {
        [Test]
        public void Pendulum_SwingsBetweenPlusAndMinusAmplitude()
        {
            Assert.AreEqual(0f, PendulumMath.Angle(0f, 2f, 30f, 0f), 1e-4f);
            Assert.AreEqual(30f, PendulumMath.Angle(0.5f, 2f, 30f, 0f), 1e-3f);
            Assert.AreEqual(-30f, PendulumMath.Angle(1.5f, 2f, 30f, 0f), 1e-3f);
            Assert.AreEqual(30f, PendulumMath.Angle(0f, 2f, 30f, 0.25f), 1e-3f, "phase offset");
        }

        [Test]
        public void Pendulum_PassesBottom_TwicePerPeriod_CountedEarlyByLead()
        {
            // Period 2 s: straight down at t = 0, 1, 2 … With a 0.2 s lead the whoosh fires at 0.8, 1.8 …
            Assert.IsTrue(PendulumMath.PassesBottom(0.7f, 0.85f, 2f, 0f, 0.2f));
            Assert.IsFalse(PendulumMath.PassesBottom(0.1f, 0.5f, 2f, 0f, 0.2f));
            Assert.IsFalse(PendulumMath.PassesBottom(0.85f, 0.85f, 2f, 0f, 0.2f), "no time passed");
            int count = 0;
            for (float t = 0.05f; t < 10.001f; t += 0.05f)
                if (PendulumMath.PassesBottom(t - 0.05f, t, 2f, 0.25f, 0.3f))
                    count++;
            Assert.AreEqual(10, count, "10 s at a 2 s period = 10 sweeps");
        }

        [Test]
        public void Pendulum_BladeHangsBelowPivot_AndSwingsSideways()
        {
            var pivot = new Vector3(0f, 9f, 0f);
            Vector3 down = PendulumMath.Blade(pivot, Vector3.right, 8f, 0f);
            Assert.AreEqual(1f, down.y, 1e-4f);
            Assert.AreEqual(0f, down.x, 1e-4f);

            Vector3 side = PendulumMath.Blade(pivot, Vector3.right, 8f, 28f);
            Assert.Greater(side.x, 3.5f, "covers the 3 m half-width corridor");
            Assert.Less(side.y, 2.0f, "still low enough to hit a standing player");
        }

        [Test]
        public void Barrage_EveryWaveUsesSpreadOutSlots_AndEverySlotOnce()
        {
            var used = new HashSet<int>();
            for (int launch = 0; launch < 12; launch++)
                Assert.IsTrue(used.Add(BarragePlan.SlotForLaunch(launch, 12, 3)), "slot used twice");
            Assert.AreEqual(12, used.Count);

            // First wave: left, middle, right of the fan.
            Assert.AreEqual(0, BarragePlan.SlotForLaunch(0, 12, 3));
            Assert.AreEqual(4, BarragePlan.SlotForLaunch(1, 12, 3));
            Assert.AreEqual(8, BarragePlan.SlotForLaunch(2, 12, 3));
            Assert.AreEqual(4, BarragePlan.WaveCount(12, 3));
        }

        [Test]
        public void Barrage_SlotsFanOutSymmetrically_AlternatingHeight()
        {
            Vector3 center = Vector3.zero, toPlayer = Vector3.forward;
            Vector3 first = BarragePlan.Slot(0, 12, center, toPlayer, 5f, 70f, 3f, 5f);
            Vector3 last = BarragePlan.Slot(11, 12, center, toPlayer, 5f, 70f, 3f, 5f);
            Assert.AreEqual(-first.x, last.x, 1e-3f);
            Assert.AreEqual(3f, first.y, 1e-4f);
            Assert.AreEqual(5f, last.y, 1e-4f);
            Assert.Greater(Mathf.Abs(first.x), 4f);
        }
    }
}
