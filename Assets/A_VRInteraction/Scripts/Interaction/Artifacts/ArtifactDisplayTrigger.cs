using System;
using System.Collections.Generic;
using Game4026.Artifacts;
using Game4026.Contracts;
using UnityEngine;
using UnityEngine.Events;

namespace Game4026.Interaction.Artifacts
{
    /// <summary>
    /// A display accepts one authored artifact. Samples its trigger volume each physics tick:
    /// this also detects sleeping bodies and objects already inside when the display is enabled.
    /// Use an unskewed BoxCollider transform (avoid rotated parents with nonuniform scale).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArtifactDisplayTrigger : MonoBehaviour
    {
        [SerializeField] private string expectedArtifactId;
        [SerializeField] private ArtifactType expectedType;
        [SerializeField] private Transform snapPoint;
        [SerializeField, Min(0f)] private float dwellSeconds = 0.35f;
        [SerializeField] private bool checkPosition = true;
        [SerializeField, Min(0f)] private float positionTolerance = 0.2f;
        [SerializeField] private bool checkRotation;
        [SerializeField, Range(0f, 180f)] private float angleTolerance = 25f;
        [SerializeField] private LayerMask artifactLayers = ~0;
        [SerializeField] private UnityEvent onRestored = new UnityEvent();

        private BoxCollider volume;
        private Collider[] overlaps = new Collider[32];
        private readonly HashSet<RestorableArtifact> present = new HashSet<RestorableArtifact>();
        private readonly Dictionary<RestorableArtifact, float> dwell = new Dictionary<RestorableArtifact, float>();
        private readonly List<RestorableArtifact> stale = new List<RestorableArtifact>();
        private RestorableArtifact restoredArtifact;
        private ArtifactRestorationTracker tracker;

        public bool IsRestored { get; private set; }
        public UnityEvent Restored => onRestored;
        public event Action<ArtifactRestoredEvent> OnArtifactRestored;

        public void Configure(string id, ArtifactType type, Transform target, float requiredDwell = 0.35f,
            bool usePositionTolerance = true, float maxDistance = 0.2f,
            bool useAngleTolerance = false, float maxAngle = 25f)
        {
            if (IsRestored) throw new InvalidOperationException("Reset the display before configuring it.");
            _ = new ArtifactRestoredEvent(id, type);
            if (!target) throw new ArgumentNullException(nameof(target));
            expectedArtifactId = id;
            expectedType = type;
            snapPoint = target;
            dwellSeconds = Mathf.Max(0f, requiredDwell);
            checkPosition = usePositionTolerance;
            positionTolerance = Mathf.Max(0f, maxDistance);
            checkRotation = useAngleTolerance;
            angleTolerance = Mathf.Clamp(maxAngle, 0f, 180f);
            ClearPending();
        }

        /// <summary>Optional. A new run needs a new tracker; this component never undoes global progress.</summary>
        public void SetTracker(ArtifactRestorationTracker restorationTracker)
        {
            if (IsRestored) throw new InvalidOperationException("Reset the display before replacing its tracker.");
            tracker = restorationTracker;
        }

        private void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        private void OnEnable() { ClearPending(); }
        private void OnDisable() { ClearPending(); }
        private void FixedUpdate() { Evaluate(Time.fixedDeltaTime); }

        /// <summary>One fixed-step evaluation; also supports deterministic component tests.</summary>
        public void Evaluate(float deltaTime)
        {
            if (!volume) volume = GetComponent<BoxCollider>();
            if (!isActiveAndEnabled || !volume.enabled || !volume.isTrigger || !snapPoint)
            {
                ClearPending();
                return;
            }
            if (IsRestored) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            var scale = transform.lossyScale;
            var halfSize = Vector3.Scale(volume.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var center = transform.TransformPoint(volume.center);
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(center, halfSize, overlaps, transform.rotation,
                    artifactLayers, QueryTriggerInteraction.Ignore);
                if (count < overlaps.Length) break;
                Array.Resize(ref overlaps, overlaps.Length * 2);
            } while (true);

            present.Clear();
            for (var i = 0; i < count; i++)
            {
                var hit = overlaps[i];
                overlaps[i] = null;
                if (!hit || !hit.attachedRigidbody) continue;
                var artifact = hit.attachedRigidbody.GetComponent<RestorableArtifact>();
                if (artifact && artifact.isActiveAndEnabled) present.Add(artifact);
            }

            stale.Clear();
            foreach (var entry in dwell) if (!present.Contains(entry.Key)) stale.Add(entry.Key);
            foreach (var artifact in stale) dwell.Remove(artifact);

            foreach (var artifact in present)
            {
                var identity = artifact.Identity;
                var acceptable = !artifact.IsRestored && !artifact.IsHeld && identity.IsValid
                    && string.Equals(identity.ArtifactId, expectedArtifactId, StringComparison.Ordinal)
                    && identity.Type == expectedType
                    && (!checkPosition || Vector3.Distance(artifact.transform.position, snapPoint.position) <= positionTolerance)
                    && (!checkRotation || Quaternion.Angle(artifact.transform.rotation, snapPoint.rotation) <= angleTolerance);
                if (!acceptable) { dwell.Remove(artifact); continue; }

                dwell.TryGetValue(artifact, out var elapsed);
                elapsed += deltaTime;
                dwell[artifact] = elapsed;
                if (elapsed < dwellSeconds || !artifact.TryLockTo(snapPoint)) continue;
                // Lock first so tracker listeners observe the completed physical state.
                if (tracker != null && !tracker.TryRestore(identity.ArtifactId, identity.Type))
                {
                    artifact.ResetRestoration();
                    dwell.Remove(artifact);
                    continue;
                }

                restoredArtifact = artifact;
                IsRestored = true;
                var notification = new ArtifactRestoredEvent(identity.ArtifactId, identity.Type);
                ClearPending();
                OnArtifactRestored?.Invoke(notification);
                onRestored.Invoke();
                return;
            }
        }

        /// <summary>
        /// Reverts the accepted object's pre-lock state and allows another placement.
        /// If a tracker was injected, inject a fresh run tracker before restoring again.
        /// </summary>
        public void ResetRestoration()
        {
            if (restoredArtifact) restoredArtifact.ResetRestoration();
            restoredArtifact = null;
            IsRestored = false;
            ClearPending();
        }

        private void ClearPending()
        {
            present.Clear();
            dwell.Clear();
            stale.Clear();
            Array.Clear(overlaps, 0, overlaps.Length);
        }
    }
}
