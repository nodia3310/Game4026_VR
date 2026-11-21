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

        // ---- Body weapon (statues): the whole body can be parried, and still takes damage otherwise ----

        private void MakeBodyWeapon()
        {
            var so = new UnityEditor.SerializedObject(blade.GetComponent<EnemyWeapon>());
            so.FindProperty("passDamageWhenNotParrying").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void BodyWeapon_OutsideWindow_MeleeDamagesEnemy()
        {
            MakeBodyWeapon();
            melee.ApplyHit(Hit(3f)); // 24 damage, no attack running
            Assert.AreEqual(76f, health.CurrentHealth, 1e-4f);
            Assert.AreEqual(0, parries);
        }

        [Test]
        public void BodyWeapon_InsideWindow_ParriesInsteadOfDamage()
        {
            MakeBodyWeapon();
            StartSwingAndWait(0.5f);
            melee.ApplyHit(Hit(3f));
            Assert.AreEqual(1, parries);
            Assert.AreEqual(100f, health.CurrentHealth);
        }

        [Test]
        public void BodyWeapon_Arrow_DamagesButNeverParries()
        {
            MakeBodyWeapon();
            StartSwingAndWait(0.5f);
            Assert.IsTrue(arrow.ApplyImpact(blade, blade.transform.position, 30f));
            Assert.AreEqual(0, parries);
            Assert.IsTrue(attack.IsAttacking);
            Assert.Less(health.CurrentHealth, 100f);
        }

        [Test]
        public void Arrow_OnFlyingBolt_DoesNotReflect()
        {
            var boltGo = new GameObject("Bolt");
            try
            {
                var boltCollider = boltGo.AddComponent<SphereCollider>();
                var bolt = boltGo.AddComponent<EnemyProjectile>();
                bolt.Launch(null, Vector3.zero, Vector3.forward, 7f, 10f);
                Assert.IsFalse(arrow.ApplyImpact(boltCollider, Vector3.zero, 30f));
                Assert.IsFalse(bolt.IsReflected);
                Assert.IsTrue(melee.ApplyHit(new MeleeHit { Collider = boltCollider, Target = boltGo, Point = Vector3.zero, Speed = 3f }));
                Assert.IsTrue(bolt.IsReflected, "Melee reflects it.");
            }
            finally { Object.DestroyImmediate(boltGo); }
        }

        private sealed class ReactingTarget : MonoBehaviour, Game4026.Contracts.IDamageable
        {
            public void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint) =>
                HitImpacts.Raise(new HitImpact(HitKind.Parry, hitPoint, 0.1f, 0.8f, 0.1f));
        }

        [Test]
        public void MeleeHit_RumblesWithTheReactionItCaused()
        {
            var go = new GameObject("Reacting");
            try
            {
                var col = go.AddComponent<SphereCollider>();
                go.AddComponent<ReactingTarget>();
                Assert.IsTrue(melee.ApplyHit(new MeleeHit { Collider = col, Target = go, Point = Vector3.zero, Speed = 3f }));
                Assert.AreEqual(0.8f, melee.LastHapticAmplitude, 1e-5f);

                melee.ApplyHit(Hit(3f)); // blade, no attack running: blocked, no feedback component here
                Assert.AreEqual(0f, melee.LastHapticAmplitude);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
