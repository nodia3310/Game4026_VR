using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// The pharaoh stays on his throne; a translucent stand behind him attacks with a giant hand:
    /// <list type="bullet">
    /// <item>Slam: the hand flies above the player, a floor marker grows (tracks the player for the first half,
    /// then locks), the palm drops; it then stays stuck on the floor — hitting it damages the boss.</item>
    /// <item>Sweep: the hand skims across a lane through the player at chest height; step out of the lane or crouch.</item>
    /// <item>ComboSlam (phase 2+): several quick slams following the player; only the last one gets stuck.</item>
    /// </list>
    /// VR comfort: markers are small, so one step (or a crouch) is always enough. Time follows the boss's hit-stop clock
    /// and the phase attack speed (<see cref="EnemyAttack.SpeedMultiplier"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class PharaohStand : MonoBehaviour
    {
        [Serializable]
        public sealed class Settings
        {
            [Header("Timing")]
            [Min(0f)] public float firstMoveDelay = 2f;
            [Tooltip("Rest between two moves (divided by the phase attack speed).")]
            [Min(0f)] public float restTime = 2.2f;
            [Min(0.05f)] public float travelTime = 0.55f;
            [Min(0.05f)] public float returnTime = 0.6f;

            [Header("Slam")]
            [Min(0.1f)] public float slamTelegraph = 1.4f;
            [Tooltip("Fraction of the telegraph during which the marker still follows the player.")]
            [Range(0f, 1f)] public float slamTrackFraction = 0.5f;
            [Min(0.1f)] public float slamRadius = 1.1f;
            [Min(0.5f)] public float slamHoverHeight = 3.2f;
            [Tooltip("The hovering hand waits this far toward the boss from the target, so the player sees it without looking up.")]
            [Min(0f)] public float slamHoverForward = 2f;
            [Min(0.02f)] public float slamDropTime = 0.14f;
            [Min(0f)] public float slamDamage = 20f;
            [Tooltip("Seconds the hand stays on the floor after the (last) slam; hit it to damage the boss.")]
            [Min(0f)] public float stuckTime = 1.8f;

            [Header("Combo slam")]
            [Min(2)] public int comboCount = 3;
            [Min(0.1f)] public float comboTelegraph = 0.85f;
            [Min(0f)] public float comboPause = 0.25f;

            [Header("Sweep")]
            [Min(0.1f)] public float sweepTelegraph = 1.5f;
            [Min(0.1f)] public float sweepDuration = 0.75f;
            [Min(1f)] public float sweepHalfLength = 5f;
            [Tooltip("Height of the hand over the floor while sweeping.")]
            [Min(0.2f)] public float sweepHeight = 1.3f;
            [Tooltip("Horizontal reach of the sweeping hand (half the lane width).")]
            [Min(0.1f)] public float sweepReach = 0.8f;
            [Tooltip("A player whose head is at or below this height over the floor ducks under the sweep.")]
            [Min(0.2f)] public float crouchHeight = 1.25f;
            [Min(0f)] public float sweepDamage = 15f;
        }

        private enum Step { Hidden, Rest, Travel, Telegraph, Drop, Stuck, Return, SweepTelegraph, Sweep }

        [SerializeField] private Settings settings = new Settings();
        [Tooltip("The stand's body (floats behind the throne).")]
        [SerializeField] private Transform figure;
        [SerializeField] private PharaohHand hand;
        [Tooltip("Where the hand waits between moves (child of the figure).")]
        [SerializeField] private Transform handRest;
        [Tooltip("Flat disc shown on the floor where the palm will land.")]
        [SerializeField] private Transform slamMarker;
        [Tooltip("Flat strip shown along the sweep lane (unit length on Z, unit width on X).")]
        [SerializeField] private Transform sweepMarker;
        [SerializeField, Min(0f)] private float floatAmplitude = 0.15f;
        [SerializeField, Min(0f)] private float floatSpeed = 1.2f;

        private EnemyController controller;
        private EnemyAttack attack;
        private PharaohBoss boss;
        private Vector3 figureBase;
        private Step step = Step.Hidden;
        private StandMove move;
        private StandMove? lastMove;
        private float stepTime, stepLength;
        private int slamsLeft;
        private bool hitThisMove;
        private Vector3 fromPos, target, sweepStart, sweepEnd;
        private Quaternion fromRot;

        public Settings Config => settings;
        public PharaohHand Hand => hand;
        public bool IsBusy => step != Step.Hidden && step != Step.Rest;
        public bool IsHandStuck => step == Step.Stuck;
        public StandMove CurrentMove => move;
        public string StepName => step.ToString();

        /// <summary>(stand, move) when a move starts.</summary>
        public event Action<PharaohStand, StandMove> MoveStarted;
        /// <summary>(stand, move, hit the player) when the palm lands / the sweep ends.</summary>
        public event Action<PharaohStand, StandMove, bool> MoveResolved;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            attack = GetComponent<EnemyAttack>();
            boss = GetComponent<PharaohBoss>();
            if (figure != null)
                figureBase = figure.localPosition;
        }

        private void OnEnable() => Hide();

        private void Update()
        {
            EnemyStateId state = controller.CurrentState;
            bool alive = state != EnemyStateId.Dead && state != EnemyStateId.None && state != EnemyStateId.Spawn;
            if (!alive)
            {
                if (step != Step.Hidden)
                    Hide();
                // Visible (but idle) while rising from spawn; gone once dead.
                SetVisible(state == EnemyStateId.Spawn);
                return;
            }
            if (step == Step.Hidden)
            {
                SetVisible(true);
                EnterRest(settings.firstMoveDelay);
            }

            float speed = attack != null ? Mathf.Max(0.1f, attack.SpeedMultiplier) : 1f;
            float dt = controller.DeltaTime * speed;
            stepTime += dt;
            FloatFigure();
            TickStep();
        }

        // ---- Steps ----

        private void TickStep()
        {
            float t = stepLength > 0f ? Mathf.Clamp01(stepTime / stepLength) : 1f;
            PlayerTarget player = PlayerTarget.Current;

            switch (step)
            {
                case Step.Rest:
                    FollowRest();
                    if (t >= 1f && player != null)
                        StartMove(player);
                    break;

                case Step.Travel:
                    if (player != null && move != StandMove.Sweep)
                        target = Floor(player.Body.position);
                    MoveHand(Vector3.Lerp(fromPos, AboveTarget(), Smooth(t)), Quaternion.Slerp(fromRot, RaisedRotation(), t));
                    if (t >= 1f)
                        Enter(Step.Telegraph, move == StandMove.ComboSlam ? settings.comboTelegraph : settings.slamTelegraph);
                    break;

                case Step.Telegraph:
                    if (player != null && t < settings.slamTrackFraction)
                        target = Floor(player.Body.position);
                    // Palm shown to the player, then turned flat just before the drop.
                    MoveHand(AboveTarget(), Quaternion.Slerp(RaisedRotation(), SlamRotation(), Smooth(Mathf.Clamp01((t - 0.7f) / 0.3f))));
                    ShowSlamMarker(t);
                    if (t >= 1f)
                    {
                        fromPos = AboveTarget();
                        Enter(Step.Drop, settings.slamDropTime);
                    }
                    break;

                case Step.Drop:
                    MoveHand(Vector3.Lerp(fromPos, target + Vector3.up * 0.25f, t * t), SlamRotation());
                    if (t >= 1f)
                        LandSlam(player);
                    break;

                case Step.Stuck:
                    if (t >= 1f)
                    {
                        hand.SetVulnerable(false);
                        BeginReturn();
                    }
                    break;

                case Step.Return:
                    MoveHand(Vector3.Lerp(fromPos, handRest.position, Smooth(t)), Quaternion.Slerp(fromRot, handRest.rotation, t));
                    if (t >= 1f)
                        EnterRest(settings.restTime);
                    break;

                case Step.SweepTelegraph:
                    MoveHand(Vector3.Lerp(fromPos, sweepStart, Smooth(Mathf.Clamp01(t * 2f))), SweepRotation());
                    ShowSweepMarker(t);
                    if (t >= 1f)
                        Enter(Step.Sweep, settings.sweepDuration);
                    break;

                case Step.Sweep:
                    Vector3 position = Vector3.Lerp(sweepStart, sweepEnd, t);
                    MoveHand(position, SweepRotation());
                    if (!hitThisMove && player != null &&
                        StandGeometry.SweepHits(position, settings.sweepReach, player.Body.position,
                            player.Head.position.y - transform.position.y, settings.crouchHeight))
                    {
                        hitThisMove = true;
                        Damage(player, settings.sweepDamage);
                    }
                    if (t >= 1f)
                    {
                        HideMarkers();
                        MoveResolved?.Invoke(this, StandMove.Sweep, hitThisMove);
                        BeginReturn();
                    }
                    break;
            }
        }

        private void StartMove(PlayerTarget player)
        {
            move = StandMoveSelector.Pick(boss != null ? boss.CurrentPhase : 0, UnityEngine.Random.value, lastMove);
            lastMove = move;
            hitThisMove = false;
            fromPos = hand.transform.position;
            fromRot = hand.transform.rotation;
            target = Floor(player.Body.position);
            MoveStarted?.Invoke(this, move);

            if (move == StandMove.Sweep)
            {
                StandGeometry.SweepLane(transform.position, target, settings.sweepHalfLength, UnityEngine.Random.value < 0.5f,
                    out sweepStart, out sweepEnd);
                sweepStart.y = sweepEnd.y = transform.position.y + settings.sweepHeight;
                Enter(Step.SweepTelegraph, settings.sweepTelegraph);
            }
            else
            {
                slamsLeft = move == StandMove.ComboSlam ? settings.comboCount : 1;
                Enter(Step.Travel, settings.travelTime);
            }
        }

        private void LandSlam(PlayerTarget player)
        {
            bool hit = player != null && StandGeometry.SlamHits(target, settings.slamRadius, player.Body.position);
            if (hit)
                Damage(player, settings.slamDamage);
            HideMarkers();
            slamsLeft--;
            MoveResolved?.Invoke(this, move, hit);

            if (slamsLeft > 0)
            {
                // Next slam of the combo: rise straight back up over the player.
                fromPos = hand.transform.position;
                fromRot = hand.transform.rotation;
                Enter(Step.Travel, Mathf.Max(settings.comboPause, 0.05f));
                return;
            }
            hand.SetVulnerable(true);
            Enter(Step.Stuck, settings.stuckTime);
        }

        private void BeginReturn()
        {
            fromPos = hand.transform.position;
            fromRot = hand.transform.rotation;
            Enter(Step.Return, settings.returnTime);
        }

        private void EnterRest(float seconds)
        {
            HideMarkers();
            Enter(Step.Rest, seconds);
        }

        private void Enter(Step next, float length)
        {
            step = next;
            stepTime = 0f;
            stepLength = length;
        }

        private void Hide()
        {
            step = Step.Hidden;
            lastMove = null;
            if (hand != null)
            {
                hand.SetVulnerable(false);
                if (handRest != null)
                    hand.transform.SetPositionAndRotation(handRest.position, handRest.rotation);
            }
            HideMarkers();
        }

        private static void Damage(PlayerTarget player, float amount)
        {
            if (amount > 0f && player.Damageable != null)
                player.Damageable.ApplyPlayerDamage(amount);
        }

        // ---- Visuals ----

        private void SetVisible(bool visible)
        {
            if (figure != null && figure.gameObject.activeSelf != visible)
                figure.gameObject.SetActive(visible);
            if (hand != null && hand.gameObject.activeSelf != visible)
                hand.gameObject.SetActive(visible);
        }

        private void FloatFigure()
        {
            if (figure == null)
                return;
            figure.localPosition = figureBase + Vector3.up * (Mathf.Sin(controller.LocalTime * floatSpeed) * floatAmplitude);
            // The stand looks at the player (yaw only).
            PlayerTarget player = PlayerTarget.Current;
            if (player != null)
            {
                Vector3 look = player.Head.position - figure.position;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-4f)
                    figure.rotation = Quaternion.Slerp(figure.rotation, Quaternion.LookRotation(look), controller.DeltaTime * 3f);
            }
        }

        private void FollowRest()
        {
            if (handRest != null)
                MoveHand(Vector3.Lerp(hand.transform.position, handRest.position, controller.DeltaTime * 6f),
                    Quaternion.Slerp(hand.transform.rotation, handRest.rotation, controller.DeltaTime * 6f));
        }

        private void MoveHand(Vector3 position, Quaternion rotation) => hand.transform.SetPositionAndRotation(position, rotation);

        private Vector3 AboveTarget()
        {
            Vector3 toBoss = transform.position - target;
            toBoss.y = 0f;
            float d = toBoss.magnitude;
            Vector3 forward = d > 1e-3f ? toBoss / d * Mathf.Min(settings.slamHoverForward, d) : Vector3.zero;
            return target + forward + Vector3.up * settings.slamHoverHeight;
        }

        private Vector3 Floor(Vector3 p) => new Vector3(p.x, transform.position.y, p.z);

        /// <summary>Palm down, fingers pointing away from the boss.</summary>
        private Quaternion SlamRotation()
        {
            Vector3 away = target - transform.position;
            away.y = 0f;
            return away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away) : transform.rotation;
        }

        /// <summary>Hand raised, fingers up, palm facing the player: readable from the front without looking up.</summary>
        private Quaternion RaisedRotation()
        {
            Vector3 away = target - transform.position;
            away.y = 0f;
            return away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(Vector3.up, -away.normalized) : transform.rotation;
        }

        /// <summary>Hand on its side, palm facing the way it travels, fingers away from the boss.</summary>
        private Quaternion SweepRotation()
        {
            Vector3 travel = sweepEnd - sweepStart;
            travel.y = 0f;
            Vector3 away = Vector3.Cross(travel, Vector3.up);
            if (Vector3.Dot(away, target - transform.position) < 0f)
                away = -away;
            if (away.sqrMagnitude < 1e-4f)
                return transform.rotation;
            // Palm (-Y of the model) turned toward the travel direction.
            return Quaternion.LookRotation(away, -travel.normalized);
        }

        private void ShowSlamMarker(float t)
        {
            if (slamMarker == null)
                return;
            if (!slamMarker.gameObject.activeSelf)
                slamMarker.gameObject.SetActive(true);
            float r = settings.slamRadius * Mathf.Lerp(0.35f, 1f, Smooth(t));
            slamMarker.SetPositionAndRotation(target + Vector3.up * 0.02f, Quaternion.identity);
            slamMarker.localScale = Vector3.one;
            SetWorldScale(slamMarker, new Vector3(r * 2f, 0.01f, r * 2f));
        }

        private void ShowSweepMarker(float t)
        {
            if (sweepMarker == null)
                return;
            if (!sweepMarker.gameObject.activeSelf)
                sweepMarker.gameObject.SetActive(true);
            Vector3 a = new Vector3(sweepStart.x, transform.position.y + 0.02f, sweepStart.z);
            Vector3 b = new Vector3(sweepEnd.x, a.y, sweepEnd.z);
            Vector3 d = b - a;
            sweepMarker.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(d));
            float width = settings.sweepReach * 2f * Mathf.Lerp(0.4f, 1f, Smooth(t));
            SetWorldScale(sweepMarker, new Vector3(width, 0.01f, d.magnitude));
        }

        private void HideMarkers()
        {
            if (slamMarker != null && slamMarker.gameObject.activeSelf)
                slamMarker.gameObject.SetActive(false);
            if (sweepMarker != null && sweepMarker.gameObject.activeSelf)
                sweepMarker.gameObject.SetActive(false);
        }

        private static void SetWorldScale(Transform t, Vector3 world)
        {
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
