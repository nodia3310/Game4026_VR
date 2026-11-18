using System;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>One boss phase, edited as a list entry on <see cref="PharaohBoss"/> in the Inspector.</summary>
    [Serializable]
    public sealed class BossPhase
    {
        [Tooltip("Label shown in logs and the debug label.")]
        public string Name = "Phase";

        [Tooltip("Enter this phase when health / max health drops to or below this value. The first phase uses 1.")]
        [Range(0f, 1f)] public float HealthThreshold = 1f;

        [Tooltip("Attack speed while in this phase (windup and cooldown are divided by it).")]
        [Min(0.1f)] public float AttackSpeedMultiplier = 1f;

        [Tooltip("Seconds of invulnerability when entering this phase.")]
        [Min(0f)] public float InvulnerableSeconds = 2f;

        [Tooltip("Start the boss's minion encounter when entering this phase.")]
        public bool StartMinions;

        [Tooltip("Objects switched on when entering this phase (e.g. a weak point). They are off before it.")]
        public GameObject[] ActivateObjects = new GameObject[0];

        public BossPhase() { }

        public BossPhase(string name, float healthThreshold, float attackSpeed = 1f, float invulnerableSeconds = 2f,
            bool startMinions = false, params GameObject[] activate)
        {
            Name = name;
            HealthThreshold = healthThreshold;
            AttackSpeedMultiplier = attackSpeed;
            InvulnerableSeconds = invulnerableSeconds;
            StartMinions = startMinions;
            ActivateObjects = activate ?? new GameObject[0];
        }
    }
}
