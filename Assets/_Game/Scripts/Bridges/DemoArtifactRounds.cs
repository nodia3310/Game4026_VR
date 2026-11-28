using System;
using Game4026.Contracts;
using Game4026.Enemy;
using Game4026.Interaction.Artifacts;
using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Bridges
{
    /// <summary>
    /// The demo hall as artifact rounds (replaces <see cref="DemoFlow"/>'s single hall encounter). Each round's guardian is an
    /// exhibit come alive: when it spawns (in front of its glass case) the exhibit vanishes, the case glass shatters onto
    /// the floor with a sound, so the player sees which case is empty. One enemy of the guardian prefab carries a small copy
    /// of the exhibit over its head. When it falls, the shrunken exhibit (A's <see cref="RestorableArtifact"/>) floats where
    /// it died; putting it back in its case (<see cref="ArtifactDisplayTrigger"/>) grows it back to full size in its old
    /// spot, banishes the round's remaining enemies and, after a pause, starts the next round. The last one back → the boss.
    /// On a restart the exhibits are put back and the artifacts hidden; broken glass stays broken.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoArtifactRounds : MonoBehaviour, IDemoHallSteps
    {
        [Serializable]
        public sealed class Round
        {
            [Tooltip("This round's enemies (its own waves and spawn points).")]
            public EncounterController encounter;
            [Tooltip("The first enemy spawned from this prefab carries the artifact. Empty = the first enemy spawned.")]
            public EnemyController guardianPrefab;
            [Tooltip("Where the guardian appears (in front of its case). Empty = its encounter spawn point.")]
            public Transform guardianSpawn;
            [Header("Case")]
            [Tooltip("The exhibit in the case: hidden while the guardian is out, shown again once the artifact is back.")]
            public GameObject exhibit;
            [Tooltip("The case glass (renderer + collider) turned off when it shatters.")]
            public GameObject[] glass = new GameObject[0];
            [Tooltip("Broken glass on the floor, shown when the case shatters.")]
            public GameObject shards;
            [Header("Artifact")]
            public RestorableArtifact artifact;
            [Tooltip("The artifact's shrunken exhibit model; its parts are laid out like the exhibit's, so growing it to the " +
                     "exhibit's pose makes it match the exhibit exactly.")]
            public Transform artifactModel;
            public ArtifactDisplayTrigger display;
            [Tooltip("Optional glow at the case, on while the artifact is out.")]
            public GameObject displayGlow;
            [Tooltip("Where the artifact appears if the guardian's death was not seen.")]
            public Transform fallbackSpawn;
            [Tooltip("Height of the carried copy over the guardian's feet.")]
            [Min(0.5f)] public float markHeight = 2.4f;
            [Tooltip("HUD names (English).")]
            public string guardianName = "guardian";
            public string artifactName = "artifact";
        }

        [SerializeField] private DemoFlow flow;
        [SerializeField] private Round[] rounds = new Round[0];
        [Tooltip("Seconds between an artifact back on its display and the next round.")]
        [SerializeField, Min(0f)] private float pauseAfterRestore = 3f;
        [Header("Case")]
        [SerializeField] private AudioClip glassBreakSound;
        [SerializeField, Range(0f, 1f)] private float glassBreakVolume = 1f;
        [Tooltip("Seconds for the returned artifact to grow back into the exhibit.")]
        [SerializeField, Min(0.05f)] private float growSeconds = 0.8f;
        [Header("Dropped artifact")]
        [Tooltip("Height over the floor where the artifact floats until picked up.")]
        [SerializeField, Min(0.2f)] private float floatHeight = 1.1f;
        [SerializeField, Min(0f)] private float spinSpeed = 60f;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.06f;
        [Tooltip("Dropped this far below where it floated (fell out of the map) → it floats there again.")]
        [SerializeField, Min(1f)] private float lostDrop = 4f;
        [Header("Guardian mark")]
        [Tooltip("Scale of the carried copy relative to the artifact.")]
        [SerializeField, Min(0.1f)] private float markScale = 1f;
        [SerializeField] private bool markLight = true;
        [SerializeField] private Color markLightColor = new Color(1f, 0.8f, 0.35f);

        private ArtifactRoundsModel model;
        private EnemyController guardian;
        private Transform mark;
        private Vector3 guardianLastPosition;
        private bool sawGuardian;
        private bool shown, everHeld;
        private Vector3 floatBase;
        private float shownTime;
        // growing back into the exhibit
        private Round growing;
        private float growStart;
        private Vector3 growFromPos, growFromScale;
        private Quaternion growFromRot;
        private Vector3 modelLocalPos, modelLocalScale;
        private Quaternion modelLocalRot;

        public ArtifactRoundsModel Model => EnsureModel();
        public EnemyController Guardian => guardian;
        public Round CurrentRound => rounds.Length > 0 ? rounds[Mathf.Clamp(EnsureModel().Round, 0, rounds.Length - 1)] : null;
        public bool IsArtifactShown => shown;
        public bool IsGrowing => growing != null;

        /// <summary>Runtime / test setup (before the hall begins).</summary>
        public void Configure(DemoFlow demoFlow, Round[] hallRounds, float pause)
        {
            Unsubscribe();
            flow = demoFlow;
            rounds = hallRounds ?? new Round[0];
            pauseAfterRestore = pause;
            model = null;
            if (isActiveAndEnabled)
                Subscribe();
            if (flow != null)
                flow.SetHallSteps(this);
            ResetCases();
        }

        private ArtifactRoundsModel EnsureModel() => model ??= new ArtifactRoundsModel(rounds.Length, pauseAfterRestore);

        private void Awake()
        {
            if (flow == null)
                flow = GetComponent<DemoFlow>();
            if (flow != null)
                flow.SetHallSteps(this);
            ResetCases();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            if (flow != null && ReferenceEquals(flow.HallSteps, this))
                flow.SetHallSteps(null);
        }

        private void Subscribe()
        {
            foreach (Round round in rounds)
            {
                if (round == null)
                    continue;
                if (round.encounter != null)
                    round.encounter.EnemySpawned += HandleSpawned;
                if (round.display != null)
                    round.display.OnArtifactRestored += HandleRestored;
            }
        }

        private void Unsubscribe()
        {
            foreach (Round round in rounds)
            {
                if (round == null)
                    continue;
                if (round.encounter != null)
                    round.encounter.EnemySpawned -= HandleSpawned;
                if (round.display != null)
                    round.display.OnArtifactRestored -= HandleRestored;
            }
            ReleaseGuardian();
        }

        // ---- IDemoHallSteps ----

        public bool IsHallDone => EnsureModel().IsComplete;

        public void BeginHall()
        {
            ResetCases();
            if (EnsureModel().Begin() == ArtifactRoundAction.StartRound)
                StartRound();
        }

        public void StopHall()
        {
            foreach (Round round in rounds)
                if (round?.encounter != null)
                    round.encounter.ForceClear();
            ReleaseGuardian();
            FinishGrow();
            if (shown && CurrentRound?.artifact != null && !CurrentRound.artifact.IsRestored)
                HideArtifact(CurrentRound);
            EnsureModel().Stop();
        }

        public void ResetHall()
        {
            ReleaseGuardian();
            foreach (Round round in rounds)
                if (round?.encounter != null)
                    round.encounter.ResetEncounter();
            ResetCases();
            EnsureModel().Stop();
        }

        public string HallHud
        {
            get
            {
                Round round = CurrentRound;
                ArtifactRoundsModel m = EnsureModel();
                if (round == null)
                    return string.Empty;
                int n = m.RoundCount;
                switch (m.Stage)
                {
                    case ArtifactRoundStage.Fighting:
                        return $"Defeat the {round.guardianName} carrying the {round.artifactName}  ({m.Round + 1}/{n})";
                    case ArtifactRoundStage.ArtifactOut:
                        return $"Return the {round.artifactName} to its case  ({m.RestoredCount}/{n})";
                    case ArtifactRoundStage.Restored:
                    case ArtifactRoundStage.Done:
                        return $"The {round.artifactName} is restored  ({m.RestoredCount}/{n})";
                }
                return string.Empty;
            }
        }

        // ---- rounds ----

        private void StartRound()
        {
            ReleaseGuardian();
            sawGuardian = false;
            Round round = CurrentRound;
            if (round?.encounter == null)
            {
                Debug.LogWarning($"[DemoArtifactRounds] round {Model.Round + 1} has no encounter; its artifact comes out at once.", this);
                Shatter(round);
                if (Model.GuardianDown())
                    ShowArtifact(round);
                return;
            }
            round.encounter.ResetEncounter();
            round.encounter.StartEncounter();
        }

        private void HandleSpawned(EncounterController encounter, EnemyController enemy, EnemyController prefab)
        {
            Round round = CurrentRound;
            if (guardian != null || round == null || encounter != round.encounter || Model.Stage != ArtifactRoundStage.Fighting)
                return;
            if (round.guardianPrefab != null && prefab != round.guardianPrefab)
                return;
            guardian = enemy;
            guardian.Killed += HandleGuardianKilled;
            guardian.Despawned += HandleGuardianDespawned;
            sawGuardian = true;
            if (round.guardianSpawn != null)
                PlaceAt(enemy, round.guardianSpawn);
            guardianLastPosition = enemy.transform.position;
            mark = BuildMark(round, enemy.transform);
            Shatter(round);
        }

        private static void PlaceAt(EnemyController enemy, Transform spot)
        {
            Vector3 position = spot.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                position = hit.position;
            if (enemy.TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh)
                agent.Warp(position);
            else
                enemy.transform.position = position;
            enemy.transform.rotation = spot.rotation;
        }

        private void HandleGuardianKilled(EnemyController enemy)
        {
            guardianLastPosition = enemy.transform.position;
            ReleaseGuardian();
            if (Model.GuardianDown())
                ShowArtifact(CurrentRound);
        }

        private void HandleGuardianDespawned(EnemyController enemy) => ReleaseGuardian();

        private void ReleaseGuardian()
        {
            if (guardian != null)
            {
                guardian.Killed -= HandleGuardianKilled;
                guardian.Despawned -= HandleGuardianDespawned;
                guardian = null;
            }
            if (mark != null)
            {
                Destroy(mark.gameObject);
                mark = null;
            }
        }

        private void HandleRestored(ArtifactRestoredEvent restored)
        {
            Round round = CurrentRound;
            if (round == null || round.artifact == null || restored.ArtifactId != round.artifact.Identity.ArtifactId)
                return;
            if (!Model.ArtifactRestored(Time.time))
                return;
            shown = false;
            if (round.displayGlow != null)
                round.displayGlow.SetActive(false);
            if (round.encounter != null)
                round.encounter.ForceClear(); // banish the rest of the round
            ReleaseGuardian();
            StartGrow(round);
        }

        private void Update()
        {
            ArtifactRoundsModel m = EnsureModel();
            Round round = CurrentRound;
            if (growing != null)
                Grow();
            if (round == null)
                return;

            if (guardian != null)
                guardianLastPosition = guardian.transform.position;

            // Every enemy of the round gone without the guardian's death seen (wrong prefab, skipped): drop it anyway.
            if (m.Stage == ArtifactRoundStage.Fighting && round.encounter != null && round.encounter.IsCleared)
            {
                ReleaseGuardian();
                Shatter(round);
                if (m.GuardianDown())
                    ShowArtifact(round);
            }

            if (shown)
                Float(round);

            if (m.Tick(Time.time) == ArtifactRoundAction.StartRound)
                StartRound();

            if (mark != null)
                mark.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);
        }

        // ---- case ----

        private void Shatter(Round round)
        {
            if (round == null)
                return;
            bool wasWhole = false;
            foreach (GameObject pane in round.glass)
                if (pane != null && pane.activeSelf)
                {
                    pane.SetActive(false);
                    wasWhole = true;
                }
            if (round.exhibit != null)
                round.exhibit.SetActive(false);
            if (round.shards != null)
                round.shards.SetActive(true);
            if (wasWhole && glassBreakSound != null)
            {
                Vector3 at = round.exhibit != null ? round.exhibit.transform.position
                    : round.display != null ? round.display.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(glassBreakSound, at, glassBreakVolume);
            }
        }

        /// <summary>
        /// Exhibits in place, artifacts hidden and off their displays (start / restart). Broken glass stays broken
        /// (a case broken in an earlier run is already open; its exhibit still vanishes, without the break sound).
        /// </summary>
        private void ResetCases()
        {
            FinishGrow();
            foreach (Round round in rounds)
            {
                if (round == null)
                    continue;
                HideArtifact(round);
                if (round.exhibit != null)
                    round.exhibit.SetActive(true);
            }
        }

        // ---- artifact ----

        private void ShowArtifact(Round round)
        {
            if (round?.artifact == null)
                return;
            Vector3 at = sawGuardian ? guardianLastPosition
                : round.fallbackSpawn != null ? round.fallbackSpawn.position
                : round.display != null ? round.display.transform.position + Vector3.right
                : transform.position;
            // Down to the floor under the death point (a flier may die in the air), then up to hand height.
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                at = hit.point;
            floatBase = at + Vector3.up * floatHeight;
            // Activate first: A's PhysicalGrabbable.Awake turns gravity on, which would drop it at once.
            round.artifact.gameObject.SetActive(true);
            PlaceFloating(round.artifact);
            if (round.displayGlow != null)
                round.displayGlow.SetActive(true);
            shown = true;
        }

        private void PlaceFloating(RestorableArtifact artifact)
        {
            artifact.transform.SetPositionAndRotation(floatBase, Quaternion.identity);
            if (artifact.TryGetComponent(out Rigidbody body))
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = floatBase;
                body.isKinematic = true; // floats until grabbed
                body.useGravity = false;
            }
            everHeld = false;
            shownTime = Time.time;
        }

        private void Float(Round round)
        {
            RestorableArtifact artifact = round.artifact;
            if (artifact == null || artifact.IsRestored)
                return;
            if (artifact.IsHeld)
            {
                everHeld = true;
                return;
            }
            if (everHeld)
            {
                if (artifact.transform.position.y < floatBase.y - lostDrop)
                    PlaceFloating(artifact); // fell out of the map: back where it dropped
                else
                    HoldIfInCase(round);
                return;
            }
            float t = Time.time - shownTime;
            artifact.transform.SetPositionAndRotation(floatBase + Vector3.up * (Mathf.Sin(t * 2f) * bobAmplitude),
                Quaternion.Euler(0f, t * spinSpeed, 0f));
        }

        /// <summary>
        /// Let go inside its case's volume → it stops there instead of falling out, so the display's dwell check can
        /// take it (cases have no shelf where the volume is; the exhibit may stand out of reach).
        /// </summary>
        private static void HoldIfInCase(Round round)
        {
            if (round.display == null || !round.display.TryGetComponent(out BoxCollider volume) ||
                !round.artifact.TryGetComponent(out Rigidbody body) || body.isKinematic)
                return;
            Vector3 p = round.artifact.transform.position;
            if ((volume.ClosestPoint(p) - p).sqrMagnitude > 1e-6f)
                return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }

        // Locked on the display → the model leaves the artifact, flies to the exhibit's spot growing to full size,
        // then the real exhibit is shown again and the artifact hidden.
        private void StartGrow(Round round)
        {
            FinishGrow();
            if (round.artifactModel == null || round.exhibit == null)
            {
                if (round.exhibit != null)
                    round.exhibit.SetActive(true);
                return;
            }
            Transform m = round.artifactModel;
            modelLocalPos = m.localPosition;
            modelLocalRot = m.localRotation;
            modelLocalScale = m.localScale;
            growFromPos = m.position;
            growFromRot = m.rotation;
            growFromScale = m.lossyScale;
            m.SetParent(null, true);
            growing = round;
            growStart = Time.time;
        }

        private void Grow()
        {
            Transform m = growing.artifactModel;
            Transform target = growing.exhibit.transform;
            float t = Mathf.Clamp01((Time.time - growStart) / growSeconds);
            float e = t * t * (3f - 2f * t);
            m.SetPositionAndRotation(Vector3.Lerp(growFromPos, target.position, e), Quaternion.Slerp(growFromRot, target.rotation, e));
            m.localScale = Vector3.Lerp(growFromScale, target.lossyScale, e);
            if (t >= 1f)
                FinishGrow();
        }

        private void FinishGrow()
        {
            if (growing == null)
                return;
            Round round = growing;
            growing = null;
            Transform m = round.artifactModel;
            if (m != null && round.artifact != null)
            {
                m.SetParent(round.artifact.transform, false);
                m.localPosition = modelLocalPos;
                m.localRotation = modelLocalRot;
                m.localScale = modelLocalScale;
                round.artifact.gameObject.SetActive(false); // the real exhibit takes over
            }
            if (round.exhibit != null)
                round.exhibit.SetActive(true);
        }

        private void HideArtifact(Round round)
        {
            shown = false;
            if (round == null)
                return;
            if (round.display != null && round.display.IsRestored)
                round.display.ResetRestoration(); // also unlocks the artifact
            if (round.artifact != null)
            {
                if (round.artifact.IsRestored)
                    round.artifact.ResetRestoration();
                round.artifact.gameObject.SetActive(false);
            }
            if (round.displayGlow != null)
                round.displayGlow.SetActive(false);
        }

        // ---- guardian mark: a floating copy of the artifact's meshes over the guardian ----

        private Transform BuildMark(Round round, Transform carrier)
        {
            if (round.artifact == null)
                return null;
            var root = new GameObject("Carried Artifact Mark").transform;
            root.SetParent(carrier, false);
            root.localPosition = Vector3.up * round.markHeight;
            Transform source = round.artifact.transform;
            foreach (MeshRenderer renderer in round.artifact.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !ActiveUnder(renderer.transform, source) ||
                    !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;
                var part = new GameObject(renderer.name);
                part.transform.SetParent(root, false);
                part.transform.localPosition = source.InverseTransformPoint(renderer.transform.position) * markScale;
                part.transform.localRotation = Quaternion.Inverse(source.rotation) * renderer.transform.rotation;
                part.transform.localScale = Vector3.Scale(renderer.transform.lossyScale,
                    InverseScale(source.lossyScale)) * markScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = part.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (markLight)
            {
                var light = new GameObject("Glow").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.type = LightType.Point;
                light.color = markLightColor;
                light.range = 2.5f;
                light.intensity = 2f;
                light.shadows = LightShadows.None;
            }
            return root;
        }

        /// <summary>Active below <paramref name="root"/> (the root itself is hidden until the artifact drops).</summary>
        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (!t.gameObject.activeSelf)
                    return false;
            return true;
        }

        private static Vector3 InverseScale(Vector3 s) =>
            new Vector3(s.x != 0f ? 1f / s.x : 0f, s.y != 0f ? 1f / s.y : 0f, s.z != 0f ? 1f / s.z : 0f);
    }
}
