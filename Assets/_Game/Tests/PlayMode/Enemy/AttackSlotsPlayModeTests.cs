using System.Collections;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>Real enemies around a player: one attacker at a time, nobody attacks from behind.</summary>
    public sealed class AttackSlotsPlayModeTests
    {
        private PlayModeWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new PlayModeWorld();
            AttackTokens.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            AttackTokens.Reset();
        }

        private EnemyController Template()
        {
            EnemyData data = world.Data("{\"spawnDuration\":0.1,\"staggerThreshold\":1000,\"waitDistance\":3.0}");
            AttackDefinition swing = world.Track(AttackDefinition.Create(hitTime: 0.4f, recovery: 0.3f, cooldown: 0.5f, maxRange: 1.6f, damage: 1f));
            data.SetAttacks(new[] { swing }, 0.2f);
            return world.Template("Enemy", data);
        }

        private PlayerTarget Player(Vector3 lookDirection)
        {
            var go = world.Track(new GameObject("Player"));
            go.transform.position = new Vector3(0f, 1.6f, 0f);
            go.transform.rotation = Quaternion.LookRotation(lookDirection);
            return go.AddComponent<PlayerTarget>();
        }

        [UnityTest]
        public IEnumerator ThreeEnemies_OneSlot_NeverTwoAttackingAtOnce()
        {
            AttackTokens.MaxTokens = 1;
            AttackTokens.ViewHalfAngle = 180f;
            Player(Vector3.forward);
            EnemyController template = Template();
            var enemies = new[]
            {
                world.Pool.Spawn(template, new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f)),
                world.Pool.Spawn(template, new Vector3(2f, 0f, 1f), Quaternion.Euler(0f, 270f, 0f)),
                world.Pool.Spawn(template, new Vector3(-2f, 0f, 1f), Quaternion.Euler(0f, 90f, 0f)),
            };

            int most = 0, attacks = 0;
            foreach (EnemyController e in enemies)
                e.Attack.AttackStarted += (_, __) => attacks++;
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                int now = 0;
                foreach (EnemyController e in enemies)
                    if (e.CurrentState == EnemyStateId.Attack) now++;
                most = Mathf.Max(most, now);
                yield return null;
            }
            Assert.AreEqual(1, most, "One slot: one attacker at a time.");
            Assert.GreaterOrEqual(attacks, 3, "They take turns.");
        }

        [UnityTest]
        public IEnumerator EnemyBehindThePlayer_DoesNotAttack_UntilThePlayerFacesIt()
        {
            PlayerTarget player = Player(Vector3.forward);
            EnemyController enemy = world.Pool.Spawn(Template(), new Vector3(0f, 0f, -1.4f), Quaternion.identity);
            int attacks = 0;
            enemy.Attack.AttackStarted += (_, __) => attacks++;

            float end = Time.time + 1.5f;
            while (Time.time < end)
                yield return null;
            Assert.AreEqual(0, attacks, "Never from behind.");
            Assert.IsTrue(enemy.MustWaitForTurn);

            // The player turns around: now it may attack.
            Vector3 look = enemy.transform.position - player.transform.position;
            look.y = 0f;
            player.transform.rotation = Quaternion.LookRotation(look);
            end = Time.time + 3f;
            while (attacks == 0 && Time.time < end)
                yield return null;
            Assert.Greater(attacks, 0);
        }
    }
}
