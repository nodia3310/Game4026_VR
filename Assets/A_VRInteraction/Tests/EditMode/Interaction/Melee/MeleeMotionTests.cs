using System;
using Game4026.Interaction.Melee;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Interaction.Melee
{
    public class MeleeMotionTests
    {
        [Test]
        public void RigTranslationAndRotation_DoNotBecomeWeaponMotion()
        {
            var oldRig = new Vector3(2, 0, 3);
            var oldRotation = Quaternion.Euler(0, 20, 0);
            var newRig = new Vector3(8, 1, -3);
            var newRotation = Quaternion.Euler(0, 80, 0);
            var handInRig = new Vector3(0.4f, 1.2f, 0.5f);
            var oldHand = oldRig + oldRotation * handInRig;
            var newHand = newRig + newRotation * handInRig;
            var rebased = MeleeMotion.RebasePoint(oldHand, oldRig, oldRotation, newRig, newRotation);
            Assert.That(Vector3.Distance(rebased, newHand), Is.LessThan(0.00001f));
        }

        [Test]
        public void HandMotionIsPreservedAfterRigCompensation()
        {
            var rotation = Quaternion.Euler(0, 90, 0);
            var rebased = MeleeMotion.RebasePoint(Vector3.forward, Vector3.zero, Quaternion.identity,
                Vector3.right * 10, rotation);
            var current = Vector3.right * 10 + rotation * (Vector3.forward + Vector3.up * 0.2f);
            Assert.That(Vector3.Distance(rebased, current) / 0.02f, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void EnergyAndForce_HaveSpecifiedUnitsAndScaling()
        {
            Assert.That(MeleeMotion.KineticEnergy(2, 3), Is.EqualTo(9));
            Assert.That(MeleeMotion.EstimatedStoppingForce(2, 3, 0.05f), Is.EqualTo(120).Within(0.001f));
            Assert.That(MeleeMotion.KineticEnergy(2, 6), Is.EqualTo(36));
        }

        [Test]
        public void InvalidPhysicalInputs_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MeleeMotion.KineticEnergy(-1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeleeMotion.KineticEnergy(1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeleeMotion.EstimatedStoppingForce(1, 2, 0));
        }

        [Test]
        public void Swing_DeduplicatesEachTargetUntilSlowedAndRearmed()
        {
            var window = new MeleeSwingWindow(1.2f, 0.4f, 0.6f);
            Assert.That(window.Advance(4, 0.02f), Is.False, "A newly grabbed weapon must settle first.");
            window.Advance(0, 0.02f);
            Assert.That(window.Advance(4, 0.02f), Is.True);
            Assert.That(window.TryRegisterTarget(10), Is.True);
            Assert.That(window.TryRegisterTarget(10), Is.False);
            Assert.That(window.TryRegisterTarget(11), Is.True);
            window.Advance(0.2f, 0.02f);
            window.Advance(4, 0.02f);
            Assert.That(window.TryRegisterTarget(10), Is.True);
            Assert.That(window.SwingId, Is.EqualTo(2));
        }

        [Test]
        public void TimedOutSwing_DoesNotReopenWhileHeldAboveThreshold()
        {
            var window = new MeleeSwingWindow(1.2f, 0.4f, 0.1f);
            window.Advance(0, 0.02f);
            window.Advance(4, 0.02f);
            Assert.That(window.Advance(4, 0.11f), Is.False);
            Assert.That(window.Advance(4, 0.02f), Is.False);
            Assert.That(window.TryRegisterTarget(10), Is.False);
            window.Advance(0.2f, 0.02f);
            Assert.That(window.Advance(4, 0.02f), Is.True);
        }

        [Test]
        public void DiscontinuityReset_RequiresSettlingBeforeAnotherAttack()
        {
            var window = new MeleeSwingWindow(1.2f, 0.4f, 0.6f);
            window.Advance(0, 0.02f);
            window.Advance(3, 0.02f);
            window.Reset();
            Assert.That(window.Advance(3, 0.02f), Is.False);
            Assert.That(window.Advance(float.PositiveInfinity, 0.02f), Is.False);
            Assert.That(window.Advance(3, 0), Is.False);
        }
    }
}
