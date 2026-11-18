using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Interaction.Artifacts
{
    [DisallowMultipleComponent]
    public sealed class ArtifactIdentity : MonoBehaviour
    {
        [SerializeField] private string artifactId;
        [SerializeField] private ArtifactType type;

        public string ArtifactId => artifactId;
        public ArtifactType Type => type;
        public bool IsValid => !string.IsNullOrWhiteSpace(artifactId)
            && type != ArtifactType.Unknown && Enum.IsDefined(typeof(ArtifactType), type);

        /// <summary>Author a unique, stable ID per physical artifact, including same-type copies.</summary>
        public void Configure(string id, ArtifactType artifactType)
        {
            if (TryGetComponent<RestorableArtifact>(out var artifact) && artifact.IsRestored)
                throw new InvalidOperationException("Reset restoration before changing identity.");
            // Reuse the shared contract's validation; identity is never generated from instance IDs.
            var identity = new ArtifactRestoredEvent(id, artifactType);
            artifactId = identity.ArtifactId;
            type = identity.Type;
        }
    }
}
