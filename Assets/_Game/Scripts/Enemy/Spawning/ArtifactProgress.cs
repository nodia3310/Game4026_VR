using System.Collections.Generic;
using Game4026.Contracts;

namespace Game4026.Enemy
{
    /// <summary>
    /// Tracks restored artifacts for one run. Duplicate ArtifactIds are ignored (contract: dedupe by ID);
    /// the boss unlocks once every required type has been restored at least once.
    /// </summary>
    public sealed class ArtifactProgress
    {
        private static readonly ArtifactType[] DefaultRequired =
        {
            ArtifactType.RosettaStone, ArtifactType.CanopicJar, ArtifactType.TutankhamunMask,
        };

        private readonly ArtifactType[] required;
        private readonly HashSet<string> ids = new HashSet<string>();
        private readonly HashSet<ArtifactType> types = new HashSet<ArtifactType>();

        public ArtifactProgress() : this(DefaultRequired) { }

        public ArtifactProgress(params ArtifactType[] requiredTypes)
        {
            required = requiredTypes != null && requiredTypes.Length > 0 ? requiredTypes : DefaultRequired;
        }

        public int RestoredCount => ids.Count;

        public bool AllRestored
        {
            get
            {
                for (int i = 0; i < required.Length; i++)
                    if (!types.Contains(required[i]))
                        return false;
                return true;
            }
        }

        public bool IsRestored(ArtifactType type) => types.Contains(type);

        /// <summary>Returns false for null events and already-seen ArtifactIds.</summary>
        public bool TryAdd(ArtifactRestoredEvent restored)
        {
            if (restored == null || !ids.Add(restored.ArtifactId))
                return false;
            types.Add(restored.Type);
            return true;
        }

        public void Reset()
        {
            ids.Clear();
            types.Clear();
        }
    }
}
