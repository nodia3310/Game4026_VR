using Game4026.Bridges;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game4026.Tests.Bridges
{
    /// <summary>The enlarged parry zone: a held player weapon near the blade parries inside the window only.</summary>
    public sealed class ParryAssistTests
    {
        private GameObject enemy, playerWeapon;
        private EnemyData data;
        private EnemyAttack attack;
        private ParryAssist right, left;
        private AttackDefinition swing, mirroredSwing;
        private int parries;
        private bool held;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            enemy = new GameObject("Enemy");
            enemy.AddComponent<EnemyHealth>().Initialize(data);
            attack = enemy.AddComponent<EnemyAttack>();
            attack.Parried += (_, __, ___) => parries++;

            right = Dagger("RightDagger", new Vector3(1f, 1f, 0f), leftHand: false);
            left = Dagger("LeftDagger", new Vector3(-1f, 1f, 0f), leftHand: true);

            playerWeapon = new GameObject("PlayerWeapon");
            playerWeapon.AddComponent<BoxCollider>().size = Vector3.one * 0.05f;

            held = true;
            right.IsHeldPlayerWeapon = c => held && c.gameObject == playerWeapon;
            left.IsHeldPlayerWeapon = right.IsHeldPlayerWeapon;

            // Hit at 0.7 s, parry window 0.35 s → open from 0.35 s.
            swing = AttackDefinition.Create(parryWindow: 0.35f);
            mirroredSwing = AttackDefinition.Create(parryWindow: 0.35f, mirror: true);
            parries = 0;
        }

        private ParryAssist Dagger(string name, Vector3 position, bool leftHand)
        {
            var dagger = new GameObject(name);
            dagger.transform.SetParent(enemy.transform, false);
            dagger.transform.localPosition = position;
            var capsule = dagger.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = 0.07f;
            capsule.height = 0.5f;
            var weapon = dagger.AddComponent<EnemyWeapon>();
            var so = new SerializedObject(weapon);
            so.FindProperty("leftHand").boolValue = leftHand;
            so.ApplyModifiedPropertiesWithoutUndo();
            return dagger.AddComponent<ParryAssist>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(playerWeapon);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(swing);
            Object.DestroyImmediate(mirroredSwing);
        }

        private float StartSwingAndWait(AttackDefinition move, float seconds)
        {
            attack.Begin(data, 0f, move);
            float t = 0f;
            while (t < seconds - 1e-5f) { t += 0.01f; attack.Tick(t, 0.01f, null); }
            return t;
        }

        private void PlaceWeapon(Vector3 position)
        {
            playerWeapon.transform.position = position;
            Physics.SyncTransforms();
        }

        [Test]
        public void HeldWeaponNearBlade_InsideWindow_Parries()
        {
            float now = StartSwingAndWait(swing, 0.5f);
            PlaceWeapon(new Vector3(1f, 1.2f, 0f)); // 0.2 m from the blade axis: outside the blade, inside the zone

            Assert.IsTrue(right.TryAssist(now));
            Assert.AreEqual(1, parries);
            Assert.IsFalse(attack.IsAttacking);
        }

        [Test]
        public void HeldWeaponPastTip_InsideWindow_Parries()
        {
            float now = StartSwingAndWait(swing, 0.5f);
            PlaceWeapon(new Vector3(1f, 1f, 0.35f)); // blade ends at 0.25 m; zone reaches further

            Assert.IsTrue(right.TryAssist(now));
        }

        [Test]
        public void BeforeWindow_NoParry()
        {
            float now = StartSwingAndWait(swing, 0.2f);
            PlaceWeapon(new Vector3(1f, 1.2f, 0f));

            Assert.IsFalse(right.TryAssist(now));
            Assert.AreEqual(0, parries);
            Assert.IsTrue(attack.IsAttacking);
        }

        [Test]
        public void WeaponOutsideZone_NoParry()
        {
            float now = StartSwingAndWait(swing, 0.5f);
            PlaceWeapon(new Vector3(1f, 1.5f, 0f));

            Assert.IsFalse(right.TryAssist(now));
            Assert.AreEqual(0, parries);
        }

        [Test]
        public void WeaponNotHeld_NoParry()
        {
            float now = StartSwingAndWait(swing, 0.5f);
            PlaceWeapon(new Vector3(1f, 1.2f, 0f));
            held = false;

            Assert.IsFalse(right.TryAssist(now));
            Assert.AreEqual(0, parries);
        }

        [Test]
        public void SmallerPadding_ShrinksZone()
        {
            float now = StartSwingAndWait(swing, 0.5f);
            PlaceWeapon(new Vector3(1f, 1.2f, 0f));
            right.RadiusPadding = 0.02f;

            Assert.IsFalse(right.TryAssist(now));
        }

        [Test]
        public void DualWield_OnlySwingingHandAssists()
        {
            float now = StartSwingAndWait(swing, 0.5f); // right-hand step
            PlaceWeapon(new Vector3(-1f, 1.2f, 0f)); // next to the idle left dagger

            Assert.IsFalse(left.TryAssist(now));
            Assert.AreEqual(0, parries);
        }

        [Test]
        public void WeaponOnBone_UsesChildColliders_NotWeakPoints()
        {
            var bone = new GameObject("WingBone");
            bone.transform.SetParent(enemy.transform, false);
            bone.transform.localPosition = new Vector3(0f, 3f, 0f);
            bone.AddComponent<EnemyWeapon>();
            var wing = new GameObject("WingCollider");
            wing.transform.SetParent(bone.transform, false);
            wing.AddComponent<SphereCollider>().radius = 0.1f;
            var weak = new GameObject("WeakPoint");
            weak.transform.SetParent(bone.transform, false);
            weak.transform.localPosition = new Vector3(0f, 2f, 0f);
            weak.AddComponent<SphereCollider>().radius = 0.1f;
            weak.AddComponent<WeakPoint>();
            var assist = bone.AddComponent<ParryAssist>();
            assist.IsHeldPlayerWeapon = right.IsHeldPlayerWeapon;
            float now = StartSwingAndWait(swing, 0.5f);

            PlaceWeapon(new Vector3(0f, 5.2f, 0f)); // next to the weak point only
            Assert.IsFalse(assist.TryAssist(now));
            PlaceWeapon(new Vector3(0f, 3.2f, 0f)); // next to the wing
            Assert.IsTrue(assist.TryAssist(now));
        }

        [Test]
        public void DualWield_MirroredStep_LeftHandAssists()
        {
            float now = StartSwingAndWait(mirroredSwing, 0.5f);
            PlaceWeapon(new Vector3(-1f, 1.2f, 0f));

            Assert.IsFalse(right.TryAssist(now));
            Assert.IsTrue(left.TryAssist(now));
            Assert.AreEqual(1, parries);
        }
    }
}
