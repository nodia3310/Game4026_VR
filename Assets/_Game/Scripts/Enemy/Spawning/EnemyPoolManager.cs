using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game4026.Enemy
{
    /// <summary>
    /// One <see cref="ObjectPool{T}"/> per enemy prefab (spec 4.6). Enemies return automatically when they
    /// despawn, so repeated spawning reuses instances instead of Instantiate/Destroy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPoolManager : MonoBehaviour
    {
        [Serializable]
        public struct Prewarm
        {
            public EnemyController Prefab;
            [Min(0)] public int Count;
        }

        [SerializeField] private Prewarm[] prewarm = new Prewarm[0];
        [SerializeField, Min(1)] private int maxPooledPerPrefab = 16;

        private readonly Dictionary<EnemyController, ObjectPool<EnemyController>> pools =
            new Dictionary<EnemyController, ObjectPool<EnemyController>>();
        private readonly Dictionary<EnemyController, ObjectPool<EnemyController>> owners =
            new Dictionary<EnemyController, ObjectPool<EnemyController>>();
        private readonly HashSet<EnemyController> active = new HashSet<EnemyController>();
        private readonly List<EnemyController> prewarmBuffer = new List<EnemyController>();

        private Transform storage;

        /// <summary>Total Instantiate calls made by this manager (for verifying reuse).</summary>
        public int CreatedCount { get; private set; }
        public int ActiveCount => active.Count;

        private void Awake()
        {
            EnsureStorage();
            for (int i = 0; i < prewarm.Length; i++)
                Warm(prewarm[i].Prefab, prewarm[i].Count);
        }

        /// <summary>Creates instances up front so the first waves do not Instantiate during play.</summary>
        public void Warm(EnemyController prefab, int count)
        {
            if (prefab == null || count <= 0)
                return;
            ObjectPool<EnemyController> pool = GetPool(prefab);
            prewarmBuffer.Clear();
            for (int i = 0; i < count; i++)
                prewarmBuffer.Add(pool.Get());
            for (int i = 0; i < prewarmBuffer.Count; i++)
                pool.Release(prewarmBuffer[i]);
            prewarmBuffer.Clear();
        }

        /// <summary>Takes an enemy from the pool, places it and activates it (it restarts from Spawn).</summary>
        public EnemyController Spawn(EnemyController prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;
            ObjectPool<EnemyController> pool = GetPool(prefab);
            EnemyController enemy = pool.Get();
            enemy.transform.SetParent(null, false);
            enemy.transform.SetPositionAndRotation(position, rotation);
            active.Add(enemy);
            enemy.gameObject.SetActive(true);
            return enemy;
        }

        /// <summary>Returns an enemy to its pool. Safe to call more than once.</summary>
        public void Release(EnemyController enemy)
        {
            if (enemy == null || !active.Remove(enemy))
                return;
            if (owners.TryGetValue(enemy, out ObjectPool<EnemyController> pool))
                pool.Release(enemy);
        }

        private ObjectPool<EnemyController> GetPool(EnemyController prefab)
        {
            if (pools.TryGetValue(prefab, out ObjectPool<EnemyController> pool))
                return pool;

            EnsureStorage();
            ObjectPool<EnemyController> created = null;
            created = new ObjectPool<EnemyController>(
                createFunc: () => Create(prefab, created),
                actionOnGet: null,
                actionOnRelease: Park,
                actionOnDestroy: DestroyInstance,
                collectionCheck: false,
                defaultCapacity: 4,
                maxSize: maxPooledPerPrefab);
            pools.Add(prefab, created);
            return created;
        }

        private EnemyController Create(EnemyController prefab, ObjectPool<EnemyController> pool)
        {
            // Instantiate under the inactive storage so Awake/OnEnable wait until Spawn activates it.
            EnemyController enemy = Instantiate(prefab, storage);
            enemy.gameObject.SetActive(false);
            enemy.name = prefab.name;
            enemy.Despawned += Release;
            owners.Add(enemy, pool);
            CreatedCount++;
            return enemy;
        }

        private void Park(EnemyController enemy)
        {
            enemy.gameObject.SetActive(false);
            enemy.transform.SetParent(storage, false);
        }

        private void DestroyInstance(EnemyController enemy)
        {
            if (enemy == null)
                return;
            enemy.Despawned -= Release;
            owners.Remove(enemy);
            Destroy(enemy.gameObject);
        }

        private void EnsureStorage()
        {
            if (storage != null)
                return;
            var go = new GameObject("PooledEnemies");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            storage = go.transform;
        }
    }
}
