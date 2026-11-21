using System.Collections;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>Wing shield on a real enemy: blocks front hits, counters after two, breaks on a heavy hit, rises after a hit.</summary>
    public sealed class EnemyGuardPlayModeTests
    {
        private PlayModeWorld world;
        private AttackDefinition guardMove, counter;

        [SetUp]
        public void SetUp()
        {
            world = new PlayModeWorld();
            AttackDefinition guard = world.Track(AttackDefinition.Create(AttackType.Guard, telegraph: 3f, hitTime: 0.3f,
                poseTime: 0.3f, recovery: 0.05f, parryWindow: 0f, damage: 0f, state: "Block", name: "Guard"));
            counter = world.Track(AttackDefinition.Create(AttackType.Melee, hitTime: 0.3f, damage: 5f, name: "Counter"));
            guardMove = world.Track(AttackDefinition.Create(AttackType.Combo, combo: new[] { guard, counter }, cooldown: 0f, name: "WingGuard"));
        }

        [TearDown]
        public void TearDown() => world.Dispose();

        private EnemyController Spawn(float reactChance, out EnemyGuard guard)
        {
            EnemyData data = world.Data("{\"spawnDuration\":0.1,\"maxHealth\":100,\"staggerThreshold\":1000}");
            EnemyController template = world.Template("Birdman", data);
            template.gameObject.AddComponent<EnemyGuard>().Configure(guardMove, reactChance, 2, 30f, 0f);
            EnemyController enemy = world.Pool.Spawn(template, Vector3.zero, Quaternion.identity);
            guard = enemy.GetComponent<EnemyGuard>();
            return enemy;
        }

        private static Vector3 Front(EnemyController e) => e.transform.position + e.transform.forward * 0.5f + Vector3.up;

        private IEnumerator RaiseGuard(EnemyController enemy, EnemyGuard guard)
        {
            yield return new WaitForSeconds(0.2f); // out of spawn
            Assert.IsTrue(enemy.ForceAttack(guardMove));
            yield return null;
            Assert.IsTrue(guard.IsGuarding);
        }

        [UnityTest]
        public IEnumerator Guard_BlocksFrontHits_NotHitsFromBehind()
        {
            EnemyController enemy = Spawn(0f, out EnemyGuard guard);
            yield return RaiseGuard(enemy, guard);
            int blocked = 0;
            guard.Blocked += (_, __) => blocked++;

            enemy.Health.TakeDamage(10f, Front(enemy), false);
            Assert.AreEqual(100f, enemy.Health.CurrentHealth);
            Assert.AreEqual(1, blocked);

            Vector3 behind = enemy.transform.position - enemy.transform.forward * 0.5f + Vector3.up;
            enemy.Health.TakeDamage(10f, behind, false);
            Assert.AreEqual(90f, enemy.Health.CurrentHealth, "The wings only cover the front.");
        }

        [UnityTest]
        public IEnumerator TwoBlocks_CounterAtOnce()
        {
            EnemyController enemy = Spawn(0f, out EnemyGuard guard);
            yield return RaiseGuard(enemy, guard);
            enemy.Health.TakeDamage(10f, Front(enemy), false);
            yield return new WaitForSeconds(0.35f); // past the hit cooldown
            enemy.Health.TakeDamage(10f, Front(enemy), false);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(counter, enemy.Attack.CurrentStep, "Not the 3 s guard: the counter comes right away.");
        }

        [UnityTest]
        public IEnumerator HeavyHit_BreaksTheGuard_LongStaggerAndExtraDamage()
        {
            EnemyController enemy = Spawn(0f, out EnemyGuard guard);
            yield return RaiseGuard(enemy, guard);
            int broken = 0;
            guard.Broken += (_, __) => broken++;

            enemy.Health.TakeDamage(40f, Front(enemy), false);
            Assert.AreEqual(1, broken);
            Assert.AreEqual(100f, enemy.Health.CurrentHealth, "The breaking blow is spent on the shield.");
            Assert.AreEqual(EnemyStateId.Hit, enemy.CurrentState);
            Assert.IsFalse(enemy.Attack.IsAttacking, "The counter is cancelled too.");

            enemy.Health.TakeDamage(10f, Front(enemy), false);
            Assert.AreEqual(85f, enemy.Health.CurrentHealth, 1e-4f, "Open to 1.5x damage while staggered.");
        }

        [UnityTest]
        public IEnumerator BeingHit_RaisesTheGuard_WhenTheReactionRolls()
        {
            EnemyController enemy = Spawn(1f, out EnemyGuard guard);
            yield return new WaitForSeconds(0.2f);
            Assert.AreNotEqual(EnemyStateId.Attack, enemy.CurrentState);

            enemy.Health.TakeDamage(10f, Front(enemy), false);
            Assert.AreEqual(90f, enemy.Health.CurrentHealth, "The first blow lands.");
            Assert.AreEqual(EnemyStateId.Attack, enemy.CurrentState);
            Assert.AreEqual(guardMove, enemy.Attack.Current);
            yield return new WaitForSeconds(0.35f); // past the hit cooldown
            enemy.Health.TakeDamage(10f, Front(enemy), false);
            Assert.AreEqual(90f, enemy.Health.CurrentHealth, "The follow-up is blocked.");
        }
    }
}
