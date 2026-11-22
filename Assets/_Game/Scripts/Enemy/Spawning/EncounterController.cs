using System;
using System.Collections.Generic;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// One encounter per hall (spec 4.6): runs its waves through <see cref="EnemyPoolManager"/>, keeps at most
    /// maxAlive enemies alive, and raises <see cref="Cleared"/> when every enemy is defeated or after <see cref="ForceClear"/>.
    /// With startOnPlayerEnter, a trigger collider on this object starts it when the player walks in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterController : MonoBehaviour
    {
        [SerializeField] private WaveDefinition[] waves = new WaveDefinition[0];
        [SerializeField] private SpawnPoint[] spawnPoints = new SpawnPoint[0];
        [SerializeField, Min(1)] private int maxAlive = 4;
        [Tooltip("Restoring this artifact force-clears the encounter. Unknown = never (e.g. the boss).")]
        [SerializeField] private ArtifactType linkedArtifact = ArtifactType.Unknown;
        [SerializeField] private bool startOnPlayerEnter = true;
        [Tooltip("Optional patrol route given to every spawned enemy.")]
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField] private EnemyPoolManager pool;
        [Tooltip("Spawned enemies chase the player right away, at any distance, and never lose them.")]
        [SerializeField] private bool alertOnSpawn = true;

        private readonly List<EnemyController> alive = new List<EnemyController>(8);
        private readonly List<EnemyController> tracked = new List<EnemyController>(8);
        private EncounterSchedule schedule;
        private int spawnCursor;

        public event Action<EncounterController> Started;
        public event Action<EncounterController> Cleared;

        public ArtifactType LinkedArtifact => linkedArtifact;
        public bool IsActive { get; private set; }
        public bool IsCleared { get; private set; }
        public bool WasForceCleared { get; private set; }
        public int AliveCount => alive.Count;
        public int KillCount { get; private set; }
        public int SpawnedCount { get; private set; }
        public int MaxAlive => maxAlive;

        /// <summary>Enemies across every wave (for progress HUDs).</summary>
        public int TotalEnemies
        {
            get
            {
                int total = 0;
                for (int i = 0; i < waves.Length; i++)
                    if (waves[i] != null)
                        total += waves[i].TotalCount;
                return total;
            }
        }

        private void Awake()
        {
            if (pool == null)
                pool = FindAnyObjectByType<EnemyPoolManager>();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < tracked.Count; i++)
                Unsubscribe(tracked[i]);
            tracked.Clear();
        }

        /// <summary>Runtime / test setup (call before StartEncounter).</summary>
        public void Configure(WaveDefinition[] encounterWaves, SpawnPoint[] points, int maxAliveCount,
            EnemyPoolManager enemyPool, ArtifactType artifact = ArtifactType.Unknown, bool startOnEnter = false)
        {
            waves = encounterWaves ?? new WaveDefinition[0];
            spawnPoints = points ?? new SpawnPoint[0];
            maxAlive = Mathf.Max(1, maxAliveCount);
            pool = enemyPool;
            linkedArtifact = artifact;
            startOnPlayerEnter = startOnEnter;
        }

        public void StartEncounter()
        {
            if (IsActive || IsCleared)
                return;
            if (pool == null)
                pool = FindAnyObjectByType<EnemyPoolManager>();
            if (pool == null)
            {
                Debug.LogError($"{nameof(EncounterController)} '{name}' has no {nameof(EnemyPoolManager)}.", this);
                return;
            }

            schedule = new EncounterSchedule(waves);
            IsActive = true;
            Started?.Invoke(this);
        }

        /// <summary>Stops further waves and despawns every enemy still alive (not counted as kills). Also works before start.</summary>
        public void ForceClear()
        {
            if (IsCleared)
                return;
            for (int i = alive.Count - 1; i >= 0; i--)
                alive[i].ForceClear();
            alive.Clear();
            WasForceCleared = true;
            Complete();
        }

        /// <summary>
        /// Back to "not started" so the encounter can run again (e.g. a pooled boss's minion encounter).
        /// Enemies still alive are force-cleared first.
        /// </summary>
        public void ResetEncounter()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
                alive[i].ForceClear();
            alive.Clear();
            schedule = null;
            spawnCursor = 0;
            IsActive = false;
            IsCleared = false;
            WasForceCleared = false;
            KillCount = 0;
            SpawnedCount = 0;
        }

        private void Update()
        {
            if (!IsActive)
                return;

            float now = Time.time;
            if (schedule.TryPeek(now, alive.Count, maxAlive, out WaveDefinition wave, out int entry))
            {
                EnemyController prefab = wave.Entries[entry].Prefab;
                if (prefab == null)
                {
                    schedule.Consume(now); // misconfigured entry: skip it instead of stalling forever
                }
                else if (TryPickSpawn(out Vector3 position, out Quaternion rotation))
                {
                    EnemyController enemy = pool.Spawn(prefab, position, rotation);
                    if (enemy != null && alertOnSpawn)
                        enemy.Alert();
                    Track(enemy);
                    schedule.Consume(now);
                }
                // else: no valid point this frame (player too close); try again next frame
            }

            if (schedule.IsFinished && alive.Count == 0)
                Complete();
        }

        private bool TryPickSpawn(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            int count = spawnPoints.Length;
            if (count == 0)
                return false;

            PlayerTarget player = PlayerTarget.Current;
            bool hasPlayer = player != null;
            Vector3 playerPosition = hasPlayer ? player.Body.position : default;

            for (int i = 0; i < count; i++)
            {
                SpawnPoint point = spawnPoints[(spawnCursor + i) % count];
                if (point == null || !point.TryGetSpawnPosition(hasPlayer, playerPosition, out position))
                    continue;

                spawnCursor = (spawnCursor + i + 1) % count;
                Vector3 look = hasPlayer ? playerPosition - position : point.transform.forward;
                look.y = 0f;
                rotation = look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look) : point.transform.rotation;
                return true;
            }
            return false;
        }

        private void Track(EnemyController enemy)
        {
            if (enemy == null)
                return;
            if (patrolPoints != null && patrolPoints.Length > 0)
                enemy.SetPatrolPoints(patrolPoints);
            enemy.Killed += HandleKilled;
            enemy.Despawned += HandleDespawned;
            alive.Add(enemy);
            tracked.Add(enemy);
            SpawnedCount++;
        }

        private void HandleKilled(EnemyController enemy)
        {
            if (alive.Remove(enemy))
                KillCount++;
        }

        private void HandleDespawned(EnemyController enemy)
        {
            alive.Remove(enemy);
            Unsubscribe(enemy);
            tracked.Remove(enemy);
        }

        private void Unsubscribe(EnemyController enemy)
        {
            if (enemy == null)
                return;
            enemy.Killed -= HandleKilled;
            enemy.Despawned -= HandleDespawned;
        }

        private void Complete()
        {
            IsActive = false;
            IsCleared = true;
            Cleared?.Invoke(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (startOnPlayerEnter && !IsActive && !IsCleared && other.GetComponentInParent<PlayerTarget>() != null)
                StartEncounter();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            for (int i = 0; i < spawnPoints.Length; i++)
                if (spawnPoints[i] != null)
                    Gizmos.DrawLine(transform.position, spawnPoints[i].transform.position);
        }
    }
}
