using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>Director routing without Play Mode: which encounter is cleared and when the boss starts.</summary>
    public sealed class EncounterDirectorTests
    {
        private GameObject root;
        private EncounterController rosettaHall;
        private EncounterController canopicHall;
        private EncounterController boss;
        private EncounterDirector director;
        private int bossStarts;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Root");
            var pool = root.AddComponent<EnemyPoolManager>();

            rosettaHall = Make("Hall_Rosetta", pool, ArtifactType.RosettaStone);
            canopicHall = Make("Hall_Canopic", pool, ArtifactType.CanopicJar);
            boss = Make("Boss", pool, ArtifactType.Unknown);

            director = root.AddComponent<EncounterDirector>();
            director.Configure(new[] { rosettaHall, canopicHall, boss }, boss);
            bossStarts = 0;
            director.BossEncounterStarted += () => bossStarts++;
        }

        private EncounterController Make(string name, EnemyPoolManager pool, ArtifactType artifact)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            var encounter = go.AddComponent<EncounterController>();
            encounter.Configure(new WaveDefinition[0], new SpawnPoint[0], 4, pool, artifact);
            return encounter;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private bool Restore(string id, ArtifactType type) =>
            director.NotifyArtifactRestored(new ArtifactRestoredEvent(id, type));

        [Test]
        public void Restore_ForceClearsOnlyTheLinkedHall()
        {
            Assert.IsTrue(Restore("rosetta", ArtifactType.RosettaStone));

            Assert.IsTrue(rosettaHall.IsCleared);
            Assert.IsTrue(rosettaHall.WasForceCleared);
            Assert.IsFalse(canopicHall.IsCleared);
            Assert.IsFalse(boss.IsCleared);
        }

        [Test]
        public void DuplicateId_IsIgnored()
        {
            Restore("rosetta", ArtifactType.RosettaStone);
            Assert.IsFalse(Restore("rosetta", ArtifactType.RosettaStone));
        }

        [Test]
        public void AllThreeRestored_StartsBossOnce()
        {
            Restore("rosetta", ArtifactType.RosettaStone);
            Restore("canopic", ArtifactType.CanopicJar);
            Assert.AreEqual(0, bossStarts);
            Assert.IsFalse(boss.IsActive);

            Restore("mask", ArtifactType.TutankhamunMask);
            Assert.AreEqual(1, bossStarts);
            Assert.IsTrue(director.BossStarted);
            Assert.IsTrue(boss.IsActive);

            Restore("mask-2", ArtifactType.TutankhamunMask);
            Assert.AreEqual(1, bossStarts, "Boss starts only once per run.");
        }

        [Test]
        public void ForceClearedEncounter_CannotStartLater()
        {
            Restore("rosetta", ArtifactType.RosettaStone);
            rosettaHall.StartEncounter();
            Assert.IsFalse(rosettaHall.IsActive);
        }

        [Test]
        public void ForceClear_RaisesClearedOnce()
        {
            int cleared = 0;
            rosettaHall.Cleared += _ => cleared++;
            rosettaHall.ForceClear();
            rosettaHall.ForceClear();
            Assert.AreEqual(1, cleared);
        }
    }
}
