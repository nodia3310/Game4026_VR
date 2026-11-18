using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// One per scene (spec 4.6). Receives restored artifacts (from the artifact bridge or the debug buttons),
    /// force-clears the encounter linked to that artifact, and starts the boss encounter once all three are restored.
    /// May later move to a level-flow manager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterDirector : MonoBehaviour
    {
        [Tooltip("Hall encounters. Leave empty to collect every EncounterController in the scene at start.")]
        [SerializeField] private EncounterController[] encounters = new EncounterController[0];
        [SerializeField] private EncounterController bossEncounter;

        private readonly ArtifactProgress progress = new ArtifactProgress();
        private bool bossStarted;
        private bool bossHooked;

        /// <summary>Raised for each newly accepted (non-duplicate) artifact.</summary>
        public event Action<ArtifactRestoredEvent> ArtifactAccepted;
        public event Action BossEncounterStarted;
        public event Action BossDefeated;

        public ArtifactProgress Progress => progress;
        public bool BossStarted => bossStarted;

        private void Awake()
        {
            if (encounters == null || encounters.Length == 0)
                encounters = FindObjectsByType<EncounterController>(FindObjectsInactive.Include);
        }

        private void OnDestroy()
        {
            if (bossHooked && bossEncounter != null)
                bossEncounter.Cleared -= HandleBossCleared;
        }

        /// <summary>Runtime / test setup.</summary>
        public void Configure(EncounterController[] hallEncounters, EncounterController boss)
        {
            encounters = hallEncounters ?? new EncounterController[0];
            bossEncounter = boss;
        }

        /// <summary>Entry point for restored artifacts. Returns false for duplicates.</summary>
        public bool NotifyArtifactRestored(ArtifactRestoredEvent restored)
        {
            if (!progress.TryAdd(restored))
                return false;

            for (int i = 0; i < encounters.Length; i++)
            {
                EncounterController encounter = encounters[i];
                if (encounter != null && encounter != bossEncounter && encounter.LinkedArtifact == restored.Type)
                    encounter.ForceClear();
            }

            ArtifactAccepted?.Invoke(restored);

            if (!bossStarted && progress.AllRestored)
                StartBoss();
            return true;
        }

        /// <summary>New run: forget restored artifacts. Encounters themselves are not reset.</summary>
        public void ResetRun()
        {
            progress.Reset();
            bossStarted = false;
        }

        private void StartBoss()
        {
            bossStarted = true;
            if (bossEncounter != null)
            {
                if (!bossHooked)
                {
                    bossEncounter.Cleared += HandleBossCleared;
                    bossHooked = true;
                }
                bossEncounter.StartEncounter();
            }
            BossEncounterStarted?.Invoke();
        }

        private void HandleBossCleared(EncounterController encounter)
        {
            if (!encounter.WasForceCleared)
                BossDefeated?.Invoke();
        }
    }
}
