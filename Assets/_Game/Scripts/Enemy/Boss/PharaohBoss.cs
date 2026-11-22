using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Pharaoh boss phase layer (spec 4.7) on top of the normal enemy (EnemyHealth + state machine).
    /// Phases are a list in the Inspector: threshold, attack speed, invulnerability, minions, objects to enable.
    /// Default: 100–60 % melee, 60–25 % summons minions, 25–0 % faster attacks and an exposed weak point.
    /// The moves come from <see cref="PharaohStand"/> (spectral hand) when the boss has one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class PharaohBoss : MonoBehaviour
    {
        [SerializeField] private List<BossPhase> phases = new List<BossPhase>
        {
            new BossPhase("Melee", 1f, invulnerableSeconds: 0f),
            new BossPhase("Summon", 0.6f, startMinions: true),
            new BossPhase("Enraged", 0.25f, attackSpeed: 1.5f),
        };

        [Tooltip("Child encounter that spawns minions (uses the scene's EnemyPoolManager).")]
        [SerializeField] private EncounterController minionEncounter;
        [SerializeField] private bool logPhases = true;

        private readonly List<float> thresholds = new List<float>();
        private EnemyController controller;
        private EnemyHealth health;
        private EnemyAttack attack;
        private BossPhaseTracker tracker;
        private float invulnerableUntil = -1f;

        /// <summary>(boss, previous phase index, new phase index)</summary>
        public event Action<PharaohBoss, int, int> PhaseChanged;

        public int CurrentPhase => tracker != null ? tracker.Current : 0;
        public string CurrentPhaseName => phases.Count > 0 ? phases[CurrentPhase].Name : string.Empty;
        public IReadOnlyList<BossPhase> Phases => phases;
        public EncounterController MinionEncounter => minionEncounter;
        public bool IsPhaseInvulnerable => invulnerableUntil >= 0f;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            health = GetComponent<EnemyHealth>();
            attack = GetComponent<EnemyAttack>();
            BuildTracker();
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            controller.Killed += HandleEnded;
            controller.Despawned += HandleEnded;
            ResetPhases();
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            controller.Killed -= HandleEnded;
            controller.Despawned -= HandleEnded;
        }

        private void Update()
        {
            if (invulnerableUntil >= 0f && Time.time >= invulnerableUntil)
                EndPhaseInvulnerability();
        }

        /// <summary>Runtime / test setup (call while the object is inactive, before it is enabled).</summary>
        public void Configure(List<BossPhase> bossPhases, EncounterController minions)
        {
            phases = bossPhases ?? new List<BossPhase>();
            minionEncounter = minions;
            BuildTracker();
        }

        private void BuildTracker()
        {
            thresholds.Clear();
            for (int i = 0; i < phases.Count; i++)
                thresholds.Add(phases[i].HealthThreshold);
            tracker = new BossPhaseTracker(thresholds);
        }

        private void ResetPhases()
        {
            tracker.Reset();
            invulnerableUntil = -1f;
            for (int i = 1; i < phases.Count; i++)
                SetObjectsActive(phases[i], false);
            if (phases.Count > 0)
                attack.SpeedMultiplier = phases[0].AttackSpeedMultiplier;
            if (minionEncounter != null)
                minionEncounter.ResetEncounter();
        }

        private void HandleDamaged(EnemyHealth _, EnemyDamageResult result)
        {
            if (result.Killed || health.MaxHealth <= 0f)
                return;

            float fraction = health.CurrentHealth / health.MaxHealth;
            if (!tracker.TryAdvance(fraction, out int from, out int to))
                return;

            // A big hit may skip a phase; still run every phase entered so none of their effects is lost.
            float invulnerable = 0f;
            for (int i = from + 1; i <= to; i++)
            {
                EnterPhase(i);
                invulnerable = Mathf.Max(invulnerable, phases[i].InvulnerableSeconds);
            }

            if (invulnerable > 0f)
            {
                health.IsInvulnerable = true;
                invulnerableUntil = Time.time + invulnerable;
            }

            if (logPhases)
                Debug.Log($"[PharaohBoss] phase {from} → {to} ({phases[to].Name}) at {fraction:P0} health", this);
            PhaseChanged?.Invoke(this, from, to);
        }

        private void EnterPhase(int index)
        {
            BossPhase phase = phases[index];
            attack.SpeedMultiplier = phase.AttackSpeedMultiplier;
            SetObjectsActive(phase, true);
            if (phase.StartMinions && minionEncounter != null)
                minionEncounter.StartEncounter();
            OnPhaseEntered(index);
        }

        /// <summary>
        /// Per-phase hook. The move set lives in <see cref="PharaohStand"/>, which reads <see cref="CurrentPhase"/>
        /// (phase 2+ adds the combo slam) and the attack speed set above.
        /// </summary>
        private void OnPhaseEntered(int index)
        {
            // TODO(boss): summon pose / taunt while minions arrive (phase 1).
        }

        private void EndPhaseInvulnerability()
        {
            invulnerableUntil = -1f;
            // Never undo the invulnerability of spawn or of a force-cleared (dying) boss.
            EnemyStateId state = controller.CurrentState;
            if (state != EnemyStateId.Dead && state != EnemyStateId.Spawn)
                health.IsInvulnerable = false;
        }

        private void HandleEnded(EnemyController _)
        {
            invulnerableUntil = -1f;
            if (minionEncounter != null && minionEncounter.IsActive)
                minionEncounter.ForceClear();
        }

        private static void SetObjectsActive(BossPhase phase, bool active)
        {
            GameObject[] objects = phase.ActivateObjects;
            if (objects == null)
                return;
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] != null)
                    objects[i].SetActive(active);
        }
    }
}
