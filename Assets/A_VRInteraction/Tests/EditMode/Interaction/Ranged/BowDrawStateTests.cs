using Game4026.Interaction.Ranged;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Interaction.Ranged
{
    public class BowDrawStateTests
    {
        [Test]
        public void SameFrameBowDropCancelsQueuedRelease()
        {
            var gate = new BowReleaseGate();
            gate.Queue(1f); // String selectExited arrives first.
            gate.Cancel(); // Bow selectExited arrives later in the same frame.
            Assert.That(gate.TryCommit(true, out _), Is.False);
        }

        [Test]
        public void PendingReleaseCannotFireAfterTrackingLossOrTwice()
        {
            var gate = new BowReleaseGate();
            gate.Queue(1f);
            Assert.That(gate.TryCommit(false, out _), Is.False);
            Assert.That(gate.TryCommit(true, out _), Is.False);
            gate.Queue(0.5f);
            Assert.That(gate.TryCommit(true, out float power), Is.True);
            Assert.That(power, Is.EqualTo(0.5f));
            Assert.That(gate.TryCommit(true, out _), Is.False);
        }

        [Test]
        public void WrongDirectionAndLateralPullDoNotCharge()
        {
            Assert.That(BowDrawState.ProjectDraw(Vector3.forward, Vector3.back, 0.6f), Is.Zero);
            Assert.That(BowDrawState.ProjectDraw(Vector3.right, Vector3.back, 0.6f), Is.Zero);
        }

        [Test]
        public void ZeroPullDoesNotFireEvenWithZeroThreshold()
        {
            var draw = new BowDrawState(); draw.Begin();
            Assert.That(draw.TryRelease(0f, out _), Is.False);
        }

        [Test]
        public void FullPullClampsAndReleaseFiresOnce()
        {
            var draw = new BowDrawState(); draw.Begin();
            draw.Sample(Vector3.back * 2f, Vector3.back, 0.6f);
            Assert.That(draw.DrawDistance, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(draw.TryRelease(0.08f, out float power), Is.True);
            Assert.That(power, Is.EqualTo(1f));
            Assert.That(draw.TryRelease(0.08f, out _), Is.False);
        }

        [Test]
        public void CancelledPullCannotFireUntilNewCycle()
        {
            var draw = new BowDrawState(); draw.Begin();
            draw.Sample(Vector3.back * 0.4f, Vector3.back, 0.6f);
            draw.Cancel();
            draw.Sample(Vector3.back, Vector3.back, 0.6f);
            Assert.That(draw.TryRelease(0.08f, out _), Is.False);
            draw.Begin();
            Assert.That(draw.DrawDistance, Is.Zero);
        }

        [Test]
        public void SmallPullIsConsumedWithoutShooting()
        {
            var draw = new BowDrawState(); draw.Begin();
            draw.Sample(Vector3.back * 0.02f, Vector3.back, 0.6f);
            Assert.That(draw.TryRelease(0.08f, out _), Is.False);
            Assert.That(draw.IsDrawing, Is.False);
        }

        [Test]
        public void DrawUsesProjectionNotHandTravelLength()
        {
            Assert.That(BowDrawState.ProjectDraw(new Vector3(0.5f, 0.5f, -0.3f), Vector3.back, 0.6f),
                Is.EqualTo(0.3f).Within(0.00001f));
        }

        [Test]
        public void BallisticsIncludesGravityAndInitialVelocity()
        {
            var point = BowDrawState.BallisticPosition(Vector3.one, Vector3.forward * 10f, Vector3.down * 10f, 2f);
            Assert.That(point, Is.EqualTo(new Vector3(1f, -19f, 21f)));
        }

        [Test]
        public void NonFiniteSampleDoesNotCharge()
        {
            Assert.That(BowDrawState.ProjectDraw(new Vector3(float.NaN, 0, 0), Vector3.back, 0.6f), Is.Zero);
        }
    }
}
