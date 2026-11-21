using System.Collections;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>Real EnemyController: a parry through the enemy's weapon staggers it and opens the ×1.5 counter window.</summary>
    public sealed class ParryPlayModeTests
    {
        private sealed class Receiver : MonoBehaviour, IPlayerDamageReceiver
        {
            public int Hits;
            public void ApplyPlayerDamage(float damage) => Hits++;
        }

        private PlayModeWorld world;

        [SetUp]
        public void SetUp() => world = new PlayModeWorld();

        [TearDown]
        public void TearDown() => world.Dispose();

        [UnityTest]
        public IEnumerator ParryInWindow_StaggersForParryDuration_WithDamageBonus_ThenNormal()
        {
            var playerGo = world.Track(new GameObject("Player"));
            playerGo.transform.position = new Vector3(0f, 1.6f, 1.2f);
            var receiver = playerGo.AddComponent<Receiver>();
            playerGo.AddComponent<PlayerTarget>();

            // Slow swing (hit 0.7 s, window 0.35 s) so the test can wait for the window comfortably.
            EnemyData data = world.Data("{\"spawnDuration\":0.1,\"staggerThreshold\":1000,\"parryStaggerDuration\":1.0,\"parriedDamageMultiplier\":1.5}");
            AttackDefinition swing = world.Track(AttackDefinition.Create(hitTime: 0.7f, cooldown: 10f, parryWindow: 0.35f));
            data.SetAttacks(new[] { swing }, 0f);
            EnemyController template = world.Template("Enemy", data);
            var dagger = new GameObject("Dagger");
            dagger.transform.SetParent(template.transform, false);
            dagger.AddComponent<BoxCollider>();
            dagger.AddComponent<EnemyWeapon>();

            EnemyController enemy = world.Pool.Spawn(template, Vector3.zero, Quaternion.identity);
            EnemyWeapon weapon = enemy.GetComponentInChildren<EnemyWeapon>();
            EnemyHealth health = enemy.Health;
            int parried = 0;
            enemy.Parried += (_, __) => parried++;

            float deadline = Time.time + 4f;
            while (Time.time < deadline && !enemy.Attack.IsParryWindowOpen)
                yield return null;
            Assert.IsTrue(enemy.Attack.IsParryWindowOpen, "Parry window should open during the swing.");

            weapon.TakeDamage(5f, weapon.transform.position, false); // what A's MeleeWeapon does via the bridge

            Assert.AreEqual(1, parried);
            Assert.AreEqual(EnemyStateId.Hit, enemy.CurrentState);
            Assert.AreEqual(1.5f, health.DamageMultiplier, 1e-4f, "Counter window active from the first frame.");
            Assert.AreEqual(100f, health.CurrentHealth, "The blade never damages the body.");

            float before = health.CurrentHealth;
            health.ApplyDamage(10f, Vector3.zero, false, Time.time + 5f);
            Assert.AreEqual(15f, before - health.CurrentHealth, 1e-4f);

            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(EnemyStateId.Hit, enemy.CurrentState, "Still staggered (1.0 s parry stagger).");
            yield return new WaitForSeconds(0.4f);
            Assert.AreNotEqual(EnemyStateId.Hit, enemy.CurrentState);
            Assert.AreEqual(1f, health.DamageMultiplier, 1e-4f, "Bonus ends with the stagger.");
            Assert.AreEqual(0, receiver.Hits, "The parried swing never landed.");
        }
    }
}
