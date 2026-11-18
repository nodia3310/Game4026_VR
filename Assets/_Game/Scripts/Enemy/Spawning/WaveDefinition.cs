using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>One wave (spec 4.6): several "enemy prefab + count" groups, spawned one by one.</summary>
    [CreateAssetMenu(fileName = "Wave", menuName = "Game4026/Enemy/Wave Definition")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EnemyController Prefab;
            [Min(0)] public int Count;

            public Entry(EnemyController prefab, int count)
            {
                Prefab = prefab;
                Count = count;
            }
        }

        [SerializeField] private Entry[] entries = new Entry[0];
        [Tooltip("Seconds between two spawns inside this wave.")]
        [SerializeField, Min(0f)] private float spawnInterval = 1.5f;
        [Tooltip("Seconds to wait before this wave starts (after the previous wave is fully defeated).")]
        [SerializeField, Min(0f)] private float startDelay;

        public IReadOnlyList<Entry> Entries => entries;
        public float SpawnInterval => spawnInterval;
        public float StartDelay => startDelay;

        public int TotalCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < entries.Length; i++)
                    total += Mathf.Max(0, entries[i].Count);
                return total;
            }
        }

        /// <summary>Runtime / test setup.</summary>
        public void Configure(Entry[] waveEntries, float interval, float delay)
        {
            entries = waveEntries ?? new Entry[0];
            spawnInterval = Mathf.Max(0f, interval);
            startDelay = Mathf.Max(0f, delay);
        }
    }
}
