using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// The pharaoh stays on his throne; a translucent stand behind him attacks with two giant hands (moves alternate hands;
    /// the idle hand rests by the stand):
    /// <list type="bullet">
    /// <item>Slam: the hand flies above the player, a floor marker grows (tracks the player for the first half,
    /// then locks), the palm drops; it then stays stuck on the floor — hitting it damages the boss.</item>
    /// <item>Sweep: the hand skims across a lane through the player at chest height; step out of the lane or crouch.</item>
    /// <item>HeadSlash: a fast knife-hand cut at head height across a lane too wide to step out of: crouch under it.
    /// A glowing bar at head height shows the cut line.</item>
    /// <item>WeaponThrow: the hand summons a translucent weapon, winds up and throws it tumbling at the player
    /// (1 / 2 / 3 weapons per phase). Hitting it with a melee weapon sends it back at the pharaoh.</item>
    /// <item>Barrage (on every phase change, not picked randomly): a fan of spectral hands appears, each holding a weapon
    /// (sword, khopesh, axe, ...), and they throw in waves of three. Every weapon can be knocked back; enough of them
    /// staggers the pharaoh. Then the <see cref="PharaohArena"/> opens (barrier down, scythe corridor).</item>
    /// <item>Clap: both hands wait on each side of the player, palms facing, then clap together; a step back or forward
    /// gets out. Both hands then stay stuck for a moment and can be hit.</item>
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
            [Tooltip("Sweep / head-slash lanes are turned this many degrees toward the boss so the player sees the hand coming.")]
            [Range(0f, 80f)] public float sideLaneAngle = 50f;
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

            [Header("Head slash (crouch to dodge)")]
            [Min(0.1f)] public float headSlashTelegraph = 1.4f;
            [Min(0.1f)] public float headSlashDuration = 0.5f;
            [Min(1f)] public float headSlashHalfLength = 5f;
            [Tooltip("Height of the hand over the floor (standing head height).")]
            [Min(0.5f)] public float headSlashHeight = 1.65f;
            [Tooltip("Half width of the cut lane: wide enough that one step does not get out of it.")]
            [Min(0.1f)] public float headSlashAcross = 2f;
            [Tooltip("Half length of the hand along the lane (how close it must be to hit).")]
            [Min(0.1f)] public float headSlashAlong = 0.9f;
            [Tooltip("A player whose head is at or below this height over the floor is under the cut.")]
            [Min(0.2f)] public float headSlashCrouchHeight = 1.3f;
            [Min(0f)] public float headSlashDamage = 18f;

            [Header("Weapon throw (reflectable)")]
            [Tooltip("Seconds the weapon takes to appear in the hand (first throw).")]
            [Min(0.1f)] public float summonTime = 1.1f;
            [Tooltip("Summon time for the follow-up throws of the same move.")]
            [Min(0.1f)] public float resummonTime = 0.5f;
            [Min(0.05f)] public float windUpTime = 0.35f;
            [Tooltip("Where the hand throws from: this far from the boss toward the player, this far to the side, this high.")]
            public Vector3 throwPoint = new Vector3(2f, 3.2f, 3.5f);
            [Min(1f)] public float throwSpeed = 9f;
            [Min(0f)] public float throwDamage = 15f;

            [Header("Clap (both hands: step back or forward)")]
            [Min(0.1f)] public float clapTelegraph = 1.4f;
            [Range(0f, 1f)] public float clapTrackFraction = 0.5f;
            [Tooltip("The hands wait this far to each side of the player.")]
            [Min(1f)] public float clapHalfWidth = 3f;
            [Min(0.3f)] public float clapHeight = 1.4f;
            [Tooltip("While waiting, the hands are this far in front of the player (toward the boss) so they are seen.")]
            [Min(0f)] public float clapForward = 2.2f;
            [Min(0.05f)] public float clapStrikeTime = 0.16f;
            [Tooltip("Half depth of the clap along the boss-player line: one step back or forward gets out.")]
            [Min(0.1f)] public float clapDepth = 0.8f;
            [Min(0f)] public float clapDamage = 22f;
            [Min(0f)] public float clapStuckTime = 1.4f;

            [Header("Barrage (phase change ultimate)")]
            [Min(1)] public int barrageCount = 12;
            [Min(1)] public int barragePerWave = 3;
            [Min(0.1f)] public float barrageSummonTime = 2.2f;
            [Tooltip("Seconds between waves: one swing, slow down, swing again (A's melee re-arms after slowing).")]
            [Min(0.1f)] public float barrageWaveInterval = 0.75f;
            [Min(1f)] public float barrageSpeed = 10f;
            [Min(0f)] public float barrageDamage = 6f;
            [Tooltip("Fan of hands around the stand: radius, half angle, low / high heights.")]
            [Min(0.5f)] public float barrageRadius = 5f;
            [Range(10f, 90f)] public float barrageHalfAngle = 70f;
            [Min(0f)] public float barrageLowHeight = 3.2f;
            [Min(0f)] public float barrageHighHeight = 5.4f;
            [Tooltip("Knock back at least this fraction of the weapons to stagger the pharaoh.")]
            [Range(0f, 1f)] public float barrageStaggerFraction = 0.6f;
            [Min(0f)] public float barrageStaggerSeconds = 3f;
            [Tooltip("Pause after the last throw before the barrier opens.")]
            [Min(0f)] public float barrageAfterTime = 1.5f;
            [Tooltip("Each hand pulls back this long before its weapon flies.")]
            [Min(0.05f)] public float barrageWindUp = 0.35f;
            [Tooltip("World size of the fan hands (the model is ~0.2 m).")]
            [Min(0.5f)] public float barrageHandSize = 5f;
        }

        private enum Step { Hidden, Rest, Travel, Telegraph, Drop, Stuck, Return, SweepTelegraph, Sweep, Summon, WindUp, BarrageSummon, BarrageWaves, BarrageAfter, ClapTelegraph, ClapStrike, ClapStuck }

        [SerializeField] private Settings settings = new Settings();
        [Tooltip("The stand's body (floats behind the throne).")]
        [SerializeField] private Transform figure;
        [Header("Right hand")]
        [SerializeField] private PharaohHand hand;
        [Tooltip("Where the hand waits between moves (child of the figure).")]
        [SerializeField] private Transform handRest;
        [Tooltip("Flat disc shown on the floor where the palm will land.")]
        [SerializeField] private Transform slamMarker;
        [Tooltip("Flat strip shown along the sweep lane (unit length on Z, unit width on X).")]
        [SerializeField] private Transform sweepMarker;
        [Tooltip("Weapon shown in the hand while it is being summoned (child of the hand, no collider).")]
        [SerializeField] private Transform heldWeapon;
        [Header("Left hand")]
        [SerializeField] private PharaohHand leftHand;
        [SerializeField] private Transform leftHandRest;
        [SerializeField] private Transform leftHeldWeapon;
        [Tooltip("Projectile thrown by WeaponThrow (pooled, reflectable).")]
        [SerializeField] private EnemyProjectile weaponPrefab;
        [Tooltip("Weapons used by the barrage, in turn (pooled, reflectable).")]
        [SerializeField] private EnemyProjectile[] barrageWeapons = new EnemyProjectile[0];
        [Tooltip("Inactive hand model copied for every barrage slot.")]
        [SerializeField] private GameObject barrageHandTemplate;
        [Header("Sound (3D, from the hand)")]
        [Tooltip("Palm hitting the floor (every slam of a combo).")]
        [SerializeField] private AudioClip slamSound;
        [Tooltip("Sweep and head slash: plays as the hand starts across.")]
        [SerializeField] private AudioClip sweepSound;
        [Tooltip("The two palms meeting.")]
        [SerializeField] private AudioClip clapSound;
        [Tooltip("A thrown weapon leaving the hand (played a bit higher).")]
        [SerializeField] private AudioClip throwSound;
        [SerializeField, Range(0f, 1f)] private float handSoundVolume = 0.9f;
        [SerializeField, Range(0f, 0.3f)] private float handSoundPitchJitter = 0.06f;
        [SerializeField, Min(0.1f)] private float handSoundMinDistance = 3f;
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
        private int throwsLeft;
        private float throwSide = 1f;
        private Vector3 activeHeldScale = Vector3.one, rightHeldScale = Vector3.one, leftHeldScale = Vector3.one;
        private PharaohHand activeHand;
        private AudioSource rightSource, leftSource;
        private Transform activeRest, activeHeld;
        private HandPose activePose, rightPose, leftPose;
        private bool activeIsLeft, lastSlamLeft;
        private Vector3 fromPosL, clapAxis, clapSide;
        private Quaternion fromRotL;
        private PharaohArena arena;
        private bool ultimatePending;
        private EnemyProjectile[] barrageHeld = new EnemyProjectile[0];
        private Transform[] barrageHands = new Transform[0];
        private Vector3[] barrageSlots = new Vector3[0];
        private int barrageLaunched;
        private int barrageReflected;
        private bool barrageStaggered;
        private float nextWaveTime;
        private float lastDt;
        private float barrageClock;
        private HandPose[] barragePoses = new HandPose[0];
        private float[] slotAppear = new float[0], slotRoll = new float[0], slotYaw = new float[0];
        private float[] slotSize = new float[0], slotPhase = new float[0], slotReleased = new float[0];
        private Vector3[] releasePos = new Vector3[0];
        private Quaternion[] releaseRot = new Quaternion[0];
        private static readonly Vector3 GripInHand = new Vector3(-0.01f, -0.035f, 0.03f); // shaft centre inside the curled fist
        private bool hitThisMove;
        private Vector3 fromPos, target, sweepStart, sweepEnd;
        private Quaternion fromRot;

        public Settings Config => settings;
        public PharaohHand Hand => hand;
        public PharaohHand LeftHand => leftHand;
        /// <summary>The hand doing the current move (the right one during a clap; both are used).</summary>
        public PharaohHand ActiveHand => activeHand;
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
            if (heldWeapon != null)
                rightHeldScale = heldWeapon.localScale;
            if (leftHeldWeapon != null)
                leftHeldScale = leftHeldWeapon.localScale;
            arena = GetComponent<PharaohArena>();
            if (hand != null)
                rightSource = EnemySfx.CreateSource(hand.gameObject, handSoundMinDistance, 40f);
            if (leftHand != null)
                leftSource = EnemySfx.CreateSource(leftHand.gameObject, handSoundMinDistance, 40f);
            BuildBarrageHands();
        }

        private void BuildBarrageHands()
        {
            int count = Mathf.Max(1, settings.barrageCount);
            barrageHeld = new EnemyProjectile[count];
            barrageSlots = new Vector3[count];
            barrageHands = new Transform[barrageHandTemplate != null ? count : 0];
            barragePoses = new HandPose[barrageHands.Length];
            for (int i = 0; i < barrageHands.Length; i++)
            {
                GameObject copy = Instantiate(barrageHandTemplate, barrageHandTemplate.transform.parent);
                copy.name = barrageHandTemplate.name + "_" + i;
                copy.SetActive(false);
                barrageHands[i] = copy.transform;
                barragePoses[i] = copy.GetComponentInChildren<HandPose>(true);
            }
            slotAppear = new float[count];
            slotRoll = new float[count];
            slotYaw = new float[count];
            slotSize = new float[count];
            slotPhase = new float[count];
            slotReleased = new float[count];
            releasePos = new Vector3[count];
            releaseRot = new Quaternion[count];
            if (hand != null)
                rightPose = hand.GetComponentInChildren<HandPose>(true);
            if (leftHand != null)
                leftPose = leftHand.GetComponentInChildren<HandPose>(true);
            SetActiveHand(false);
        }

        private void OnDisable()
        {
            if (boss != null)
                boss.PhaseChanged -= HandlePhaseChanged;
        }

        private void Start()
        {
            if (boss != null)
            {
                boss.PhaseChanged -= HandlePhaseChanged;
                boss.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void HandlePhaseChanged(PharaohBoss _, int from, int to)
        {
            if (to > from && to > 0)
                ultimatePending = true;
        }

        /// <summary>Test / debug: run the phase-change ultimate (barrage, then the barrier opens) after the current move.</summary>
        public void QueueUltimate() => ultimatePending = true;

        private void OnEnable()
        {
            Hide();
            ultimatePending = false;
            if (boss != null)
            {
                boss.PhaseChanged -= HandlePhaseChanged;
                boss.PhaseChanged += HandlePhaseChanged;
            }
        }

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
            lastDt = dt;
            stepTime += dt;
            FloatFigure();
            TickStep();
        }

        // ---- Steps ----

        private void TickStep()
        {
            float t = stepLength > 0f ? Mathf.Clamp01(stepTime / stepLength) : 1f;
            PlayerTarget player = PlayerTarget.Current;
            bool both = UsesBothHands;
            if (activePose != null)
                activePose.Target = MainHandCurl();
            HandPose idlePose = activeIsLeft ? rightPose : leftPose;
            if (idlePose != null)
                idlePose.Target = both ? MainHandCurl() : IsBarrageStep ? 0.55f : 0.35f;
            if (!both)
                FollowIdleHand();

            switch (step)
            {
                case Step.Rest:
                    FollowRest();
                    if (t >= 1f && player != null)
                    {
                        // While the barrier is open the stand holds back: the scythes guard the corridor.
                        if (arena != null && arena.IsOpen)
                            break;
                        if (ultimatePending)
                            StartBarrage(player);
                        else
                            StartMove(player);
                    }
                    break;

                case Step.Travel:
                    if (player != null && !IsSideMove)
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
                        activeHand.SetVulnerable(false);
                        BeginReturn();
                    }
                    break;

                case Step.Return:
                    MoveHand(Vector3.Lerp(fromPos, activeRest.position, Smooth(t)), Quaternion.Slerp(fromRot, activeRest.rotation, t));
                    if (move == StandMove.Clap && leftHand != null && leftHandRest != null)
                        leftHand.transform.SetPositionAndRotation(Vector3.Lerp(fromPosL, leftHandRest.position, Smooth(t)),
                            Quaternion.Slerp(fromRotL, leftHandRest.rotation, t));
                    if (t >= 1f)
                        EnterRest(settings.restTime);
                    break;

                case Step.SweepTelegraph:
                    MoveHand(Vector3.Lerp(fromPos, sweepStart, Smooth(Mathf.Clamp01(t * 2f))), SideRotation());
                    ShowSweepMarker(t);
                    if (t >= 1f)
                    {
                        PlayHandSound(activeHand, sweepSound);
                        Enter(Step.Sweep, move == StandMove.HeadSlash ? settings.headSlashDuration : settings.sweepDuration);
                    }
                    break;

                case Step.Sweep:
                    Vector3 position = Vector3.Lerp(sweepStart, sweepEnd, t);
                    MoveHand(position, SideRotation());
                    if (!hitThisMove && player != null && SideMoveHits(position, player))
                    {
                        hitThisMove = true;
                        Damage(player, move == StandMove.HeadSlash ? settings.headSlashDamage : settings.sweepDamage);
                    }
                    if (t >= 1f)
                    {
                        HideMarkers();
                        MoveResolved?.Invoke(this, move, hitThisMove);
                        BeginReturn();
                    }
                    break;

                case Step.Summon:
                    MoveHand(Vector3.Lerp(fromPos, ThrowPoint(player), Smooth(Mathf.Clamp01(t * 1.6f))),
                        Quaternion.Slerp(fromRot, AimRotation(player), Mathf.Clamp01(t * 1.6f)));
                    ShowHeldWeapon(Smooth(t));
                    if (t >= 1f)
                    {
                        fromPos = activeHand.transform.position;
                        Enter(Step.WindUp, settings.windUpTime);
                    }
                    break;

                case Step.BarrageSummon:
                    FollowRest();
                    barrageClock += lastDt;
                    for (int i = 0; i < barrageHeld.Length; i++)
                        PoseBarrageSlot(i, player, t, 0f);
                    if (t >= 1f)
                    {
                        nextWaveTime = 0.25f; // a beat before the first wave so its wind-up shows
                        Enter(Step.BarrageWaves, float.PositiveInfinity);
                    }
                    break;

                case Step.BarrageWaves:
                    FollowRest();
                    barrageClock += lastDt;
                    for (int i = 0; i < barrageHeld.Length; i++)
                        PoseBarrageSlot(i, player, 1f, WindUpFor(i));
                    if (stepTime >= nextWaveTime)
                    {
                        LaunchBarrageWave(player);
                        nextWaveTime = stepTime + settings.barrageWaveInterval;
                    }
                    if (barrageLaunched >= barrageHeld.Length)
                        Enter(Step.BarrageAfter, settings.barrageAfterTime);
                    break;

                case Step.BarrageAfter:
                    FollowRest();
                    barrageClock += lastDt;
                    for (int i = 0; i < barrageHeld.Length; i++)
                        PoseBarrageSlot(i, player, 1f, 0f);
                    if (t >= 1f)
                    {
                        HideBarrageHands();
                        if (arena != null)
                            arena.Open();
                        EnterRest(settings.restTime);
                    }
                    break;

                case Step.ClapTelegraph:
                    if (player != null && t < settings.clapTrackFraction)
                        SetClapTarget(player);
                    PlaceClapHands(Smooth(Mathf.Clamp01(t * 2.5f)), 1f);
                    ShowClapMarker(t);
                    if (t >= 1f)
                        Enter(Step.ClapStrike, settings.clapStrikeTime);
                    break;

                case Step.ClapStrike:
                    PlaceClapHands(1f, 1f - t * t);
                    if (t >= 1f)
                    {
                        HideMarkers();
                        PlayHandSound(hand, clapSound);
                        bool clapHit = player != null && StandGeometry.ClapHits(target, clapAxis, player.Body.position,
                            settings.clapDepth, settings.clapHalfWidth);
                        if (clapHit)
                            Damage(player, settings.clapDamage);
                        MoveResolved?.Invoke(this, StandMove.Clap, clapHit);
                        hand.SetVulnerable(true);
                        if (leftHand != null)
                            leftHand.SetVulnerable(true);
                        Enter(Step.ClapStuck, settings.clapStuckTime);
                    }
                    break;

                case Step.ClapStuck:
                    if (t >= 1f)
                    {
                        hand.SetVulnerable(false);
                        if (leftHand != null)
                            leftHand.SetVulnerable(false);
                        BeginReturn();
                    }
                    break;

                case Step.WindUp:
                    // Pull back (away from the player) and up, then let go.
                    Vector3 back = player != null ? fromPos - player.Head.position : -transform.forward;
                    back.y = 0f;
                    back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.zero;
                    MoveHand(fromPos + (back * 0.8f + Vector3.up * 0.4f) * Smooth(t), AimRotation(player));
                    if (t >= 1f)
                        ReleaseWeapon(player);
                    break;
            }
        }

        private void StartMove(PlayerTarget player)
        {
            move = StandMoveSelector.Pick(boss != null ? boss.CurrentPhase : 0, UnityEngine.Random.value, lastMove);
            lastMove = move;
            hitThisMove = false;
            fromPos = activeHand.transform.position;
            fromRot = activeHand.transform.rotation;
            target = Floor(player.Body.position);
            MoveStarted?.Invoke(this, move);

            if (move == StandMove.Clap)
            {
                SetActiveHand(false); // right drives the timeline; both hands move
                fromPos = hand.transform.position;
                fromRot = hand.transform.rotation;
                if (leftHand != null)
                {
                    fromPosL = leftHand.transform.position;
                    fromRotL = leftHand.transform.rotation;
                }
                SetClapTarget(player);
                Enter(Step.ClapTelegraph, settings.clapTelegraph);
                return;
            }
            if (move == StandMove.WeaponThrow)
            {
                throwsLeft = StandMoveSelector.ThrowCount(boss != null ? boss.CurrentPhase : 0);
                throwSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                UseHand(throwSide < 0f); // the boss's left side throws with the left hand
                Enter(Step.Summon, settings.summonTime);
                return;
            }
            if (IsSideMove)
            {
                bool head = move == StandMove.HeadSlash;
                bool fromLeft = UnityEngine.Random.value < 0.5f;
                UseHand(fromLeft); // the hand on the side the lane starts from
                StandGeometry.SweepLane(transform.position, target, head ? settings.headSlashHalfLength : settings.sweepHalfLength,
                    fromLeft, out sweepStart, out sweepEnd, settings.sideLaneAngle);
                sweepStart.y = sweepEnd.y = transform.position.y + (head ? settings.headSlashHeight : settings.sweepHeight);
                Enter(Step.SweepTelegraph, head ? settings.headSlashTelegraph : settings.sweepTelegraph);
            }
            else
            {
                slamsLeft = move == StandMove.ComboSlam ? settings.comboCount : 1;
                lastSlamLeft = !lastSlamLeft; // slams alternate hands
                UseHand(lastSlamLeft);
                Enter(Step.Travel, settings.travelTime);
            }
        }

        // ---- Barrage ----

        private void StartBarrage(PlayerTarget player)
        {
            ultimatePending = false;
            move = StandMove.Barrage;
            lastMove = null;
            barrageLaunched = 0;
            barrageReflected = 0;
            barrageStaggered = false;
            barrageClock = 0f;
            MoveStarted?.Invoke(this, move);
            RandomiseSlots();
            for (int i = 0; i < barrageHeld.Length; i++)
            {
                barrageHeld[i] = null;
                if (barrageWeapons == null || barrageWeapons.Length == 0)
                    continue;
                EnemyProjectile prefab = barrageWeapons[i % barrageWeapons.Length];
                EnemyProjectile weapon = prefab != null ? EnemyProjectilePool.Get(prefab) : null;
                if (weapon == null)
                    continue; // pool cap: fewer weapons
                weapon.Reflected += HandleBarrageReflected;
                weapon.Expired += HandleBarrageExpired;
                weapon.ShowHeld(); // pooled projectiles stay hidden until Launch; these are shown while held
                barrageHeld[i] = weapon;
            }
            for (int i = 0; i < barrageHands.Length; i++)
                if (barragePoses[i] != null)
                    barragePoses[i].Snap(0.1f); // open, ready to receive the weapon
            for (int i = 0; i < barrageHeld.Length; i++)
                PoseBarrageSlot(i, player, 0f, 0f);
            Enter(Step.BarrageSummon, settings.barrageSummonTime);
        }

        /// <summary>Each hand gets its own tilt, size, sway rhythm and arrival time, so the fan never moves as one block.</summary>
        private void RandomiseSlots()
        {
            int count = barrageHeld.Length;
            for (int i = 0; i < count; i++)
            {
                slotRoll[i] = UnityEngine.Random.Range(-22f, 22f);
                slotYaw[i] = UnityEngine.Random.Range(-14f, 14f);
                slotSize[i] = UnityEngine.Random.Range(0.88f, 1.15f);
                slotPhase[i] = UnityEngine.Random.Range(0f, 6.2832f);
                slotReleased[i] = -1f;
            }
            // Arrival order: shuffled, spread over the first ~65 % of the summon.
            for (int i = 0; i < count; i++)
                slotAppear[i] = count > 1 ? i / (float)(count - 1) * 0.65f : 0f;
            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                float tmp = slotAppear[i];
                slotAppear[i] = slotAppear[j];
                slotAppear[j] = tmp;
            }
        }

        /// <summary>0..1 pull-back of a hand whose weapon flies in the next wave.</summary>
        private float WindUpFor(int slot)
        {
            int perWave = Mathf.Max(1, settings.barragePerWave);
            for (int n = 0; n < perWave; n++)
            {
                int launch = barrageLaunched + n;
                if (launch >= barrageHeld.Length)
                    break;
                if (BarragePlan.SlotForLaunch(launch, barrageHeld.Length, perWave) == slot)
                    return Mathf.Clamp01(1f - (nextWaveTime - stepTime) / settings.barrageWindUp);
            }
            return 0f;
        }

        /// <summary>
        /// Slot i of the fan. Before its throw: the hand rises in (open), the weapon forms in it, the fingers close round the
        /// handle; it floats and sways on its own rhythm and pulls back just before the throw. After the throw: follow-through,
        /// fingers open, the hand fades out.
        /// </summary>
        private void PoseBarrageSlot(int i, PlayerTarget player, float summon, float windUp)
        {
            Transform h = i < barrageHands.Length ? barrageHands[i] : null;
            HandPose pose = i < barragePoses.Length ? barragePoses[i] : null;

            if (slotReleased[i] >= 0f)
            {
                float r = (barrageClock - slotReleased[i]) / 0.45f;
                if (h == null)
                    return;
                if (r >= 1f)
                {
                    if (h.gameObject.activeSelf)
                        h.gameObject.SetActive(false);
                    return;
                }
                Vector3 follow = releaseRot[i] * Vector3.up * -1f; // palm direction = toward the player
                h.SetPositionAndRotation(releasePos[i] + follow * (0.9f * Smooth(r)), releaseRot[i]);
                h.localScale = Vector3.one * (WorldToLocal(h, settings.barrageHandSize * slotSize[i]) * (1f - r));
                if (pose != null)
                    pose.Target = 0f;
                return;
            }

            EnemyProjectile weapon = barrageHeld[i];
            float handGrow = Smooth(Mathf.Clamp01((summon - slotAppear[i]) / 0.25f));
            float weaponGrow = Smooth(Mathf.Clamp01((summon - slotAppear[i] - 0.1f) / 0.25f));
            bool show = handGrow > 0.001f && weapon != null;

            // Frame of this slot: forward = toward the player, up = tilted by the hand's own roll + sway.
            Vector3 toPlayer = player != null ? player.Body.position - transform.position : transform.forward;
            Vector3 center = figure != null ? new Vector3(figure.position.x, transform.position.y, figure.position.z) : transform.position;
            Vector3 slot = BarragePlan.Slot(i, barrageHeld.Length, center, toPlayer, settings.barrageRadius,
                settings.barrageHalfAngle, settings.barrageLowHeight, settings.barrageHighHeight);
            float ph = slotPhase[i];
            Vector3 aim = player != null ? Vector3.Lerp(player.Body.position, player.Head.position, 0.5f) - slot : toPlayer;
            aim.y = 0f;
            Quaternion frame = (aim.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(aim.normalized) : transform.rotation)
                * Quaternion.Euler(-32f * Smooth(windUp), slotYaw[i], slotRoll[i] + Mathf.Sin(barrageClock * 1.1f + ph) * 5f);
            Vector3 fwd = frame * Vector3.forward, up = frame * Vector3.up, right = frame * Vector3.right;
            Vector3 sway = Vector3.up * (Mathf.Sin(barrageClock * 1.3f + ph) * 0.14f)
                + right * (Mathf.Sin(barrageClock * 0.8f + ph * 1.7f) * 0.09f);
            // Rise in from below as it appears; pull back and up before the throw.
            Vector3 grip = slot + sway - Vector3.up * (1.2f * (1f - handGrow))
                - fwd * (0.7f * Smooth(windUp)) + Vector3.up * (0.25f * Smooth(windUp));
            barrageSlots[i] = grip;

            if (h != null)
            {
                if (h.gameObject.activeSelf != show)
                    h.gameObject.SetActive(show);
                if (show)
                {
                    // Fingers across (to the right), palm toward the player, thumb up: a fist round an upright handle.
                    Quaternion handRot = Quaternion.LookRotation(right, -fwd);
                    float size = settings.barrageHandSize * slotSize[i] * handGrow;
                    h.SetPositionAndRotation(grip - handRot * (GripInHand * size), handRot);
                    h.localScale = Vector3.one * WorldToLocal(h, size);
                    if (pose != null)
                        pose.Target = weaponGrow > 0.3f ? 0.9f : 0.12f;
                    releasePos[i] = h.position;
                    releaseRot[i] = handRot;
                }
            }

            if (weapon != null && !weapon.IsFlying)
            {
                // Blade up, handle in the fist, broad side to the player.
                WeaponGrip g = weapon.GetComponent<WeaponGrip>();
                float sign = g != null ? g.HandleSign : -1f;
                Vector3 local = g != null ? g.LocalGrip : Vector3.zero;
                Quaternion rot = Quaternion.LookRotation(up * -sign, -fwd);
                float scale = Mathf.Max(0.01f, weaponGrow);
                weapon.transform.SetPositionAndRotation(grip - rot * (local * scale), rot);
                weapon.transform.localScale = Vector3.one * scale;
            }
        }

        private static float WorldToLocal(Transform t, float worldSize)
        {
            float parent = t.parent != null ? t.parent.lossyScale.x : 1f;
            return parent > 1e-5f ? worldSize / parent : worldSize;
        }

        private void LaunchBarrageWave(PlayerTarget player)
        {
            int perWave = Mathf.Max(1, settings.barragePerWave);
            for (int n = 0; n < perWave && barrageLaunched < barrageHeld.Length; n++, barrageLaunched++)
            {
                int slot = BarragePlan.SlotForLaunch(barrageLaunched, barrageHeld.Length, perWave);
                EnemyProjectile weapon = barrageHeld[slot];
                barrageHeld[slot] = null;
                slotReleased[slot] = barrageClock; // the hand follows through and fades
                if (weapon == null || player == null)
                    continue;
                Vector3 from = weapon.transform.position;
                Vector3 aim = Vector3.Lerp(player.Body.position, player.Head.position, 0.5f);
                weapon.transform.localScale = Vector3.one;
                float speed = attack != null ? Mathf.Max(1f, attack.SpeedMultiplier) : 1f;
                weapon.Launch(controller, from, aim - from, settings.barrageSpeed * speed, settings.barrageDamage);
            }
        }

        /// <summary>Main hand finger curl for the current step: flat for slams, straight for the knife-hand, a fist round the dagger.</summary>
        private float MainHandCurl()
        {
            switch (step)
            {
                case Step.Travel:
                case Step.Telegraph:
                    return 0.05f;
                case Step.Drop:
                case Step.Stuck:
                    return 0f;
                case Step.SweepTelegraph:
                case Step.Sweep:
                    return move == StandMove.HeadSlash ? 0f : 0.12f;
                case Step.Summon:
                case Step.WindUp:
                    return 0.88f;
                case Step.BarrageSummon:
                case Step.BarrageWaves:
                case Step.BarrageAfter:
                    return 0.55f;
                case Step.ClapTelegraph:
                case Step.ClapStrike:
                case Step.ClapStuck:
                    return 0f;
                default:
                    return 0.35f;
            }
        }

        private void HandleBarrageReflected(EnemyProjectile weapon)
        {
            barrageReflected++;
            int needed = Mathf.CeilToInt(settings.barrageCount * settings.barrageStaggerFraction);
            if (!barrageStaggered && needed > 0 && barrageReflected >= needed)
            {
                barrageStaggered = true;
                controller.Stagger(settings.barrageStaggerSeconds);
            }
        }

        private void HandleBarrageExpired(EnemyProjectile weapon, bool struck)
        {
            weapon.Reflected -= HandleBarrageReflected;
            weapon.Expired -= HandleBarrageExpired;
        }

        /// <summary>Weapons still held (never thrown) go back to the pool; all fan hands hide.</summary>
        private void ReleaseBarrage()
        {
            for (int i = 0; i < barrageHeld.Length; i++)
            {
                EnemyProjectile weapon = barrageHeld[i];
                barrageHeld[i] = null;
                if (weapon == null || weapon.IsFlying)
                    continue;
                weapon.Reflected -= HandleBarrageReflected;
                weapon.Expired -= HandleBarrageExpired;
                weapon.transform.localScale = Vector3.one;
                EnemyProjectilePool.Release(weapon);
            }
            HideBarrageHands();
        }

        private void HideBarrageHands()
        {
            for (int i = 0; i < barrageHands.Length; i++)
                if (barrageHands[i] != null && barrageHands[i].gameObject.activeSelf)
                    barrageHands[i].gameObject.SetActive(false);
        }

        private void ReleaseWeapon(PlayerTarget player)
        {
            ShowHeldWeapon(0f);
            PlayHandSound(activeHand, throwSound, 1.25f);
            Vector3 from = activeHeld != null ? activeHeld.position : activeHand.transform.position;
            if (weaponPrefab != null && player != null)
            {
                EnemyProjectile weapon = EnemyProjectilePool.Get(weaponPrefab);
                if (weapon != null)
                {
                    Vector3 aim = Vector3.Lerp(player.Body.position, player.Head.position, 0.5f);
                    float speed = attack != null ? attack.SpeedMultiplier : 1f;
                    weapon.Launch(controller, from, aim - from, settings.throwSpeed * Mathf.Max(1f, speed), settings.throwDamage);
                }
            }
            MoveResolved?.Invoke(this, StandMove.WeaponThrow, false);

            throwsLeft--;
            if (throwsLeft > 0)
            {
                throwSide = -throwSide; // next one from the other side, with the other hand
                UseHand(throwSide < 0f);
                Enter(Step.Summon, settings.resummonTime);
                return;
            }
            BeginReturn();
        }

        /// <summary>In front of the boss toward the player, to one side, up high: visible and clearly "winding up".</summary>
        private Vector3 ThrowPoint(PlayerTarget player)
        {
            Vector3 toPlayer = player != null ? player.Body.position - transform.position : transform.forward;
            toPlayer.y = 0f;
            toPlayer = toPlayer.sqrMagnitude > 1e-4f ? toPlayer.normalized : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer) * throwSide;
            Vector3 p = settings.throwPoint;
            return transform.position + side * p.x + Vector3.up * p.y + toPlayer * p.z;
        }

        /// <summary>Hand raised, palm toward the player (the weapon stands up behind the fingers).</summary>
        private Quaternion AimRotation(PlayerTarget player)
        {
            Vector3 to = player != null ? player.Head.position - activeHand.transform.position : transform.forward;
            to.y = 0f;
            return to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(Vector3.up, -to.normalized) : activeHand.transform.rotation;
        }

        private void ShowHeldWeapon(float amount)
        {
            if (activeHeld == null)
                return;
            bool show = amount > 0.001f;
            if (activeHeld.gameObject.activeSelf != show)
                activeHeld.gameObject.SetActive(show);
            if (show)
                activeHeld.localScale = activeHeldScale * amount;
        }

        private bool IsSideMove => move == StandMove.Sweep || move == StandMove.HeadSlash;

        private bool SideMoveHits(Vector3 hand, PlayerTarget player)
        {
            float headHeight = player.Head.position.y - transform.position.y;
            if (move == StandMove.HeadSlash)
                return StandGeometry.LaneHits(hand, sweepEnd - sweepStart, player.Body.position,
                    settings.headSlashAlong, settings.headSlashAcross, headHeight, settings.headSlashCrouchHeight);
            return StandGeometry.SweepHits(hand, settings.sweepReach, player.Body.position, headHeight, settings.crouchHeight);
        }

        private void LandSlam(PlayerTarget player)
        {
            bool hit = player != null && StandGeometry.SlamHits(target, settings.slamRadius, player.Body.position);
            PlayHandSound(activeHand, slamSound);
            if (hit)
                Damage(player, settings.slamDamage);
            HideMarkers();
            slamsLeft--;
            MoveResolved?.Invoke(this, move, hit);

            if (slamsLeft > 0)
            {
                // Next slam of the combo comes from the other hand; this one goes back to rest.
                lastSlamLeft = !lastSlamLeft;
                UseHand(lastSlamLeft);
                Enter(Step.Travel, Mathf.Max(settings.comboPause, 0.05f));
                return;
            }
            activeHand.SetVulnerable(true);
            Enter(Step.Stuck, settings.stuckTime);
        }

        private void BeginReturn()
        {
            fromPos = activeHand.transform.position;
            fromRot = activeHand.transform.rotation;
            if (leftHand != null)
            {
                fromPosL = leftHand.transform.position;
                fromRotL = leftHand.transform.rotation;
            }
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
            HideHeld(heldWeapon);
            HideHeld(leftHeldWeapon);
            ReleaseBarrage();
            ResetHand(hand, handRest);
            ResetHand(leftHand, leftHandRest);
            SetActiveHand(false);
            HideMarkers();
        }

        private static void ResetHand(PharaohHand h, Transform rest)
        {
            if (h == null)
                return;
            h.SetVulnerable(false);
            if (rest != null)
                h.transform.SetPositionAndRotation(rest.position, rest.rotation);
        }

        private static void HideHeld(Transform held)
        {
            if (held != null && held.gameObject.activeSelf)
                held.gameObject.SetActive(false);
        }

        // ---- Hands ----

        private void SetActiveHand(bool left)
        {
            if (left && leftHand == null)
                left = false;
            activeIsLeft = left;
            activeHand = left ? leftHand : hand;
            activeRest = left ? leftHandRest : handRest;
            activeHeld = left ? leftHeldWeapon : heldWeapon;
            activeHeldScale = left ? leftHeldScale : rightHeldScale;
            activePose = left ? leftPose : rightPose;
        }

        /// <summary>Switches the moving hand and starts the next motion from where that hand is now.</summary>
        private void UseHand(bool left)
        {
            SetActiveHand(left);
            fromPos = activeHand.transform.position;
            fromRot = activeHand.transform.rotation;
        }

        private bool UsesBothHands =>
            move == StandMove.Clap && (step == Step.ClapTelegraph || step == Step.ClapStrike || step == Step.ClapStuck || step == Step.Return);

        private bool IsBarrageStep => step == Step.BarrageSummon || step == Step.BarrageWaves || step == Step.BarrageAfter;

        /// <summary>The hand that is not attacking drifts back to its rest beside the stand.</summary>
        private void FollowIdleHand()
        {
            PharaohHand idle = activeIsLeft ? hand : leftHand;
            Transform rest = activeIsLeft ? handRest : leftHandRest;
            if (idle == null || rest == null)
                return;
            float k = Mathf.Clamp01(controller.DeltaTime * 5f);
            idle.transform.SetPositionAndRotation(Vector3.Lerp(idle.transform.position, rest.position, k),
                Quaternion.Slerp(idle.transform.rotation, rest.rotation, k));
        }

        // ---- Clap ----

        private void SetClapTarget(PlayerTarget player)
        {
            if (player == null)
                return;
            target = Floor(player.Body.position);
            clapAxis = transform.position - target;
            clapAxis.y = 0f;
            clapAxis = clapAxis.sqrMagnitude > 1e-4f ? clapAxis.normalized : transform.forward;
            // Right hand on the boss's right (the player's left when facing the boss).
            clapSide = Vector3.Cross(Vector3.up, -clapAxis);
        }

        /// <summary>Hands at <paramref name="open"/> × half width to each side, palms facing, fingers up; in = travel blend.</summary>
        private void PlaceClapHands(float travel, float open)
        {
            // Waiting in front of the player on both sides (in view without turning the head), closing in diagonally.
            Vector3 centre = target + Vector3.up * settings.clapHeight + clapAxis * (settings.clapForward * open);
            float half = Mathf.Lerp(0.35f, settings.clapHalfWidth, open);
            Vector3 right = centre + clapSide * half;
            Vector3 left = centre - clapSide * half;
            // Palm (-Y of the model) toward the centre.
            Quaternion rightRot = Quaternion.LookRotation(Vector3.up, clapSide);
            Quaternion leftRot = Quaternion.LookRotation(Vector3.up, -clapSide);
            hand.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, right, travel), Quaternion.Slerp(fromRot, rightRot, travel));
            if (leftHand != null)
                leftHand.transform.SetPositionAndRotation(Vector3.Lerp(fromPosL, left, travel), Quaternion.Slerp(fromRotL, leftRot, travel));
        }

        /// <summary>A red band across the boss-player line where the hands will meet.</summary>
        private void ShowClapMarker(float t)
        {
            if (sweepMarker == null)
                return;
            if (!sweepMarker.gameObject.activeSelf)
                sweepMarker.gameObject.SetActive(true);
            sweepMarker.SetPositionAndRotation(target + Vector3.up * 0.02f, Quaternion.LookRotation(clapSide));
            float depth = settings.clapDepth * 2f * Mathf.Lerp(0.5f, 1f, Smooth(t));
            SetWorldScale(sweepMarker, new Vector3(depth, 0.01f, settings.clapHalfWidth * 2f));
        }

        private void PlayHandSound(PharaohHand h, AudioClip clip, float pitch = 1f)
        {
            AudioSource source = h == null ? null : h == leftHand ? leftSource : rightSource;
            if (source == null || clip == null || handSoundVolume <= 0f)
                return;
            source.pitch = pitch * (1f + UnityEngine.Random.Range(-handSoundPitchJitter, handSoundPitchJitter));
            source.PlayOneShot(clip, handSoundVolume);
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
            if (leftHand != null && leftHand.gameObject.activeSelf != visible)
                leftHand.gameObject.SetActive(visible);
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

        /// <summary>Both hands drift back to their rests.</summary>
        private void FollowRest()
        {
            float k = Mathf.Clamp01(controller.DeltaTime * 6f);
            if (hand != null && handRest != null)
                hand.transform.SetPositionAndRotation(Vector3.Lerp(hand.transform.position, handRest.position, k),
                    Quaternion.Slerp(hand.transform.rotation, handRest.rotation, k));
            if (leftHand != null && leftHandRest != null)
                leftHand.transform.SetPositionAndRotation(Vector3.Lerp(leftHand.transform.position, leftHandRest.position, k),
                    Quaternion.Slerp(leftHand.transform.rotation, leftHandRest.rotation, k));
        }

        private void MoveHand(Vector3 position, Quaternion rotation) => activeHand.transform.SetPositionAndRotation(position, rotation);

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

        /// <summary>Sweep: palm pushing the way it travels. Head slash: knife-hand, fingers leading, palm down.</summary>
        private Quaternion SideRotation()
        {
            if (move == StandMove.HeadSlash)
            {
                Vector3 dir = sweepEnd - sweepStart;
                dir.y = 0f;
                return dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;
            }
            return SweepRotation();
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
            if (move == StandMove.HeadSlash)
            {
                // A thin glowing bar at head height along the cut, growing out from the side the hand comes from.
                Vector3 tip = Vector3.Lerp(sweepStart, sweepEnd, Smooth(Mathf.Clamp01(t * 1.5f)));
                Vector3 span = tip - sweepStart;
                if (span.sqrMagnitude < 1e-4f)
                    span = (sweepEnd - sweepStart) * 0.01f;
                sweepMarker.SetPositionAndRotation(sweepStart + span * 0.5f, Quaternion.LookRotation(span));
                SetWorldScale(sweepMarker, new Vector3(0.12f, 0.12f, span.magnitude));
                return;
            }
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
