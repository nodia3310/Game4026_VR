using System.Collections;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Playable-demo run for one hall: hall waves → fade + teleport to the boss realm → boss → victory → back to the hall.
    /// The player dying → fade out, restart in place (encounters reset, player health restored, back at the start).
    /// The HUD is OnGUI on desktop; in a headset <see cref="DemoWorldHud"/> shows <see cref="HudMessage"/>.
    /// The player is re-resolved whenever <see cref="PlayerTarget.Current"/> changes (A's desktop adapter swaps the XR rig
    /// for its own camera a couple of frames after start).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoFlow : MonoBehaviour
    {
        [SerializeField] private EncounterController hallEncounter;
        [SerializeField] private EncounterController bossEncounter;
        [Tooltip("Where the player is put back on restart. Empty = where the player was at start.")]
        [SerializeField] private Transform playerStart;
        [Tooltip("Countdown before the first wave (after the player is ready).")]
        [SerializeField, Min(0f)] private float introDelay = 3f;
        [Header("Start")]
        [Tooltip("First run: no countdown until the player is ready — picks up a weapon (MarkPlayerReady, e.g. from " +
                 "DemoStartOnGrab) or walks away from the start. Restarts after a defeat do not wait.")]
        [SerializeField] private bool waitForPlayerReady = true;
        [Tooltip("Walking this far (horizontally) from the start also counts as ready.")]
        [SerializeField, Min(0.5f)] private float leaveStartRadius = 5f;
        [SerializeField, Min(0f)] private float bossDelay = 4f;
        [SerializeField, Min(0f)] private float restartDelay = 4f;
        [Header("Boss realm")]
        [Tooltip("Where the player arrives for the boss fight. Empty = the boss fight happens in the hall.")]
        [SerializeField] private Transform realmArrival;
        [Tooltip("Transparent unlit material (both faces) for the fade quad.")]
        [SerializeField] private Material fadeMaterial;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;
        [Tooltip("Seconds after the victory before the player is brought back to the hall.")]
        [SerializeField, Min(0f)] private float returnDelay = 5f;
        [SerializeField] private bool showHud = true;
        [Tooltip("Testing: skip the hall waves and go straight to the boss (also after a restart).")]
        [SerializeField] private bool startAtBoss;

        private DemoFlowModel model;
        private PlayerTarget player;
        private PlayerVitals vitals;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private GUIStyle style;
        private ScreenFader fader;
        private Coroutine transition;
        private bool restartPending;
        private IDemoHallSteps hallSteps;

        public DemoPhase Phase => model != null ? model.Phase : DemoPhase.Intro;
        public EncounterController HallEncounter => hallEncounter;
        public EncounterController BossEncounter => bossEncounter;
        public bool ShowHud => showHud;
        /// <summary>
        /// Replaces the single hall encounter (e.g. artifact rounds): the flow begins / stops / resets these steps and
        /// goes to the boss when they are done. Null = back to the hall encounter.
        /// </summary>
        public void SetHallSteps(IDemoHallSteps steps) => hallSteps = steps;
        public IDemoHallSteps HallSteps => hallSteps;
        /// <summary>Seconds left before the hall fight starts; -1 while not counting down (waiting for the player, or not in the intro).</summary>
        public float IntroRemaining => model != null && model.Phase == DemoPhase.Intro && model.IsReady ? model.IntroRemaining(Time.time) : -1f;
        /// <summary>True while the first intro waits for the player to pick up a weapon.</summary>
        public bool IsWaitingForPlayer => model != null && model.Phase == DemoPhase.Intro && !model.IsReady;
        /// <summary>Current HUD line (objective / phase), empty when nothing to show.</summary>
        public string HudMessage => model != null ? HudText() : string.Empty;
        /// <summary>Player health as (current, max) when readable.</summary>
        public bool TryGetPlayerHealth(out float current, out float max)
        {
            current = max = 0f;
            return vitals != null && vitals.TryGetHealth(out current, out max);
        }

        /// <summary>Runtime / test setup.</summary>
        public void Configure(EncounterController hall, EncounterController boss, float intro, float bossWait, float restart)
        {
            hallEncounter = hall;
            bossEncounter = boss;
            introDelay = intro;
            bossDelay = bossWait;
            restartDelay = restart;
            model = null;
        }

        private void Start()
        {
            ResolvePlayer();
            if (player != null)
            {
                startPosition = playerStart != null ? playerStart.position : player.transform.position;
                startRotation = playerStart != null ? playerStart.rotation : player.transform.rotation;
            }
            else if (playerStart != null)
            {
                startPosition = playerStart.position;
                startRotation = playerStart.rotation;
            }
            EnsureModel();
            model.Begin(Time.time, waitForPlayerReady);
            if (startAtBoss)
                SkipToBoss();
        }

        /// <summary>The player is ready (picked up a weapon): the intro countdown starts. Ignored when not waiting.</summary>
        public void MarkPlayerReady()
        {
            EnsureModel();
            if (model.MarkReady(Time.time))
                Debug.Log("[DemoFlow] player ready — first wave in " + introDelay + " s", this);
        }

        /// <summary>Test shortcut: clears the hall (no kills counted) and starts the boss intro / teleport.</summary>
        public void SkipToBoss()
        {
            EnsureModel();
            if (model.Phase != DemoPhase.Intro && model.Phase != DemoPhase.Hall)
                return;
            StopHall();
            model.SkipToBoss(Time.time);
        }

        /// <summary>Binds to <see cref="PlayerTarget.Current"/> if it changed: health adapter and the fade quad on its head.</summary>
        private void ResolvePlayer()
        {
            PlayerTarget current = PlayerTarget.Current;
            if (current == null || current == player)
                return;
            player = current;
            vitals = PlayerVitals.From(player.Damageable);
            if (!vitals.CanReadHealth)
                Debug.LogWarning("[DemoFlow] player health can't be read; defeat/restart is disabled.", this);
            if (fadeMaterial != null)
            {
                float alpha = fader != null ? fader.Alpha : 0f;
                if (fader != null)
                    Destroy(fader.gameObject);
                fader = ScreenFader.Create(player.Head, fadeMaterial);
                if (alpha > 0f)
                    fader.FadeTo(alpha, 0f);
            }
        }

        private void EnsureModel()
        {
            if (model != null)
                return;
            model = new DemoFlowModel(introDelay, bossDelay, restartDelay);
            model.PhaseChanged += HandlePhaseChanged;
        }

        private void Update()
        {
            ResolvePlayer();
            EnsureModel();
            bool hallCleared = hallSteps != null ? hallSteps.IsHallDone : hallEncounter == null || hallEncounter.IsCleared;
            bool bossCleared = bossEncounter == null || (bossEncounter.IsCleared && !bossEncounter.WasForceCleared);
            bool dead = vitals != null && vitals.IsDead;
            if (IsWaitingForPlayer && player != null)
            {
                Vector3 offset = player.Head.position - startPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude > leaveStartRadius * leaveStartRadius)
                    MarkPlayerReady();
            }

            switch (model.Tick(Time.time, hallCleared, bossCleared, dead))
            {
                case DemoAction.StartHall:
                    if (hallSteps != null)
                        hallSteps.BeginHall();
                    else if (hallEncounter != null)
                        hallEncounter.StartEncounter();
                    break;
                case DemoAction.StartBoss:
                    if (bossEncounter != null)
                        bossEncounter.StartEncounter();
                    break;
                case DemoAction.Restart:
                    // The model keeps asking until Begin; start the routine only once.
                    if (!restartPending)
                    {
                        restartPending = true;
                        Run(RestartRoutine());
                    }
                    break;
            }
        }

        private void HandlePhaseChanged(DemoPhase from, DemoPhase to)
        {
            Debug.Log($"[DemoFlow] {from} → {to}", this);
            switch (to)
            {
                case DemoPhase.Defeat:
                    // Stop every fight so nothing keeps hitting the fallen player.
                    StopHall();
                    if (bossEncounter != null)
                        bossEncounter.ForceClear();
                    Run(null);
                    if (fader != null)
                        fader.FadeTo(1f, Mathf.Max(fadeSeconds, restartDelay * 0.8f));
                    break;
                case DemoPhase.BossIntro:
                    if (realmArrival != null)
                        Run(TeleportRoutine(realmArrival.position, realmArrival.rotation, 1f));
                    break;
                case DemoPhase.Victory:
                    if (realmArrival != null)
                        Run(TeleportRoutine(startPosition, startRotation, returnDelay));
                    break;
            }
        }

        private void StopHall()
        {
            if (hallSteps != null)
                hallSteps.StopHall();
            if (hallEncounter != null)
                hallEncounter.ForceClear();
        }

        private void Run(IEnumerator routine)
        {
            if (transition != null)
                StopCoroutine(transition);
            transition = routine != null ? StartCoroutine(routine) : null;
        }

        private IEnumerator TeleportRoutine(Vector3 position, Quaternion rotation, float wait)
        {
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);
            yield return Fade(1f);
            if (player != null)
                MovePlayer(player, position, rotation);
            yield return Fade(0f);
        }

        private IEnumerator RestartRoutine()
        {
            if (fader != null && fader.Alpha < 1f)
                yield return Fade(1f);
            Restart();
            yield return Fade(0f);
        }

        private IEnumerator Fade(float alpha)
        {
            if (fader == null)
                yield break;
            fader.FadeTo(alpha, fadeSeconds);
            while (fader.IsFading)
                yield return null;
        }

        /// <summary>Back to the intro: encounters reset, player healed and moved to the start.</summary>
        public void Restart()
        {
            restartPending = false;
            if (hallSteps != null)
                hallSteps.ResetHall();
            if (hallEncounter != null)
                hallEncounter.ResetEncounter();
            if (bossEncounter != null)
                bossEncounter.ResetEncounter();
            if (vitals != null)
                vitals.Reset();
            if (player != null)
                MovePlayer(player, startPosition, startRotation);
            EnsureModel();
            model.Begin(Time.time);
            if (startAtBoss)
                SkipToBoss();
        }

        private static void MovePlayer(PlayerTarget target, Vector3 position, Quaternion rotation) =>
            PlayerRig.MoveTo(target, position, rotation);

        private void OnGUI()
        {
            if (!showHud || model == null || DemoWorldHud.InHeadset)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };

            string text = HudText();
            if (string.IsNullOrEmpty(text))
                return;
            var rect = new Rect(0f, 20f, Screen.width, 80f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        private static readonly string[] Countdown = new string[10];

        private static string CountdownText(int seconds)
        {
            if (seconds >= Countdown.Length)
                return $"The exhibits are waking up... {seconds}";
            return Countdown[seconds] ??= $"The exhibits are waking up... {seconds}";
        }

        private string HudText()
        {
            switch (model.Phase)
            {
                case DemoPhase.Intro:
                    if (!model.IsReady)
                        return "Pick up a weapon from the table";
                    int left = Mathf.CeilToInt(model.IntroRemaining(Time.time));
                    return left > 0 ? CountdownText(left) : "The exhibits are waking up...";
                case DemoPhase.Hall:
                    if (hallSteps != null)
                        return hallSteps.HallHud;
                    return hallEncounter != null
                        ? $"Defeat the guardians  {hallEncounter.KillCount} / {hallEncounter.TotalEnemies}"
                        : string.Empty;
                case DemoPhase.BossIntro:
                    return "The Pharaoh awakens";
                case DemoPhase.Boss:
                    return PharaohArena.Current != null && PharaohArena.Current.IsOpen
                        ? "The barrier is down! Strike the Pharaoh!"
                        : "Defeat the Pharaoh";
                case DemoPhase.Victory:
                    return "Victory!";
                case DemoPhase.Defeat:
                    return "You fell... restarting";
            }
            return string.Empty;
        }
    }
}
