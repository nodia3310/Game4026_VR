using System.Collections.Generic;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class AttackSelectorTests
    {
        private readonly List<Object> created = new List<Object>();
        private AttackSelector selector;

        [SetUp]
        public void SetUp() => selector = new AttackSelector();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        private AttackDefinition Make(float min, float max, float weight = 1f, float cooldown = 2f,
            AttackType type = AttackType.Melee, AttackDefinition[] combo = null)
        {
            AttackDefinition a = AttackDefinition.Create(type, minRange: min, maxRange: max, weight: weight,
                cooldown: cooldown, combo: combo);
            created.Add(a);
            return a;
        }

        [Test]
        public void Select_OnlyAttacksWhoseRangeContainsTheDistance()
        {
            var attacks = new[] { Make(0f, 1.8f), Make(3f, 5f) };

            Assert.AreEqual(0, selector.Select(attacks, 1f, 0f, 0.99f));
            Assert.AreEqual(1, selector.Select(attacks, 4f, 0f, 0f));
            Assert.AreEqual(-1, selector.Select(attacks, 2.5f, 0f, 0.5f), "Gap between ranges.");
            Assert.AreEqual(-1, selector.Select(attacks, 6f, 0f, 0.5f));
        }

        [Test]
        public void Select_ByWeight()
        {
            var attacks = new[] { Make(0f, 2f, weight: 3f), Make(0f, 2f, weight: 1f) };

            Assert.AreEqual(0, selector.Select(attacks, 1f, 0f, 0.0f));
            Assert.AreEqual(0, selector.Select(attacks, 1f, 0f, 0.74f));
            Assert.AreEqual(1, selector.Select(attacks, 1f, 0f, 0.76f));
            Assert.AreEqual(1, selector.Select(attacks, 1f, 0f, 1.0f));
        }

        [Test]
        public void Select_WeightedDistribution_RoughlyMatchesWeights()
        {
            var attacks = new[] { Make(0f, 2f, weight: 3f), Make(0f, 2f, weight: 1f) };
            int first = 0;
            for (int i = 0; i < 1000; i++)
                if (selector.Select(attacks, 1f, 0f, i / 1000f) == 0)
                    first++;
            Assert.AreEqual(750, first, 2);
        }

        [Test]
        public void OwnCooldown_SkipsThatAttackOnly()
        {
            var attacks = new[] { Make(0f, 2f, cooldown: 3f), Make(0f, 2f, cooldown: 1f) };
            selector.MarkStarted(0, attacks[0], 10f, 1f);

            Assert.AreEqual(1, selector.Select(attacks, 1f, 12.9f, 0f));
            Assert.AreEqual(0, selector.Select(attacks, 1f, 13f, 0f));
        }

        [Test]
        public void GlobalCooldown_BlocksEverything_AfterAMoveEnds()
        {
            var attacks = new[] { Make(0f, 2f, cooldown: 0f) };
            selector.MarkEnded(10f, 1.2f, 1f);

            Assert.AreEqual(-1, selector.Select(attacks, 1f, 11.1f, 0f));
            Assert.AreEqual(0, selector.Select(attacks, 1f, 11.2f, 0f));
        }

        [Test]
        public void Cooldowns_ScaleWithSpeedMultiplier()
        {
            var attacks = new[] { Make(0f, 2f, cooldown: 4f) };
            selector.MarkStarted(0, attacks[0], 0f, 2f);
            Assert.AreEqual(-1, selector.Select(attacks, 1f, 1.9f, 0f));
            Assert.AreEqual(0, selector.Select(attacks, 1f, 2f, 0f));
        }

        [Test]
        public void UnsupportedOrEmpty_NeverSelected()
        {
            var attacks = new[]
            {
                Make(0f, 2f, type: AttackType.Projectile),
                Make(0f, 2f, type: AttackType.Charge),
                Make(0f, 2f, type: AttackType.Combo),            // no steps
                Make(0f, 2f, weight: 0f),
                null,
            };
            Assert.AreEqual(-1, selector.Select(attacks, 1f, 0f, 0.5f));
        }

        [Test]
        public void Reset_ClearsCooldowns()
        {
            var attacks = new[] { Make(0f, 2f) };
            selector.MarkStarted(0, attacks[0], 0f, 1f);
            selector.MarkEnded(0f, 5f, 1f);
            selector.Reset();
            Assert.AreEqual(0, selector.Select(attacks, 1f, 0f, 0f));
        }

        [Test]
        public void HoldRange_IsLongestPointBlankReach()
        {
            var attacks = new[] { Make(0f, 1.8f), Make(0f, 2.0f), Make(3f, 5f) };
            Assert.AreEqual(2.0f, AttackSelector.HoldRange(attacks), 1e-4f);
            Assert.AreEqual(0f, AttackSelector.HoldRange(new[] { Make(4f, 12f) }), "Ranged-only enemies never stop to wait.");
        }
    }
}
