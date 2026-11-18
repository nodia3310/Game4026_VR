using Game4026.Contracts;
using Game4026.Enemy;
using Game4026.Interaction.Artifacts;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>
    /// Forwards A's <see cref="ArtifactDisplayTrigger.OnArtifactRestored"/> (every trigger in the scene) to the
    /// <see cref="EncounterDirector"/>. A's code is not modified. Duplicate IDs are ignored by the director.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArtifactEventBridge : MonoBehaviour
    {
        [SerializeField] private EncounterDirector director;
        [SerializeField] private bool logInstalls = true;

        private ArtifactDisplayTrigger[] triggers = new ArtifactDisplayTrigger[0];

        private void Start() => Refresh();

        private void OnDestroy() => Unsubscribe();

        /// <summary>Rescans the scene for display triggers (call again if they are spawned later).</summary>
        public void Refresh()
        {
            if (director == null)
                director = FindAnyObjectByType<EncounterDirector>();
            if (director == null)
            {
                Debug.LogWarning($"[{nameof(ArtifactEventBridge)}] no {nameof(EncounterDirector)} in the scene.", this);
                return;
            }

            Unsubscribe();
            triggers = FindObjectsByType<ArtifactDisplayTrigger>(FindObjectsInactive.Include);
            for (int i = 0; i < triggers.Length; i++)
                triggers[i].OnArtifactRestored += HandleRestored;

            if (logInstalls)
                Debug.Log($"[{nameof(ArtifactEventBridge)}] watching {triggers.Length} artifact display trigger(s).", this);
        }

        private void HandleRestored(ArtifactRestoredEvent restored)
        {
            if (director != null)
                director.NotifyArtifactRestored(restored);
        }

        private void Unsubscribe()
        {
            for (int i = 0; i < triggers.Length; i++)
                if (triggers[i] != null)
                    triggers[i].OnArtifactRestored -= HandleRestored;
        }
    }
}
