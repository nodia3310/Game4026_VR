using System.Collections;
using System.Collections.Generic;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>
    /// Full encounter flow in Play Mode with a runtime-baked NavMesh and a code-built enemy template:
    /// start → enemies spawn from the pool → all killed → Cleared, plus ForceClear and the boss trigger.
    /// </summary>
    public sealed class EncounterPlayModeTests
    {
        private readonly List<Object> created = new List<Object>();
        private NavMeshSurface surface;
        private EnemyData data;
        private EnemyController template;
        private EnemyPoolManager pool;
        private SpawnPoint[] points;

        [SetUp]
        public void SetUp()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            created.Add(floor);

            data = ScriptableObject.CreateInstance<EnemyData>();
            JsonUtility.FromJsonOverwrite("{\"spawnDuration\":0.1,\"despawnDuration\":0.2}", data);
            created.Add(data);

            // Inactive template stands in for a prefab: Awake runs only on pooled copies.
            var templateGo = new GameObject("EnemyTemplate");
            templateGo.SetActive(false);
            template = templateGo.AddComponent<EnemyController>();
            template.GetComponent<EnemyHealth>().Initialize(data);
            var body = templateGo.AddComponent<CapsuleCollider>();
            body.center = Vector3.up;
            created.Add(templateGo);

            var poolGo = new GameObject("Pool");
            pool = poolGo.AddComponent<EnemyPoolManager>();
            created.Add(poolGo);

            points = new[] { Point(new Vector3(-3f, 0f, 0f)), Point(new Vector3(3f, 0f, 0f)) };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (EnemyController e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include))
                Object.Destroy(e.gameObject);
            foreach (Object o in created)
                if (o != null) Object.Destroy(o);
            created.Clear();
            if (surface != null)
                surface.RemoveData();
        }

        private SpawnPoint Point(Vector3 position)
        {
            var go = new GameObject("SpawnPoint");
            go.transform.position = position;
            created.Add(go);
            return go.AddComponent<SpawnPoint>();
        }

        private WaveDefinition Wave(int count, float interval, float delay)
        {
            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            wave.Configure(new[] { new WaveDefinition.Entry(template, count) }, interval, delay);
            created.Add(wave);
            return wave;
        }

        private EncounterController Encounter(params WaveDefinition[] waves)
        {
            var go = new GameObject("Encounter");
            created.Add(go);
            var encounter = go.AddComponent<EncounterController>();
            encounter.Configure(waves, points, 2, pool);
            return encounter;
        }

        private static void KillReadyEnemies()
        {
            foreach (EnemyController e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude))
            {
                EnemyStateId s = e.CurrentState;
                if (s != EnemyStateId.Spawn && s != EnemyStateId.Dead && s != EnemyStateId.None)
                    e.Health.ApplyDamage(1000f, e.transform.position, false, Time.time);
            }
        }

        [UnityTest]
        public IEnumerator Encounter_RunsToCleared_WithinMaxAlive_ReusingPooledEnemies()
        {
            // Wave 2 waits longer than the despawn delay, so its enemies must come back from the pool.
            EncounterController encounter = Encounter(Wave(2, 0.05f, 0f), Wave(2, 0.05f, 0.5f));
            int cleared = 0, maxAliveSeen = 0;
            encounter.Cleared += _ => cleared++;

            encounter.StartEncounter();
            float deadline = Time.time + 15f;
            while (!encounter.IsCleared && Time.time < deadline)
            {
                maxAliveSeen = Mathf.Max(maxAliveSeen, encounter.AliveCount);
                KillReadyEnemies();
                yield return null;
            }

            Assert.IsTrue(encounter.IsCleared, "Encounter did not clear in time.");
            Assert.IsFalse(encounter.WasForceCleared);
            Assert.AreEqual(1, cleared);
            Assert.AreEqual(4, encounter.SpawnedCount);
            Assert.AreEqual(4, encounter.KillCount);
            Assert.LessOrEqual(maxAliveSeen, 2);
            Assert.AreEqual(2, pool.CreatedCount, "Second wave should reuse pooled enemies, not Instantiate new ones.");
        }

        [UnityTest]
        public IEnumerator ForceClear_DespawnsAliveEnemies_WithoutKills()
        {
            EncounterController encounter = Encounter(Wave(2, 0.05f, 0f), Wave(2, 0f, 0f));
            encounter.StartEncounter();

            float deadline = Time.time + 5f;
            while (encounter.AliveCount < 2 && Time.time < deadline)
                yield return null;
            Assert.AreEqual(2, encounter.AliveCount);

            encounter.ForceClear();
            Assert.IsTrue(encounter.IsCleared);
            Assert.IsTrue(encounter.WasForceCleared);
            Assert.AreEqual(0, encounter.KillCount);

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, pool.ActiveCount, "Cleared enemies return to the pool after the despawn delay.");
            Assert.AreEqual(2, encounter.SpawnedCount, "No further waves after ForceClear.");
        }

        [UnityTest]
        public IEnumerator Director_AllArtifacts_StartBoss_AndBossClearRaisesDefeated()
        {
            EncounterController hall = Encounter(Wave(1, 0f, 0f));
            hall.Configure(new[] { Wave(1, 0f, 0f) }, points, 2, pool, ArtifactType.RosettaStone);
            EncounterController boss = Encounter(Wave(1, 0f, 0f));

            var directorGo = new GameObject("Director");
            created.Add(directorGo);
            var director = directorGo.AddComponent<EncounterDirector>();
            director.Configure(new[] { hall }, boss);
            int defeated = 0;
            director.BossDefeated += () => defeated++;

            hall.StartEncounter();
            yield return new WaitForSeconds(0.3f);

            director.NotifyArtifactRestored(new ArtifactRestoredEvent("r", ArtifactType.RosettaStone));
            Assert.IsTrue(hall.WasForceCleared);
            director.NotifyArtifactRestored(new ArtifactRestoredEvent("c", ArtifactType.CanopicJar));
            director.NotifyArtifactRestored(new ArtifactRestoredEvent("m", ArtifactType.TutankhamunMask));
            Assert.IsTrue(boss.IsActive);

            float deadline = Time.time + 10f;
            while (!boss.IsCleared && Time.time < deadline)
            {
                KillReadyEnemies();
                yield return null;
            }

            Assert.IsTrue(boss.IsCleared);
            Assert.AreEqual(1, defeated);
        }

        [UnityTest]
        public IEnumerator SpawnPoint_RejectsPlayerTooClose_AndOffNavMesh()
        {
            yield return null;
            Assert.IsTrue(points[0].TryGetSpawnPosition(false, default, out _));
            Assert.IsFalse(points[0].TryGetSpawnPosition(true, points[0].transform.position + Vector3.right * 2f, out _));
            Assert.IsTrue(points[0].TryGetSpawnPosition(true, points[0].transform.position + Vector3.right * 4f, out _));

            SpawnPoint offMesh = Point(new Vector3(100f, 0f, 100f));
            Assert.IsFalse(offMesh.TryGetSpawnPosition(false, default, out _));
        }
    }
}
