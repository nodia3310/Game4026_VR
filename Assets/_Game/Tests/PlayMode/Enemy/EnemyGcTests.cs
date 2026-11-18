using System;
using System.Collections;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>
    /// Spec M4: no per-frame GC allocation from enemy scripts with 4 enemies chasing/attacking.
    /// Frames are driven through EnemyController.Tick directly, so Editor allocations are not counted.
    /// </summary>
    public sealed class EnemyGcTests
    {
        private sealed class SilentReceiver : MonoBehaviour, IPlayerDamageReceiver
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
        public IEnumerator FourEnemies_ChasingAndAttacking_AllocateNothingPerFrame()
        {
            var playerGo = world.Track(new GameObject("Player"));
            // Head = this transform (no head assigned), so put it at eye height; at y = 0 the floor blocks line of sight.
            playerGo.transform.position = new Vector3(0f, 1.6f, 1.2f);
            var receiver = playerGo.AddComponent<SilentReceiver>();
            playerGo.AddComponent<PlayerTarget>();

            EnemyController template = world.Template("Enemy", world.Data("{\"spawnDuration\":0.1}"));
            var enemies = new EnemyController[4];
            Vector3[] positions =
            {
                new Vector3(0f, 0f, 0f),   // in attack range
                new Vector3(-5f, 0f, -3f), // chasing
                new Vector3(5f, 0f, -3f),
                new Vector3(0f, 0f, -6f),
            };
            for (int i = 0; i < enemies.Length; i++)
                enemies[i] = world.Pool.Spawn(template, positions[i], Quaternion.identity);

            float deadline = Time.time + 3f;
            while (Time.time < deadline && !AllEngaged(enemies))
                yield return null;
            Assert.IsTrue(AllEngaged(enemies), "All four enemies should be chasing or attacking.");

            float now = Time.time;
            const float dt = 1f / 72f; // Quest frame time
            for (int i = 0; i < 300; i++) // warm-up: JIT, first-path allocations
                TickAll(enemies, ref now, dt);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 720; i++) // 10 simulated seconds
                TickAll(enemies, ref now, dt);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, $"Enemy scripts allocated {allocated} bytes over 720 frames.");
            Assert.Greater(receiver.Hits, 0, "The attack path was exercised.");
        }

        private static bool AllEngaged(EnemyController[] enemies)
        {
            foreach (EnemyController e in enemies)
            {
                EnemyStateId s = e.CurrentState;
                if (s != EnemyStateId.Chase && s != EnemyStateId.Attack)
                    return false;
            }
            return true;
        }

        private static void TickAll(EnemyController[] enemies, ref float now, float dt)
        {
            now += dt;
            for (int i = 0; i < enemies.Length; i++)
                enemies[i].Tick(now, dt);
        }
    }
}
