using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class EnemyHealthModelTests
    {
        private const float MaxHealth = 100f;
        private const float Multiplier = 2.5f;
        private const float Cooldown = 0.3f;
        private const float StaggerThreshold = 25f;

        private static EnemyHealthModel CreateModel() =>
            new EnemyHealthModel(MaxHealth, Multiplier, Cooldown, StaggerThreshold);

        [Test]
        public void BodyHit_AppliesBaseDamage()
        {
            var model = CreateModel();

            Assert.IsTrue(model.TryApplyDamage(10f, Vector3.zero, false, 0f, out var result));
            Assert.AreEqual(10f, result.AppliedDamage, 1e-4f);
            Assert.AreEqual(90f, model.CurrentHealth, 1e-4f);
            Assert.IsFalse(result.IsWeakPoint);
        }

        [Test]
        public void WeakPointHit_AppliesMultiplierExactlyOnce()
        {
            var model = CreateModel();

            model.TryApplyDamage(10f, Vector3.zero, true, 0f, out var result);

            Assert.AreEqual(25f, result.AppliedDamage, 1e-4f);
            Assert.AreEqual(75f, model.CurrentHealth, 1e-4f);
            Assert.IsTrue(result.IsWeakPoint);
        }

        [Test]
        public void HitWithinCooldown_IsIgnored()
        {
            var model = CreateModel();
            model.TryApplyDamage(10f, Vector3.zero, false, 1f, out _);

            Assert.IsFalse(model.TryApplyDamage(10f, Vector3.zero, false, 1f + Cooldown * 0.5f, out _));
            Assert.AreEqual(90f, model.CurrentHealth, 1e-4f);
        }

        [Test]
        public void HitAfterCooldown_IsApplied()
        {
            var model = CreateModel();
            model.TryApplyDamage(10f, Vector3.zero, false, 1f, out _);

            Assert.IsTrue(model.TryApplyDamage(10f, Vector3.zero, false, 1f + Cooldown + 0.001f, out _));
            Assert.AreEqual(80f, model.CurrentHealth, 1e-4f);
        }

        [Test]
        public void IgnoredHit_DoesNotRestartCooldown()
        {
            var model = CreateModel();
            model.TryApplyDamage(10f, Vector3.zero, false, 0f, out _);
            model.TryApplyDamage(10f, Vector3.zero, false, 0.2f, out _); // ignored

            Assert.IsTrue(model.TryApplyDamage(10f, Vector3.zero, false, 0.31f, out _));
        }

        [Test]
        public void LethalHit_KillsOnce_AndClampsAppliedDamage()
        {
            var model = CreateModel();
            model.TryApplyDamage(90f, Vector3.zero, false, 0f, out _);

            Assert.IsTrue(model.TryApplyDamage(50f, Vector3.zero, false, 1f, out var lethal));
            Assert.IsTrue(lethal.Killed);
            Assert.AreEqual(10f, lethal.AppliedDamage, 1e-4f);
            Assert.AreEqual(0f, model.CurrentHealth);
            Assert.IsTrue(model.IsDead);

            Assert.IsFalse(model.TryApplyDamage(50f, Vector3.zero, false, 2f, out _), "Dead enemies ignore further hits.");
        }

        [Test]
        public void LethalHit_DoesNotStagger()
        {
            var model = CreateModel();
            model.TryApplyDamage(200f, Vector3.zero, true, 0f, out var result);

            Assert.IsTrue(result.Killed);
            Assert.IsFalse(result.CausedStagger);
        }

        [TestCase(24.9f, false, false)]
        [TestCase(25f, false, true)]
        [TestCase(1f, true, true)]
        public void Stagger_WhenDamageAtThresholdOrWeakPoint(float damage, bool weakPoint, bool expected)
        {
            var model = CreateModel();
            model.TryApplyDamage(damage, Vector3.zero, weakPoint, 0f, out var result);

            Assert.AreEqual(expected, result.CausedStagger);
        }

        [Test]
        public void Stagger_UsesDamageAfterWeakPointScaling()
        {
            var model = new EnemyHealthModel(MaxHealth, Multiplier, Cooldown, staggerThreshold: 20f);
            model.TryApplyDamage(10f, Vector3.zero, true, 0f, out var result); // 25 >= 20

            Assert.IsTrue(result.CausedStagger);
        }

        [Test]
        public void Invulnerable_IgnoresDamage()
        {
            var model = CreateModel();
            model.IsInvulnerable = true;

            Assert.IsFalse(model.TryApplyDamage(10f, Vector3.zero, false, 0f, out _));
            Assert.AreEqual(MaxHealth, model.CurrentHealth);
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDamage_IsIgnored(float damage)
        {
            var model = CreateModel();

            Assert.IsFalse(model.TryApplyDamage(damage, Vector3.zero, false, 0f, out _));
            Assert.AreEqual(MaxHealth, model.CurrentHealth);
        }

        [Test]
        public void Reset_RestoresHealth_AndClearsDeathCooldownAndInvulnerability()
        {
            var model = CreateModel();
            model.TryApplyDamage(500f, Vector3.zero, false, 5f, out _);
            model.IsInvulnerable = true;

            model.Reset();

            Assert.AreEqual(MaxHealth, model.CurrentHealth);
            Assert.IsFalse(model.IsDead);
            Assert.IsFalse(model.IsInvulnerable);
            Assert.IsTrue(model.TryApplyDamage(10f, Vector3.zero, false, 5f, out _), "Cooldown from the previous life must not carry over.");
        }

        [Test]
        public void DamageMultiplier_ScalesAllDamage_WithWeakPoint_AndResets()
        {
            var m = new EnemyHealthModel(100f, 2.5f, 0f, 1000f);
            m.DamageMultiplier = 1.5f;
            m.TryApplyDamage(10f, Vector3.zero, false, 0f, out EnemyDamageResult body);
            m.TryApplyDamage(10f, Vector3.zero, true, 1f, out EnemyDamageResult weak);

            Assert.AreEqual(15f, body.AppliedDamage, 1e-4f);
            Assert.AreEqual(37.5f, weak.AppliedDamage, 1e-4f);
            m.Reset();
            Assert.AreEqual(1f, m.DamageMultiplier);
        }

        [Test]
        public void DamageMultiplier_InvalidValues_AreSafe()
        {
            var m = new EnemyHealthModel(100f, 2.5f, 0f, 1000f);
            m.DamageMultiplier = float.NaN;
            Assert.AreEqual(1f, m.DamageMultiplier);
            m.DamageMultiplier = -3f;
            Assert.AreEqual(0f, m.DamageMultiplier);
        }
    }
}
