using Game4026.Bridges;
using Game4026.Enemy;
using Game4026.Interaction.Melee;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Bridges
{
    /// <summary>
    /// End to end without XR: a hand-built MeleeHit / arrow impact goes through the bridge into a real
    /// EnemyHealth + WeakPoint (100 HP, x2.5 weak point, defaults from EnemyData).
    /// </summary>
    public sealed class DamageBridgeTests
    {
        private GameObject enemy;
        private GameObject weaponGo;
        private EnemyData data;
        private EnemyHealth health;
        private Collider body;
        private Collider weakPoint;
        private MeleeDamageBridge melee;
        private ArrowDamageBridge arrow;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            enemy = new GameObject("Enemy");
            body = enemy.AddComponent<CapsuleCollider>();
            health = enemy.AddComponent<EnemyHealth>();
            health.Initialize(data);

            var head = new GameObject("Head");
            head.transform.SetParent(enemy.transform, false);
            weakPoint = head.AddComponent<SphereCollider>();
            head.AddComponent<WeakPoint>();

            // Bridges only; no MeleeWeapon / ArrowProjectile (those need XR and physics).
            weaponGo = new GameObject("Bridges");
            melee = weaponGo.AddComponent<MeleeDamageBridge>();
            melee.Damage = ImpactDamageCurve.MeleeDefault;
            arrow = weaponGo.AddComponent<ArrowDamageBridge>();
            arrow.Damage = ImpactDamageCurve.ArrowDefault;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(weaponGo);
            Object.DestroyImmediate(data);
        }

        private static MeleeHit Hit(Collider c, float speed) =>
            new MeleeHit { Collider = c, Target = c.gameObject, Point = c.transform.position, Speed = speed };

        [Test]
        public void MeleeBodyHit_AppliesCurveDamage()
        {
            Assert.IsTrue(melee.ApplyHit(Hit(body, 3f)));      // 24
            Assert.AreEqual(76f, health.CurrentHealth, 1e-4f);
        }

        [Test]
        public void MeleeWeakPointHit_AppliesCurveThenMultiplier()
        {
            Assert.IsTrue(melee.ApplyHit(Hit(weakPoint, 3f))); // 24 * 2.5 = 60
            Assert.AreEqual(40f, health.CurrentHealth, 1e-4f);
        }

        [Test]
        public void MeleeVeryFastHit_IsCappedAtMax()
        {
            melee.ApplyHit(Hit(body, 50f));                    // capped at 40
            Assert.AreEqual(60f, health.CurrentHealth, 1e-4f);
        }

        [Test]
        public void MeleeHitOnNonDamageable_ReturnsFalse()
        {
            var wall = new GameObject("Wall");
            try
            {
                Assert.IsFalse(melee.ApplyHit(Hit(wall.AddComponent<BoxCollider>(), 3f)));
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void MeleeHitWithNullCollider_ReturnsFalse()
        {
            Assert.IsFalse(melee.ApplyHit(new MeleeHit { Speed = 3f }));
        }

        [Test]
        public void ArrowBodyImpact_AppliesCurveDamage()
        {
            Assert.IsTrue(arrow.ApplyImpact(body, Vector3.zero, 30f)); // 30
            Assert.AreEqual(70f, health.CurrentHealth, 1e-4f);
        }

        [Test]
        public void ArrowWeakPointImpact_AppliesMultiplier()
        {
            Assert.IsTrue(arrow.ApplyImpact(weakPoint, Vector3.zero, 30f)); // 30 * 2.5 = 75
            Assert.AreEqual(25f, health.CurrentHealth, 1e-4f);
        }
    }
}
