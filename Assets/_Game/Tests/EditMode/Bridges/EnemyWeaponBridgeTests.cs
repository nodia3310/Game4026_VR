using Game4026.Bridges;
using Game4026.Enemy;
using Game4026.Interaction.Melee;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Bridges
{
    /// <summary>A's melee weapon parries through the enemy's blade collider; arrows never do; the blade never damages the body.</summary>
    public sealed class EnemyWeaponBridgeTests
    {
        private GameObject enemy, bridges;
        private EnemyData data;
        private EnemyHealth health;
        private EnemyAttack attack;
        private Collider blade;
        private MeleeDamageBridge melee;
        private ArrowDamageBridge arrow;
        private AttackDefinition swing;
        private int parries;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            enemy = new GameObject("Enemy");
            enemy.AddComponent<CapsuleCollider>();
            health = enemy.AddComponent<EnemyHealth>();
            health.Initialize(data);
            attack = enemy.AddComponent<EnemyAttack>();
            attack.Parried += (_, __, ___) => parries++;

            var dagger = new GameObject("Dagger");
            dagger.transform.SetParent(enemy.transform, false);
            blade = dagger.AddComponent<BoxCollider>();
            dagger.AddComponent<EnemyWeapon>();

            bridges = new GameObject("Bridges");
            melee = bridges.AddComponent<MeleeDamageBridge>();
            melee.Damage = ImpactDamageCurve.MeleeDefault;
            arrow = bridges.AddComponent<ArrowDamageBridge>();
            arrow.Damage = ImpactDamageCurve.ArrowDefault;

            // Hit at 0.7 s, parry window 0.35 s → open from 0.35 s.
            swing = AttackDefinition.Create(parryWindow: 0.35f);
            parries = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(bridges);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(swing);
        }

        private void StartSwingAndWait(float seconds)
        {
            attack.Begin(data, 0f, swing);
            float t = 0f;
            while (t < seconds - 1e-5f) { t += 0.01f; attack.Tick(t, 0.01f, null); }
        }

        private MeleeHit Hit(float speed) =>
            new MeleeHit { Collider = blade, Target = blade.gameObject, Point = blade.transform.position, Speed = speed };

        [Test]
        public void MeleeHitOnBlade_InsideWindow_Parries_WithoutBodyDamage()
        {
            StartSwingAndWait(0.5f);
            melee.ApplyHit(Hit(3f));

            Assert.AreEqual(1, parries);
            Assert.IsFalse(attack.IsAttacking);
            Assert.AreEqual(100f, health.CurrentHealth);
        }

        [Test]
        public void MeleeHitOnBlade_BeforeWindow_IsBlocked_NoParry_NoDamage()
        {
            StartSwingAndWait(0.2f);
            melee.ApplyHit(Hit(3f));

            Assert.AreEqual(0, parries);
            Assert.IsTrue(attack.IsAttacking);
            Assert.AreEqual(100f, health.CurrentHealth);
        }

        [Test]
        public void ArrowOnBlade_NeverParries()
        {
            StartSwingAndWait(0.5f);
            Assert.IsFalse(arrow.ApplyImpact(blade, blade.transform.position, 30f));

            Assert.AreEqual(0, parries);
            Assert.IsTrue(attack.IsAttacking);
            Assert.AreEqual(100f, health.CurrentHealth);
        }
    }
}
