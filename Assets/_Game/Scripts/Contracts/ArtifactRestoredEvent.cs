using System;

namespace Game4026.Contracts
{
    /// <summary>
    /// Immutable notification emitted by A after an artifact has been accepted and locked
    /// into its matching display. B consumes it to advance encounters or unlock the boss.
    /// This is a payload, not a global event bus.
    /// </summary>
    public sealed class ArtifactRestoredEvent
    {
        /// <summary>
        /// Stable authored ID for this artifact instance, not a Unity instance ID or its type.
        /// Consumers deduplicate by this ID within a run and reset that set on a new run.
        /// </summary>
        public string ArtifactId { get; }

        public ArtifactType Type { get; }

        public ArtifactRestoredEvent(string artifactId, ArtifactType type)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
                throw new ArgumentException("A stable artifact ID is required.", nameof(artifactId));

            if (type == ArtifactType.Unknown || !Enum.IsDefined(typeof(ArtifactType), type))
                throw new ArgumentOutOfRangeException(nameof(type), type, "A known artifact type is required.");

            ArtifactId = artifactId;
            Type = type;
        }
    }
}
