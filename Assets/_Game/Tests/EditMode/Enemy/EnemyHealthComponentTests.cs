using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>
    /// Simulates A's weapon call path without any XR components:
    /// hit collider -> GetComponentInParent&lt;IDamageable&gt;() -> TakeDamage.
    /// </summary>
    public sealed class EnemyHealthComponentTests
    {
        private GameObject root;
        private EnemyData data;
        private EnemyHealth health;
        private Collider bodyCollider;
        private Collider weakPointCollider;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance<EnemyData>(); // spec defaults: 100 HP, x2.5, 0.3 s cooldown

            root = new GameObject("Enemy");
            bodyCollider = root.AddComponent<CapsuleCollider>();
            health = root.AddComponent<EnemyHealth>();
            health.Initialize(data);

            var head = new GameObject("Head_WeakPoint");
            head.transform.SetParent(root.transform, false);
            weakPointCollider = head.AddComponent<SphereCollider>();
            head.AddComponent<WeakPoint>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(data);
        }

        private static IDamageable ResolveLikeWeapon(Collider hit) => hit.GetComponentInParent<IDamageable>();

        [Test]
        public void BodyCollider_ResolvesToEnemyHealth_AndAppliesBaseDamage()
        {
            IDamageable target = ResolveLikeWeapon(bodyCollider);

            Assert.AreSame(health, target);
            target.TakeDamage(10f, bodyCollider.transform.position, false);
            Assert.AreEqual(90f, health.CurrentHealth, 1e-4f);
        }

        [Test]
        public void WeakPointCollider_ResolvesToWeakPoint_AndAppliesMultiplier()
        {
            IDamageable target = ResolveLikeWeapon(weakPointCollider);

            Assert.IsInstanceOf<WeakPoint>(target);
            target.TakeDamage(10f, weakPointCollider.transform.position, false); // A always passes false
            Assert.AreEqual(75f, health.CurrentHealth, 1e-4f);
        }

        [Test]
        public void WeakPoint_ForwardsIsWeakPointTrue_ToDamagedEvent()
        {
            EnemyDamageResult received = default;
            health.Damaged += (_, r) => received = r;

            ResolveLikeWeapon(weakPointCollider).TakeDamage(10f, Vector3.one, false);

            Assert.IsTrue(received.IsWeakPoint);
            Assert.IsTrue(received.CausedStagger);
            Assert.AreEqual(25f, received.AppliedDamage, 1e-4f);
            Assert.AreEqual(Vector3.one, received.HitPoint);
        }

        [Test]
        public void Died_FiresExactlyOnce()
        {
            int deaths = 0;
            health.Died += _ => deaths++;

            health.ApplyDamage(60f, Vector3.zero, false, 0f);
            health.ApplyDamage(60f, Vector3.zero, false, 1f);
            health.ApplyDamage(60f, Vector3.zero, false, 2f);
            health.ApplyDamage(60f, Vector3.zero, true, 3f);

            Assert.AreEqual(1, deaths);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void ResetState_RestoresHealth_AndAllowsDyingAgain()
        {
            int deaths = 0;
            health.Died += _ => deaths++;
            health.ApplyDamage(500f, Vector3.zero, false, 0f);

            health.ResetState();

            Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
            Assert.IsFalse(health.IsDead);

            health.ApplyDamage(500f, Vector3.zero, false, 0f);
            Assert.AreEqual(2, deaths);
        }

        [Test]
        public void IgnoredHit_DoesNotRaiseDamaged()
        {
            int hits = 0;
            health.Damaged += (_, __) => hits++;

            health.ApplyDamage(10f, Vector3.zero, false, 0f);
            health.ApplyDamage(10f, Vector3.zero, false, 0.1f); // within cooldown

            Assert.AreEqual(1, hits);
        }
    }
}
