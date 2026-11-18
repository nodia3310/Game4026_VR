using System.Collections;
using System.Collections.Generic;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>
    /// Pharaoh phases on a code-built boss (100 HP): 60 % → summon phase (invulnerable, minions),
    /// 25 % → enraged phase (weak point enabled, faster attacks), death → minions cleared.
    /// </summary>
    public sealed class BossPlayModeTests
    {
        private PlayModeWorld world;
        private EnemyController bossTemplate;
        private float hitTime;

        [SetUp]
        public void SetUp()
        {
            world = new PlayModeWorld();
            hitTime = 100f;

            EnemyData minionData = world.Data("{\"spawnDuration\":0.1,\"despawnDuration\":0.2}");
            EnemyController minion = world.Template("Minion", minionData);

            EnemyData bossData = world.Data("{\"maxHealth\":100,\"spawnDuration\":0.1,\"despawnDuration\":0.2,\"staggerThreshold\":1000}");
            bossTemplate = world.Template("Boss", bossData);
            Transform root = bossTemplate.transform;

            var weak = new GameObject("WeakPoint_P3");
            weak.transform.SetParent(root, false);
            weak.AddComponent<BoxCollider>();
            weak.AddComponent<WeakPoint>();

            var minionsGo = new GameObject("Minions");
            minionsGo.transform.SetParent(root, false);
            var minions = minionsGo.AddComponent<EncounterController>();
            minions.Configure(new[] { world.Wave(minion, 2) },
                new[] { world.Point(root, new Vector3(-4f, 0f, 0f)), world.Point(root, new Vector3(4f, 0f, 0f)) },
                2, world.Pool);

            var boss = bossTemplate.gameObject.AddComponent<PharaohBoss>();
            boss.Configure(new List<BossPhase>
            {
                new BossPhase("Melee", 1f, invulnerableSeconds: 0f),
                new BossPhase("Summon", 0.6f, invulnerableSeconds: 0.5f, startMinions: true),
                new BossPhase("Enraged", 0.25f, attackSpeed: 1.5f, invulnerableSeconds: 0.5f, activate: weak),
            }, minions);
        }

        [TearDown]
        public void TearDown() => world.Dispose();

        private bool Hit(EnemyController boss, float damage)
        {
            hitTime += 1f; // past the 0.3 s hit cooldown
            return boss.Health.ApplyDamage(damage, boss.transform.position, false, hitTime);
        }

        private IEnumerator SpawnBoss(System.Action<EnemyController, PharaohBoss> ready)
        {
            EnemyController boss = world.Pool.Spawn(bossTemplate, Vector3.zero, Quaternion.identity);
            yield return new WaitForSeconds(0.2f); // past spawnDuration
            ready(boss, boss.GetComponent<PharaohBoss>());
        }

        [UnityTest]
        public IEnumerator Phases_ChangeAtThresholds_WithInvulnerabilityMinionsAndWeakPoint()
        {
            EnemyController boss = null;
            PharaohBoss pharaoh = null;
            yield return SpawnBoss((b, p) => { boss = b; pharaoh = p; });

            var changes = new List<(int, int)>();
            pharaoh.PhaseChanged += (_, from, to) => changes.Add((from, to));
            GameObject weak = boss.transform.Find("WeakPoint_P3").gameObject;
            Assert.IsFalse(weak.activeSelf, "Phase-3 weak point starts hidden.");

            Hit(boss, 30f); // 70 %
            Assert.AreEqual(0, pharaoh.CurrentPhase);

            Hit(boss, 15f); // 55 % → phase 2
            Assert.AreEqual(1, pharaoh.CurrentPhase);
            Assert.IsTrue(pharaoh.MinionEncounter.IsActive, "Minions start in phase 2.");
            Assert.IsFalse(Hit(boss, 10f), "Invulnerable right after the phase change.");
            Assert.AreEqual(55f, boss.Health.CurrentHealth, 1e-3f);

            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(Hit(boss, 35f)); // 20 % → phase 3
            Assert.AreEqual(2, pharaoh.CurrentPhase);
            Assert.IsTrue(weak.activeSelf, "Weak point appears in phase 3.");
            Assert.AreEqual(1.5f, boss.Attack.SpeedMultiplier, 1e-4f);

            CollectionAssert.AreEqual(new[] { (0, 1), (1, 2) }, changes);

            yield return new WaitForSeconds(0.7f);
            Hit(boss, 1000f);
            Assert.AreEqual(EnemyStateId.Dead, boss.CurrentState);
            Assert.IsTrue(pharaoh.MinionEncounter.IsCleared && pharaoh.MinionEncounter.WasForceCleared,
                "Minions are cleared when the boss dies.");
        }

        [UnityTest]
        public IEnumerator BigHit_SkippingAPhase_StillRunsItsEffects()
        {
            EnemyController boss = null;
            PharaohBoss pharaoh = null;
            yield return SpawnBoss((b, p) => { boss = b; pharaoh = p; });

            Hit(boss, 90f); // 100 % → 10 %

            Assert.AreEqual(2, pharaoh.CurrentPhase);
            Assert.IsTrue(pharaoh.MinionEncounter.IsActive, "Skipped summon phase still starts minions.");
            Assert.IsTrue(boss.transform.Find("WeakPoint_P3").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator PooledBoss_StartsFreshOnReuse()
        {
            EnemyController boss = null;
            PharaohBoss pharaoh = null;
            yield return SpawnBoss((b, p) => { boss = b; pharaoh = p; });
            Hit(boss, 90f);
            yield return new WaitForSeconds(0.7f);
            Hit(boss, 1000f);
            yield return new WaitForSeconds(0.5f); // despawn → back to pool

            EnemyController again = world.Pool.Spawn(bossTemplate, Vector3.zero, Quaternion.identity);
            yield return null;
            Assert.AreSame(boss, again, "Pool reuses the same boss instance.");
            Assert.AreEqual(0, pharaoh.CurrentPhase);
            Assert.AreEqual(1f, again.Attack.SpeedMultiplier, 1e-4f);
            Assert.IsFalse(again.transform.Find("WeakPoint_P3").gameObject.activeSelf);
            Assert.IsFalse(pharaoh.MinionEncounter.IsCleared, "Minion encounter is reset for the new fight.");
        }
    }
}
