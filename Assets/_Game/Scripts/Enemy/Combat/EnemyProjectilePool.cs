using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game4026.Enemy
{
    /// <summary>
    /// One <see cref="ObjectPool{T}"/> per projectile prefab, with a global cap on live projectiles (spec 4.5: 8).
    /// Pooled instances live under one inactive-at-rest root so they survive enemy despawns.
    /// </summary>
    public static class EnemyProjectilePool
    {
        public const int MaxActive = 8;

        private static readonly Dictionary<EnemyProjectile, ObjectPool<EnemyProjectile>> pools =
            new Dictionary<EnemyProjectile, ObjectPool<EnemyProjectile>>();
        private static Transform root;

        public static int ActiveCount { get; private set; }

        /// <summary>A ready projectile (inactive until <see cref="EnemyProjectile.Launch"/>), or null at the cap.</summary>
        public static EnemyProjectile Get(EnemyProjectile prefab)
        {
            if (prefab == null || ActiveCount >= MaxActive)
                return null;
            if (!pools.TryGetValue(prefab, out ObjectPool<EnemyProjectile> pool))
            {
                pool = new ObjectPool<EnemyProjectile>(
                    () => Create(prefab),
                    p => { },
                    p => p.gameObject.SetActive(false),
                    p => { if (p != null) Object.Destroy(p.gameObject); },
                    collectionCheck: false, defaultCapacity: 4, maxSize: MaxActive);
                pools.Add(prefab, pool);
            }
            EnemyProjectile projectile = pool.Get();
            projectile.Pool = pool;
            ActiveCount++;
            return projectile;
        }

        internal static void Release(EnemyProjectile projectile)
        {
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            if (projectile.Pool != null)
                projectile.Pool.Release(projectile);
            else
                projectile.gameObject.SetActive(false);
        }

        private static EnemyProjectile Create(EnemyProjectile prefab)
        {
            if (root == null)
            {
                var go = new GameObject("EnemyProjectiles");
                Object.DontDestroyOnLoad(go);
                root = go.transform;
            }
            EnemyProjectile p = Object.Instantiate(prefab, root);
            p.gameObject.SetActive(false);
            return p;
        }

        // Domain reload may be off: drop pools from the previous Play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pools.Clear();
            root = null;
            ActiveCount = 0;
        }
    }
}
