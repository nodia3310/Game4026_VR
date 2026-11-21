using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>Attack slots: how many may attack at once, elites cost more, and only enemies in front of the player.</summary>
    public sealed class AttackTokensTests
    {
        private ScriptableObject a, b, c;

        [SetUp]
        public void SetUp()
        {
            AttackTokens.Reset();
            a = ScriptableObject.CreateInstance<EnemyData>();
            b = ScriptableObject.CreateInstance<EnemyData>();
            c = ScriptableObject.CreateInstance<EnemyData>();
        }

        [TearDown]
        public void TearDown()
        {
            AttackTokens.Reset();
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
        }

        [Test]
        public void TwoSlots_ThirdAttackerWaits_UntilOneIsReleased()
        {
            Assert.IsTrue(AttackTokens.TryTake(a, 1));
            Assert.IsTrue(AttackTokens.TryTake(b, 1));
            Assert.IsFalse(AttackTokens.TryTake(c, 1));
            Assert.IsTrue(AttackTokens.TryTake(a, 1), "Holding already counts.");
            Assert.AreEqual(2, AttackTokens.Used);

            AttackTokens.Release(a);
            AttackTokens.Release(a); // twice is harmless
            Assert.AreEqual(1, AttackTokens.Used);
            Assert.IsTrue(AttackTokens.TryTake(c, 1));
        }

        [Test]
        public void Elite_TakesBothSlots_AndCanAttackAloneEvenIfItCostsMore()
        {
            Assert.IsTrue(AttackTokens.TryTake(a, 2));
            Assert.IsFalse(AttackTokens.HasRoom(b, 1), "Nobody joins the elite.");
            AttackTokens.Release(a);

            Assert.IsTrue(AttackTokens.TryTake(b, 1));
            Assert.IsFalse(AttackTokens.HasRoom(a, 2), "The elite waits while another enemy attacks.");
            AttackTokens.Release(b);
            Assert.IsTrue(AttackTokens.TryTake(a, 5), "Alone, the cost never blocks.");
        }

        [Test]
        public void Disabled_EveryoneMayAttack()
        {
            AttackTokens.Enabled = false;
            Assert.IsTrue(AttackTokens.TryTake(a, 1));
            Assert.IsTrue(AttackTokens.TryTake(b, 1));
            Assert.IsTrue(AttackTokens.TryTake(c, 1));
            Assert.AreEqual(0, AttackTokens.Used);
        }

        [Test]
        public void InView_OnlyInsideTheConeWherePlayerLooks()
        {
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Assert.IsTrue(AttackTokens.InView(new Vector3(0f, 0f, 3f), head, Vector3.forward, 70f));
            Assert.IsTrue(AttackTokens.InView(new Vector3(2f, 0f, 2f), head, Vector3.forward, 70f), "45 degrees is inside.");
            Assert.IsFalse(AttackTokens.InView(new Vector3(3f, 0f, 0f), head, Vector3.forward, 70f), "Beside the player.");
            Assert.IsFalse(AttackTokens.InView(new Vector3(0f, 0f, -3f), head, Vector3.forward, 70f), "Behind the player.");
            Assert.IsTrue(AttackTokens.InView(new Vector3(0f, 0f, -3f), head, Vector3.forward, 180f));
            Assert.Greater(AttackTokens.AngleFromView(new Vector3(3f, 0f, 0f), head, Vector3.forward), 0f, "Right is positive.");
        }
    }
}
