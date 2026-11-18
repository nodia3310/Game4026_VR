using System.Collections.Generic;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class EncounterScheduleTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        private WaveDefinition Wave(float interval, float delay, params int[] counts)
        {
            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            var entries = new WaveDefinition.Entry[counts.Length];
            for (int i = 0; i < counts.Length; i++)
                entries[i] = new WaveDefinition.Entry(null, counts[i]);
            wave.Configure(entries, interval, delay);
            created.Add(wave);
            return wave;
        }

        private static bool Spawn(EncounterSchedule s, float t, int alive, int max = 4)
        {
            if (!s.TryPeek(t, alive, max, out _, out _))
                return false;
            s.Consume(t);
            return true;
        }

        [Test]
        public void FirstWave_SpawnsImmediately_ThenEveryInterval()
        {
            var s = new EncounterSchedule(new[] { Wave(1f, 0f, 3) });

            Assert.IsTrue(Spawn(s, 0f, 0));
            Assert.IsFalse(Spawn(s, 0.5f, 1));
            Assert.IsTrue(Spawn(s, 1f, 1));
            Assert.IsTrue(Spawn(s, 2f, 2));
            Assert.IsTrue(s.IsFinished);
            Assert.AreEqual(3, s.SpawnedTotal);
        }

        [Test]
        public void FirstWave_HonoursStartDelay()
        {
            var s = new EncounterSchedule(new[] { Wave(0f, 2f, 1) });

            Assert.IsFalse(Spawn(s, 0f, 0));   // delay starts counting here
            Assert.IsFalse(Spawn(s, 1.9f, 0));
            Assert.IsTrue(Spawn(s, 2f, 0));
        }

        [Test]
        public void MaxAlive_PausesSpawning_UntilASlotOpens()
        {
            var s = new EncounterSchedule(new[] { Wave(0f, 0f, 5) });
            Spawn(s, 0f, 0, max: 2);
            Spawn(s, 0f, 1, max: 2);

            Assert.IsFalse(s.TryPeek(1f, 2, 2, out _, out _));
            Assert.IsTrue(Spawn(s, 1f, 1, max: 2));
        }

        [Test]
        public void NextWave_WaitsUntilAllDefeated_ThenStartDelay()
        {
            var s = new EncounterSchedule(new[] { Wave(0f, 0f, 1), Wave(0f, 2f, 1) });
            Assert.IsTrue(Spawn(s, 0f, 0));
            Assert.AreEqual(1, s.CurrentWave);

            Assert.IsFalse(Spawn(s, 10f, 1), "Previous wave still alive.");
            Assert.IsFalse(Spawn(s, 11f, 0), "Delay starts once everyone is dead.");
            Assert.IsFalse(Spawn(s, 12.9f, 0));
            Assert.IsTrue(Spawn(s, 13f, 0));
            Assert.IsTrue(s.IsFinished);
        }

        [Test]
        public void Entries_AreSpawnedInOrder()
        {
            var s = new EncounterSchedule(new[] { Wave(0f, 0f, 2, 1) });
            var order = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(s.TryPeek(0f, 0, 4, out _, out int entry));
                order.Add(entry);
                s.Consume(0f);
            }
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, order);
        }

        [Test]
        public void EmptyWavesAndEntries_AreSkipped()
        {
            var s = new EncounterSchedule(new[] { Wave(0f, 0f, 0), null, Wave(0f, 0f, 0, 1, 0) });

            Assert.IsTrue(s.TryPeek(0f, 0, 4, out _, out int entry));
            Assert.AreEqual(1, entry);
            s.Consume(0f);
            Assert.IsTrue(s.IsFinished);
        }

        [Test]
        public void NoWaves_IsFinishedImmediately()
        {
            Assert.IsTrue(new EncounterSchedule(new WaveDefinition[0]).IsFinished);
            Assert.IsTrue(new EncounterSchedule(null).IsFinished);
        }

        [Test]
        public void Peek_DoesNotAdvance()
        {
            var s = new EncounterSchedule(new[] { Wave(0f, 0f, 1) });
            s.TryPeek(0f, 0, 4, out _, out _);
            s.TryPeek(0f, 0, 4, out _, out _);
            Assert.AreEqual(0, s.SpawnedTotal);
            Assert.IsFalse(s.IsFinished);
        }
    }
}
