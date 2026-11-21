using System.Collections;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>A real enemy fires a pooled bolt: unreflected it hits the player, reflected it hits the shooter's weak point.</summary>
    public sealed class ProjectilePlayModeTests
    {
        private sealed class Receiver : MonoBehaviour, IPlayerDamageReceiver
        {
            public int Hits;
            public void ApplyPlayerDamage(float damage) => Hits++;
        }

        private PlayModeWorld world;
        private Receiver receiver;
        private EnemyController enemy;

        [SetUp]
        public void SetUp() => world = new PlayModeWorld();

        [TearDown]
        public void TearDown()
        {
            foreach (EnemyProjectile p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsInactive.Include))
                if (p.IsFlying) p.Step(100f); // end live bolts so the pool count resets
            world.Dispose();
        }

        private IEnumerator SpawnShooter()
        {
            var playerGo = world.Track(new GameObject("Player"));
            playerGo.transform.position = new Vector3(0f, 1.6f, 7f);
            receiver = playerGo.AddComponent<Receiver>();
            playerGo.AddComponent<PlayerTarget>();

            // Bolt prefab built in code (inactive template).
            var boltGo = world.Track(new GameObject("BoltTemplate"));
            boltGo.SetActive(false);
            var col = boltGo.AddComponent<CapsuleCollider>();
            col.direction = 2; col.radius = 0.12f; col.height = 0.6f;
            var boltPrefab = boltGo.AddComponent<EnemyProjectile>();

            EnemyData data = world.Data("{\"spawnDuration\":0.1,\"maxHealth\":100,\"staggerThreshold\":1000}");
            AttackDefinition laser = world.Track(AttackDefinition.Create(AttackType.Projectile, telegraph: 0.1f, hitTime: 0.2f,
                minRange: 2f, maxRange: 14f, cooldown: 30f, damage: 10f, projectile: boltPrefab, projectileSpeed: 8f, parryWindow: 0f));
            data.SetAttacks(new[] { laser }, 0f);

            EnemyController template = world.Template("Shooter", data);
            var eye = new GameObject("Eye");
            eye.transform.SetParent(template.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.5f, 0.5f);
            var so = new UnityEditor.SerializedObject(template);
            so.FindProperty("eye").objectReferenceValue = eye.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            var weak = new GameObject("WeakPoint");
            weak.transform.SetParent(template.transform, false);
            weak.transform.localPosition = new Vector3(0f, 1.6f, 0.45f); // sticks out of the body capsule
            weak.AddComponent<SphereCollider>().radius = 0.2f;
            weak.AddComponent<WeakPoint>();

            enemy = world.Pool.Spawn(template, Vector3.zero, Quaternion.identity);
            yield return null;
        }

        private IEnumerator WaitForBolt(System.Action<EnemyProjectile> onFired)
        {
            EnemyProjectile fired = null;
            enemy.Attack.ProjectileFired += (_, b) => fired = b;
            float deadline = Time.time + 4f;
            while (fired == null && Time.time < deadline)
                yield return null;
            Assert.IsNotNull(fired, "The enemy should fire its bolt.");
            onFired(fired);
        }

        [UnityTest]
        public IEnumerator Unreflected_HitsPlayer()
        {
            yield return SpawnShooter();
            EnemyProjectile bolt = null;
            yield return WaitForBolt(b => bolt = b);

            float deadline = Time.time + 3f;
            while (bolt.IsFlying && Time.time < deadline)
                yield return null;
            Assert.AreEqual(1, receiver.Hits);
            Assert.AreEqual(100f, enemy.Health.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator Reflected_HitsShootersWeakPoint()
        {
            yield return SpawnShooter();
            EnemyProjectile bolt = null;
            yield return WaitForBolt(b => bolt = b);
            yield return new WaitForSeconds(0.2f);

            bolt.TakeDamage(15f, bolt.transform.position, false); // the player's melee weapon
            Assert.IsTrue(bolt.IsReflected);

            float deadline = Time.time + 3f;
            while (bolt.IsFlying && Time.time < deadline)
                yield return null;
            Assert.AreEqual(0, receiver.Hits);
            Assert.AreEqual(100f - 20f * 2.5f, enemy.Health.CurrentHealth, 1e-3f, "Reflected damage 20 × weak point 2.5.");
        }
    }
}
