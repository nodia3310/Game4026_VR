using System;
using System.Collections.Generic;
using Game4026.Contracts;

namespace Game4026.Artifacts
{
    /// <summary>
    /// Tracks one run's required artifacts without scene, physics or XR dependencies.
    /// A future display adapter must verify its target, release and locking conditions
    /// before calling TryRestore. Create a new tracker when restarting a run.
    /// </summary>
    public sealed class ArtifactRestorationTracker
    {
        private readonly Dictionary<string, ArtifactType> expectedArtifacts;
        private readonly HashSet<string> restoredIds = new HashSet<string>(StringComparer.Ordinal);

        public int RestoredCount => restoredIds.Count;
        public int TotalCount => expectedArtifacts.Count;
        public bool IsComplete => RestoredCount == TotalCount;

        /// <summary>Raised synchronously once per accepted ID, after progress is updated.</summary>
        public event Action<ArtifactRestoredEvent> OnArtifactRestored;

        /// <summary>Copies a nonempty plan so subsequent caller edits cannot alter this run.</summary>
        public ArtifactRestorationTracker(IReadOnlyDictionary<string, ArtifactType> expectedArtifacts)
        {
            if (expectedArtifacts == null)
                throw new ArgumentNullException(nameof(expectedArtifacts));

            if (expectedArtifacts.Count == 0)
                throw new ArgumentException("At least one artifact is required.", nameof(expectedArtifacts));

            this.expectedArtifacts = new Dictionary<string, ArtifactType>(StringComparer.Ordinal);
            foreach (var entry in expectedArtifacts)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    throw new ArgumentException("Every artifact requires a stable ID.", nameof(expectedArtifacts));

                if (entry.Value == ArtifactType.Unknown || !Enum.IsDefined(typeof(ArtifactType), entry.Value))
                    throw new ArgumentException("Every artifact requires a known type.", nameof(expectedArtifacts));

                this.expectedArtifacts.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Accepts a matching planned ID/type exactly once. IDs use ordinal, case-sensitive
        /// comparison. Unknown IDs, mismatches and repeated attempts do not change progress
        /// or emit a notification. Invoke from Unity's main thread; this class is not thread-safe.
        /// </summary>
        public bool TryRestore(string artifactId, ArtifactType type)
        {
            if (string.IsNullOrWhiteSpace(artifactId)
                || !expectedArtifacts.TryGetValue(artifactId, out var expectedType)
                || expectedType != type
                || !restoredIds.Add(artifactId))
            {
                return false;
            }

            OnArtifactRestored?.Invoke(new ArtifactRestoredEvent(artifactId, type));
            return true;
        }
    }
}
