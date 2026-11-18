using System.Collections.Generic;

namespace Game4026.Enemy
{
    /// <summary>
    /// Engine-free wave timing for one encounter:
    /// a wave starts startDelay after the previous wave is fully defeated (the first wave after StartEncounter);
    /// inside a wave one enemy spawns every spawnInterval while alive &lt; maxAlive.
    /// Callers <see cref="TryPeek"/>, attempt the spawn, then <see cref="Consume"/> only if it succeeded.
    /// </summary>
    public sealed class EncounterSchedule
    {
        private readonly IReadOnlyList<WaveDefinition> waves;

        private int waveIndex;
        private int entryIndex;
        private int spawnedInEntry;
        private bool waveWaiting;
        private bool waveRunning;
        private float waveReadyTime;
        private float nextSpawnTime;

        public bool IsFinished { get; private set; }
        public int CurrentWave => waveIndex;
        public int SpawnedTotal { get; private set; }

        public EncounterSchedule(IReadOnlyList<WaveDefinition> waves)
        {
            this.waves = waves ?? new WaveDefinition[0];
            SkipEmpty();
        }

        /// <summary>True when the next enemy is due now. Does not advance the schedule.</summary>
        public bool TryPeek(float now, int alive, int maxAlive, out WaveDefinition wave, out int entry)
        {
            wave = null;
            entry = -1;
            if (IsFinished)
                return false;

            if (!waveRunning)
            {
                // Wave N+1 waits for every enemy of the previous waves to be defeated.
                if (SpawnedTotal > 0 && alive > 0)
                    return false;
                if (!waveWaiting)
                {
                    waveWaiting = true;
                    waveReadyTime = now + waves[waveIndex].StartDelay;
                }
                if (now < waveReadyTime)
                    return false;
                waveRunning = true;
                nextSpawnTime = now;
            }

            if (alive >= maxAlive || now < nextSpawnTime)
                return false;

            wave = waves[waveIndex];
            entry = entryIndex;
            return true;
        }

        /// <summary>Marks the peeked enemy as spawned and advances.</summary>
        public void Consume(float now)
        {
            if (IsFinished || !waveRunning)
                return;

            WaveDefinition wave = waves[waveIndex];
            SpawnedTotal++;
            spawnedInEntry++;
            nextSpawnTime = now + wave.SpawnInterval;

            if (spawnedInEntry >= wave.Entries[entryIndex].Count)
            {
                entryIndex++;
                spawnedInEntry = 0;
            }

            SkipEmptyEntries();
            if (entryIndex >= wave.Entries.Count)
            {
                waveIndex++;
                entryIndex = 0;
                spawnedInEntry = 0;
                waveRunning = false;
                waveWaiting = false;
                SkipEmpty();
            }
        }

        private void SkipEmptyEntries()
        {
            WaveDefinition wave = waves[waveIndex];
            while (entryIndex < wave.Entries.Count && wave.Entries[entryIndex].Count <= 0)
                entryIndex++;
        }

        private void SkipEmpty()
        {
            while (waveIndex < waves.Count && (waves[waveIndex] == null || waves[waveIndex].TotalCount <= 0))
                waveIndex++;
            if (waveIndex >= waves.Count)
            {
                IsFinished = true;
                return;
            }
            entryIndex = 0;
            SkipEmptyEntries();
        }
    }
}
