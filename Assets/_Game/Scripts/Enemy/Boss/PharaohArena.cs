using System;
using Game4026.Contracts;
using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Keeps the player away from the seated pharaoh:
    /// <list type="bullet">
    /// <item>A barrier (radius ~5 m) blocks the way; a player who still gets inside (room-scale walking) is pushed out.</item>
    /// <item><see cref="Open"/> (after the phase-change barrage): the barrier drops for a while, a corridor with swinging
    /// pendulum scythes appears between the player and the throne, and the boss takes ×2 damage.</item>
    /// <item>When it closes, a shockwave pushes a nearby player back with a short fade ("blink"), never a sliding push (VR comfort).</item>
    /// </list>
    /// Corridor and pendulums sit under <see cref="arenaRoot"/>, kept at the boss's spawn rotation (the boss may turn).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class PharaohArena : MonoBehaviour
    {
        [Serializable]
        public sealed class Settings
        {
            [Header("Barrier")]
            [Min(1f)] public float barrierRadius = 5f;
            [Tooltip("Seconds a player may stay inside the closed barrier before being pushed out.")]
            [Min(0f)] public float insideGrace = 0.6f;

            [Header("Opening")]
            [Min(1f)] public float openSeconds = 12f;
            [Min(1f)] public float openDamageMultiplier = 2f;
            [Tooltip("The shockwave ring starts pulsing this long before the barrier closes.")]
            [Min(0f)] public float closeWarning = 1.5f;

            [Header("Shockwave push")]
            [Tooltip("Players within barrier radius + this are pushed when it closes.")]
            [Min(0f)] public float pushMargin = 1.5f;
            [Tooltip("Where the player reappears: this far from the boss, on the line from the boss through the player.")]
            [Min(1f)] public float pushDistance = 12f;
            [Min(0f)] public float pushDamage = 8f;

            [Header("Pendulum scythes (hung from the ceiling)")]
            [Tooltip("Height of the blade's lowest point over the floor (chest height: crouching does not help, timing does).")]
            [Min(0.3f)] public float bladeBottomHeight = 1.2f;
            [Tooltip("Gap between the ceiling and the pivot.")]
            [Min(0f)] public float ceilingMargin = 0.3f;
            [Tooltip("Arm length used when no ceiling is found above a pendulum.")]
            [Min(1f)] public float fallbackArm = 14f;
            [Range(5f, 80f)] public float pendulumAmplitude = 35f;
            [Min(0.3f)] public float pendulumPeriod = 3.2f;
            [Tooltip("Half length of the blade's edge along the swing.")]
            [Min(0.1f)] public float bladeHalfLength = 2.2f;
            [Min(0.05f)] public float bladeHitRadius = 0.55f;
            [Min(0f)] public float pendulumDamage = 12f;
            [Min(0f)] public float pendulumHitCooldown = 1.2f;
        }

        [SerializeField] private Settings settings = new Settings();
        [Tooltip("Barrier wall (visual + solid collider + ArenaBarrier).")]
        [SerializeField] private GameObject barrier;
        [Tooltip("Parent of the corridor walls and pendulums; held at the boss's spawn rotation.")]
        [SerializeField] private Transform arenaRoot;
        [Tooltip("Corridor walls (shown while open).")]
        [SerializeField] private GameObject corridor;
        [Tooltip("Pendulum pivots (children of arenaRoot), each with children Arm (unit cylinder) and Blade. They are hung from the ceiling at spawn.")]
        [SerializeField] private Transform[] pendulums = new Transform[0];
        [Tooltip("Flat ring for the closing shockwave.")]
        [SerializeField] private Transform shockRing;

        private EnemyController controller;
        private EnemyHealth health;
        private Quaternion spawnRotation;
        private float openUntil;
        private float insideTime;
        private float[] nextPendulumHit = new float[0];
        private float[] armLength = new float[0];
        private float ringTime = -1f;
        private bool blinking;

        public static PharaohArena Current { get; private set; }
        public Settings Config => settings;
        public bool IsOpen { get; private set; }
        public float OpenRemaining => IsOpen ? Mathf.Max(0f, openUntil - controller.LocalTime) : 0f;

        public event Action<PharaohArena> Opened;
        public event Action<PharaohArena> Closed;
        /// <summary>(arena, pushed the player)</summary>
        public event Action<PharaohArena, bool> Shockwave;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            health = GetComponent<EnemyHealth>();
            nextPendulumHit = new float[pendulums.Length];
            armLength = new float[pendulums.Length];
        }

        private void OnEnable()
        {
            Current = this;
            spawnRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            SetOpen(false);
            SetActive(barrier, false);
            insideTime = 0f;
            blinking = false;
            ringTime = -1f;
            SetActive(shockRing != null ? shockRing.gameObject : null, false);
            HangPendulums();
        }

        /// <summary>
        /// Hangs every pendulum from the ceiling above it: pivot just under the ceiling, arm long enough that the blade's
        /// lowest point is <see cref="Settings.bladeBottomHeight"/> over the floor.
        /// </summary>
        private void HangPendulums()
        {
            float floor = transform.position.y;
            for (int i = 0; i < pendulums.Length; i++)
            {
                Transform pivot = pendulums[i];
                if (pivot == null)
                    continue;
                pivot.localRotation = Quaternion.identity;
                Vector3 p = pivot.position;
                float top = floor + settings.bladeBottomHeight + settings.fallbackArm;
                RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, floor + 2f, p.z), Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                for (int h = 0; h < hits.Length; h++)
                    if (!hits[h].transform.IsChildOf(transform) && hits[h].point.y < best)
                        best = hits[h].point.y;
                if (best < float.PositiveInfinity)
                    top = best - settings.ceilingMargin;
                pivot.position = new Vector3(p.x, top, p.z);
                float arm = Mathf.Max(1f, top - floor - settings.bladeBottomHeight);
                armLength[i] = arm;

                float s = pivot.lossyScale.y > 1e-5f ? pivot.lossyScale.y : 1f;
                Transform armT = pivot.Find("Arm");
                if (armT != null)
                {
                    armT.localPosition = new Vector3(0f, -arm * 0.5f / s, 0f);
                    Vector3 ls = armT.localScale;
                    armT.localScale = new Vector3(ls.x, arm * 0.5f / s, ls.z); // unit cylinder is 2 high
                }
                Transform blade = pivot.Find("Blade");
                if (blade != null)
                    blade.localPosition = new Vector3(0f, -arm / s, 0f);
            }
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            EnemyStateId state = controller.CurrentState;
            if (state == EnemyStateId.Dead || state == EnemyStateId.None)
            {
                if (IsOpen)
                    SetOpen(false);
                SetActive(barrier, false);
                SetActive(shockRing != null ? shockRing.gameObject : null, false);
                return;
            }
            SetActive(barrier, !IsOpen);
            if (arenaRoot != null)
                arenaRoot.rotation = spawnRotation;

            PlayerTarget player = PlayerTarget.Current;
            float now = controller.LocalTime;
            TickRing();

            if (IsOpen)
            {
                if (health.DamageMultiplier < settings.openDamageMultiplier)
                    health.DamageMultiplier = settings.openDamageMultiplier; // re-applied if a stagger reset it
                TickPendulums(now, player);
                if (openUntil - now <= settings.closeWarning)
                    PulseRing(now);
                if (now >= openUntil)
                    Close(true);
                return;
            }

            // Closed: nobody stays inside.
            if (player != null && !blinking && HorizontalDistance(player) < settings.barrierRadius)
            {
                insideTime += controller.DeltaTime;
                if (insideTime >= settings.insideGrace)
                    Blast(player, true);
            }
            else
                insideTime = 0f;
        }

        /// <summary>Drops the barrier: corridor + pendulums appear, the boss takes extra damage until it closes.</summary>
        public void Open()
        {
            if (IsOpen)
                return;
            openUntil = controller.LocalTime + settings.openSeconds;
            for (int i = 0; i < nextPendulumHit.Length; i++)
                nextPendulumHit[i] = 0f;
            SetOpen(true);
            Opened?.Invoke(this);
        }

        /// <summary>Raises the barrier again; with <paramref name="push"/>, a shockwave sends a nearby player back.</summary>
        public void Close(bool push)
        {
            if (!IsOpen)
                return;
            SetOpen(false);
            if (push)
            {
                PlayerTarget player = PlayerTarget.Current;
                Blast(player, player != null && HorizontalDistance(player) <= settings.barrierRadius + settings.pushMargin);
            }
            Closed?.Invoke(this);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            SetActive(barrier, !open && controller != null && controller.CurrentState != EnemyStateId.Dead);
            SetActive(corridor, open);
            for (int i = 0; i < pendulums.Length; i++)
                SetActive(pendulums[i] != null ? pendulums[i].gameObject : null, open);
            if (!open && health != null && Mathf.Approximately(health.DamageMultiplier, settings.openDamageMultiplier))
                health.DamageMultiplier = 1f;
        }

        // ---- Pendulums ----

        private void TickPendulums(float now, PlayerTarget player)
        {
            for (int i = 0; i < pendulums.Length; i++)
            {
                Transform pivot = pendulums[i];
                if (pivot == null)
                    continue;
                float phase = pendulums.Length > 1 ? i / (float)pendulums.Length : 0f;
                float angle = PendulumMath.Angle(now, settings.pendulumPeriod, settings.pendulumAmplitude, phase);
                pivot.localRotation = Quaternion.Euler(0f, 0f, angle);

                if (player == null || now < nextPendulumHit[i])
                    continue;
                Vector3 blade = pivot.position + pivot.TransformDirection(Vector3.down) * armLength[i];
                Vector3 along = pivot.TransformDirection(Vector3.right); // the edge lies along the swing
                Vector3 a = blade - along * settings.bladeHalfLength;
                Vector3 b = blade + along * settings.bladeHalfLength;
                if (EnemyProjectile.SegmentDistance(a, b, player.Body.position, player.Head.position) <= settings.bladeHitRadius)
                {
                    nextPendulumHit[i] = now + settings.pendulumHitCooldown;
                    player.Damageable?.ApplyPlayerDamage(settings.pendulumDamage);
                }
            }
        }

        // ---- Shockwave ----

        private void Blast(PlayerTarget player, bool pushPlayer)
        {
            insideTime = 0f;
            ringTime = 0f;
            if (pushPlayer && player != null && !blinking)
            {
                player.Damageable?.ApplyPlayerDamage(settings.pushDamage);
                Vector3 away = player.Body.position - transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-4f)
                    away = (arenaRoot != null ? arenaRoot.forward : transform.forward);
                away.Normalize();
                Vector3 to = transform.position + away * settings.pushDistance;
                if (NavMesh.SamplePosition(to, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    to = hit.position;
                StartCoroutine(BlinkRoutine(player, to, Quaternion.LookRotation(-away)));
            }
            Shockwave?.Invoke(this, pushPlayer);
        }

        private System.Collections.IEnumerator BlinkRoutine(PlayerTarget player, Vector3 to, Quaternion facing)
        {
            blinking = true;
            yield return PlayerRig.Blink(player, to, facing);
            blinking = false;
        }

        private void PulseRing(float now)
        {
            if (shockRing == null)
                return;
            SetActive(shockRing.gameObject, true);
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 18f);
            SetRingRadius(settings.barrierRadius * (0.95f + 0.1f * pulse));
        }

        /// <summary>After a blast the ring expands outward for half a second.</summary>
        private void TickRing()
        {
            if (shockRing == null || ringTime < 0f)
            {
                if (shockRing != null && !IsOpen && ringTime < 0f)
                    SetActive(shockRing.gameObject, false);
                return;
            }
            ringTime += controller.DeltaTime;
            float t = ringTime / 0.5f;
            if (t >= 1f)
            {
                ringTime = -1f;
                SetActive(shockRing.gameObject, false);
                return;
            }
            SetActive(shockRing.gameObject, true);
            SetRingRadius(Mathf.Lerp(settings.barrierRadius, settings.barrierRadius + settings.pushMargin + 4f, t));
        }

        private void SetRingRadius(float radius)
        {
            shockRing.position = new Vector3(transform.position.x, transform.position.y + 0.03f, transform.position.z);
            Vector3 parent = shockRing.parent != null ? shockRing.parent.lossyScale : Vector3.one;
            shockRing.localScale = new Vector3(radius * 2f / parent.x, 0.01f / parent.y, radius * 2f / parent.z);
        }

        private float HorizontalDistance(PlayerTarget player) =>
            EnemyGeometry.HorizontalDistance(transform.position, player.Body.position);

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }
    }
}
